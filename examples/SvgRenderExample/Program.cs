using FlexRender;
using FlexRender.Barcode.Svg;
using FlexRender.Configuration;
using FlexRender.Parsing;
using FlexRender.QrCode.Svg;

// Prepare data for the business card template
var data = new ObjectValue
{
    ["name"] = new StringValue("Jane Doe"),
    ["title"] = new StringValue("Senior Developer"),
    ["email"] = new StringValue("jane@example.com"),
    ["phone"] = new StringValue("+1 (555) 123-4567"),
    ["website"] = new StringValue("example.com"),
    ["qrUrl"] = new StringValue("https://example.com/vcard/jane-doe")
};

// Resolve paths
var baseDirectory = AppContext.BaseDirectory;
var templatePath = Path.Combine(baseDirectory, "template.yaml");
var svgOutputPath = Path.Combine(baseDirectory, "output.svg");

if (!File.Exists(templatePath))
{
    var devPath = Path.Combine(Directory.GetCurrentDirectory(), "examples", "SvgRenderExample", "template.yaml");
    if (File.Exists(devPath))
    {
        templatePath = devPath;
        svgOutputPath = Path.Combine(Directory.GetCurrentDirectory(), "examples", "SvgRenderExample", "output.svg");
    }
    else
    {
        Console.Error.WriteLine($"Template not found: {templatePath}");
        return 1;
    }
}

Console.WriteLine("FlexRender SVG Render Example");
Console.WriteLine("=============================");
Console.WriteLine();

// --- Configuration 1: SVG-only (vector output, no native dependencies) ---
Console.WriteLine("Configuration 1: SVG-only (vector QR/barcode)");
Console.WriteLine("  No native dependencies — pure vector output");
Console.WriteLine();

using var svgOnly = new FlexRenderBuilder()
    .WithSvg(svg => svg
        .WithQrSvg()
        .WithBarcodeSvg())
    .Build();

Console.WriteLine("Rendering business card to SVG...");
var parser = new TemplateParser();
var template = await parser.ParseFileAsync(templatePath);
var svgContent = await svgOnly.RenderToSvg(template, data);
await File.WriteAllTextAsync(svgOutputPath, svgContent);
Console.WriteLine($"SVG output saved: {svgOutputPath}");
Console.WriteLine();

// --- Configuration 2: SVG + Skia raster backend ---
// Uncomment below to also produce PNG output via raster backend.
// Requires SkiaSharp native libraries.
//
// Console.WriteLine("Configuration 2: SVG + Skia raster backend");
// using var svgWithRaster = new FlexRenderBuilder()
//     .WithSvg(svg => svg
//         .WithQrSvg()
//         .WithBarcodeSvg()
//         .WithSkia())
//     .Build();
//
// var pngOutputPath = Path.ChangeExtension(svgOutputPath, ".png");
// Console.WriteLine("Rendering business card to PNG via raster backend...");
// var pngBytes = await svgWithRaster.RenderFile(templatePath, data, ImageFormat.Png);
// await File.WriteAllBytesAsync(pngOutputPath, pngBytes);
// Console.WriteLine($"PNG output saved: {pngOutputPath}");

Console.WriteLine("Done!");

return 0;
