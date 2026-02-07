using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.ImageSharp.Tests.Snapshots;

/// <summary>
/// Visual snapshot tests for the ImageSharp rendering engine.
/// Tests various element types and layout configurations against golden images.
/// </summary>
/// <remarks>
/// <para>
/// This test class covers rendering features supported by ImageSharp:
/// <list type="bullet">
/// <item>Text elements (simple, styled, multiline, variables)</item>
/// <item>Flex layouts (column, row, nested, grow, alignment)</item>
/// <item>Backgrounds (solid color, nested backgrounds)</item>
/// <item>Padding and margin</item>
/// <item>Separators</item>
/// </list>
/// </para>
/// <para>
/// QR codes, barcodes, and image elements are not yet supported by the
/// ImageSharp rendering engine and are excluded from these tests.
/// </para>
/// <para>
/// All tests are macOS-only to match the Skia snapshot test pattern and
/// ensure consistent font rendering for golden image generation.
/// Run with <c>UPDATE_SNAPSHOTS=true</c> to regenerate golden images.
/// </para>
/// </remarks>
public sealed class ImageSharpVisualSnapshotTests : ImageSharpSnapshotTestBase
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

        AssertSnapshot("is_text_simple", template, new ObjectValue());
    }

    /// <summary>
    /// Tests styled text with red color and center alignment.
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
            Width = "300"
        });

        AssertSnapshot("is_text_styled", template, new ObjectValue());
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

        AssertSnapshot("is_text_multiline", template, new ObjectValue());
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

        AssertSnapshot("is_text_variables", template, data);
    }

    #endregion

    #region Flex Layout

    /// <summary>
    /// Tests vertical flex column layout with gap between items.
    /// </summary>
    [Fact]
    public void FlexColumn()
    {
        if (!OperatingSystem.IsMacOS()) return;

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

        AssertSnapshot("is_flex_column", template, new ObjectValue());
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

        AssertSnapshot("is_flex_row", template, new ObjectValue());
    }

    /// <summary>
    /// Tests two levels of nested flex containers (row inside column).
    /// </summary>
    [Fact]
    public void FlexNested()
    {
        if (!OperatingSystem.IsMacOS()) return;

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

        AssertSnapshot("is_flex_nested", template, new ObjectValue());
    }

    /// <summary>
    /// Tests flex grow distribution among child elements (1:2:1 ratio).
    /// </summary>
    [Fact]
    public void FlexGrow()
    {
        if (!OperatingSystem.IsMacOS()) return;

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

        AssertSnapshot("is_flex_grow", template, new ObjectValue());
    }

    /// <summary>
    /// Tests cross-axis alignment with center alignment in a row.
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

        AssertSnapshot("is_flex_align_center", template, new ObjectValue());
    }

    /// <summary>
    /// Tests justify-content: center with boxes centered along the main axis.
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

        AssertSnapshot("is_flex_justify_center", template, new ObjectValue());
    }

    /// <summary>
    /// Tests justify-content: space-between with even distribution of space between boxes.
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

        AssertSnapshot("is_flex_justify_space_between", template, new ObjectValue());
    }

    #endregion

    #region Background and Spacing

    /// <summary>
    /// Tests flex container with a solid red background color.
    /// </summary>
    [Fact]
    public void FlexWithBackground()
    {
        if (!OperatingSystem.IsMacOS()) return;

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

        AssertSnapshot("is_flex_with_background", template, new ObjectValue());
    }

    /// <summary>
    /// Tests nested flex containers with different background colors at each level.
    /// Outer container has gray background, inner has light blue, and text has yellow.
    /// </summary>
    [Fact]
    public void NestedBackgrounds()
    {
        if (!OperatingSystem.IsMacOS()) return;

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

        AssertSnapshot("is_nested_backgrounds", template, new ObjectValue());
    }

    /// <summary>
    /// Tests flex container with padding around child text element.
    /// </summary>
    [Fact]
    public void FlexWithPadding()
    {
        if (!OperatingSystem.IsMacOS()) return;

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

        AssertSnapshot("is_flex_with_padding", template, new ObjectValue());
    }

    /// <summary>
    /// Tests two flex containers with margin creating spacing between them.
    /// </summary>
    [Fact]
    public void FlexWithMargin()
    {
        if (!OperatingSystem.IsMacOS()) return;

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

        AssertSnapshot("is_flex_with_margin", template, new ObjectValue());
    }

    /// <summary>
    /// Tests flex container with both background and padding to verify
    /// that padding creates space inside the background area.
    /// </summary>
    [Fact]
    public void BackgroundWithPadding()
    {
        if (!OperatingSystem.IsMacOS()) return;

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

        AssertSnapshot("is_background_with_padding", template, new ObjectValue());
    }

    /// <summary>
    /// Tests element with both margin and background to verify that
    /// margin creates space outside the background area.
    /// </summary>
    [Fact]
    public void ElementWithMarginAndBackground()
    {
        if (!OperatingSystem.IsMacOS()) return;

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

        AssertSnapshot("is_element_with_margin_and_background", template, new ObjectValue());
    }

    #endregion

    #region Separator

    /// <summary>
    /// Tests horizontal separator rendering with default thickness.
    /// </summary>
    [Fact]
    public void SeparatorBasic()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(300, 200);

        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Gap = "10",
            Width = "280"
        };

        flex.AddChild(new TextElement { Content = "Above separator", Size = "14", Color = "#000000" });
        flex.AddChild(new SeparatorElement
        {
            Orientation = SeparatorOrientation.Horizontal,
            Color = "#000000",
            Thickness = 1f,
            Style = SeparatorStyle.Solid,
            Width = "280"
        });
        flex.AddChild(new TextElement { Content = "Below separator", Size = "14", Color = "#000000" });

        template.AddElement(flex);

        AssertSnapshot("is_separator_basic", template, new ObjectValue());
    }

    /// <summary>
    /// Tests styled separator with increased thickness and custom color.
    /// </summary>
    [Fact]
    public void SeparatorStyled()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(300, 200);

        var flex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Gap = "15",
            Width = "280"
        };

        flex.AddChild(new TextElement { Content = "Section 1", Size = "16", Color = "#333333" });
        flex.AddChild(new SeparatorElement
        {
            Orientation = SeparatorOrientation.Horizontal,
            Color = "#cc0000",
            Thickness = 3f,
            Style = SeparatorStyle.Solid,
            Width = "280"
        });
        flex.AddChild(new TextElement { Content = "Section 2", Size = "16", Color = "#333333" });

        template.AddElement(flex);

        AssertSnapshot("is_separator_styled", template, new ObjectValue());
    }

    #endregion

    #region QR Code

    /// <summary>
    /// Tests QR code rendering with default settings.
    /// </summary>
    [Fact]
    public void QrCodeBasic()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(300, 300);
        template.AddElement(new QrElement
        {
            Data = "https://example.com",
            Size = 200,
            Foreground = "#000000",
            Background = "#ffffff"
        });

        AssertSnapshot("is_qr_basic", template, new ObjectValue());
    }

    /// <summary>
    /// Tests QR code with custom foreground color.
    /// </summary>
    [Fact]
    public void QrCodeColored()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(300, 300);
        template.AddElement(new QrElement
        {
            Data = "test data",
            Size = 200,
            Foreground = "#0000ff",
            Background = "#ffff00"
        });

        AssertSnapshot("is_qr_colored", template, new ObjectValue());
    }

    #endregion

    #region Barcode

    /// <summary>
    /// Tests basic Code128 barcode rendering.
    /// </summary>
    [Fact]
    public void BarcodeBasic()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(300, 120);
        template.AddElement(new BarcodeElement
        {
            Data = "ABC123",
            BarcodeWidth = 280,
            BarcodeHeight = 100,
            Foreground = "#000000",
            Background = "#ffffff",
            ShowText = true
        });

        AssertSnapshot("is_barcode_basic", template, new ObjectValue());
    }

    /// <summary>
    /// Tests barcode rendering without text.
    /// </summary>
    [Fact]
    public void BarcodeNoText()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var template = CreateTemplate(300, 100);
        template.AddElement(new BarcodeElement
        {
            Data = "HELLO",
            BarcodeWidth = 280,
            BarcodeHeight = 80,
            Foreground = "#000000",
            Background = "#ffffff",
            ShowText = false
        });

        AssertSnapshot("is_barcode_no_text", template, new ObjectValue());
    }

    #endregion

    #region Image

    /// <summary>
    /// Tests image element rendering with a base64 inline image.
    /// Uses a 20x20 checkerboard (red/blue) to make fit mode effects visible.
    /// </summary>
    [Fact]
    public void ImageBase64()
    {
        if (!OperatingSystem.IsMacOS()) return;

        // Create a 20x20 checkerboard test image (red/blue quadrants) as base64
        using var ms = new MemoryStream();
        using (var testImg = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(20, 20))
        {
            for (var py = 0; py < 20; py++)
            {
                for (var px = 0; px < 20; px++)
                {
                    testImg[px, py] = ((px / 10) + (py / 10)) % 2 == 0
                        ? new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 0, 0, 255)   // red
                        : new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 255, 255);   // blue
                }
            }
            testImg.Save(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        }
        var base64 = Convert.ToBase64String(ms.ToArray());
        var dataUrl = $"data:image/png;base64,{base64}";

        var template = CreateTemplate(200, 200);
        template.AddElement(new ImageElement
        {
            Src = dataUrl,
            ImageWidth = 100,
            ImageHeight = 100,
            Fit = ImageFit.Fill
        });

        AssertSnapshot("is_image_base64", template, new ObjectValue());
    }

    /// <summary>
    /// Tests image element rendering from a temporary PNG file with contain fit mode.
    /// Uses a 4-color gradient pattern (red, green, blue, yellow quadrants) to verify
    /// file loading and contain mode scaling.
    /// </summary>
    [Fact]
    public void ImageFileCheckerboard()
    {
        if (!OperatingSystem.IsMacOS()) return;

        // Create a 40x40 4-color gradient pattern as a temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"flexrender_test_{Guid.NewGuid():N}.png");
        try
        {
            using (var testImg = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(40, 40))
            {
                for (var py = 0; py < 40; py++)
                {
                    for (var px = 0; px < 40; px++)
                    {
                        testImg[px, py] = (px < 20, py < 20) switch
                        {
                            (true, true)   => new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 0, 0, 255),     // top-left red
                            (false, true)  => new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 255, 0, 255),     // top-right green
                            (true, false)  => new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 255, 255),     // bottom-left blue
                            (false, false) => new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 255, 0, 255),   // bottom-right yellow
                        };
                    }
                }
                using var fs = File.Create(tempPath);
                testImg.Save(fs, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
            }

            var template = CreateTemplate(200, 200);
            template.AddElement(new ImageElement
            {
                Src = tempPath,
                ImageWidth = 150,
                ImageHeight = 100,
                Fit = ImageFit.Contain
            });

            AssertSnapshot("is_image_file_checkerboard", template, new ObjectValue());
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    #endregion
}
