using Microsoft.Extensions.DependencyInjection;
using FlexRender;
using FlexRender.Abstractions;
using FlexRender.Barcode;
using FlexRender.Parsing;
using FlexRender.QrCode;
using FlexRender.Yaml;

// Build service provider with FlexRender services
var services = new ServiceCollection();
services.AddFlexRender(builder => builder
    .WithSkia(skia => skia.WithQr().WithBarcode()));
using var serviceProvider = services.BuildServiceProvider();

// Get the renderer from DI
var renderer = serviceProvider.GetRequiredService<IFlexRender>();

// Determine paths
var baseDirectory = AppContext.BaseDirectory;
var templatePath = Path.Combine(baseDirectory, "template.yaml");
var outputPath = Path.Combine(baseDirectory, "output.png");

// Handle running from different directories
if (!File.Exists(templatePath))
{
    // Try relative path from current directory (for development)
    var devTemplatePath = Path.Combine(Directory.GetCurrentDirectory(), "examples", "YamlRenderExample", "template.yaml");
    if (File.Exists(devTemplatePath))
    {
        templatePath = devTemplatePath;
        outputPath = Path.Combine(Directory.GetCurrentDirectory(), "examples", "YamlRenderExample", "output.png");
    }
    else
    {
        Console.Error.WriteLine($"Template file not found: {templatePath}");
        return 1;
    }
}

Console.WriteLine("FlexRender YAML Render Example");
Console.WriteLine("==============================");
Console.WriteLine();

// Parse template just for displaying info
Console.WriteLine($"Loading template: {templatePath}");
var parser = new TemplateParser();
var template = await parser.ParseFile(templatePath, CancellationToken.None);
Console.WriteLine($"Template name: {template.Name ?? "unnamed"}");

// Prepare data for variable substitution
var data = new ObjectValue
{
    ["templateName"] = new StringValue(template.Name ?? "example")
};

// Render to PNG file using the simple IFlexRender API
Console.WriteLine();
Console.WriteLine("Rendering to PNG...");

await using var outputStream = File.Create(outputPath);
await renderer.RenderFile(outputStream, templatePath, data, ImageFormat.Png);

Console.WriteLine($"Output saved: {outputPath}");
Console.WriteLine();
Console.WriteLine("Done!");

return 0;
