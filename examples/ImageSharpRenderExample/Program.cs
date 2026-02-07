using FlexRender;
using FlexRender.Abstractions;
using FlexRender.Barcode.ImageSharp;
using FlexRender.Configuration;
using FlexRender.QrCode.ImageSharp;
using FlexRender.Yaml;

// Build renderer with ImageSharp backend — pure .NET, no native dependencies.
// Ideal for containers, serverless, and cross-platform deployments where
// SkiaSharp native libraries are unavailable.
using var renderer = new FlexRenderBuilder()
    .WithImageSharp(imageSharp => imageSharp
        .WithQr()
        .WithBarcode())
    .Build();

// Prepare data for the product label template
var data = new ObjectValue
{
    ["company"] = new StringValue("FlexRender Inc."),
    ["product"] = new StringValue("Smart Widget Pro"),
    ["sku"] = new StringValue("SKU-2025-0042"),
    ["description"] = new StringValue("High-performance widget with advanced features"),
    ["price"] = new StringValue("$49.99"),
    ["qrUrl"] = new StringValue("https://example.com/products/smart-widget-pro")
};

// Resolve paths
var baseDirectory = AppContext.BaseDirectory;
var templatePath = Path.Combine(baseDirectory, "template.yaml");
var outputPath = Path.Combine(baseDirectory, "output.png");

if (!File.Exists(templatePath))
{
    var devPath = Path.Combine(Directory.GetCurrentDirectory(), "examples", "ImageSharpRenderExample", "template.yaml");
    if (File.Exists(devPath))
    {
        templatePath = devPath;
        outputPath = Path.Combine(Directory.GetCurrentDirectory(), "examples", "ImageSharpRenderExample", "output.png");
    }
    else
    {
        Console.Error.WriteLine($"Template not found: {templatePath}");
        return 1;
    }
}

Console.WriteLine("FlexRender ImageSharp Render Example");
Console.WriteLine("====================================");
Console.WriteLine();
Console.WriteLine($"Template: {templatePath}");
Console.WriteLine("Backend:  ImageSharp (pure .NET, no native deps)");
Console.WriteLine("Features: QR code + barcode");
Console.WriteLine();

Console.WriteLine("Rendering product label to PNG...");
await using var outputStream = File.Create(outputPath);
await renderer.RenderFile(outputStream, templatePath, data, ImageFormat.Png);

Console.WriteLine($"Output saved: {outputPath}");
Console.WriteLine();
Console.WriteLine("Done!");

return 0;
