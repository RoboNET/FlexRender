using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Reproduction tests for known layout bugs.
/// Each test constructs a programmatic AST (no YAML parsing), renders to an in-memory
/// bitmap, and checks pixel colors at specific coordinates to verify correct layout behavior.
///
/// Tests are marked with <c>[Fact(Skip = "Known bug: ...")]</c> so they are documented
/// in the test suite but do not fail CI until the underlying bugs are fixed.
/// </summary>
public sealed class FlexLayoutBugTests : IDisposable
{
    private readonly SkiaRenderer _renderer = new();

    public void Dispose()
    {
        _renderer.Dispose();
    }

    // ================================================================
    // Bug #1: align-items: end does not work with auto-height containers
    // ================================================================

    /// <summary>
    /// Bug #1: align-items does not work with auto-height containers (align: end).
    ///
    /// When a row flex container has no explicit height (auto-sized from content),
    /// the container height should be determined by the tallest child (80px).
    /// Then align-items: end should align all children to the bottom of that 80px region.
    ///
    /// Expected behavior per CSS Flexbox spec:
    ///   - Container auto-height = max child height = 80px
    ///   - Child 1 (40px): Y = 80 - 40 = 40
    ///   - Child 2 (60px): Y = 80 - 60 = 20
    ///   - Child 3 (80px): Y = 80 - 80 = 0
    ///
    /// Actual behavior:
    ///   All children are aligned to the top (Y = 0), as if align-items were "start".
    ///   The align-items: end property is completely ignored when height is auto.
    ///
    /// See: docs/known-issues/layout-bugs.md
    /// </summary>
    [Fact]
    public void AlignItemsEnd_AutoHeightRow_ChildrenAlignedToBottom()
    {
        // Arrange: Row container with align: end, no explicit height, 3 children of different heights.
        // Each child has a distinct background color so we can verify position via pixel checks.
        //
        //   Container: direction=row, align=end, NO height
        //   Child 0: 60x40, red    (#FF0000)
        //   Child 1: 60x60, green  (#00FF00)
        //   Child 2: 60x80, blue   (#0000FF)
        //
        //   Expected auto-height = 80px (tallest child)
        //   Expected positions (Y relative to container):
        //     red:   Y = 80 - 40 = 40
        //     green: Y = 80 - 60 = 20
        //     blue:  Y = 80 - 80 = 0

        var container = new FlexElement
        {
            Direction = FlexDirection.Row,
            Align = AlignItems.End,
            // No Height set -- auto-sized
            Width = "180",
            Children = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "60", Height = "40",
                    Background = "#FF0000",
                    Children = new List<TemplateElement>()
                },
                new FlexElement
                {
                    Width = "60", Height = "60",
                    Background = "#00FF00",
                    Children = new List<TemplateElement>()
                },
                new FlexElement
                {
                    Width = "60", Height = "80",
                    Background = "#0000FF",
                    Children = new List<TemplateElement>()
                }
            }
        };

        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Both,
                Width = 180,
                Height = 80,
                Background = "#FFFFFF"
            },
            Elements = new List<TemplateElement> { container }
        };

        // Act: Render to bitmap
        using var bitmap = new SKBitmap(180, 80);
        var data = new ObjectValue();
        _renderer.Render(bitmap, template, data);

        // Assert: Check pixel colors at strategic positions.

        // --- Red child (40px tall) should be at Y=40..79 ---
        // Top of red child (Y=40, X=30 center of first column): should be red
        var redTop = bitmap.GetPixel(30, 40);
        Assert.True(redTop.Red > 200 && redTop.Green < 50 && redTop.Blue < 50,
            $"Expected RED at (30, 40) [top of red child at align:end], got R={redTop.Red} G={redTop.Green} B={redTop.Blue}");

        // Above red child (Y=10, X=30): should be white (empty space)
        var aboveRed = bitmap.GetPixel(30, 10);
        Assert.True(aboveRed.Red > 200 && aboveRed.Green > 200 && aboveRed.Blue > 200,
            $"Expected WHITE at (30, 10) [above red child], got R={aboveRed.Red} G={aboveRed.Green} B={aboveRed.Blue}");

        // --- Green child (60px tall) should be at Y=20..79 ---
        // Top of green child (Y=20, X=90 center of second column): should be green
        var greenTop = bitmap.GetPixel(90, 20);
        Assert.True(greenTop.Green > 200 && greenTop.Red < 50 && greenTop.Blue < 50,
            $"Expected GREEN at (90, 20) [top of green child at align:end], got R={greenTop.Red} G={greenTop.Green} B={greenTop.Blue}");

        // Above green child (Y=5, X=90): should be white (empty space)
        var aboveGreen = bitmap.GetPixel(90, 5);
        Assert.True(aboveGreen.Red > 200 && aboveGreen.Green > 200 && aboveGreen.Blue > 200,
            $"Expected WHITE at (90, 5) [above green child], got R={aboveGreen.Red} G={aboveGreen.Green} B={aboveGreen.Blue}");

        // --- Blue child (80px tall) should fill entire height Y=0..79 ---
        // Top of blue child (Y=2, X=150 center of third column): should be blue
        var blueTop = bitmap.GetPixel(150, 2);
        Assert.True(blueTop.Blue > 200 && blueTop.Red < 50 && blueTop.Green < 50,
            $"Expected BLUE at (150, 2) [top of blue child], got R={blueTop.Red} G={blueTop.Green} B={blueTop.Blue}");
    }

    /// <summary>
    /// Bug #1 (layout-only verification): Verifies the layout engine assigns correct Y
    /// positions for children in a row container with align-items: end and auto height.
    ///
    /// This test uses only the LayoutEngine (no rendering) so it isolates the layout bug
    /// from any rendering issues.
    /// </summary>
    [Fact]
    public void AlignItemsEnd_AutoHeightRow_LayoutPositions()
    {
        // Arrange
        var container = new FlexElement
        {
            Direction = FlexDirection.Row,
            Align = AlignItems.End,
            // No Height set -- auto-sized
            Children = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "60", Height = "40",
                    Children = new List<TemplateElement>()
                },
                new FlexElement
                {
                    Width = "60", Height = "60",
                    Children = new List<TemplateElement>()
                },
                new FlexElement
                {
                    Width = "60", Height = "80",
                    Children = new List<TemplateElement>()
                }
            }
        };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 180 },
            Elements = new List<TemplateElement> { container }
        };

        var engine = new LayoutEngine();

        // Act
        var root = engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: Container auto-height should be 80 (tallest child)
        Assert.Equal(80f, flex.Height, 1f);

        // Assert: Children should be bottom-aligned within the 80px container
        // Child 0 (40px): Y = 80 - 40 = 40
        Assert.Equal(40f, flex.Children[0].Y, 1f);
        // Child 1 (60px): Y = 80 - 60 = 20
        Assert.Equal(20f, flex.Children[1].Y, 1f);
        // Child 2 (80px): Y = 80 - 80 = 0
        Assert.Equal(0f, flex.Children[2].Y, 1f);
    }

    // ================================================================
    // Bug #2: align-items: center does not work with auto-height containers
    // ================================================================

    /// <summary>
    /// Bug #2: align-items does not work with auto-height containers (align: center).
    ///
    /// When a row flex container has no explicit height (auto-sized from content),
    /// the container height should be determined by the tallest child (80px).
    /// Then align-items: center should vertically center all children within that 80px region.
    ///
    /// Expected behavior per CSS Flexbox spec:
    ///   - Container auto-height = max child height = 80px
    ///   - Child 1 (40px): Y = (80 - 40) / 2 = 20
    ///   - Child 2 (60px): Y = (80 - 60) / 2 = 10
    ///   - Child 3 (80px): Y = (80 - 80) / 2 = 0
    ///
    /// Actual behavior:
    ///   All children are aligned to the top (Y = 0), as if align-items were "start".
    ///   The align-items: center property is completely ignored when height is auto.
    ///
    /// See: docs/known-issues/layout-bugs.md
    /// </summary>
    [Fact]
    public void AlignItemsCenter_AutoHeightRow_ChildrenVerticallyCentered()
    {
        // Arrange: Row container with align: center, no explicit height, 3 children of different heights.
        //
        //   Container: direction=row, align=center, NO height
        //   Child 0: 60x40, red    (#FF0000)
        //   Child 1: 60x60, green  (#00FF00)
        //   Child 2: 60x80, blue   (#0000FF)
        //
        //   Expected auto-height = 80px (tallest child)
        //   Expected positions (Y relative to container):
        //     red:   Y = (80 - 40) / 2 = 20
        //     green: Y = (80 - 60) / 2 = 10
        //     blue:  Y = (80 - 80) / 2 = 0

        var container = new FlexElement
        {
            Direction = FlexDirection.Row,
            Align = AlignItems.Center,
            // No Height set -- auto-sized
            Width = "180",
            Children = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "60", Height = "40",
                    Background = "#FF0000",
                    Children = new List<TemplateElement>()
                },
                new FlexElement
                {
                    Width = "60", Height = "60",
                    Background = "#00FF00",
                    Children = new List<TemplateElement>()
                },
                new FlexElement
                {
                    Width = "60", Height = "80",
                    Background = "#0000FF",
                    Children = new List<TemplateElement>()
                }
            }
        };

        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Both,
                Width = 180,
                Height = 80,
                Background = "#FFFFFF"
            },
            Elements = new List<TemplateElement> { container }
        };

        // Act: Render to bitmap
        using var bitmap = new SKBitmap(180, 80);
        var data = new ObjectValue();
        _renderer.Render(bitmap, template, data);

        // Assert: Check pixel colors at strategic positions.

        // --- Red child (40px tall) should be centered at Y=20..59 ---
        // Center of red child (Y=40, X=30): should be red
        var redCenter = bitmap.GetPixel(30, 40);
        Assert.True(redCenter.Red > 200 && redCenter.Green < 50 && redCenter.Blue < 50,
            $"Expected RED at (30, 40) [center of red child], got R={redCenter.Red} G={redCenter.Green} B={redCenter.Blue}");

        // Above red child (Y=5, X=30): should be white (empty space above centered child)
        var aboveRed = bitmap.GetPixel(30, 5);
        Assert.True(aboveRed.Red > 200 && aboveRed.Green > 200 && aboveRed.Blue > 200,
            $"Expected WHITE at (30, 5) [above centered red child], got R={aboveRed.Red} G={aboveRed.Green} B={aboveRed.Blue}");

        // Below red child (Y=65, X=30): should be white (empty space below centered child)
        var belowRed = bitmap.GetPixel(30, 65);
        Assert.True(belowRed.Red > 200 && belowRed.Green > 200 && belowRed.Blue > 200,
            $"Expected WHITE at (30, 65) [below centered red child], got R={belowRed.Red} G={belowRed.Green} B={belowRed.Blue}");

        // --- Green child (60px tall) should be centered at Y=10..69 ---
        // Top of green child (Y=12, X=90): should be green
        var greenTop = bitmap.GetPixel(90, 12);
        Assert.True(greenTop.Green > 200 && greenTop.Red < 50 && greenTop.Blue < 50,
            $"Expected GREEN at (90, 12) [top of centered green child], got R={greenTop.Red} G={greenTop.Green} B={greenTop.Blue}");

        // Above green child (Y=3, X=90): should be white
        var aboveGreen = bitmap.GetPixel(90, 3);
        Assert.True(aboveGreen.Red > 200 && aboveGreen.Green > 200 && aboveGreen.Blue > 200,
            $"Expected WHITE at (90, 3) [above centered green child], got R={aboveGreen.Red} G={aboveGreen.Green} B={aboveGreen.Blue}");

        // --- Blue child (80px tall) should fill entire height Y=0..79 ---
        var blueTop = bitmap.GetPixel(150, 2);
        Assert.True(blueTop.Blue > 200 && blueTop.Red < 50 && blueTop.Green < 50,
            $"Expected BLUE at (150, 2) [top of blue child], got R={blueTop.Red} G={blueTop.Green} B={blueTop.Blue}");
    }

    /// <summary>
    /// Bug #2 (layout-only verification): Verifies the layout engine assigns correct Y
    /// positions for children in a row container with align-items: center and auto height.
    /// </summary>
    [Fact]
    public void AlignItemsCenter_AutoHeightRow_LayoutPositions()
    {
        // Arrange
        var container = new FlexElement
        {
            Direction = FlexDirection.Row,
            Align = AlignItems.Center,
            // No Height set -- auto-sized
            Children = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "60", Height = "40",
                    Children = new List<TemplateElement>()
                },
                new FlexElement
                {
                    Width = "60", Height = "60",
                    Children = new List<TemplateElement>()
                },
                new FlexElement
                {
                    Width = "60", Height = "80",
                    Children = new List<TemplateElement>()
                }
            }
        };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 180 },
            Elements = new List<TemplateElement> { container }
        };

        var engine = new LayoutEngine();

        // Act
        var root = engine.ComputeLayout(template);
        var flex = root.Children[0];

        // Assert: Container auto-height should be 80 (tallest child)
        Assert.Equal(80f, flex.Height, 1f);

        // Assert: Children should be vertically centered within the 80px container
        // Child 0 (40px): Y = (80 - 40) / 2 = 20
        Assert.Equal(20f, flex.Children[0].Y, 1f);
        // Child 1 (60px): Y = (80 - 60) / 2 = 10
        Assert.Equal(10f, flex.Children[1].Y, 1f);
        // Child 2 (80px): Y = (80 - 80) / 2 = 0
        Assert.Equal(0f, flex.Children[2].Y, 1f);
    }

    // ================================================================
    // Bug #3: Vertical separator without explicit height is too small
    // ================================================================

    /// <summary>
    /// Bug #3: Vertical separator without explicit height renders as a tiny dot.
    ///
    /// When a vertical separator is placed inside a row container with an explicit height
    /// (e.g., 100px), and the separator has no explicit height, it should stretch to the
    /// container height (or at minimum the content height of 80px) via the default
    /// align-items: stretch behavior.
    ///
    /// Expected behavior per CSS Flexbox spec:
    ///   - Default align-items for flex containers is "stretch"
    ///   - A vertical separator (cross-axis dimension = height in a row) should stretch
    ///     to fill the container cross-axis size (100px)
    ///   - The separator should render as a visible vertical line spanning the full height
    ///
    /// Actual behavior:
    ///   The separator renders as a tiny dot or minimal-height line because its intrinsic
    ///   height (thickness, typically 1-2px) is used instead of stretching.
    ///
    /// Note: This test uses a container WITH explicit height=100. The existing test
    /// <c>ComputeLayout_VerticalSeparatorInRowFlex_StretchesHeight</c> already passes
    /// for this case. This additional test verifies rendering at the pixel level and
    /// also tests the scenario where the container height comes from sibling content
    /// rather than an explicit value.
    ///
    /// See: docs/known-issues/layout-bugs.md
    /// </summary>
    [Fact]
    public void VerticalSeparator_InRowWithExplicitHeight_StretchesToContainerHeight()
    {
        // Arrange: Row container (100px height) with two colored boxes and a vertical separator between them.
        //
        //   Container: direction=row, height=100px, width=200px
        //   Child 0: 80x80 red box
        //   Child 1: vertical separator, thickness=2, color=#000000, NO explicit height
        //   Child 2: 80x80 blue box
        //
        //   Expected: Separator stretches to 100px height (container height)
        //   Layout:
        //     red:       X=0,   W=80, H=80 (or stretched to 100 if align=stretch)
        //     separator: X=80,  W=2,  H=100
        //     blue:      X=82,  W=80, H=80 (or stretched to 100 if align=stretch)

        var container = new FlexElement
        {
            Direction = FlexDirection.Row,
            Height = "100",
            Width = "200",
            Align = AlignItems.Stretch, // default, but explicit for clarity
            Background = "#FFFFFF",
            Children = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "80", Height = "80",
                    Background = "#FF0000",
                    Children = new List<TemplateElement>()
                },
                new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Vertical,
                    Thickness = 2f,
                    Color = "#000000",
                    Style = SeparatorStyle.Solid
                    // No Height -- should stretch via align-items: stretch
                },
                new FlexElement
                {
                    Width = "80", Height = "80",
                    Background = "#0000FF",
                    Children = new List<TemplateElement>()
                }
            }
        };

        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Both,
                Width = 200,
                Height = 100,
                Background = "#FFFFFF"
            },
            Elements = new List<TemplateElement> { container }
        };

        // Act: Render to bitmap
        using var bitmap = new SKBitmap(200, 100);
        var data = new ObjectValue();
        _renderer.Render(bitmap, template, data);

        // Assert: The separator should be a visible black line spanning most of the height.
        // The separator is at approximately X=80..81 (2px wide).
        // Check that the separator is visible at multiple Y positions along the height.

        // Near the top (Y=5): separator should be black
        var sepTop = bitmap.GetPixel(81, 5);
        Assert.True(sepTop.Red < 50 && sepTop.Green < 50 && sepTop.Blue < 50,
            $"Expected BLACK separator at (81, 5) [near top], got R={sepTop.Red} G={sepTop.Green} B={sepTop.Blue}");

        // At the middle (Y=50): separator should be black
        var sepMid = bitmap.GetPixel(81, 50);
        Assert.True(sepMid.Red < 50 && sepMid.Green < 50 && sepMid.Blue < 50,
            $"Expected BLACK separator at (81, 50) [middle], got R={sepMid.Red} G={sepMid.Green} B={sepMid.Blue}");

        // Near the bottom (Y=90): separator should be black
        var sepBottom = bitmap.GetPixel(81, 90);
        Assert.True(sepBottom.Red < 50 && sepBottom.Green < 50 && sepBottom.Blue < 50,
            $"Expected BLACK separator at (81, 90) [near bottom], got R={sepBottom.Red} G={sepBottom.Green} B={sepBottom.Blue}");
    }

    /// <summary>
    /// Bug #3 variant: Vertical separator should stretch when container height is determined
    /// by sibling content (auto-height container with tall siblings).
    ///
    /// This is the more common real-world scenario: the container has no explicit height,
    /// but its height is determined by the tallest child (80px). The separator should
    /// stretch to match this computed height.
    ///
    /// See: docs/known-issues/layout-bugs.md
    /// </summary>
    [Fact]
    public void VerticalSeparator_InAutoHeightRow_StretchesToContentHeight()
    {
        // Arrange: Row container (auto height) with two colored boxes and a vertical separator.
        //
        //   Container: direction=row, NO height, width=200px
        //   Child 0: 80x80 red box
        //   Child 1: vertical separator, thickness=2, NO height
        //   Child 2: 80x80 blue box
        //
        //   Expected auto-height = 80px (tallest child)
        //   Separator should stretch to 80px height

        var container = new FlexElement
        {
            Direction = FlexDirection.Row,
            // No Height -- auto-sized
            Width = "200",
            Background = "#FFFFFF",
            Children = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "80", Height = "80",
                    Background = "#FF0000",
                    Children = new List<TemplateElement>()
                },
                new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Vertical,
                    Thickness = 2f,
                    Color = "#000000",
                    Style = SeparatorStyle.Solid
                },
                new FlexElement
                {
                    Width = "80", Height = "80",
                    Background = "#0000FF",
                    Children = new List<TemplateElement>()
                }
            }
        };

        var template = new Template
        {
            Canvas = new CanvasSettings
            {
                Fixed = FixedDimension.Both,
                Width = 200,
                Height = 80,
                Background = "#FFFFFF"
            },
            Elements = new List<TemplateElement> { container }
        };

        // Act: Render to bitmap
        using var bitmap = new SKBitmap(200, 80);
        var data = new ObjectValue();
        _renderer.Render(bitmap, template, data);

        // Assert: The separator should span from Y=0 to Y=79 (80px total).
        // Check at multiple Y positions.

        // Near the top (Y=5)
        var sepTop = bitmap.GetPixel(81, 5);
        Assert.True(sepTop.Red < 50 && sepTop.Green < 50 && sepTop.Blue < 50,
            $"Expected BLACK separator at (81, 5) [near top], got R={sepTop.Red} G={sepTop.Green} B={sepTop.Blue}");

        // At the middle (Y=40)
        var sepMid = bitmap.GetPixel(81, 40);
        Assert.True(sepMid.Red < 50 && sepMid.Green < 50 && sepMid.Blue < 50,
            $"Expected BLACK separator at (81, 40) [middle], got R={sepMid.Red} G={sepMid.Green} B={sepMid.Blue}");

        // Near the bottom (Y=75)
        var sepBottom = bitmap.GetPixel(81, 75);
        Assert.True(sepBottom.Red < 50 && sepBottom.Green < 50 && sepBottom.Blue < 50,
            $"Expected BLACK separator at (81, 75) [near bottom], got R={sepBottom.Red} G={sepBottom.Green} B={sepBottom.Blue}");
    }

    /// <summary>
    /// Bug #3 (layout-only verification): Verifies the layout engine assigns correct height
    /// to a vertical separator in a row with explicit container height.
    /// </summary>
    [Fact]
    public void VerticalSeparator_InRowWithExplicitHeight_LayoutStretchesHeight()
    {
        // Arrange
        var container = new FlexElement
        {
            Direction = FlexDirection.Row,
            Height = "100",
            Width = "200",
            Children = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "80", Height = "80",
                    Children = new List<TemplateElement>()
                },
                new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Vertical,
                    Thickness = 2f
                    // No explicit Height
                },
                new FlexElement
                {
                    Width = "80", Height = "80",
                    Children = new List<TemplateElement>()
                }
            }
        };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { container }
        };

        var engine = new LayoutEngine();

        // Act
        var root = engine.ComputeLayout(template);
        var flex = root.Children[0];
        var separatorNode = flex.Children[1];

        // Assert: Container should have height 100
        Assert.Equal(100f, flex.Height, 1f);

        // Assert: Separator width should be its thickness
        Assert.Equal(2f, separatorNode.Width, 1f);

        // Assert: Separator should stretch to container cross-axis size (100px)
        // This is the bug: actual height is likely 2 (thickness) instead of 100
        Assert.True(separatorNode.Height >= 80f,
            $"Separator height should be >= 80 (container content height), got {separatorNode.Height}");
        Assert.Equal(100f, separatorNode.Height, 1f);
    }

    /// <summary>
    /// Bug #3 (layout-only, auto-height variant): Verifies the layout engine assigns correct
    /// height to a vertical separator when container height comes from sibling content.
    /// </summary>
    [Fact]
    public void VerticalSeparator_InAutoHeightRow_LayoutStretchesToSiblingHeight()
    {
        // Arrange
        var container = new FlexElement
        {
            Direction = FlexDirection.Row,
            // No Height -- auto-sized from content
            Width = "200",
            Children = new List<TemplateElement>
            {
                new FlexElement
                {
                    Width = "80", Height = "80",
                    Children = new List<TemplateElement>()
                },
                new SeparatorElement
                {
                    Orientation = SeparatorOrientation.Vertical,
                    Thickness = 2f
                    // No explicit Height
                },
                new FlexElement
                {
                    Width = "80", Height = "80",
                    Children = new List<TemplateElement>()
                }
            }
        };

        var template = new Template
        {
            Canvas = new CanvasSettings { Width = 200 },
            Elements = new List<TemplateElement> { container }
        };

        var engine = new LayoutEngine();

        // Act
        var root = engine.ComputeLayout(template);
        var flex = root.Children[0];
        var separatorNode = flex.Children[1];

        // Assert: Container auto-height should be 80 (tallest child)
        Assert.Equal(80f, flex.Height, 1f);

        // Assert: Separator should stretch to container cross-axis size (80px)
        Assert.Equal(2f, separatorNode.Width, 1f);
        Assert.Equal(80f, separatorNode.Height, 1f);
    }

    // ================================================================
    // Bug #4: Image without explicit size does not inherit from container
    // ================================================================
    //
    // Status: ✅ FIXED
    //
    // This bug was fixed by modifying:
    // 1. LayoutEngine.cs - use ContainerHeight instead of DefaultTextHeight fallback
    // 2. ImageProvider.cs - added layoutWidth/layoutHeight parameters
    // 3. RenderingEngine.cs - pass computed layout dimensions to ImageProvider
    //
    // Tests for image sizing were moved to LayoutEngineTests.cs:
    // - ComputeLayout_ImageWithoutSize_UsesContainerDimensions
    // - ComputeLayout_ImageWithoutSize_InSizedContainer
    //
    // See also: ImageProviderTests.cs for rendering-level tests
}
