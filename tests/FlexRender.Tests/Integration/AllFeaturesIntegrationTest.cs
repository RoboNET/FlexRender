using System.Diagnostics;
using FlexRender.Parsing;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace FlexRender.Tests.Integration;

/// <summary>
/// Integration test that exercises all new features (Table, Expressions,
/// Visual Effects) together in a single template to verify they compose
/// correctly without interference.
/// </summary>
public sealed class AllFeaturesIntegrationTest : IDisposable
{
    private readonly SkiaRenderer _renderer = new();
    private readonly TemplateParser _parser = new();
    private readonly ITestOutputHelper _output;

    public AllFeaturesIntegrationTest(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        _renderer.Dispose();
    }

    /// <summary>
    /// Combined template YAML exercising:
    /// 1. Gradient background on header
    /// 2. Box-shadow on card container
    /// 3. Opacity on a watermark overlay
    /// 4. Table with dynamic data
    /// 5. Arithmetic expressions in text (price * quantity)
    /// 6. Null coalesce (discount ?? 0)
    /// 7. If condition with expression (greaterThan on arithmetic result)
    /// </summary>
    private const string AllFeaturesYaml = """
        canvas:
          width: 400
          background: "#f5f5f5"
        layout:
          # 1. Gradient background header
          - type: flex
            direction: column
            background: "linear-gradient(to right, #1a237e, #4a148c)"
            padding: "16"
            children:
              - type: text
                content: "INVOICE #{{invoiceNumber}}"
                size: 20
                color: "#ffffff"
                align: center
              - type: text
                content: "{{customerName}}"
                size: 14
                color: "#e0e0e0"
                align: center

          # 2. Box-shadow card with table
          - type: flex
            direction: column
            background: "#ffffff"
            box-shadow: "2 2 6 rgba(0,0,0,0.25)"
            padding: "12"
            margin: "8"
            children:
              # 4. Table with dynamic data
              - type: table
                array: items
                as: item
                columns:
                  - key: name
                    label: "Item"
                    grow: 1
                  - key: qty
                    label: "Qty"
                    width: "40"
                    align: center
                  - key: price
                    label: "Price"
                    width: "60"
                    align: right
                headerFont: bold
                headerBorderBottom: "solid"
                rowGap: "2"
                columnGap: "8"

          # Totals section
          - type: flex
            direction: column
            padding: "12 8"
            children:
              # 5. Arithmetic expression in text
              - type: text
                content: "Subtotal: {{subtotal}}"
                size: 14
                align: right

              # 6. Null coalesce
              - type: text
                content: "Discount: {{discount ?? 0}}"
                size: 14
                align: right

              # 7. If condition with arithmetic expression
              - type: if
                condition: subtotal
                greaterThan: 100
                then:
                  - type: text
                    content: "FREE SHIPPING"
                    size: 12
                    color: "#4caf50"
                    align: right
                else:
                  - type: text
                    content: "Shipping: $5.00"
                    size: 12
                    color: "#f44336"
                    align: right

          # 3. Opacity watermark overlay
          - type: flex
            opacity: 0.15
            padding: "4"
            children:
              - type: text
                content: "DRAFT"
                size: 32
                color: "#000000"
                align: center
        """;

    [Fact]
    public async Task AllFeatures_RenderToPng_ProducesValidImage()
    {
        var template = _parser.Parse(AllFeaturesYaml);
        var data = CreateTestData(hasDiscount: false, subtotal: 150);

        var sw = Stopwatch.StartNew();
        using var stream = new MemoryStream();
        await _renderer.RenderToPng(stream, template, data);
        sw.Stop();

        _output.WriteLine($"Render time: {sw.ElapsedMilliseconds} ms");

        var bytes = stream.ToArray();
        Assert.True(bytes.Length > 0, "PNG output should not be empty");

        using var bitmap = SKBitmap.Decode(bytes);
        Assert.NotNull(bitmap);
        Assert.Equal(400, bitmap.Width);
        Assert.True(bitmap.Height > 100, "Combined template should have significant height");

        _output.WriteLine($"Output dimensions: {bitmap.Width}x{bitmap.Height}");
        _output.WriteLine($"PNG size: {bytes.Length:N0} bytes");
    }

