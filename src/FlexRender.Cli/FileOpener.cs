using System.Diagnostics;

namespace FlexRender.Cli;

/// <summary>
/// Opens files in the system's default application using <see cref="ProcessStartInfo.UseShellExecute"/>.
/// </summary>
public static class FileOpener
{
    /// <summary>
    /// Ensures the directory for the specified file path exists, creating it if necessary.
    /// </summary>
    /// <param name="filePath">The file path whose parent directory should be created if needed.</param>
    public static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Opens the specified file in the system's default application.
    /// </summary>
    /// <param name="filePath">The absolute path to the file to open.</param>
    /// <remarks>
    /// Uses <see cref="ProcessStartInfo.UseShellExecute"/> set to <c>true</c>,
    /// which delegates to the OS shell on all platforms (macOS, Linux, Windows)
    /// without spawning a command interpreter, avoiding command injection risks.
    /// Failures are logged as warnings and do not throw exceptions.
    /// </remarks>
    public static void Open(string filePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo(filePath)
            {
                UseShellExecute = true
            };

            using var process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Could not open file in default viewer: {ex.Message}");
        }
    }
}
