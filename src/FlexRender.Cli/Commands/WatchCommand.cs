using System.CommandLine;
using FlexRender.Abstractions;
using FlexRender.Parsing;
using FlexRender.Skia;
using FlexRender.Yaml;

namespace FlexRender.Cli.Commands;

/// <summary>
/// Command to watch files and re-render on changes.
/// </summary>
public static class WatchCommand
{
    /// <summary>
    /// Creates the watch command.
    /// </summary>
    /// <returns>The configured watch command.</returns>
    public static Command Create()
    {
        var templateArg = new Argument<FileInfo>("template")
        {
            Description = "Path to the YAML template file"
        };

        var dataOption = new Option<FileInfo?>("--data", "-d")
        {
            Description = "Path to the JSON data file"
        };

        var outputOption = new Option<FileInfo?>("--output", "-o")
        {
            Description = "Path to the output image file"
        };

        var qualityOption = new Option<int>("--quality")
        {
            Description = "JPEG quality (0-100, only applies to JPEG output)",
            DefaultValueFactory = _ => 90
        };

        var openOption = new Option<bool>("--open")
        {
            Description = "Open the output file in the default viewer after initial render"
        };

        var bmpColorOption = new Option<BmpColorMode>("--bmp-color")
        {
            Description = "BMP color mode: Bgra32, Rgb24, Rgb565, Grayscale8, Grayscale4, Monochrome1 (only applies to BMP output)",
            DefaultValueFactory = _ => BmpColorMode.Bgra32
        };

        var command = new Command("watch", "Watch files and re-render on changes")
        {
            templateArg,
            dataOption,
            outputOption,
            qualityOption,
            openOption,
            bmpColorOption
        };

        command.SetAction(async (parseResult) =>
        {
            var templateFile = parseResult.GetValue(templateArg);
            var dataFile = parseResult.GetValue(dataOption);
            var outputFile = parseResult.GetValue(outputOption);
            var quality = parseResult.GetValue(qualityOption);
            var open = parseResult.GetValue(openOption);
            var bmpColor = parseResult.GetValue(bmpColorOption);
            var verbose = parseResult.GetValue(GlobalOptions.Verbose);
            var basePath = parseResult.GetValue(GlobalOptions.BasePath);

            return await Execute(templateFile!, dataFile, outputFile, quality, open, bmpColor, verbose, basePath);
        });

        return command;
    }

