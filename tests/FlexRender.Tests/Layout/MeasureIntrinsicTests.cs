using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for the MeasureIntrinsic pass that computes intrinsic sizes bottom-up.
/// </summary>
public class MeasureIntrinsicTests
{
    private readonly LayoutEngine _engine = new();

    // ============================================
    // TextElement Intrinsic Sizing
    // ============================================

    [Fact]
    public void MeasureIntrinsic_TextElement_DefaultSize_ReturnsLineHeight()
    {
        var text = new TextElement { Content = "Hello", Size = "16" };

        var sizes = _engine.MeasureAllIntrinsics(text);
        var size = sizes[text];

        // Default fontSize=16, lineHeight = 16 * 1.4 = 22.4
        Assert.Equal(22.4f, size.MinHeight, 1);
        Assert.Equal(22.4f, size.MaxHeight, 1);
        Assert.Equal(0f, size.MinWidth);
        Assert.Equal(0f, size.MaxWidth);
    }

    [Fact]
    public void MeasureIntrinsic_TextElement_WithExplicitWidth_OverridesIntrinsic()
    {
        var text = new TextElement { Content = "Hello", Size = "16", Width = "200" };

        var sizes = _engine.MeasureAllIntrinsics(text);
        var size = sizes[text];

        Assert.Equal(200f, size.MinWidth);
        Assert.Equal(200f, size.MaxWidth);
    }

