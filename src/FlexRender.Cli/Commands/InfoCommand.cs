using System.CommandLine;
using System.Text.RegularExpressions;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;

namespace FlexRender.Cli.Commands;

/// <summary>
/// Command to display template information.
/// </summary>
public static partial class InfoCommand
{
    /// <summary>
    /// Creates the info command.
    /// </summary>
    /// <returns>The configured info command.</returns>
    public static Command Create()
    {
        var templateArg = new Argument<FileInfo>("template")
        {
            Description = "Path to the YAML template file"
        };

        var command = new Command("info", "Show template information (size, fonts, variables)")
        {
            templateArg
        };

        command.SetAction(async (parseResult) =>
        {
            var templateFile = parseResult.GetValue(templateArg);

            return await Execute(templateFile!);
        });

        return command;
    }

    private static async Task<int> Execute(FileInfo templateFile)
    {
        if (!templateFile.Exists)
        {
            Console.Error.WriteLine($"Error: Template file not found: {templateFile.FullName}");
            return 1;
        }

        try
        {
            var parser = new TemplateParser();
            var template = await parser.ParseFile(templateFile.FullName, CancellationToken.None);

            PrintTemplateInfo(template, templateFile);
            return 0;
        }
        catch (TemplateParseException ex)
        {
            Console.Error.WriteLine($"Parse error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintTemplateInfo(Template template, FileInfo templateFile)
    {
        Console.WriteLine($"Template: {templateFile.Name}");
        Console.WriteLine($"  Name: {template.Name ?? "(unnamed)"}");
        Console.WriteLine($"  Version: {template.Version}");
        Console.WriteLine();

        Console.WriteLine("Canvas:");
        Console.WriteLine($"  Fixed dimension: {template.Canvas.Fixed}");
        Console.WriteLine($"  Width: {template.Canvas.Width}px");
        Console.WriteLine($"  Height: {template.Canvas.Height}px");
        Console.WriteLine($"  Background: {template.Canvas.Background}");
        Console.WriteLine();

        Console.WriteLine("Elements:");
        Console.WriteLine($"  Total: {template.Elements.Count}");

        var elementTypes = template.Elements
            .GroupBy(e => e.GetType().Name.Replace("Element", ""))
            .OrderBy(g => g.Key);

        foreach (var group in elementTypes)
        {
            Console.WriteLine($"  - {group.Key}: {group.Count()}");
        }

        // Collect fonts used
        var fonts = template.Elements
            .OfType<TextElement>()
            .Select(t => t.Font)
            .Distinct()
            .OrderBy(f => f);

        if (fonts.Any())
        {
            Console.WriteLine();
            Console.WriteLine("Fonts used:");
            foreach (var font in fonts)
            {
                Console.WriteLine($"  - {font}");
            }
        }

        // Collect variables (basic extraction from {{...}} patterns)
        var variables = ExtractVariables(template);
        if (variables.Any())
        {
            Console.WriteLine();
            Console.WriteLine("Variables:");
            foreach (var variable in variables)
            {
                Console.WriteLine($"  - {variable}");
            }
        }
    }

    private static IEnumerable<string> ExtractVariables(Template template)
    {
        var variables = new HashSet<string>();
        var pattern = VariablePattern();

        foreach (var element in template.Elements)
        {
            if (element is TextElement textElement)
            {
                var matches = pattern.Matches(textElement.Content);
                foreach (Match match in matches)
                {
                    variables.Add(match.Groups[1].Value.Trim());
                }
            }
        }

        return variables.OrderBy(v => v);
    }

    [GeneratedRegex(@"\{\{([^}#/]+)\}\}")]
    private static partial Regex VariablePattern();
}
