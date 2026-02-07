using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Snapshots;

/// <summary>
/// SVG golden snapshot tests for the SVG rendering engine.
/// Tests various element types rendered to SVG markup against golden <c>.svg</c> files.
/// </summary>
/// <remarks>
/// <para>
/// SVG output is deterministic text, so snapshot comparison is an exact string match.
/// No pixel tolerance or cross-platform font differences apply.
/// </para>
/// <para>
/// Run with <c>UPDATE_SNAPSHOTS=true</c> to regenerate golden SVG files.
/// </para>
/// </remarks>
public sealed class SvgSnapshotTests : SvgSnapshotTestBase
{
    /// <summary>
    /// Tests basic text element rendering with center alignment.
    /// Verifies that a simple text element produces correct SVG text markup
    /// with the expected font-size, fill color, and text-anchor attributes.
    /// </summary>
    [Fact]
    public void SvgTextBasic()
    {
        var template = CreateTemplate(300, 100);
        template.AddElement(new TextElement
        {
            Content = "Hello World",
            Size = "16",
            Color = "#000000",
            Align = TextAlign.Center,
            Width = "300"
        });

        AssertSvgSnapshot("svg_text_basic", template, new ObjectValue());
    }

    /// <summary>
    /// Tests horizontal separator rendering with dashed style.
    /// Verifies that a dashed separator produces an SVG line element
    /// with the correct stroke-dasharray attribute.
    /// </summary>
    [Fact]
    public void SvgSeparatorHorizontal()
    {
        var template = CreateTemplate(300, 50);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Padding = "10",
            Width = "300"
        };

        flex.AddChild(new SeparatorElement
        {
            Orientation = SeparatorOrientation.Horizontal,
            Style = SeparatorStyle.Dashed,
            Thickness = 2f,
            Color = "#333333"
        });

        template.AddElement(flex);

        AssertSvgSnapshot("svg_separator_horizontal", template, new ObjectValue());
    }

    /// <summary>
    /// Tests flex container with column direction containing three text children.
    /// Verifies that vertical stacking produces correct SVG positioning
    /// for each child element.
    /// </summary>
    [Fact]
    public void SvgFlexColumn()
    {
        var template = CreateTemplate(300, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Gap = "10",
            Padding = "10"
        };

        flex.AddChild(new TextElement { Content = "First", Size = "14", Color = "#000000" });
        flex.AddChild(new TextElement { Content = "Second", Size = "14", Color = "#000000" });
        flex.AddChild(new TextElement { Content = "Third", Size = "14", Color = "#000000" });

        template.AddElement(flex);

        AssertSvgSnapshot("svg_flex_column", template, new ObjectValue());
    }

    /// <summary>
    /// Tests flex container with row direction containing two text children.
    /// Verifies that horizontal layout produces correct SVG positioning
    /// with children placed side by side.
    /// </summary>
    [Fact]
    public void SvgFlexRow()
    {
        var template = CreateTemplate(300, 100);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Gap = "20",
            Padding = "10"
        };

        flex.AddChild(new TextElement { Content = "Left", Size = "14", Color = "#000000" });
        flex.AddChild(new TextElement { Content = "Right", Size = "14", Color = "#000000" });

        template.AddElement(flex);

        AssertSvgSnapshot("svg_flex_row", template, new ObjectValue());
    }

    /// <summary>
    /// Tests nested flex elements with different background colors.
    /// Verifies that SVG rect elements are emitted with correct fill attributes
    /// for each nesting level and that positioning is accurate.
    /// </summary>
    [Fact]
    public void SvgBackgroundColors()
    {
        var template = CreateTemplate(300, 200);

        var outer = new FlexElement
        {
            Direction = FlexDirection.Column,
            Background = "#e0e0e0",
            Padding = "15",
            Width = "280",
            Height = "180"
        };

        var inner = new FlexElement
        {
            Direction = FlexDirection.Column,
            Background = "#87ceeb",
            Padding = "10",
            Width = "220",
            Height = "120"
        };

        inner.AddChild(new TextElement
        {
            Content = "Nested Content",
            Size = "14",
            Color = "#000000",
            Background = "#ffff00"
        });

        outer.AddChild(inner);
        template.AddElement(outer);

        AssertSvgSnapshot("svg_background_colors", template, new ObjectValue());
    }

    /// <summary>
    /// Tests element with border property producing SVG stroke rect.
    /// Verifies that the border shorthand is parsed correctly and rendered
    /// as an SVG rect with fill="none" and appropriate stroke attributes.
    /// </summary>
    [Fact]
    public void SvgBorderBasic()
    {
        var template = CreateTemplate(300, 150);

        var box = new FlexElement
        {
            Direction = FlexDirection.Column,
            Width = "200",
            Height = "100",
            Border = "2 solid #cc0000",
            Padding = "10"
        };

        box.AddChild(new TextElement
        {
            Content = "Bordered Box",
            Size = "14",
            Color = "#333333"
        });

        template.AddElement(box);

        AssertSvgSnapshot("svg_border_basic", template, new ObjectValue());
    }

    /// <summary>
    /// Tests QR code element rendering as native SVG paths.
    /// Verifies that the QrSvgProvider generates vector path elements
    /// rather than rasterized base64 images.
    /// </summary>
    [Fact]
    public void SvgQrBasic()
    {
        var template = CreateTemplate(200, 200);

        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Padding = "10"
        };

        flex.AddChild(new QrElement
        {
            Data = "https://example.com",
            Width = "150",
            Height = "150",
            Foreground = "#000000",
            Background = "#ffffff"
        });

        template.AddElement(flex);

        AssertSvgSnapshot("svg_qr_basic", template, new ObjectValue());
    }

    /// <summary>
    /// Tests barcode element rendering as native SVG paths.
    /// Verifies that the BarcodeSvgProvider generates vector path elements
    /// for Code 128 barcodes with correct foreground and background fills.
    /// </summary>
    [Fact]
    public void SvgBarcodeBasic()
    {
        var template = CreateTemplate(300, 120);

        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Padding = "10"
        };

        flex.AddChild(new BarcodeElement
        {
            Data = "ABC-123",
            Format = BarcodeFormat.Code128,
            BarcodeWidth = 250,
            BarcodeHeight = 80,
            Foreground = "#000000",
            Background = "#ffffff"
        });

        template.AddElement(flex);

        AssertSvgSnapshot("svg_barcode_basic", template, new ObjectValue());
    }
}
