using System.CommandLine;
using FlexRender.Parsing;

namespace FlexRender.Cli.Commands;

/// <summary>
/// Command to validate a template file without rendering.
/// </summary>
public static class ValidateCommand
{
    /// <summary>
    /// Creates the validate command.
    /// </summary>
    /// <returns>The configured validate command.</returns>
    public static Command Create()
    {
        var templateArg = new Argument<FileInfo>("template")
        {
            Description = "Path to the YAML template file"
        };

        var command = new Command("validate", "Validate a template file without rendering")
        {
            templateArg
        };

        command.SetAction(async (parseResult) =>
        {
            var templateFile = parseResult.GetValue(templateArg);
            var verbose = parseResult.GetValue(GlobalOptions.Verbose);

            return await Execute(templateFile!, verbose);
        });

        return command;
    }

    private static Task<int> Execute(FileInfo templateFile, bool verbose)
    {
        if (!templateFile.Exists)
        {
            Console.Error.WriteLine($"Error: Template file not found: {templateFile.FullName}");
            return Task.FromResult(1);
        }

        try
        {
            var parser = new TemplateParser();
            var template = parser.ParseFile(templateFile.FullName);

            if (verbose)
            {
                Console.WriteLine($"Template: {template.Name ?? "(unnamed)"}");
                Console.WriteLine($"Version: {template.Version}");
                Console.WriteLine($"Canvas: {template.Canvas.Width}x{template.Canvas.Height}px ({template.Canvas.Fixed})");
                Console.WriteLine($"Elements: {template.Elements.Count}");
            }

            Console.WriteLine($"Valid: {templateFile.Name}");
            return Task.FromResult(0);
        }
        catch (TemplateParseException ex)
        {
            Console.Error.WriteLine($"Validation error: {ex.Message}");
            return Task.FromResult(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return Task.FromResult(1);
        }
    }
}