    private static async Task<int> Execute(
        FileInfo templateFile,
        FileInfo? dataFile,
        FileInfo? outputFile,
        int quality,
        bool open,
        BmpColorMode bmpColor,
        bool verbose,
        DirectoryInfo? basePath)
    {
        // Validate output file is specified
        if (outputFile is null)
        {
            Console.Error.WriteLine("Error: Output file is required. Use -o or --output to specify.");
            return 1;
        }

        // Validate template exists
        if (!templateFile.Exists)
        {
            Console.Error.WriteLine($"Error: Template file not found: {templateFile.FullName}");
            return 1;
        }

        // Validate output format
        OutputFormat format;
        try
        {
            format = OutputFormatExtensions.FromPath(outputFile.FullName);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        // Validate data file if specified
        if (dataFile is not null && !dataFile.Exists)
        {
            Console.Error.WriteLine($"Error: Data file not found: {dataFile.FullName}");
            return 1;
        }

        // Validate quality range
        if (quality < 0 || quality > 100)
        {
            Console.Error.WriteLine($"Error: Quality must be between 0 and 100, got {quality}");
            return 1;
        }

        // Create renderer with base path
        var effectiveBasePath = basePath?.FullName ?? templateFile.DirectoryName!;
        using var renderer = Program.CreateRenderBuilder(effectiveBasePath).Build();

        // Set BMP color mode if applicable
#pragma warning disable CS0618 // Obsolete BmpColorMode property - CLI still supports legacy option
        if (renderer is SkiaRender skiaRender)
            skiaRender.BmpColorMode = bmpColor;
#pragma warning restore CS0618

        Console.WriteLine("Starting watch mode...");
        Console.WriteLine($"  Template: {templateFile.FullName}");
        if (dataFile is not null)
        {
            Console.WriteLine($"  Data: {dataFile.FullName}");
        }
        Console.WriteLine($"  Output: {outputFile.FullName}");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop watching.");
        Console.WriteLine();

        // Initial render
        var renderResult = await RenderOnce(renderer, templateFile, dataFile, outputFile, format, verbose);
        if (renderResult != 0 && verbose)
        {
            Console.WriteLine("Initial render failed, will retry on file changes.");
        }
        else if (renderResult == 0 && open)
        {
            FileOpener.Open(outputFile.FullName);
        }

        // Set up file watchers and debouncers
        var watchers = new List<FileSystemWatcher>();
        var debouncers = new List<Debouncer>();
        var cts = new CancellationTokenSource();

        try
        {
            // Watch template file
            var (templateWatcher, templateDebouncer) = CreateWatcher(templateFile, async () =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Template changed: {templateFile.Name}");
                await RenderOnce(renderer, templateFile, dataFile, outputFile, format, verbose);
            });
            watchers.Add(templateWatcher);
            debouncers.Add(templateDebouncer);

            // Watch data file if specified
            if (dataFile is not null)
            {
                var (dataWatcher, dataDebouncer) = CreateWatcher(dataFile, async () =>
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Data changed: {dataFile.Name}");
                    await RenderOnce(renderer, templateFile, dataFile, outputFile, format, verbose);
                });
                watchers.Add(dataWatcher);
                debouncers.Add(dataDebouncer);
            }

            // Wait for cancellation
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (TaskCanceledException)
            {
                // Expected when Ctrl+C is pressed
            }

            Console.WriteLine();
            Console.WriteLine("Watch mode stopped.");
            return 0;
        }
        finally
        {
            // Dispose debouncers first to stop any pending timer callbacks
            foreach (var debouncer in debouncers)
            {
                debouncer.Dispose();
            }

            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }
        }
    }

    /// <summary>
    /// Creates a file system watcher with debounced change handling.
    /// </summary>
    /// <param name="file">The file to watch.</param>
    /// <param name="onChange">The action to execute when the file changes.</param>
    /// <returns>A tuple containing the watcher and debouncer for proper disposal.</returns>
    private static (FileSystemWatcher Watcher, Debouncer Debouncer) CreateWatcher(
        FileInfo file,
        Func<Task> onChange)
    {
        var watcher = new FileSystemWatcher
        {
            Path = file.DirectoryName!,
            Filter = file.Name,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        // Debounce using timer-based approach with lock-based synchronization
        var debouncer = new Debouncer(TimeSpan.FromMilliseconds(100), onChange);

        watcher.Changed += (_, _) => debouncer.Trigger();

        return (watcher, debouncer);
    }

    /// <summary>
    /// Provides thread-safe debouncing for file system events.
    /// Delays execution until no new triggers occur within the specified interval.
    /// </summary>
    private sealed class Debouncer : IDisposable
    {
        private readonly TimeSpan _delay;
        private readonly Func<Task> _action;
        private readonly object _lock = new();
        private Timer? _timer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Debouncer"/> class.
        /// </summary>
        /// <param name="delay">The debounce delay interval.</param>
        /// <param name="action">The action to execute after the debounce period.</param>
        public Debouncer(TimeSpan delay, Func<Task> action)
        {
            _delay = delay;
            _action = action;
        }

        /// <summary>
        /// Triggers the debouncer, resetting the timer if already running.
        /// </summary>
        public void Trigger()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                // Dispose existing timer and create a new one to reset the delay
                _timer?.Dispose();
                _timer = new Timer(
                    callback: OnTimerElapsed,
                    state: null,
                    dueTime: _delay,
                    period: Timeout.InfiniteTimeSpan);
            }
        }

        private void OnTimerElapsed(object? state)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                // Clean up timer before executing action
                _timer?.Dispose();
                _timer = null;
            }

            // Execute action outside the lock to prevent blocking other triggers
            // Fire-and-forget with exception handling
            _ = ExecuteAction();
        }

        private async Task ExecuteAction()
        {
            try
            {
                await _action();
            }
            catch (Exception ex)
            {
                // Log but don't crash the watcher
                Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] Debounced action error: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _timer?.Dispose();
                _timer = null;
            }
        }
    }

    private static async Task<int> RenderOnce(
        IFlexRender renderer,
        FileInfo templateFile,
        FileInfo? dataFile,
        FileInfo outputFile,
        OutputFormat format,
        bool verbose)
    {
        try
        {
            // Load data if provided
            ObjectValue? data = null;
            if (dataFile is not null)
            {
                data = DataLoader.LoadFromFile(dataFile.FullName);
            }

            // Ensure output directory exists
            FileOpener.EnsureDirectoryExists(outputFile.FullName);

            // Map output format to ImageFormat
            var imageFormat = format switch
            {
                OutputFormat.Png => ImageFormat.Png,
                OutputFormat.Jpeg => ImageFormat.Jpeg,
                OutputFormat.Bmp => ImageFormat.Bmp,
                _ => ImageFormat.Png
            };

            // Render directly to file
            await using var outputStream = File.Create(outputFile.FullName);
            await renderer.RenderFile(outputStream, templateFile.FullName, data, imageFormat);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Rendered: {outputFile.Name}");
            return 0;
        }
        catch (TemplateParseException ex)
        {
            Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] Template error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }
}
