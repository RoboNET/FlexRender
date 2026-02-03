using System.CommandLine;
using System.Globalization;

namespace FlexRender.Cli;

/// <summary>
/// Global options available to all CLI commands.
/// </summary>
public static class GlobalOptions
{
    /// <summary>
    /// Enable verbose output.
    /// </summary>
    public static Option<bool> Verbose { get; } = CreateVerboseOption();

    /// <summary>
    /// Path to fonts directory.
    /// </summary>
    public static Option<DirectoryInfo?> Fonts { get; } = CreateFontsOption();

    /// <summary>
    /// Output scale factor.
    /// </summary>
    public static Option<float> Scale { get; } = CreateScaleOption();

    /// <summary>
    /// Base path for resolving relative file paths in templates.
    /// </summary>
    public static Option<DirectoryInfo?> BasePath { get; } = CreateBasePathOption();

    private static Option<bool> CreateVerboseOption()
    {
        var option = new Option<bool>("--verbose", "-v")
        {
            Description = "Enable verbose output",
            Recursive = true
        };
        return option;
    }

    private static Option<DirectoryInfo?> CreateFontsOption()
    {
        var option = new Option<DirectoryInfo?>("--fonts")
        {
            Description = "Path to fonts directory",
            Recursive = true
        };
        return option;
    }

    private static Option<DirectoryInfo?> CreateBasePathOption()
    {
        var option = new Option<DirectoryInfo?>("--base-path")
        {
            Description = "Base path for resolving relative file paths in templates (default: template directory)",
            Recursive = true
        };
        return option;
    }

    private static Option<float> CreateScaleOption()
    {
        var option = new Option<float>("--scale")
        {
            Description = "Output scale factor (e.g., 2.0 for retina)",
            DefaultValueFactory = _ => 1.0f,
            Recursive = true,
            CustomParser = result =>
            {
                if (result.Tokens.Count == 0)
                    return 1.0f;

                var token = result.Tokens[0].Value;
                if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    return value;

                result.AddError($"Cannot parse '{token}' as a scale factor. Use a decimal number like 2.0");
                return 1.0f;
            }
        };
        return option;
    }
}
