using FlexRender.Layout;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Parsing.Ast;

/// <summary>
/// Tests for QrElement AST model.
/// </summary>
public class QrElementTests
{
    /// <summary>
    /// Verifies default values are set correctly.
    /// </summary>
    [Fact]
    public void QrElement_DefaultValues()
    {
        var qr = new QrElement();

        Assert.Equal("", qr.Data);
        Assert.Equal(100, qr.Size);
        Assert.Equal(ErrorCorrectionLevel.M, qr.ErrorCorrection);
        Assert.Equal("#000000", qr.Foreground);
        Assert.Null(qr.Background);
        Assert.Equal("none", qr.Rotate);
    }

    /// <summary>
    /// Verifies custom values can be set.
    /// </summary>
    [Fact]
    public void QrElement_CustomValues()
    {
        var qr = new QrElement
        {
            Data = "https://example.com",
            Size = 150,
            ErrorCorrection = ErrorCorrectionLevel.H,
            Foreground = "#0000ff",
            Background = "#ffff00",
            Rotate = "right"
        };

        Assert.Equal("https://example.com", qr.Data);
        Assert.Equal(150, qr.Size);
        Assert.Equal(ErrorCorrectionLevel.H, qr.ErrorCorrection);
        Assert.Equal("#0000ff", qr.Foreground);
        Assert.Equal("#ffff00", qr.Background);
        Assert.Equal("right", qr.Rotate);
    }

    /// <summary>
    /// Verifies QrElement has correct ElementType.
    /// </summary>
    [Fact]
    public void QrElement_HasCorrectType()
    {
        var qr = new QrElement();

        Assert.IsAssignableFrom<TemplateElement>(qr);
        Assert.Equal(ElementType.Qr, qr.Type);
    }

    /// <summary>
    /// Verifies all error correction levels are available.
    /// </summary>
    [Theory]
    [InlineData(ErrorCorrectionLevel.L)]
    [InlineData(ErrorCorrectionLevel.M)]
    [InlineData(ErrorCorrectionLevel.Q)]
    [InlineData(ErrorCorrectionLevel.H)]
    public void ErrorCorrectionLevel_AllLevelsExist(ErrorCorrectionLevel level)
    {
        var qr = new QrElement { ErrorCorrection = level };
        Assert.Equal(level, qr.ErrorCorrection);
    }

    /// <summary>
    /// Verifies flex item properties have correct defaults.
    /// </summary>
    [Fact]
    public void QrElement_FlexItemProperties_DefaultValues()
    {
        var qr = new QrElement();

        Assert.Equal(0f, qr.Grow);
        Assert.Equal(1f, qr.Shrink);
        Assert.Equal("auto", qr.Basis);
        Assert.Equal(AlignSelf.Auto, qr.AlignSelf);
        Assert.Equal(0, qr.Order);
        Assert.Null(qr.Width);
        Assert.Null(qr.Height);
    }
}