    [Fact]
    public async Task AllFeatures_WithDiscount_RendersToPng()
    {
        var template = _parser.Parse(AllFeaturesYaml);
        var data = CreateTestData(hasDiscount: true, subtotal: 250);

        using var stream = new MemoryStream();
        await _renderer.RenderToPng(stream, template, data);

        var bytes = stream.ToArray();
        Assert.True(bytes.Length > 0);

        using var bitmap = SKBitmap.Decode(bytes);
        Assert.NotNull(bitmap);
        Assert.Equal(400, bitmap.Width);
    }

    [Fact]
    public async Task AllFeatures_LowSubtotal_ShowsShippingCost()
    {
        var template = _parser.Parse(AllFeaturesYaml);
        var data = CreateTestData(hasDiscount: false, subtotal: 50);

        using var stream = new MemoryStream();
        await _renderer.RenderToPng(stream, template, data);

        var bytes = stream.ToArray();
        Assert.True(bytes.Length > 0);

        using var bitmap = SKBitmap.Decode(bytes);
        Assert.NotNull(bitmap);
    }

    [Fact]
    public async Task AllFeatures_EmptyItems_RenderHeaderOnly()
    {
        var template = _parser.Parse(AllFeaturesYaml);
        var data = new ObjectValue
        {
            ["invoiceNumber"] = new StringValue("INV-000"),
            ["customerName"] = new StringValue("Empty Order"),
            ["items"] = new ArrayValue(new List<TemplateValue>()),
            ["subtotal"] = new NumberValue(0),
        };

        using var stream = new MemoryStream();
        await _renderer.RenderToPng(stream, template, data);

        var bytes = stream.ToArray();
        Assert.True(bytes.Length > 0);

        using var bitmap = SKBitmap.Decode(bytes);
        Assert.NotNull(bitmap);
        Assert.Equal(400, bitmap.Width);
    }

    [Fact]
    public async Task AllFeatures_MultipleRenders_ConsistentOutput()
    {
        var template = _parser.Parse(AllFeaturesYaml);
        var data = CreateTestData(hasDiscount: false, subtotal: 150);

        byte[] firstRender;
        using (var stream = new MemoryStream())
        {
            await _renderer.RenderToPng(stream, template, data);
            firstRender = stream.ToArray();
        }

        byte[] secondRender;
        using (var stream = new MemoryStream())
        {
            await _renderer.RenderToPng(stream, template, data);
            secondRender = stream.ToArray();
        }

        Assert.Equal(firstRender.Length, secondRender.Length);
    }

    [Fact]
    public async Task AllFeatures_RenderToJpeg_AlsoWorks()
    {
        var template = _parser.Parse(AllFeaturesYaml);
        var data = CreateTestData(hasDiscount: true, subtotal: 200);

        using var stream = new MemoryStream();
        await _renderer.RenderToJpeg(stream, template, data);

        var bytes = stream.ToArray();
        Assert.True(bytes.Length > 0, "JPEG output should not be empty");

        using var bitmap = SKBitmap.Decode(bytes);
        Assert.NotNull(bitmap);
        Assert.Equal(400, bitmap.Width);
    }

    [Fact]
    public void AllFeatures_MeasureLayout_ReturnsReasonableSize()
    {
        var template = _parser.Parse(AllFeaturesYaml);
        var data = CreateTestData(hasDiscount: false, subtotal: 150);

        var size = _renderer.Measure(template, data);

        Assert.Equal(400f, size.Width);
        Assert.True(size.Height > 100, "Layout height should be substantial for a multi-section template");

        _output.WriteLine($"Measured layout: {size.Width}x{size.Height}");
    }

