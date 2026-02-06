using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Layout;

public class LayoutEngineTests
{
    private readonly LayoutEngine _engine = new();

    // Validation Tests: Negative Grow Values
    [Fact]
    public void ComputeLayout_NegativeFlexGrow_ThrowsArgumentException()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Height = "200",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Item", Grow = -1f }
                    }
                }
            }
        };

        Assert.Throws<ArgumentException>(() => _engine.ComputeLayout(template));
    }

    [Fact]
    public void ComputeLayout_ZeroFlexGrow_Succeeds()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Height = "200",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Item", Grow = 0f }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        Assert.NotNull(root);
    }

    [Fact]
    public void ComputeLayout_NegativeNestedFlexElementGrow_ThrowsArgumentException()
    {
        // The negative Grow is on a nested FlexElement (child of outer flex)
        // This tests that Grow is validated when computing flex distribution
        var innerFlex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Grow = -0.5f
        };
        innerFlex.AddChild(new TextElement { Content = "Item" });

        var outerFlex = new FlexElement
        {
            Direction = FlexDirection.Column,
            Height = "200"
        };
        outerFlex.AddChild(innerFlex);

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement> { outerFlex }
        };

        Assert.Throws<ArgumentException>(() => _engine.ComputeLayout(template));
    }

    [Fact]
    public void ComputeLayout_EmptyTemplate_ReturnsRootNode()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 }
        };

        var root = _engine.ComputeLayout(template);

        Assert.NotNull(root);
        Assert.Equal(0f, root.X);
        Assert.Equal(0f, root.Y);
        Assert.Equal(300f, root.Width);
    }

    [Fact]
    public void ComputeLayout_SingleText_CreatesChildNode()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello" }
            }
        };

        var root = _engine.ComputeLayout(template);

        Assert.Single(root.Children);
        Assert.IsType<TextElement>(root.Children[0].Element);
    }

    [Fact]
    public void ComputeLayout_ColumnDirection_StacksVertically()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "50" },
                        new TextElement { Content = "Second", Height = "50" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.Equal(2, flex.Children.Count);
        Assert.Equal(0f, flex.Children[0].Y);
        Assert.Equal(50f, flex.Children[1].Y);
    }

    [Fact]
    public void ComputeLayout_RowDirection_StacksHorizontally()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Width = "100" },
                        new TextElement { Content = "Second", Width = "100" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.Equal(2, flex.Children.Count);
        Assert.Equal(0f, flex.Children[0].X);
        Assert.Equal(100f, flex.Children[1].X);
    }

    [Fact]
    public void ComputeLayout_WithGap_AddsSpaceBetweenItems()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Gap = "10",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "50" },
                        new TextElement { Content = "Second", Height = "50" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.Equal(0f, flex.Children[0].Y);
        Assert.Equal(60f, flex.Children[1].Y); // 50 + 10 gap
    }

    [Fact]
    public void ComputeLayout_WithPadding_OffsetsChildren()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Padding = "20",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "50" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.Equal(20f, flex.Children[0].X);
        Assert.Equal(20f, flex.Children[0].Y);
    }

    [Fact]
    public void ComputeLayout_WithPercentGap_CalculatesFromWidth()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Gap = "5%", // 5% of 200 = 10
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "50" },
                        new TextElement { Content = "Second", Height = "50" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.Equal(0f, flex.Children[0].Y);
        Assert.Equal(60f, flex.Children[1].Y); // 50 + 10 gap
    }

    [Fact]
    public void ComputeLayout_JustifyCenter_CentersItems()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Height = "200",
                    Justify = JustifyContent.Center,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Item", Height = "50" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // (200 - 50) / 2 = 75
        Assert.Equal(75f, flex.Children[0].Y);
    }

    [Fact]
    public void ComputeLayout_JustifyEnd_AlignToEnd()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Height = "200",
                    Justify = JustifyContent.End,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Item", Height = "50" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // 200 - 50 = 150
        Assert.Equal(150f, flex.Children[0].Y);
    }

    [Fact]
    public void ComputeLayout_JustifySpaceBetween_DistributesSpace()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Height = "200",
                    Justify = JustifyContent.SpaceBetween,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "50" },
                        new TextElement { Content = "Last", Height = "50" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.Equal(0f, flex.Children[0].Y);
        Assert.Equal(150f, flex.Children[1].Y); // 200 - 50 = 150
    }

    [Fact]
    public void ComputeLayout_RowJustifySpaceBetween_WithFlexChildren_DistributesSpace()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Justify = JustifyContent.SpaceBetween,
                    Children = new List<TemplateElement>
                    {
                        new FlexElement
                        {
                            Padding = "8 16",
                            Children = new List<TemplateElement>
                            {
                                new TextElement { Content = "A", Size = "14" }
                            }
                        },
                        new FlexElement
                        {
                            Padding = "8 16",
                            Children = new List<TemplateElement>
                            {
                                new TextElement { Content = "B", Size = "14" }
                            }
                        }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Two children should NOT have equal widths (300/2=150 each)
        // They should have intrinsic content-based widths
        Assert.True(flex.Children[0].Width < 100,
            $"Child 0 width {flex.Children[0].Width} should be content-based, not stretched to fill row");
        Assert.True(flex.Children[1].Width < 100,
            $"Child 1 width {flex.Children[1].Width} should be content-based, not stretched to fill row");

        // Second child should be pushed to the end (space-between)
        var child1Right = flex.Children[1].X + flex.Children[1].Width;
        Assert.True(child1Right > 250,
            $"Child 1 right edge {child1Right} should be near container end (300) with space-between");
    }

    [Fact]
    public void ComputeLayout_ColumnAutoHeight_JustifyCenter_NoNegativePositions()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Children = new List<TemplateElement>
                    {
                        new FlexElement
                        {
                            // Column flex with auto-height and justify: center
                            Justify = JustifyContent.Center,
                            Gap = "8",
                            Children = new List<TemplateElement>
                            {
                                new TextElement { Content = "First", Height = "60" },
                                new TextElement { Content = "Second", Height = "20" }
                            }
                        }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var rowFlex = root.Children[0];
        var columnChild = rowFlex.Children[0];

        // Children should NOT have negative Y positions
        Assert.True(columnChild.Children[0].Y >= 0,
            $"First child Y={columnChild.Children[0].Y} should not be negative in auto-height column with justify: center");
        Assert.True(columnChild.Children[1].Y >= 0,
            $"Second child Y={columnChild.Children[1].Y} should not be negative in auto-height column with justify: center");
    }

    [Fact]
    public void ComputeLayout_FlexGrow_DistributesFreeSpace()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Height = "200",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Fixed", Height = "50", Grow = 0 },
                        new TextElement { Content = "Grows", Grow = 1 }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.Equal(50f, flex.Children[0].Height);
        Assert.Equal(150f, flex.Children[1].Height); // 200 - 50 = 150
    }

    [Fact]
    public void ComputeLayout_FlexGrow_ProportionalDistribution()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Height = "200",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "One", Grow = 1 },
                        new TextElement { Content = "Two", Grow = 2 }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Total grow = 3, so 1/3 and 2/3 of 200 (minus default heights)
        var total = flex.Children[0].Height + flex.Children[1].Height;
        Assert.Equal(200f, total, 1);
        Assert.True(flex.Children[1].Height > flex.Children[0].Height);
    }

    [Fact]
    public void ComputeLayout_AlignCenter_CentersOnCrossAxis()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Height = "100",
                    Align = AlignItems.Center,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Item", Width = "100", Height = "40" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // (100 - 40) / 2 = 30
        Assert.Equal(30f, flex.Children[0].Y);
    }

    [Fact]
    public void ComputeLayout_AlignEnd_AlignsToEnd()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Height = "100",
                    Align = AlignItems.End,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Item", Width = "100", Height = "40" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // 100 - 40 = 60
        Assert.Equal(60f, flex.Children[0].Y);
    }

    [Fact]
    public void ComputeLayout_AlignStretch_StretchesToFill()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Height = "100",
                    Align = AlignItems.Stretch,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Item", Width = "100" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.Equal(100f, flex.Children[0].Height);
    }

    [Fact]
    public void ComputeLayout_RowWithPercentWidthsAndPadding_ChildrenFitWithinBounds()
    {
        // Simulates receipt-style layout: row with padding and 3 children at 33% each
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 630 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Padding = "12",
                    Gap = "0",
                    Children = new List<TemplateElement>
                    {
                        new FlexElement { Direction = FlexDirection.Column, Width = "33%" },
                        new FlexElement { Direction = FlexDirection.Column, Width = "33%" },
                        new FlexElement { Direction = FlexDirection.Column, Width = "33%" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var row = root.Children[0];

        // Available width = 630 - 24 (padding) = 606
        // Each child should be 33% of 606 = 199.98
        var availableWidth = 606f;
        var expectedChildWidth = availableWidth * 0.33f;

        Assert.Equal(3, row.Children.Count);

        // Child 0: X = 12 (padding)
        Assert.Equal(12f, row.Children[0].X);
        Assert.Equal(expectedChildWidth, row.Children[0].Width, 1);

        // Child 1: X = 12 + child0.Width
        var expectedX1 = 12f + row.Children[0].Width;
        Assert.Equal(expectedX1, row.Children[1].X, 1);
        Assert.Equal(expectedChildWidth, row.Children[1].Width, 1);

        // Child 2: X = 12 + child0.Width + child1.Width
        var expectedX2 = 12f + row.Children[0].Width + row.Children[1].Width;
        Assert.Equal(expectedX2, row.Children[2].X, 1);
        Assert.Equal(expectedChildWidth, row.Children[2].Width, 1);

        // All children should fit within the content area (padding to width-padding)
        var rightEdge = row.Children[2].X + row.Children[2].Width;
        Assert.True(rightEdge <= 630f - 12f, $"Right edge {rightEdge} exceeds content boundary {630f - 12f}");
    }

    [Fact]
    public void ComputeLayout_RowWithPercentWidthsAndGap_ChildrenFitWithinBounds()
    {
        // Simulates receipt-style first row: 66% + 33% with gap
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 630 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Padding = "12",
                    Gap = "8",
                    Children = new List<TemplateElement>
                    {
                        new FlexElement { Direction = FlexDirection.Column, Width = "66%" },
                        new FlexElement { Direction = FlexDirection.Column, Width = "33%" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var row = root.Children[0];

        // Available width = 630 - 24 (padding) = 606
        var availableWidth = 606f;
        var gapWidth = 8f;

        Assert.Equal(2, row.Children.Count);

        // Child 0: X = 12 (padding)
        // Original width = 66% of 606 = 399.96, but flex-shrink reduces it
        // because 66% + 33% + gap exceeds available width.
        Assert.Equal(12f, row.Children[0].X);

        // Child 1: X = 12 + child0.Width + gap
        var expectedX1 = 12f + row.Children[0].Width + gapWidth;
        Assert.Equal(expectedX1, row.Children[1].X, 1);

        // With flex-shrink, children shrink proportionally to fit within container.
        // Total used width should now fit within available width.
        var rightEdge = row.Children[1].X + row.Children[1].Width;
        Assert.True(rightEdge <= 12f + availableWidth + 0.1f,
            $"Right edge {rightEdge} should not exceed available width {12f + availableWidth}");

        // Verify proportions are preserved: child0 should be ~2x child1 (66% vs 33%)
        var ratio = row.Children[0].Width / row.Children[1].Width;
        Assert.Equal(2.0f, ratio, 0);
    }

    [Fact]
    public void ComputeLayout_NestedFlexInRow_ChildrenGetCorrectWidth()
    {
        // Test that nested flex elements in a row get correct width
        // This tests the scenario where text in column 3 might get wrong width
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 630 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Padding = "12",
                    Children = new List<TemplateElement>
                    {
                        new FlexElement
                        {
                            Direction = FlexDirection.Column,
                            Width = "33%",
                            Children = new List<TemplateElement>
                            {
                                new TextElement { Content = "Test text" }
                            }
                        }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var row = root.Children[0];
        var column = row.Children[0];
        var text = column.Children[0];

        // Column width should be 33% of (630 - 24) = 199.98
        var expectedColumnWidth = (630f - 24f) * 0.33f;
        Assert.Equal(expectedColumnWidth, column.Width, 1);

        // Text width should be column width (no padding on column)
        // Text without explicit width should get container width
        Assert.Equal(expectedColumnWidth, text.Width, 1);
    }

    [Fact]
    public void ComputeLayout_TwoRowsStackVertically_SecondRowBelowFirst()
    {
        // Simulates wb-receipt: two rows should stack vertically
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 630 },
            Elements = new List<TemplateElement>
            {
                // First row with QR
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Padding = "12",
                    Gap = "8",
                    Children = new List<TemplateElement>
                    {
                        new FlexElement
                        {
                            Direction = FlexDirection.Column,
                            Width = "66%",
                            Children = new List<TemplateElement>
                            {
                                new TextElement { Content = "Title", Size = "14px" },
                                new TextElement { Content = "5000", Size = "44px" }
                            }
                        },
                        new FlexElement
                        {
                            Direction = FlexDirection.Column,
                            Width = "33%",
                            Align = AlignItems.Center,
                            Children = new List<TemplateElement>
                            {
                                new QrElement { Data = "test", Size = 70 }
                            }
                        }
                    }
                },
                // Second row with details
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Padding = "12",
                    Gap = "0",
                    Children = new List<TemplateElement>
                    {
                        new FlexElement
                        {
                            Direction = FlexDirection.Column,
                            Width = "33%",
                            Children = new List<TemplateElement>
                            {
                                new TextElement { Content = "Detail 1" },
                                new TextElement { Content = "Detail 2" }
                            }
                        },
                        new FlexElement
                        {
                            Direction = FlexDirection.Column,
                            Width = "33%",
                            Children = new List<TemplateElement>
                            {
                                new TextElement { Content = "Detail 3" }
                            }
                        },
                        new FlexElement
                        {
                            Direction = FlexDirection.Column,
                            Width = "33%",
                            Children = new List<TemplateElement>
                            {
                                new TextElement { Content = "Support text that should not overlap with QR" }
                            }
                        }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);

        Assert.Equal(2, root.Children.Count);

        var row1 = root.Children[0];
        var row2 = root.Children[1];

        // Row 1 should start at Y = 0
        Assert.Equal(0f, row1.Y);

        // Row 1 should have positive height (at least QR size + padding)
        Assert.True(row1.Height >= 70 + 24, $"Row 1 height {row1.Height} should be >= 94 (QR 70 + padding 24)");

        // Row 2 should start after row 1
        Assert.Equal(row1.Height, row2.Y);

        // Row 2 Y should be positive
        Assert.True(row2.Y > 0, $"Row 2 Y position {row2.Y} should be > 0");

        // The third column text should not overlap with QR
        // Third column in row 2 starts at X = 12 + 33% + 33% = 12 + ~400 = ~412
        var row2Col3 = row2.Children[2];
        var qrColumn = row1.Children[1];
        var qr = qrColumn.Children[0];

        // QR position in row 1
        var qrAbsoluteX = qrColumn.X;
        var qrAbsoluteY = row1.Y + qr.Y;

        // Text position in row 2
        var textAbsoluteY = row2.Y + row2Col3.Y + row2Col3.Children[0].Y;

        // Text should be below QR
        Assert.True(textAbsoluteY >= qrAbsoluteY + qr.Height,
            $"Text Y {textAbsoluteY} should be >= QR bottom {qrAbsoluteY + qr.Height}");
    }

    [Fact]
    public void ComputeLayout_TextWithWrap_HeightIncreasesWithWrapping()
    {
        var engine = new LayoutEngine();
        engine.TextMeasurer = (element, fontSize, maxWidth) =>
        {
            var lineHeight = fontSize * 1.4f;
            var singleLineWidth = 200f;
            if (maxWidth >= singleLineWidth)
                return new LayoutSize(singleLineWidth, lineHeight);

            var lines = (int)Math.Ceiling(singleLineWidth / maxWidth);
            return new LayoutSize(maxWidth, lineHeight * lines);
        };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Long text that wraps", Size = "16", Wrap = true }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        var singleLineHeight = 16f * 1.4f;
        Assert.True(textNode.Height > singleLineHeight + 1f,
            $"Text height {textNode.Height} should be > {singleLineHeight} (single line) when text wraps");
    }

    [Fact]
    public void ComputeLayout_TextWithNewlines_HeightMatchesLineCount()
    {
        var engine = new LayoutEngine();
        engine.TextMeasurer = (element, fontSize, maxWidth) =>
        {
            var lineHeight = fontSize * 1.4f;
            var lineCount = element.Content.Split('\n').Length;
            var maxLineWidth = 50f;
            return new LayoutSize(maxLineWidth, lineHeight * lineCount);
        };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Line 1\nLine 2\nLine 3", Size = "16" }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        var singleLineHeight = 16f * 1.4f;
        var expectedHeight = singleLineHeight * 3;
        Assert.Equal(expectedHeight, textNode.Height, 1);
    }

    [Fact]
    public void ComputeLayout_TextWithLineHeight_FallbackUsesLineHeight()
    {
        var engine = new LayoutEngine();
        // No TextMeasurer — uses fallback

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Size = "20", LineHeight = "2.0" }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // fontSize=20, lineHeight multiplier=2.0 → height = 20 * 2.0 = 40
        Assert.Equal(40f, textNode.Height, 1f);
    }

    [Fact]
    public void ComputeLayout_TextWithLineHeightPx_FallbackUsesAbsoluteValue()
    {
        var engine = new LayoutEngine();

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Size = "20", LineHeight = "30px" }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // lineHeight=30px → height = 30
        Assert.Equal(30f, textNode.Height, 1f);
    }

    [Fact]
    public void ComputeLayout_BaseFontSize_AffectsDefaultTextHeight()
    {
        var engine = new LayoutEngine { BaseFontSize = 12f };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Size = "1em" }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // 1em with BaseFontSize=12 -> fontSize=12, height = 12 * 1.4 = 16.8
        Assert.Equal(16.8f, textNode.Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_DefaultBaseFontSize_Is16()
    {
        var engine = new LayoutEngine();

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new TextElement { Content = "Hello", Size = "1em" }
            }
        };

        var root = engine.ComputeLayout(template);
        var textNode = root.Children[0];

        // 1em with default BaseFontSize=16 -> fontSize=16, height = 16 * 1.4 = 22.4
        Assert.Equal(22.4f, textNode.Height, 0.1f);
    }

    [Fact]
    public void ComputeLayout_HorizontalSeparator_StretchesToContainerWidth()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement>
            {
                new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Horizontal,
                    Thickness = 2f
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var node = root.Children[0];

        Assert.Equal(400f, node.Width);
        Assert.Equal(2f, node.Height);
    }

    [Fact]
    public void ComputeLayout_SeparatorInColumnFlex_StretchesWidth()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Above", Height = "30" },
                        new SeparatorElement { Thickness = 1f },
                        new TextElement { Content = "Below", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.Equal(3, flex.Children.Count);
        var separatorNode = flex.Children[1];
        Assert.Equal(300f, separatorNode.Width);
        Assert.Equal(1f, separatorNode.Height);
        // Separator Y = height of first child
        Assert.Equal(30f, separatorNode.Y);
    }

    [Fact]
    public void ComputeLayout_SeparatorWithExplicitWidth_UsesExplicitWidth()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement>
            {
                new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Horizontal,
                    Thickness = 2f,
                    Width = "200"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var node = root.Children[0];

        Assert.Equal(200f, node.Width);
    }

    [Fact]
    public void ComputeLayout_VerticalSeparatorInRowFlex_StretchesHeight()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Height = "80",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Left", Width = "100" },
                        new SeparatorElement
                        {
                            Orientation = SeparatorOrientation.Vertical,
                            Thickness = 2f
                        },
                        new TextElement { Content = "Right", Width = "100" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];
        var separatorNode = flex.Children[1];

        Assert.Equal(2f, separatorNode.Width);
        // Stretch cross-axis in row flex: align=stretch (default) stretches height
        Assert.Equal(80f, separatorNode.Height);
    }

    [Fact]
    public void ComputeLayout_SeparatorWithPadding_IncludesPadding()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Horizontal,
                    Thickness = 2f,
                    Padding = "4"
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var node = root.Children[0];

        // Height = thickness + padding*2 = 2 + 8 = 10
        Assert.Equal(10f, node.Height);
    }

    [Fact]
    public void ComputeLayout_VerticalSeparatorWithoutParentHeight_UsesThicknessNotContainerHeight()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Vertical,
                    Thickness = 2f
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var node = root.Children[0];

        // Width is stretched to container width by the root column flex (align=stretch default)
        // The key assertion: height should use thickness as fallback, not the
        // unconstrained container height (10000px)
        Assert.True(node.Height < 100f, $"Expected reasonable height, got {node.Height}px (should not be 10000px)");
        Assert.Equal(2f, node.Height);
    }

    [Fact]
    public void ComputeLayout_WrappedTextInFlex_ParentHeightAccommodatesWrapping()
    {
        var engine = new LayoutEngine();
        engine.TextMeasurer = (element, fontSize, maxWidth) =>
        {
            var lineHeight = fontSize * 1.4f;
            var singleLineWidth = 250f;
            if (maxWidth >= singleLineWidth)
                return new LayoutSize(singleLineWidth, lineHeight);

            var lines = (int)Math.Ceiling(singleLineWidth / maxWidth);
            return new LayoutSize(maxWidth, lineHeight * lines);
        };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 100 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Long wrapping text", Size = "16", Wrap = true },
                        new TextElement { Content = "After", Size = "16", Height = "20" }
                    }
                }
            }
        };

        var root = engine.ComputeLayout(template);
        var flex = root.Children[0];
        var wrappedText = flex.Children[0];
        var afterText = flex.Children[1];

        var singleLineHeight = 16f * 1.4f;
        Assert.True(wrappedText.Height > singleLineHeight + 1f,
            $"Wrapped text height {wrappedText.Height} should be > {singleLineHeight}");
        Assert.True(afterText.Y >= wrappedText.Height,
            $"After text Y={afterText.Y} should be >= wrapped text height {wrappedText.Height}");
    }

    // ============================================
    // Non-Uniform Padding Layout Tests
    // ============================================

    [Fact]
    public void ComputeLayout_FlexWithTwoValuePadding_OffsetsChildrenCorrectly()
    {
        // padding: "10 30" -> top/bottom=10, left/right=30
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Padding = "10 30",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "50" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Child X should be left padding = 30
        Assert.Equal(30f, flex.Children[0].X);
        // Child Y should be top padding = 10
        Assert.Equal(10f, flex.Children[0].Y);
    }

    [Fact]
    public void ComputeLayout_FlexWithFourValuePadding_OffsetsChildrenCorrectly()
    {
        // padding: "10 20 30 40" -> top=10, right=20, bottom=30, left=40
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Padding = "10 20 30 40",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "50" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Child X should be left padding = 40
        Assert.Equal(40f, flex.Children[0].X);
        // Child Y should be top padding = 10
        Assert.Equal(10f, flex.Children[0].Y);
    }

    [Fact]
    public void ComputeLayout_FlexColumnNonUniformPadding_HeightIncludesTopAndBottom()
    {
        // padding: "10 20 30 40" -> top=10, right=20, bottom=30, left=40
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Padding = "10 20 30 40",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "First", Height = "50" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Height = child height(50) + top padding(10) + bottom padding(30) = 90
        Assert.Equal(90f, flex.Height);
    }

    [Fact]
    public void ComputeLayout_FlexColumnNonUniformPadding_WidthReducedByHorizontalPadding()
    {
        // padding: "10 30" -> top/bottom=10, left/right=30
        // Container width = 300, inner width = 300 - 30 - 30 = 240
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Padding = "10 30",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Test", Height = "40" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // The child should stretch to inner width = 300 - 30 - 30 = 240
        Assert.Equal(240f, flex.Children[0].Width);
    }

    [Fact]
    public void ComputeLayout_FlexRowNonUniformPadding_ChildPositionedCorrectly()
    {
        // padding: "5 25" -> top/bottom=5, left/right=25
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Padding = "5 25",
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Width = "100", Height = "40" },
                        new TextElement { Content = "B", Width = "100", Height = "40" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // First child X = left padding = 25
        Assert.Equal(25f, flex.Children[0].X);
        // First child Y = top padding = 5
        Assert.Equal(5f, flex.Children[0].Y);
        // Second child X = 25 + 100 = 125
        Assert.Equal(125f, flex.Children[1].X);
    }

    [Fact]
    public void ComputeLayout_FlexWithExplicitHeightAndNonUniformPadding_JustifyWorks()
    {
        // padding: "20 10 40 10" -> top=20, right=10, bottom=40, left=10
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Height = "200",
                    Padding = "20 10 40 10",
                    Justify = JustifyContent.Center,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Item", Height = "40" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Available height for children = 200 - top(20) - bottom(40) = 140
        // Free space = 140 - 40 = 100
        // Center: Y = top padding + free space / 2 = 20 + 50 = 70
        Assert.Equal(70f, flex.Children[0].Y);
    }

    // ============================================
    // Row Justify Tests (with FlexElement children)
    // ============================================

    [Fact]
    public void ComputeLayout_RowJustifyStart_WithFlexChildren_PositionsAtStart()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Justify = JustifyContent.Start,
                    Children = new List<TemplateElement>
                    {
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "A", Size = "14" } } },
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "B", Size = "14" } } },
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "C", Size = "14" } } }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Children should be positioned consecutively from the start
        Assert.Equal(0f, flex.Children[0].X);
        Assert.True(flex.Children[1].X > flex.Children[0].X);
        Assert.True(flex.Children[2].X > flex.Children[1].X);
        // Total used width should be much less than 300 (3 small items)
        var totalUsedWidth = flex.Children[2].X + flex.Children[2].Width;
        Assert.True(totalUsedWidth < 200, $"Children should be content-sized, total={totalUsedWidth}");
    }

    [Fact]
    public void ComputeLayout_RowJustifyCenter_WithFlexChildren_CentersGroup()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Justify = JustifyContent.Center,
                    Children = new List<TemplateElement>
                    {
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "A", Size = "14" } } },
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "B", Size = "14" } } },
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "C", Size = "14" } } }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // First child should start after center offset (X > 0)
        Assert.True(flex.Children[0].X > 0, $"First child X={flex.Children[0].X} should be > 0 for centered group");

        // The group should be centered: left offset ≈ right offset
        var totalUsedWidth = flex.Children[2].X + flex.Children[2].Width - flex.Children[0].X;
        var leftOffset = flex.Children[0].X;
        var rightOffset = 300 - (flex.Children[2].X + flex.Children[2].Width);
        Assert.True(Math.Abs(leftOffset - rightOffset) < 1,
            $"Group should be centered: leftOffset={leftOffset}, rightOffset={rightOffset}");
    }

    [Fact]
    public void ComputeLayout_RowJustifyEnd_WithFlexChildren_PositionsAtEnd()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Justify = JustifyContent.End,
                    Children = new List<TemplateElement>
                    {
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "A", Size = "14" } } },
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "B", Size = "14" } } },
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "C", Size = "14" } } }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Last child's right edge should be at 300
        var lastChildRight = flex.Children[2].X + flex.Children[2].Width;
        Assert.True(Math.Abs(lastChildRight - 300) < 1,
            $"Last child right edge {lastChildRight} should be at container end (300)");

        // First child should have a positive X offset
        Assert.True(flex.Children[0].X > 0,
            $"First child X={flex.Children[0].X} should be > 0 for end-justified layout");
    }

    [Fact]
    public void ComputeLayout_RowJustifySpaceAround_WithFlexChildren_DistributesSpaceAround()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Justify = JustifyContent.SpaceAround,
                    Children = new List<TemplateElement>
                    {
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "A", Size = "14" } } },
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "B", Size = "14" } } },
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "C", Size = "14" } } }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Space before first child should be > 0
        var spaceBefore = flex.Children[0].X;
        Assert.True(spaceBefore > 0, $"Space before first child should be > 0, got {spaceBefore}");

        // Gap between children
        var gapBetween01 = flex.Children[1].X - (flex.Children[0].X + flex.Children[0].Width);
        var gapBetween12 = flex.Children[2].X - (flex.Children[1].X + flex.Children[1].Width);

        // Space-around: space before first = half of gap between items
        Assert.True(Math.Abs(spaceBefore * 2 - gapBetween01) < 1,
            $"Space before ({spaceBefore}) should be half of gap between items ({gapBetween01})");

        // Gaps between items should be equal
        Assert.True(Math.Abs(gapBetween01 - gapBetween12) < 1,
            $"Gaps between items should be equal: {gapBetween01} vs {gapBetween12}");
    }

    [Fact]
    public void ComputeLayout_RowJustifySpaceEvenly_WithFlexChildren_EqualSpacing()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Justify = JustifyContent.SpaceEvenly,
                    Children = new List<TemplateElement>
                    {
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "A", Size = "14" } } },
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "B", Size = "14" } } },
                        new FlexElement { Padding = "8 16", Children = new List<TemplateElement> { new TextElement { Content = "C", Size = "14" } } }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // All gaps should be equal: before first, between items, after last
        var spaceBefore = flex.Children[0].X;
        var gapBetween01 = flex.Children[1].X - (flex.Children[0].X + flex.Children[0].Width);
        var gapBetween12 = flex.Children[2].X - (flex.Children[1].X + flex.Children[1].Width);
        var spaceAfter = 300 - (flex.Children[2].X + flex.Children[2].Width);

        Assert.True(Math.Abs(spaceBefore - gapBetween01) < 1,
            $"Space before ({spaceBefore}) should equal gap 0-1 ({gapBetween01})");
        Assert.True(Math.Abs(gapBetween01 - gapBetween12) < 1,
            $"Gap 0-1 ({gapBetween01}) should equal gap 1-2 ({gapBetween12})");
        Assert.True(Math.Abs(gapBetween12 - spaceAfter) < 1,
            $"Gap 1-2 ({gapBetween12}) should equal space after ({spaceAfter})");
    }

    // ============================================
    // Column Justify Tests (missing cases)
    // ============================================

    [Fact]
    public void ComputeLayout_ColumnJustifyStart_PositionsAtTop()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Height = "300",
                    Justify = JustifyContent.Start,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Height = "30" },
                        new TextElement { Content = "B", Height = "30" },
                        new TextElement { Content = "C", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.Equal(0f, flex.Children[0].Y);
        Assert.Equal(30f, flex.Children[1].Y);
        Assert.Equal(60f, flex.Children[2].Y);
    }

    [Fact]
    public void ComputeLayout_ColumnJustifySpaceAround_DistributesSpaceAround()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Height = "300",
                    Justify = JustifyContent.SpaceAround,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Height = "30" },
                        new TextElement { Content = "B", Height = "30" },
                        new TextElement { Content = "C", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Total children height = 90. Free space = 300 - 90 = 210
        // Space around each = 210 / 3 = 70. Half-space = 35.
        // First child Y = 35
        Assert.Equal(35f, flex.Children[0].Y, 1);

        // Gap between items = 70 (full space around)
        var gapBetween01 = flex.Children[1].Y - (flex.Children[0].Y + 30);
        var gapBetween12 = flex.Children[2].Y - (flex.Children[1].Y + 30);
        Assert.Equal(70f, gapBetween01, 1);
        Assert.Equal(70f, gapBetween12, 1);
    }

    [Fact]
    public void ComputeLayout_ColumnJustifySpaceEvenly_EqualSpacing()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Height = "300",
                    Justify = JustifyContent.SpaceEvenly,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Height = "30" },
                        new TextElement { Content = "B", Height = "30" },
                        new TextElement { Content = "C", Height = "30" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Total children height = 90. Free space = 300 - 90 = 210
        // Equal spacing = 210 / 4 = 52.5. First child Y = 52.5.
        Assert.Equal(52.5f, flex.Children[0].Y, 1);

        // All gaps should be equal
        var spaceBefore = flex.Children[0].Y;
        var gapBetween01 = flex.Children[1].Y - (flex.Children[0].Y + 30);
        var gapBetween12 = flex.Children[2].Y - (flex.Children[1].Y + 30);
        var spaceAfter = 300 - (flex.Children[2].Y + 30);

        Assert.True(Math.Abs(spaceBefore - gapBetween01) < 1,
            $"Space before ({spaceBefore}) should equal gap 0-1 ({gapBetween01})");
        Assert.True(Math.Abs(gapBetween01 - gapBetween12) < 1,
            $"Gap 0-1 ({gapBetween01}) should equal gap 1-2 ({gapBetween12})");
        Assert.True(Math.Abs(gapBetween12 - spaceAfter) < 1,
            $"Gap 1-2 ({gapBetween12}) should equal space after ({spaceAfter})");
    }

    // ============================================
    // Column Align Tests (cross-axis: horizontal)
    // ============================================

    [Fact]
    public void ComputeLayout_ColumnAlignStart_PositionsAtLeft()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Align = AlignItems.Start,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Short", Size = "14", Width = "100" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.Equal(0f, flex.Children[0].X);
        Assert.Equal(100f, flex.Children[0].Width);
    }

    [Fact]
    public void ComputeLayout_ColumnAlignCenter_CentersHorizontally()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Align = AlignItems.Center,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Short", Size = "14", Width = "100" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // (300 - 100) / 2 = 100
        Assert.Equal(100f, flex.Children[0].X);
        Assert.Equal(100f, flex.Children[0].Width);
    }

    [Fact]
    public void ComputeLayout_ColumnAlignEnd_PositionsAtRight()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Align = AlignItems.End,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Short", Size = "14", Width = "100" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // 300 - 100 = 200
        Assert.Equal(200f, flex.Children[0].X);
        Assert.Equal(100f, flex.Children[0].Width);
    }

    [Fact]
    public void ComputeLayout_ColumnAlignStretch_StretchesWidth()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Align = AlignItems.Stretch,
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "Short", Size = "14" }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Child without explicit width should stretch to container width
        Assert.Equal(300f, flex.Children[0].Width);
    }

    // ============================================
    // Row Grow Test (with FlexElement children)
    // ============================================

    [Fact]
    public void ComputeLayout_RowGrow_WithFlexChildren_DistributesFreeSpace()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "300",
                    Children = new List<TemplateElement>
                    {
                        new FlexElement { Grow = 1, Padding = "4", Children = new List<TemplateElement> { new TextElement { Content = "A", Size = "12" } } },
                        new FlexElement { Grow = 2, Padding = "4", Children = new List<TemplateElement> { new TextElement { Content = "B", Size = "12" } } },
                        new FlexElement { Grow = 1, Padding = "4", Children = new List<TemplateElement> { new TextElement { Content = "C", Size = "12" } } }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Middle child (grow=2) should be roughly twice as wide as outer children (grow=1)
        var child0Width = flex.Children[0].Width;
        var child1Width = flex.Children[1].Width;
        var child2Width = flex.Children[2].Width;
        Assert.True(child1Width > child0Width * 1.5, $"grow:2 ({child1Width}) should be significantly wider than grow:1 ({child0Width})");
        Assert.True(Math.Abs(child0Width - child2Width) < 1, $"Both grow:1 children should be equal width: {child0Width} vs {child2Width}");
        // All children should fill the container
        var totalWidth = child0Width + child1Width + child2Width;
        Assert.True(Math.Abs(totalWidth - 300) < 1, $"Children should fill container: total={totalWidth}");
    }

    // ============================================
    // Shrink Tests (row and column)
    // ============================================

    [Fact]
    public void ComputeLayout_RowShrink_WithFlexChildren_ShrinksProportionally()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Row,
                    Width = "200",  // Intentionally smaller than children's total
                    Children = new List<TemplateElement>
                    {
                        new FlexElement { Width = "120", Shrink = 1, Padding = "4", Children = new List<TemplateElement> { new TextElement { Content = "A", Size = "12" } } },
                        new FlexElement { Width = "120", Shrink = 2, Padding = "4", Children = new List<TemplateElement> { new TextElement { Content = "B", Size = "12" } } }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Total overflow = 240 - 200 = 40
        // shrink weighted: A = 1*120=120, B = 2*120=240, total=360
        // A shrinks by 40*120/360 ~ 13.3, B shrinks by 40*240/360 ~ 26.7
        Assert.True(flex.Children[0].Width > flex.Children[1].Width,
            $"Shrink:1 ({flex.Children[0].Width}) should be wider than Shrink:2 ({flex.Children[1].Width})");
        var totalWidth = flex.Children[0].Width + flex.Children[1].Width;
        Assert.True(Math.Abs(totalWidth - 200) < 1, $"Children should fit in 200px container: total={totalWidth}");
    }

    [Fact]
    public void ComputeLayout_ColumnShrink_WithExplicitHeight_ShrinksProportionally()
    {
        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 300 },
            Elements = new List<TemplateElement>
            {
                new FlexElement
                {
                    Direction = FlexDirection.Column,
                    Height = "100",  // Smaller than children total (120+120=240)
                    Children = new List<TemplateElement>
                    {
                        new TextElement { Content = "A", Height = "120", Shrink = 1 },
                        new TextElement { Content = "B", Height = "120", Shrink = 2 }
                    }
                }
            }
        };

        var root = _engine.ComputeLayout(template);
        var flex = root.Children[0];

        Assert.True(flex.Children[0].Height > flex.Children[1].Height,
            $"Shrink:1 ({flex.Children[0].Height}) should be taller than Shrink:2 ({flex.Children[1].Height})");
        var totalHeight = flex.Children[0].Height + flex.Children[1].Height;
        Assert.True(Math.Abs(totalHeight - 100) < 1, $"Children should fit in 100px: total={totalHeight}");
    }

    // ============================================
    // Justify-Content Overflow Fallback Tests
    // ============================================

    [Fact]
    public void ComputeLayout_SpaceBetween_NegativeFreeSpace_FallbackToStart()
    {
        // Arrange: row flex width=200, justify=SpaceBetween, children total width > 200
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "200",
            Justify = JustifyContent.SpaceBetween
        };
        flex.AddChild(new TextElement { Content = "A", Width = "120", Height = "30", Shrink = 0 });
        flex.AddChild(new TextElement { Content = "B", Width = "120", Height = "30", Shrink = 0 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        var engine = new LayoutEngine();
        var root = engine.ComputeLayout(template);
        var flexNode = root.Children[0];

        // Assert: should fallback to Start (items start at X=0)
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(120f, flexNode.Children[1].X, 0.1f);
    }

    [Fact]
    public void ComputeLayout_SpaceAround_NegativeFreeSpace_FallbackToStart()
    {
        // Arrange: same pattern but with SpaceAround
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "200",
            Justify = JustifyContent.SpaceAround
        };
        flex.AddChild(new TextElement { Content = "A", Width = "120", Height = "30", Shrink = 0 });
        flex.AddChild(new TextElement { Content = "B", Width = "120", Height = "30", Shrink = 0 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        var engine = new LayoutEngine();
        var root = engine.ComputeLayout(template);
        var flexNode = root.Children[0];

        // Assert: should fallback to Start
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(120f, flexNode.Children[1].X, 0.1f);
    }

    [Fact]
    public void ComputeLayout_SpaceEvenly_NegativeFreeSpace_FallbackToStart()
    {
        // Arrange: same pattern but with SpaceEvenly
        var flex = new FlexElement
        {
            Direction = FlexDirection.Row,
            Width = "200",
            Justify = JustifyContent.SpaceEvenly
        };
        flex.AddChild(new TextElement { Content = "A", Width = "120", Height = "30", Shrink = 0 });
        flex.AddChild(new TextElement { Content = "B", Width = "120", Height = "30", Shrink = 0 });

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 400 },
            Elements = new List<TemplateElement> { flex }
        };

        var engine = new LayoutEngine();
        var root = engine.ComputeLayout(template);
        var flexNode = root.Children[0];

        // Assert: should fallback to Start
        Assert.Equal(0f, flexNode.Children[0].X, 0.1f);
        Assert.Equal(120f, flexNode.Children[1].X, 0.1f);
    }
}
