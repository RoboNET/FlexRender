using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Snapshots;

/// <summary>
/// Visual snapshot tests for FlexRender rendering.
/// Tests various element types and layout configurations against golden images.
/// </summary>
/// <remarks>
/// <para>
/// This test class contains 26 snapshot tests covering:
/// <list type="bullet">
/// <item>Text elements (simple, styled, multiline, variables)</item>
/// <item>QR codes and barcodes</item>
/// <item>Images with different fit modes</item>
/// <item>Flex layouts (column, row, nested, grow, alignment, mixed content)</item>
/// <item>Background and spacing (background colors, padding, margin)</item>
/// </list>
/// </para>
/// <para>
/// All tests use programmatic template construction for precise control.
/// Run with <c>UPDATE_SNAPSHOTS=true</c> to regenerate golden images.
/// </para>
/// </remarks>
public sealed class VisualSnapshotTests : SnapshotTestBase
{
    #region Text Elements

    /// <summary>
    /// Tests basic text rendering with default settings.
    /// </summary>
    [Fact]
    public void TextSimple()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(300, 200);
        template.AddElement(new TextElement
        {
            Content = "Hello World",
            Size = "16",
            Color = "#000000"
        });

        AssertSnapshot("text_simple", template, new ObjectValue());
    }

    /// <summary>
    /// Tests styled text with bold font, red color, and center alignment.
    /// </summary>
    [Fact]
    public void TextStyled()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(300, 200);
        template.AddElement(new TextElement
        {
            Content = "Styled Text",
            Size = "24",
            Color = "#cc0000",
            Align = TextAlign.Center,
            Font = "bold"
        });

        AssertSnapshot("text_styled", template, new ObjectValue());
    }

    /// <summary>
    /// Tests multiline text with maxLines constraint and ellipsis overflow.
    /// </summary>
    [Fact]
    public void TextMultiline()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(300, 200);
        template.AddElement(new TextElement
        {
            Content = "This is a very long text that should wrap to multiple lines and eventually be truncated with an ellipsis because it exceeds the maximum number of lines allowed.",
            Size = "14",
            Color = "#000000",
            Wrap = true,
            MaxLines = 2,
            Overflow = TextOverflow.Ellipsis,
            Width = "280"
        });

        AssertSnapshot("text_multiline", template, new ObjectValue());
    }

    /// <summary>
    /// Tests template variable substitution in text content.
    /// </summary>
    [Fact]
    public void TextVariables()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(300, 200);
        template.AddElement(new TextElement
        {
            Content = "Hello, {{name}}!",
            Size = "18",
            Color = "#000000"
        });

        var data = new ObjectValue
        {
            ["name"] = new StringValue("World")
        };

        AssertSnapshot("text_variables", template, data);
    }

    #endregion

    #region QR and Barcode

    /// <summary>
    /// Tests basic QR code generation.
    /// </summary>
    [Fact]
    public void QrBasic()
    {
        var template = CreateTemplate(300, 200);
        template.AddElement(new QrElement
        {
            Data = "https://example.com",
            Size = 100
        });

        AssertSnapshot("qr_basic", template, new ObjectValue());
    }

    /// <summary>
    /// Tests QR code with custom foreground and background colors.
    /// </summary>
    [Fact]
    public void QrStyled()
    {
        var template = CreateTemplate(300, 200);
        template.AddElement(new QrElement
        {
            Data = "https://example.com/styled",
            Size = 100,
            Foreground = "#000080",
            Background = "#f0f0f0"
        });

        AssertSnapshot("qr_styled", template, new ObjectValue());
    }

    /// <summary>
    /// Tests Code128 barcode generation with visible text.
    /// </summary>
    [Fact]
    public void BarcodeCode128()
    {
        var template = CreateTemplate(300, 200);
        template.AddElement(new BarcodeElement
        {
            Data = "ABC-123",
            Format = BarcodeFormat.Code128,
            BarcodeWidth = 200,
            BarcodeHeight = 80,
            ShowText = true
        });

        AssertSnapshot("barcode_code128", template, new ObjectValue());
    }

    #endregion

    #region Image

    /// <summary>
    /// Tests image rendering with contain fit mode.
    /// </summary>
    [Fact]
    public void ImageContain()
    {
        var imageData = CreateTestImageBase64(100, 60, SKColors.Blue);

        var template = CreateTemplate(300, 200);
        template.AddElement(new ImageElement
        {
            Src = imageData,
            ImageWidth = 150,
            ImageHeight = 150,
            Fit = ImageFit.Contain
        });

        AssertSnapshot("image_contain", template, new ObjectValue());
    }

    /// <summary>
    /// Tests image rendering with cover fit mode.
    /// </summary>
    [Fact]
    public void ImageCover()
    {
        var imageData = CreateTestImageBase64(100, 60, SKColors.Green);

        var template = CreateTemplate(300, 200);
        template.AddElement(new ImageElement
        {
            Src = imageData,
            ImageWidth = 150,
            ImageHeight = 150,
            Fit = ImageFit.Cover
        });

        AssertSnapshot("image_cover", template, new ObjectValue());
    }

    #endregion

    #region Flex Layout

    /// <summary>
    /// Tests vertical flex column layout with gap.
    /// </summary>
    [Fact]
    public void FlexColumn()
    {
        var template = CreateTemplate(300, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Gap = "10"
        };
        flex.AddChild(new TextElement { Content = "Item 1", Size = "14" });
        flex.AddChild(new TextElement { Content = "Item 2", Size = "14" });
        flex.AddChild(new TextElement { Content = "Item 3", Size = "14" });

        template.AddElement(flex);

        AssertSnapshot("flex_column", template, new ObjectValue());
    }

    /// <summary>
    /// Tests horizontal flex row layout with space-between justification.
    /// </summary>
    [Fact]
    public void FlexRow()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(300, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Justify = JustifyContent.SpaceBetween,
            Width = "300"
        };
        flex.AddChild(new TextElement { Content = "Left", Size = "14" });
        flex.AddChild(new TextElement { Content = "Center", Size = "14" });
        flex.AddChild(new TextElement { Content = "Right", Size = "14" });

        template.AddElement(flex);

        AssertSnapshot("flex_row", template, new ObjectValue());
    }

    /// <summary>
    /// Tests two levels of nested flex containers (row inside column).
    /// </summary>
    [Fact]
    public void FlexNested2Levels()
    {
        var template = CreateTemplate(300, 200);

        var innerRow = new FlexElement
        {
            Direction = FlexDirection.Row,
            Gap = "5"
        };
        innerRow.AddChild(new TextElement { Content = "A", Size = "14" });
        innerRow.AddChild(new TextElement { Content = "B", Size = "14" });

        var outerColumn = new FlexElement
        {
            Direction = FlexDirection.Column,
            Gap = "10"
        };
        outerColumn.AddChild(new TextElement { Content = "Header", Size = "16" });
        outerColumn.AddChild(innerRow);
        outerColumn.AddChild(new TextElement { Content = "Footer", Size = "14" });

        template.AddElement(outerColumn);

        AssertSnapshot("flex_nested_2levels", template, new ObjectValue());
    }

    /// <summary>
    /// Tests three levels of nested flex containers.
    /// </summary>
    [Fact]
    public void FlexNested3Levels()
    {
        var template = CreateTemplate(300, 200);

        var innermost = new FlexElement
        {
            Direction = FlexDirection.Row,
            Gap = "3"
        };
        innermost.AddChild(new TextElement { Content = "X", Size = "12" });
        innermost.AddChild(new TextElement { Content = "Y", Size = "12" });

        var middle = new FlexElement
        {
            Direction = FlexDirection.Column,
            Gap = "5"
        };
        middle.AddChild(new TextElement { Content = "Middle", Size = "14" });
        middle.AddChild(innermost);

        var outer = new FlexElement
        {
            Direction = FlexDirection.Column,
            Gap = "10"
        };
        outer.AddChild(new TextElement { Content = "Outer", Size = "16" });
        outer.AddChild(middle);
        outer.AddChild(new TextElement { Content = "End", Size = "14" });

        template.AddElement(outer);

        AssertSnapshot("flex_nested_3levels", template, new ObjectValue());
    }

    /// <summary>
    /// Tests flex grow distribution among child elements (1:2:1 ratio).
    /// </summary>
    [Fact]
    public void FlexGrowDistribute()
    {
        var template = CreateTemplate(300, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "280"
        };
        flex.AddChild(new TextElement { Content = "1x", Size = "14", Grow = 1 });
        flex.AddChild(new TextElement { Content = "2x", Size = "14", Grow = 2 });
        flex.AddChild(new TextElement { Content = "1x", Size = "14", Grow = 1 });

        template.AddElement(flex);

        AssertSnapshot("flex_grow_distribute", template, new ObjectValue());
    }

    /// <summary>
    /// Tests cross-axis alignment with center alignment.
    /// </summary>
    [Fact]
    public void FlexAlignItems()
    {
        var template = CreateTemplate(300, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Align = AlignItems.Center,
            Height = "100",
            Width = "280"
        };
        flex.AddChild(new TextElement { Content = "Small", Size = "12" });
        flex.AddChild(new TextElement { Content = "Medium", Size = "18" });
        flex.AddChild(new TextElement { Content = "Large", Size = "24" });

        template.AddElement(flex);

        AssertSnapshot("flex_align_items", template, new ObjectValue());
    }

    /// <summary>
    /// Tests space-around justification on main axis.
    /// </summary>
    [Fact]
    public void FlexJustifyAll()
    {
        var template = CreateTemplate(300, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Justify = JustifyContent.SpaceAround,
            Width = "280"
        };
        flex.AddChild(new TextElement { Content = "A", Size = "14" });
        flex.AddChild(new TextElement { Content = "B", Size = "14" });
        flex.AddChild(new TextElement { Content = "C", Size = "14" });

        template.AddElement(flex);

        AssertSnapshot("flex_justify_all", template, new ObjectValue());
    }

    /// <summary>
    /// Tests <c>align: start</c> with boxes of different heights aligned to the top of the container.
    /// </summary>
    [Fact]
    public void FlexAlignStart()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(400, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Align = AlignItems.Start,
            Width = "380",
            Height = "140",
            Gap = "10",
            Background = "#f0f0f0"
        };

        var box1 = new FlexElement
        {
            Background = "#3498db",
            Width = "80",
            Height = "40",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "40px", Size = "12", Color = "#ffffff" }
            }
        };

        var box2 = new FlexElement
        {
            Background = "#e74c3c",
            Width = "80",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "60px", Size = "12", Color = "#ffffff" }
            }
        };

        var box3 = new FlexElement
        {
            Background = "#2ecc71",
            Width = "80",
            Height = "80",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "80px", Size = "12", Color = "#ffffff" }
            }
        };

        flex.AddChild(box1);
        flex.AddChild(box2);
        flex.AddChild(box3);
        template.AddElement(flex);

        AssertSnapshot("flex_align_start", template, new ObjectValue());
    }

    /// <summary>
    /// Tests <c>align: center</c> with boxes of different heights centered vertically in the container.
    /// </summary>
    [Fact]
    public void FlexAlignCenter()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(400, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Align = AlignItems.Center,
            Width = "380",
            Height = "140",
            Gap = "10",
            Background = "#f0f0f0"
        };

        var box1 = new FlexElement
        {
            Background = "#3498db",
            Width = "80",
            Height = "40",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "40px", Size = "12", Color = "#ffffff" }
            }
        };

        var box2 = new FlexElement
        {
            Background = "#e74c3c",
            Width = "80",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "60px", Size = "12", Color = "#ffffff" }
            }
        };

        var box3 = new FlexElement
        {
            Background = "#2ecc71",
            Width = "80",
            Height = "80",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "80px", Size = "12", Color = "#ffffff" }
            }
        };

        flex.AddChild(box1);
        flex.AddChild(box2);
        flex.AddChild(box3);
        template.AddElement(flex);

        AssertSnapshot("flex_align_center", template, new ObjectValue());
    }

    /// <summary>
    /// Tests <c>align: end</c> with boxes of different heights aligned to the bottom of the container.
    /// </summary>
    [Fact]
    public void FlexAlignEnd()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(400, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Align = AlignItems.End,
            Width = "380",
            Height = "140",
            Gap = "10",
            Background = "#f0f0f0"
        };

        var box1 = new FlexElement
        {
            Background = "#3498db",
            Width = "80",
            Height = "40",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "40px", Size = "12", Color = "#ffffff" }
            }
        };

        var box2 = new FlexElement
        {
            Background = "#e74c3c",
            Width = "80",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "60px", Size = "12", Color = "#ffffff" }
            }
        };

        var box3 = new FlexElement
        {
            Background = "#2ecc71",
            Width = "80",
            Height = "80",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "80px", Size = "12", Color = "#ffffff" }
            }
        };

        flex.AddChild(box1);
        flex.AddChild(box2);
        flex.AddChild(box3);
        template.AddElement(flex);

        AssertSnapshot("flex_align_end", template, new ObjectValue());
    }

    /// <summary>
    /// Tests <c>align: stretch</c> with boxes stretching to fill the full container height.
    /// Child boxes have no explicit height so they stretch to the container's 140px height.
    /// </summary>
    [Fact]
    public void FlexAlignStretch()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(400, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Align = AlignItems.Stretch,
            Width = "380",
            Height = "140",
            Gap = "10",
            Background = "#f0f0f0"
        };

        var box1 = new FlexElement
        {
            Background = "#3498db",
            Width = "80",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "Stretch", Size = "12", Color = "#ffffff" }
            }
        };

        var box2 = new FlexElement
        {
            Background = "#e74c3c",
            Width = "80",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "Stretch", Size = "12", Color = "#ffffff" }
            }
        };

        var box3 = new FlexElement
        {
            Background = "#2ecc71",
            Width = "80",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "Stretch", Size = "12", Color = "#ffffff" }
            }
        };

        flex.AddChild(box1);
        flex.AddChild(box2);
        flex.AddChild(box3);
        template.AddElement(flex);

        AssertSnapshot("flex_align_stretch", template, new ObjectValue());
    }

    /// <summary>
    /// Tests <c>align: baseline</c> with text elements of different font sizes
    /// aligned along their text baselines.
    /// </summary>
    [Fact]
    public void FlexAlignBaseline()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(400, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Align = AlignItems.Baseline,
            Width = "380",
            Height = "140",
            Gap = "10",
            Background = "#f0f0f0"
        };

        var box1 = new FlexElement
        {
            Background = "#3498db",
            Padding = "8",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "Small", Size = "12", Color = "#ffffff" }
            }
        };

        var box2 = new FlexElement
        {
            Background = "#e74c3c",
            Padding = "8",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "Medium", Size = "20", Color = "#ffffff" }
            }
        };

        var box3 = new FlexElement
        {
            Background = "#2ecc71",
            Padding = "8",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "Large", Size = "32", Color = "#ffffff" }
            }
        };

        flex.AddChild(box1);
        flex.AddChild(box2);
        flex.AddChild(box3);
        template.AddElement(flex);

        AssertSnapshot("flex_align_baseline", template, new ObjectValue());
    }

    /// <summary>
    /// Tests <c>justify: start</c> with boxes packed toward the start of the main axis.
    /// </summary>
    [Fact]
    public void FlexJustifyStart()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(400, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Justify = JustifyContent.Start,
            Width = "380",
            Height = "140",
            Gap = "10",
            Background = "#f0f0f0"
        };

        var box1 = new FlexElement
        {
            Background = "#3498db",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "A", Size = "14", Color = "#ffffff" }
            }
        };

        var box2 = new FlexElement
        {
            Background = "#e74c3c",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "B", Size = "14", Color = "#ffffff" }
            }
        };

        var box3 = new FlexElement
        {
            Background = "#2ecc71",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "C", Size = "14", Color = "#ffffff" }
            }
        };

        flex.AddChild(box1);
        flex.AddChild(box2);
        flex.AddChild(box3);
        template.AddElement(flex);

        AssertSnapshot("flex_justify_start", template, new ObjectValue());
    }

    /// <summary>
    /// Tests <c>justify: center</c> with boxes centered along the main axis.
    /// </summary>
    [Fact]
    public void FlexJustifyCenter()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(400, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Justify = JustifyContent.Center,
            Width = "380",
            Height = "140",
            Gap = "10",
            Background = "#f0f0f0"
        };

        var box1 = new FlexElement
        {
            Background = "#3498db",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "A", Size = "14", Color = "#ffffff" }
            }
        };

        var box2 = new FlexElement
        {
            Background = "#e74c3c",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "B", Size = "14", Color = "#ffffff" }
            }
        };

        var box3 = new FlexElement
        {
            Background = "#2ecc71",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "C", Size = "14", Color = "#ffffff" }
            }
        };

        flex.AddChild(box1);
        flex.AddChild(box2);
        flex.AddChild(box3);
        template.AddElement(flex);

        AssertSnapshot("flex_justify_center", template, new ObjectValue());
    }

    /// <summary>
    /// Tests <c>justify: end</c> with boxes packed toward the end of the main axis.
    /// </summary>
    [Fact]
    public void FlexJustifyEnd()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(400, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Justify = JustifyContent.End,
            Width = "380",
            Height = "140",
            Gap = "10",
            Background = "#f0f0f0"
        };

        var box1 = new FlexElement
        {
            Background = "#3498db",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "A", Size = "14", Color = "#ffffff" }
            }
        };

        var box2 = new FlexElement
        {
            Background = "#e74c3c",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "B", Size = "14", Color = "#ffffff" }
            }
        };

        var box3 = new FlexElement
        {
            Background = "#2ecc71",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "C", Size = "14", Color = "#ffffff" }
            }
        };

        flex.AddChild(box1);
        flex.AddChild(box2);
        flex.AddChild(box3);
        template.AddElement(flex);

        AssertSnapshot("flex_justify_end", template, new ObjectValue());
    }

    /// <summary>
    /// Tests <c>justify: space-between</c> with even distribution of space between boxes.
    /// First item at start, last item at end, remaining space distributed evenly.
    /// </summary>
    [Fact]
    public void FlexJustifySpaceBetween()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(400, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Justify = JustifyContent.SpaceBetween,
            Width = "380",
            Height = "140",
            Background = "#f0f0f0"
        };

        var box1 = new FlexElement
        {
            Background = "#3498db",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "A", Size = "14", Color = "#ffffff" }
            }
        };

        var box2 = new FlexElement
        {
            Background = "#e74c3c",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "B", Size = "14", Color = "#ffffff" }
            }
        };

        var box3 = new FlexElement
        {
            Background = "#2ecc71",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "C", Size = "14", Color = "#ffffff" }
            }
        };

        flex.AddChild(box1);
        flex.AddChild(box2);
        flex.AddChild(box3);
        template.AddElement(flex);

        AssertSnapshot("flex_justify_space_between", template, new ObjectValue());
    }

    /// <summary>
    /// Tests <c>justify: space-evenly</c> with equal spacing around and between all boxes.
    /// The space before the first item, between each item, and after the last item are all equal.
    /// </summary>
    [Fact]
    public void FlexJustifySpaceEvenly()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(400, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Justify = JustifyContent.SpaceEvenly,
            Width = "380",
            Height = "140",
            Background = "#f0f0f0"
        };

        var box1 = new FlexElement
        {
            Background = "#3498db",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "A", Size = "14", Color = "#ffffff" }
            }
        };

        var box2 = new FlexElement
        {
            Background = "#e74c3c",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "B", Size = "14", Color = "#ffffff" }
            }
        };

        var box3 = new FlexElement
        {
            Background = "#2ecc71",
            Width = "60",
            Height = "60",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "C", Size = "14", Color = "#ffffff" }
            }
        };

        flex.AddChild(box1);
        flex.AddChild(box2);
        flex.AddChild(box3);
        template.AddElement(flex);

        AssertSnapshot("flex_justify_space_evenly", template, new ObjectValue());
    }

    /// <summary>
    /// Tests space-between justification with FlexElement children in a row.
    /// Verifies that FlexElement children use intrinsic sizing, leaving free
    /// space for justify-content to distribute.
    /// </summary>
    [Fact]
    public void FlexRowJustifyWithFlexChildren()
    {
        var template = CreateTemplate(400, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Justify = JustifyContent.SpaceBetween,
            Width = "380",
            Padding = "10"
        };

        var child1 = new FlexElement
        {
            Background = "#e74c3c",
            Padding = "8 16",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "Start", Size = "14", Color = "#ffffff" }
            }
        };

        var child2 = new FlexElement
        {
            Background = "#3498db",
            Padding = "8 16",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "Middle", Size = "14", Color = "#ffffff" }
            }
        };

        var child3 = new FlexElement
        {
            Background = "#2ecc71",
            Padding = "8 16",
            Children = new List<TemplateElement>
            {
                new TextElement { Content = "End", Size = "14", Color = "#ffffff" }
            }
        };

        flex.AddChild(child1);
        flex.AddChild(child2);
        flex.AddChild(child3);
        template.AddElement(flex);

        AssertSnapshot("flex_row_justify_with_flex_children", template, new ObjectValue());
    }

    /// <summary>
    /// Tests that column flex with auto-height and justify: center does not
    /// produce negative child positions when content exceeds available space.
    /// </summary>
    [Fact]
    public void FlexColumnAutoHeightJustifyCenter()
    {
        var template = CreateTemplate(300, 200);
        var row = new FlexElement
        {
            Direction = FlexDirection.Row,
            Gap = "10"
        };

        var column = new FlexElement
        {
            Justify = JustifyContent.Center,
            Align = AlignItems.Center,
            Gap = "8",
            Children = new List<TemplateElement>
            {
                new BarcodeElement
                {
                    Data = "TEST-123",
                    BarcodeWidth = 200,
                    BarcodeHeight = 60,
                    ShowText = true
                },
                new TextElement { Content = "Label text", Size = "10" }
            }
        };

        row.AddChild(column);
        template.AddElement(row);

        AssertSnapshot("flex_column_autoheight_justify_center", template, new ObjectValue());
    }

    /// <summary>
    /// Tests mixed content types (text, QR, barcode) in a single flex row.
    /// </summary>
    [Fact]
    public void FlexMixedContent()
    {
        var template = CreateTemplate(300, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Align = AlignItems.Center,
            Gap = "10"
        };
        flex.AddChild(new TextElement { Content = "Code:", Size = "14" });
        flex.AddChild(new QrElement { Data = "TEST", Size = 50 });
        flex.AddChild(new BarcodeElement { Data = "123", BarcodeWidth = 80, BarcodeHeight = 40, ShowText = false });

        template.AddElement(flex);

        AssertSnapshot("flex_mixed_content", template, new ObjectValue());
    }

    /// <summary>
    /// Tests percentage-based width distribution (30% and 70%).
    /// </summary>
    [Fact]
    public void FlexPercentWidths()
    {
        var template = CreateTemplate(300, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "280"
        };
        flex.AddChild(new TextElement { Content = "30%", Size = "14", Width = "30%" });
        flex.AddChild(new TextElement { Content = "70%", Size = "14", Width = "70%" });

        template.AddElement(flex);

        AssertSnapshot("flex_percent_widths", template, new ObjectValue());
    }

    /// <summary>
    /// Tests combination of padding and gap with multiple children.
    /// </summary>
    [Fact]
    public void FlexPaddingGapCombo()
    {
        var template = CreateTemplate(300, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Padding = "20",
            Gap = "10",
            Width = "280"
        };
        flex.AddChild(new TextElement { Content = "First", Size = "14" });
        flex.AddChild(new TextElement { Content = "Second", Size = "14" });
        flex.AddChild(new TextElement { Content = "Third", Size = "14" });
        flex.AddChild(new TextElement { Content = "Fourth", Size = "14" });

        template.AddElement(flex);

        AssertSnapshot("flex_padding_gap_combo", template, new ObjectValue());
    }

    #endregion

    #region Background and Spacing

    /// <summary>
    /// Tests flex container with a solid red background color.
    /// </summary>
    [Fact]
    public void FlexWithBackground()
    {
        var template = CreateTemplate(300, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Background = "#ff0000",
            Width = "200",
            Height = "100"
        };
        flex.AddChild(new TextElement { Content = "Red Background", Size = "14", Color = "#ffffff" });

        template.AddElement(flex);

        AssertSnapshot("flex_with_background", template, new ObjectValue());
    }

    /// <summary>
    /// Tests text element with a solid blue background color.
    /// </summary>
    [Fact]
    public void TextWithBackground()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(300, 200);
        template.AddElement(new TextElement
        {
            Content = "Blue Background",
            Size = "16",
            Color = "#ffffff",
            Background = "#0000ff"
        });

        AssertSnapshot("text_with_background", template, new ObjectValue());
    }

    /// <summary>
    /// Tests nested flex containers with different background colors at each level.
    /// Outer container has gray background, inner has light blue, and text has yellow.
    /// </summary>
    [Fact]
    public void NestedBackgrounds()
    {
        var template = CreateTemplate(300, 200);

        var outer = new FlexElement
        {
            Direction = FlexDirection.Column,
            Background = "#808080",
            Padding = "15",
            Width = "250",
            Height = "150"
        };

        var inner = new FlexElement
        {
            Direction = FlexDirection.Column,
            Background = "#87ceeb",
            Padding = "10",
            Width = "200",
            Height = "100"
        };

        inner.AddChild(new TextElement
        {
            Content = "Nested Text",
            Size = "14",
            Color = "#000000",
            Background = "#ffff00"
        });

        outer.AddChild(inner);
        template.AddElement(outer);

        AssertSnapshot("nested_backgrounds", template, new ObjectValue());
    }

    /// <summary>
    /// Tests flex container with padding around child text element.
    /// </summary>
    [Fact]
    public void FlexWithPadding()
    {
        var template = CreateTemplate(300, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Padding = "20",
            Background = "#e0e0e0",
            Width = "200"
        };
        flex.AddChild(new TextElement { Content = "Padded Content", Size = "14", Color = "#000000" });

        template.AddElement(flex);

        AssertSnapshot("flex_with_padding", template, new ObjectValue());
    }

    /// <summary>
    /// Tests two flex containers with margin creating spacing between them.
    /// </summary>
    [Fact]
    public void FlexWithMargin()
    {
        var template = CreateTemplate(300, 200);

        var container = new FlexElement
        {
            Direction = FlexDirection.Column
        };

        var first = new FlexElement
        {
            Direction = FlexDirection.Column,
            Background = "#ff6b6b",
            Margin = "10",
            Width = "150",
            Height = "50"
        };
        first.AddChild(new TextElement { Content = "First Box", Size = "12", Color = "#ffffff" });

        var second = new FlexElement
        {
            Direction = FlexDirection.Column,
            Background = "#4ecdc4",
            Margin = "10",
            Width = "150",
            Height = "50"
        };
        second.AddChild(new TextElement { Content = "Second Box", Size = "12", Color = "#ffffff" });

        container.AddChild(first);
        container.AddChild(second);
        template.AddElement(container);

        AssertSnapshot("flex_with_margin", template, new ObjectValue());
    }

    /// <summary>
    /// Tests flex container with both background and padding to verify
    /// that padding creates space inside the background area.
    /// </summary>
    [Fact]
    public void BackgroundWithPadding()
    {
        var template = CreateTemplate(300, 200);
        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Background = "#2196f3",
            Padding = "25",
            Width = "220",
            Height = "120"
        };
        flex.AddChild(new TextElement { Content = "Inside Padding", Size = "14", Color = "#ffffff" });

        template.AddElement(flex);

        AssertSnapshot("background_with_padding", template, new ObjectValue());
    }

    /// <summary>
    /// Tests element with both margin and background to verify that
    /// margin creates space outside the background area.
    /// </summary>
    [Fact]
    public void ElementWithMarginAndBackground()
    {
        var template = CreateTemplate(300, 200);

        var container = new FlexElement
        {
            Direction = FlexDirection.Column,
            Background = "#f5f5f5",
            Width = "280",
            Height = "180"
        };

        var child = new FlexElement
        {
            Direction = FlexDirection.Column,
            Background = "#9c27b0",
            Margin = "20",
            Padding = "15",
            Width = "200",
            Height = "100"
        };
        child.AddChild(new TextElement { Content = "Margin Outside", Size = "14", Color = "#ffffff" });

        container.AddChild(child);
        template.AddElement(container);

        AssertSnapshot("element_with_margin_and_background", template, new ObjectValue());
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a template with the specified canvas size and white background.
    /// </summary>
    /// <param name="width">The canvas width in pixels.</param>
    /// <param name="height">The canvas height in pixels.</param>
    /// <returns>A new template configured with the specified dimensions.</returns>
    private static Template CreateTemplate(int width, int height)
    {
        return new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Width,
                Width = width,
                Background = "#ffffff"
            }
        };
    }

    /// <summary>
    /// Creates a test image as a base64 data URL.
    /// </summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="color">The fill color for the image.</param>
    /// <returns>A base64 data URL string representing the image.</returns>
    private static string CreateTestImageBase64(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var base64 = Convert.ToBase64String(data.ToArray());

        return $"data:image/png;base64,{base64}";
    }

    #endregion
}
