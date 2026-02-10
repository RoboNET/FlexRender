using FlexRender;
using FlexRender.Barcode;
using FlexRender.Configuration;
using FlexRender.QrCode;
using FlexRender.Yaml;

// Build renderer with Skia backend — full native rendering with QR + barcode support.
// On Linux, add SkiaSharp.NativeAssets.Linux to avoid DllNotFoundException.
using var renderer = new FlexRenderBuilder()
    .WithSkia(skia => skia
        .WithQr()
        .WithBarcode())
    .Build();

// Prepare data for the receipt template
var data = new ObjectValue
{
    ["shop"] = new StringValue("FlexRender Store"),
    ["items"] = new ArrayValue(new TemplateValue[]
    {
        new ObjectValue { ["name"] = new StringValue("Widget A"), ["price"] = new StringValue("$9.99") },
        new ObjectValue { ["name"] = new StringValue("Widget B"), ["price"] = new StringValue("$14.50") },
        new ObjectValue { ["name"] = new StringValue("Gadget C"), ["price"] = new StringValue("$24.99") }
    }),
    ["total"] = new StringValue("$49.48"),
    ["qrUrl"] = new StringValue("https://example.com/receipt/12345"),
    ["barcode"] = new StringValue("RCP-2025-12345")
};

// Resolve paths
var baseDirectory = AppContext.BaseDirectory;
var templatePath = Path.Combine(baseDirectory, "template.yaml");
var outputPath = Path.Combine(baseDirectory, "output.png");

if (!File.Exists(templatePath))
{
    var devPath = Path.Combine(Directory.GetCurrentDirectory(), "examples", "SkiaRenderExample", "template.yaml");
    if (File.Exists(devPath))
    {
        templatePath = devPath;
        outputPath = Path.Combine(Directory.GetCurrentDirectory(), "examples", "SkiaRenderExample", "output.png");
    }
    else
    {
        Console.Error.WriteLine($"Template not found: {templatePath}");
        return 1;
    }
}

Console.WriteLine("FlexRender Skia Render Example");
Console.WriteLine("==============================");
Console.WriteLine();
Console.WriteLine($"Template: {templatePath}");
Console.WriteLine("Backend:  Skia (native rendering)");
Console.WriteLine("Features: QR code + barcode");
Console.WriteLine();

Console.WriteLine("Rendering receipt to PNG...");
await using var outputStream = File.Create(outputPath);
await renderer.RenderFile(outputStream, templatePath, data, ImageFormat.Png);

Console.WriteLine($"Output saved: {outputPath}");
Console.WriteLine();
Console.WriteLine("Done!");

return 0;