    [Fact]
    public void MeasureIntrinsic_TextElement_WithExplicitHeight_OverridesIntrinsic()
    {
        var text = new TextElement { Content = "Hello", Size = "16", Height = "50" };

        var sizes = _engine.MeasureAllIntrinsics(text);
        var size = sizes[text];

        Assert.Equal(50f, size.MinHeight);
        Assert.Equal(50f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_TextElement_WithPadding_AddsPadding()
    {
        var text = new TextElement { Content = "Hello", Size = "16", Padding = "10" };

        var sizes = _engine.MeasureAllIntrinsics(text);
        var size = sizes[text];

        // Height: 16 * 1.4 = 22.4 + 10*2 = 42.4
        Assert.Equal(42.4f, size.MinHeight, 1);
        Assert.Equal(42.4f, size.MaxHeight, 1);
        // Width: 0 + 10*2 = 20
        Assert.Equal(20f, size.MinWidth, 1);
        Assert.Equal(20f, size.MaxWidth, 1);
    }

    [Fact]
    public void MeasureIntrinsic_TextElement_WithMargin_AddsMargin()
    {
        var text = new TextElement { Content = "Hello", Size = "16", Margin = "5" };

        var sizes = _engine.MeasureAllIntrinsics(text);
        var size = sizes[text];

        // Height: 16 * 1.4 = 22.4 + 5*2 = 32.4
        Assert.Equal(32.4f, size.MinHeight, 1);
        Assert.Equal(32.4f, size.MaxHeight, 1);
    }

    // ============================================
    // QrElement Intrinsic Sizing
    // ============================================

    [Fact]
    public void MeasureIntrinsic_QrElement_ReturnsQrSize()
    {
        var qr = new QrElement { Data = "test", Size = 80 };

        var sizes = _engine.MeasureAllIntrinsics(qr);
        var size = sizes[qr];

        Assert.Equal(80f, size.MinWidth);
        Assert.Equal(80f, size.MaxWidth);
        Assert.Equal(80f, size.MinHeight);
        Assert.Equal(80f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_QrElement_WithPadding_AddsPadding()
    {
        var qr = new QrElement { Data = "test", Size = 80, Padding = "5" };

        var sizes = _engine.MeasureAllIntrinsics(qr);
        var size = sizes[qr];

        Assert.Equal(90f, size.MinWidth);
        Assert.Equal(90f, size.MaxWidth);
        Assert.Equal(90f, size.MinHeight);
        Assert.Equal(90f, size.MaxHeight);
    }

    // ============================================
    // BarcodeElement Intrinsic Sizing
    // ============================================

    [Fact]
    public void MeasureIntrinsic_BarcodeElement_ReturnsBarcodeSize()
    {
        var barcode = new BarcodeElement { Data = "123", BarcodeWidth = 200, BarcodeHeight = 80 };

        var sizes = _engine.MeasureAllIntrinsics(barcode);
        var size = sizes[barcode];

        Assert.Equal(200f, size.MinWidth);
        Assert.Equal(200f, size.MaxWidth);
        Assert.Equal(80f, size.MinHeight);
        Assert.Equal(80f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_BarcodeElement_WithPadding_AddsPadding()
    {
        var barcode = new BarcodeElement { Data = "123", BarcodeWidth = 200, BarcodeHeight = 80, Padding = "10" };

        var sizes = _engine.MeasureAllIntrinsics(barcode);
        var size = sizes[barcode];

        Assert.Equal(220f, size.MinWidth);
        Assert.Equal(220f, size.MaxWidth);
        Assert.Equal(100f, size.MinHeight);
        Assert.Equal(100f, size.MaxHeight);
    }

    // ============================================
    // ImageElement Intrinsic Sizing
    // ============================================

    [Fact]
    public void MeasureIntrinsic_ImageElement_WithExplicitSize_ReturnsExplicitSize()
    {
        var image = new ImageElement { Src = "test.png", ImageWidth = 100, ImageHeight = 50 };

        var sizes = _engine.MeasureAllIntrinsics(image);
        var size = sizes[image];

        Assert.Equal(100f, size.MinWidth);
        Assert.Equal(100f, size.MaxWidth);
        Assert.Equal(50f, size.MinHeight);
        Assert.Equal(50f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_ImageElement_WithoutSize_ReturnsZero()
    {
        var image = new ImageElement { Src = "test.png" };

        var sizes = _engine.MeasureAllIntrinsics(image);
        var size = sizes[image];

        Assert.Equal(0f, size.MinWidth);
        Assert.Equal(0f, size.MaxWidth);
        Assert.Equal(0f, size.MinHeight);
        Assert.Equal(0f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_ImageElement_WithPadding_AddsPadding()
    {
        var image = new ImageElement { Src = "test.png", ImageWidth = 100, ImageHeight = 50, Padding = "8" };

        var sizes = _engine.MeasureAllIntrinsics(image);
        var size = sizes[image];

        Assert.Equal(116f, size.MinWidth);   // 100 + 8*2
        Assert.Equal(116f, size.MaxWidth);
        Assert.Equal(66f, size.MinHeight);   // 50 + 8*2
        Assert.Equal(66f, size.MaxHeight);
    }

    // ============================================
    // FlexElement Intrinsic Sizing - Column
    // ============================================

    [Fact]
    public void MeasureIntrinsic_FlexColumn_SumsChildrenHeights()
    {
        var flex = new FlexElement { Direction = FlexDirection.Column };
        flex.AddChild(new TextElement { Content = "A", Size = "16", Height = "30" });
        flex.AddChild(new TextElement { Content = "B", Size = "16", Height = "40" });

        var sizes = _engine.MeasureAllIntrinsics(flex);
        var size = sizes[flex];

        Assert.Equal(70f, size.MinHeight);
        Assert.Equal(70f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_FlexColumn_MaxChildWidth()
    {
        var flex = new FlexElement { Direction = FlexDirection.Column };
        flex.AddChild(new QrElement { Data = "a", Size = 50 });
        flex.AddChild(new QrElement { Data = "b", Size = 80 });

        var sizes = _engine.MeasureAllIntrinsics(flex);
        var size = sizes[flex];

        Assert.Equal(80f, size.MaxWidth);
        Assert.Equal(80f, size.MinWidth);
    }

    [Fact]
    public void MeasureIntrinsic_FlexColumn_WithGap_AddsGapToHeight()
    {
        var flex = new FlexElement { Direction = FlexDirection.Column, Gap = "10" };
        flex.AddChild(new TextElement { Content = "A", Height = "30" });
        flex.AddChild(new TextElement { Content = "B", Height = "40" });

        var sizes = _engine.MeasureAllIntrinsics(flex);
        var size = sizes[flex];

        Assert.Equal(80f, size.MinHeight);
        Assert.Equal(80f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_FlexColumn_WithPadding_AddsPadding()
    {
        var flex = new FlexElement { Direction = FlexDirection.Column, Padding = "15" };
        flex.AddChild(new QrElement { Data = "a", Size = 50 });

        var sizes = _engine.MeasureAllIntrinsics(flex);
        var size = sizes[flex];

        Assert.Equal(80f, size.MaxWidth);
        Assert.Equal(80f, size.MaxHeight);
    }

    // ============================================
    // FlexElement Intrinsic Sizing - Row
    // ============================================

    [Fact]
    public void MeasureIntrinsic_FlexRow_SumsChildrenWidths()
    {
        var flex = new FlexElement { Direction = FlexDirection.Row };
        flex.AddChild(new QrElement { Data = "a", Size = 50 });
        flex.AddChild(new QrElement { Data = "b", Size = 80 });

        var sizes = _engine.MeasureAllIntrinsics(flex);
        var size = sizes[flex];

        Assert.Equal(130f, size.MaxWidth);
    }

    [Fact]
    public void MeasureIntrinsic_FlexRow_MaxChildHeight()
    {
        var flex = new FlexElement { Direction = FlexDirection.Row };
        flex.AddChild(new BarcodeElement { Data = "a", BarcodeWidth = 100, BarcodeHeight = 30 });
        flex.AddChild(new BarcodeElement { Data = "b", BarcodeWidth = 100, BarcodeHeight = 60 });

        var sizes = _engine.MeasureAllIntrinsics(flex);
        var size = sizes[flex];

        Assert.Equal(60f, size.MaxHeight);
        Assert.Equal(60f, size.MinHeight);
    }

    [Fact]
    public void MeasureIntrinsic_FlexRow_WithGap_AddsGapToWidth()
    {
        var flex = new FlexElement { Direction = FlexDirection.Row, Gap = "8" };
        flex.AddChild(new QrElement { Data = "a", Size = 50 });
        flex.AddChild(new QrElement { Data = "b", Size = 50 });

        var sizes = _engine.MeasureAllIntrinsics(flex);
        var size = sizes[flex];

        Assert.Equal(108f, size.MaxWidth);
    }

    // ============================================
    // FlexElement with explicit Width/Height overrides
    // ============================================

    [Fact]
    public void MeasureIntrinsic_FlexElement_WithExplicitWidth_OverridesIntrinsic()
    {
        var flex = new FlexElement { Direction = FlexDirection.Column, Width = "200" };
        flex.AddChild(new QrElement { Data = "a", Size = 50 });

        var sizes = _engine.MeasureAllIntrinsics(flex);
        var size = sizes[flex];

        Assert.Equal(200f, size.MinWidth);
        Assert.Equal(200f, size.MaxWidth);
    }

    [Fact]
    public void MeasureIntrinsic_FlexElement_WithExplicitHeight_OverridesIntrinsic()
    {
        var flex = new FlexElement { Direction = FlexDirection.Column, Height = "300" };
        flex.AddChild(new QrElement { Data = "a", Size = 50 });

        var sizes = _engine.MeasureAllIntrinsics(flex);
        var size = sizes[flex];

        Assert.Equal(300f, size.MinHeight);
        Assert.Equal(300f, size.MaxHeight);
    }

    // ============================================
    // Nested Flex
    // ============================================

    [Fact]
    public void MeasureIntrinsic_NestedFlex_ComputesRecursively()
    {
        var innerFlex = new FlexElement { Direction = FlexDirection.Row };
        innerFlex.AddChild(new QrElement { Data = "a", Size = 40 });
        innerFlex.AddChild(new QrElement { Data = "b", Size = 40 });

        var outerFlex = new FlexElement { Direction = FlexDirection.Column };
        outerFlex.AddChild(innerFlex);
        outerFlex.AddChild(new BarcodeElement { Data = "c", BarcodeWidth = 100, BarcodeHeight = 30 });

        var sizes = _engine.MeasureAllIntrinsics(outerFlex);
        var outerSize = sizes[outerFlex];
        var innerSize = sizes[innerFlex];

        Assert.Equal(80f, innerSize.MaxWidth);
        Assert.Equal(40f, innerSize.MaxHeight);

        Assert.Equal(100f, outerSize.MaxWidth);
        Assert.Equal(70f, outerSize.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_EmptyFlex_ReturnsZero()
    {
        var flex = new FlexElement { Direction = FlexDirection.Column };

        var sizes = _engine.MeasureAllIntrinsics(flex);
        var size = sizes[flex];

        Assert.Equal(0f, size.MinWidth);
        Assert.Equal(0f, size.MaxWidth);
        Assert.Equal(0f, size.MinHeight);
        Assert.Equal(0f, size.MaxHeight);
    }

    // ============================================
    // SeparatorElement Intrinsic Sizing
    // ============================================

    [Fact]
    public void MeasureIntrinsic_HorizontalSeparator_ReturnsThicknessAsHeight()
    {
        var separator = new SeparatorElement
        {
            Orientation = SeparatorOrientation.Horizontal,
            Thickness = 2f
        };

        var sizes = _engine.MeasureAllIntrinsics(separator);
        var size = sizes[separator];

        Assert.Equal(0f, size.MinWidth);
        Assert.Equal(0f, size.MaxWidth);
        Assert.Equal(2f, size.MinHeight);
        Assert.Equal(2f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_VerticalSeparator_ReturnsThicknessAsWidth()
    {
        var separator = new SeparatorElement
        {
            Orientation = SeparatorOrientation.Vertical,
            Thickness = 3f
        };

        var sizes = _engine.MeasureAllIntrinsics(separator);
        var size = sizes[separator];

        Assert.Equal(3f, size.MinWidth);
        Assert.Equal(3f, size.MaxWidth);
        Assert.Equal(0f, size.MinHeight);
        Assert.Equal(0f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_HorizontalSeparator_WithPadding_AddsPadding()
    {
        var separator = new SeparatorElement
        {
            Orientation = SeparatorOrientation.Horizontal,
            Thickness = 2f,
            Padding = "5"
        };

        var sizes = _engine.MeasureAllIntrinsics(separator);
        var size = sizes[separator];

        // Height: 2 + 5*2 = 12, Width: 0 + 5*2 = 10
        Assert.Equal(10f, size.MinWidth);
        Assert.Equal(10f, size.MaxWidth);
        Assert.Equal(12f, size.MinHeight);
        Assert.Equal(12f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_VerticalSeparator_WithMargin_AddsMargin()
    {
        var separator = new SeparatorElement
        {
            Orientation = SeparatorOrientation.Vertical,
            Thickness = 1f,
            Margin = "4"
        };

        var sizes = _engine.MeasureAllIntrinsics(separator);
        var size = sizes[separator];

        // Width: 1 + 4*2 = 9, Height: 0 + 4*2 = 8
        Assert.Equal(9f, size.MinWidth);
        Assert.Equal(9f, size.MaxWidth);
        Assert.Equal(8f, size.MinHeight);
        Assert.Equal(8f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_DefaultSeparator_ReturnsThickness1()
    {
        var separator = new SeparatorElement();

        var sizes = _engine.MeasureAllIntrinsics(separator);
        var size = sizes[separator];

        Assert.Equal(0f, size.MinWidth);
        Assert.Equal(0f, size.MaxWidth);
        Assert.Equal(1f, size.MinHeight);
        Assert.Equal(1f, size.MaxHeight);
    }

    // ============================================
    // Non-Uniform Padding Intrinsic Tests
    // ============================================

    [Fact]
    public void MeasureIntrinsic_TextElement_WithTwoValuePadding_AppliesNonUniform()
    {
        // padding: "10 20" -> top/bottom=10, left/right=20
        var text = new TextElement { Content = "Hello", Size = "16", Padding = "10 20" };

        var sizes = _engine.MeasureAllIntrinsics(text);
        var size = sizes[text];

        // Height: 16 * 1.4 = 22.4 + top(10) + bottom(10) = 42.4
        Assert.Equal(42.4f, size.MinHeight, 1);
        Assert.Equal(42.4f, size.MaxHeight, 1);
        // Width: 0 + left(20) + right(20) = 40
        Assert.Equal(40f, size.MinWidth, 1);
        Assert.Equal(40f, size.MaxWidth, 1);
    }

    [Fact]
    public void MeasureIntrinsic_TextElement_WithFourValuePadding_AppliesNonUniform()
    {
        // padding: "10 20 30 40" -> top=10, right=20, bottom=30, left=40
        var text = new TextElement { Content = "Hello", Size = "16", Padding = "10 20 30 40" };

        var sizes = _engine.MeasureAllIntrinsics(text);
        var size = sizes[text];

        // Height: 16 * 1.4 = 22.4 + top(10) + bottom(30) = 62.4
        Assert.Equal(62.4f, size.MinHeight, 1);
        Assert.Equal(62.4f, size.MaxHeight, 1);
        // Width: 0 + left(40) + right(20) = 60
        Assert.Equal(60f, size.MinWidth, 1);
        Assert.Equal(60f, size.MaxWidth, 1);
    }

    [Fact]
    public void MeasureIntrinsic_QrElement_WithTwoValuePadding_AppliesNonUniform()
    {
        // padding: "5 15" -> top/bottom=5, left/right=15
        var qr = new QrElement { Data = "test", Size = 80, Padding = "5 15" };

        var sizes = _engine.MeasureAllIntrinsics(qr);
        var size = sizes[qr];

        // Width: 80 + left(15) + right(15) = 110
        Assert.Equal(110f, size.MinWidth);
        Assert.Equal(110f, size.MaxWidth);
        // Height: 80 + top(5) + bottom(5) = 90
        Assert.Equal(90f, size.MinHeight);
        Assert.Equal(90f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_FlexColumn_WithNonUniformPadding_AddsPadding()
    {
        // padding: "10 20" -> top/bottom=10, left/right=20
        var flex = new FlexElement { Direction = FlexDirection.Column, Padding = "10 20" };
        flex.AddChild(new QrElement { Data = "a", Size = 50 });

        var sizes = _engine.MeasureAllIntrinsics(flex);
        var size = sizes[flex];

        // Width: 50 + left(20) + right(20) = 90
        Assert.Equal(90f, size.MaxWidth);
        // Height: 50 + top(10) + bottom(10) = 70
        Assert.Equal(70f, size.MaxHeight);
    }

    [Fact]
    public void MeasureIntrinsic_FlexColumn_WithFourValuePadding_AddsPadding()
    {
        // padding: "10 20 30 40" -> top=10, right=20, bottom=30, left=40
        var flex = new FlexElement { Direction = FlexDirection.Column, Padding = "10 20 30 40" };
        flex.AddChild(new QrElement { Data = "a", Size = 50 });

        var sizes = _engine.MeasureAllIntrinsics(flex);
        var size = sizes[flex];

        // Width: 50 + left(40) + right(20) = 110
        Assert.Equal(110f, size.MaxWidth);
        // Height: 50 + top(10) + bottom(30) = 90
        Assert.Equal(90f, size.MaxHeight);
    }
}
