using FlexRender;
using FlexRender.Barcode;
using FlexRender.Configuration;
using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.QrCode;

// Build template programmatically — no YAML parsing required
var template = new Template
{
    Name = "ast-example",
    Version = 1,
    Canvas = new CanvasSettings
    {
        Fixed = FixedDimension.Width,
        Width = 400,
        Background = "#ffffff"
    }
};

// Title text — large, centered, blue
template.AddElement(new TextElement
{
    Content = "FlexRender AST Example",
    Size = "24",
    Color = "#1a73e8",
    Align = TextAlign.Center
});

// Separator between title and content
template.AddElement(new SeparatorElement
{
    Style = SeparatorStyle.Solid,
    Thickness = 1f,
    Color = "#dadce0",
    Margin = "8 0"
});

// Flex row with "Left" and "Right" labels using space-between
var row = new FlexElement
{
    Direction = FlexDirection.Row,
    Justify = JustifyContent.SpaceBetween,
    Padding = "8 0"
};

row.AddChild(new TextElement
{
    Content = "Left",
    Size = "16",
    Color = "#333333"
});

row.AddChild(new TextElement
{
    Content = "Right",
    Size = "16",
    Color = "#333333"
});

template.AddElement(row);

// Footer text — smaller, gray
template.AddElement(new TextElement
{
    Content = "Built without YAML!",
    Size = "12",
    Color = "#999999",
    Align = TextAlign.Center,
    Margin = "8 0 0 0"
});

// Render using FlexRenderBuilder — no DI container needed for simple use
using var renderer = new FlexRenderBuilder()
    .WithSkia(skia => skia.WithQr().WithBarcode())
    .Build();
var data = new ObjectValue();

// Determine output path
var outputPath = Path.Combine(AppContext.BaseDirectory, "output.png");
if (!Directory.Exists(Path.GetDirectoryName(outputPath)!))
{
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
}

Console.WriteLine("FlexRender AST Render Example");
Console.WriteLine("=============================");
Console.WriteLine();
Console.WriteLine("Building template from code (no YAML)...");
Console.WriteLine($"  Canvas: {template.Canvas.Width}px wide, background {template.Canvas.Background.Value}");
Console.WriteLine($"  Elements: {template.Elements.Count}");
Console.WriteLine();

Console.WriteLine("Rendering to PNG...");
await using var outputStream = File.Create(outputPath);
await renderer.Render(outputStream, template, data, ImageFormat.Png);

Console.WriteLine($"Output saved: {outputPath}");
Console.WriteLine();
Console.WriteLine("Done!");

return 0;