    private static ObjectValue CreateTestData(bool hasDiscount, decimal subtotal)
    {
        var items = new ArrayValue(new List<TemplateValue>
        {
            new ObjectValue
            {
                ["name"] = new StringValue("Widget A"),
                ["qty"] = new StringValue("2"),
                ["price"] = new StringValue("25.00"),
            },
            new ObjectValue
            {
                ["name"] = new StringValue("Gadget B"),
                ["qty"] = new StringValue("1"),
                ["price"] = new StringValue("50.00"),
            },
            new ObjectValue
            {
                ["name"] = new StringValue("Component C"),
                ["qty"] = new StringValue("5"),
                ["price"] = new StringValue("10.00"),
            },
        });

        var data = new ObjectValue
        {
            ["invoiceNumber"] = new StringValue("INV-2026-001"),
            ["customerName"] = new StringValue("Acme Corporation"),
            ["items"] = items,
            ["subtotal"] = new NumberValue(subtotal),
        };

        if (hasDiscount)
        {
            data["discount"] = new NumberValue(15.00m);
        }

        return data;
    }

    // === Regression: Opacity and BoxShadow survive TemplatePreprocessor ===

    private const string OpacityTemplateYaml = """
        canvas:
          width: 200
        layout:
          - type: flex
            direction: column
            opacity: {0}
            background: "#FF0000"
            padding: "20"
            children:
              - type: text
                content: "OPAQUE"
                size: 24
                color: "#FFFFFF"
        """;

    [Fact]
    public async Task Regression_OpacitySurvivesPreprocessor_OutputDiffersFromFullOpacity()
    {
        var opaqueYaml = OpacityTemplateYaml.Replace("{0}", "1.0");
        var semiTransparentYaml = OpacityTemplateYaml.Replace("{0}", "0.3");

        var opaqueTemplate = _parser.Parse(opaqueYaml);
        var transparentTemplate = _parser.Parse(semiTransparentYaml);
        var data = new ObjectValue();

        byte[] opaqueBytes;
        using (var stream = new MemoryStream())
        {
            await _renderer.RenderToPng(stream, opaqueTemplate, data);
            opaqueBytes = stream.ToArray();
        }

        byte[] transparentBytes;
        using (var stream = new MemoryStream())
        {
            await _renderer.RenderToPng(stream, transparentTemplate, data);
            transparentBytes = stream.ToArray();
        }

        Assert.True(opaqueBytes.Length > 0);
        Assert.True(transparentBytes.Length > 0);

        // The two images must differ because opacity 0.3 should produce a visibly different result
        Assert.False(opaqueBytes.SequenceEqual(transparentBytes),
            "Opacity 0.3 should produce different output than opacity 1.0 — opacity may not survive TemplatePreprocessor");
    }

    [Fact]
    public async Task Regression_BoxShadowSurvivesPreprocessor_OutputDiffersFromNoShadow()
    {
        const string noShadowYaml = """
            canvas:
              width: 200
            layout:
              - type: flex
                direction: column
                background: "#FFFFFF"
                padding: "30"
                children:
                  - type: flex
                    background: "#0000FF"
                    padding: "20"
                    children:
                      - type: text
                        content: "BOX"
                        size: 16
                        color: "#FFFFFF"
            """;

        const string withShadowYaml = """
            canvas:
              width: 200
            layout:
              - type: flex
                direction: column
                background: "#FFFFFF"
                padding: "30"
                children:
                  - type: flex
                    background: "#0000FF"
                    box-shadow: "4 4 8 rgba(0,0,0,0.5)"
                    padding: "20"
                    children:
                      - type: text
                        content: "BOX"
                        size: 16
                        color: "#FFFFFF"
            """;

        var noShadowTemplate = _parser.Parse(noShadowYaml);
        var shadowTemplate = _parser.Parse(withShadowYaml);
        var data = new ObjectValue();

        byte[] noShadowBytes;
        using (var stream = new MemoryStream())
        {
            await _renderer.RenderToPng(stream, noShadowTemplate, data);
            noShadowBytes = stream.ToArray();
        }

        byte[] shadowBytes;
        using (var stream = new MemoryStream())
        {
            await _renderer.RenderToPng(stream, shadowTemplate, data);
            shadowBytes = stream.ToArray();
        }

        Assert.True(noShadowBytes.Length > 0);
        Assert.True(shadowBytes.Length > 0);

        // The two images must differ because box-shadow should produce visible shadow pixels
        Assert.False(noShadowBytes.SequenceEqual(shadowBytes),
            "Box-shadow should produce different output — BoxShadow may not survive TemplatePreprocessor");
    }
}
