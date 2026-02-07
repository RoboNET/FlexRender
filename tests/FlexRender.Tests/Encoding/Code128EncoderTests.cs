using FlexRender.Barcode.Code128;
using Xunit;

namespace FlexRender.Tests.Encoding;

/// <summary>
/// Tests for <see cref="Code128Encoding"/>.
/// </summary>
public sealed class Code128EncoderTests
{
    [Fact]
    public void BuildPattern_SimpleData_ReturnsPattern()
    {
        var result = Code128Encoding.BuildPattern("ABC");

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        // Pattern should only contain '0' and '1'
        Assert.All(result, c => Assert.True(c == '0' || c == '1'));
    }

    [Fact]
    public void BuildPattern_IncludesStartPattern()
    {
        var result = Code128Encoding.BuildPattern("A");

        // Code128 Start B pattern
        Assert.StartsWith("11010010000", result);
    }

    [Fact]
    public void BuildPattern_IncludesStopPattern()
    {
        var result = Code128Encoding.BuildPattern("A");

        Assert.EndsWith("1100011101011", result);
    }

    [Fact]
    public void BuildPattern_SameInput_SameOutput()
    {
        var result1 = Code128Encoding.BuildPattern("Hello");
        var result2 = Code128Encoding.BuildPattern("Hello");

        Assert.Equal(result1, result2);
    }

    [Theory]
    [InlineData("ABC123")]
    [InlineData("Hello World")]
    [InlineData("!@#$%^")]
    public void BuildPattern_VariousInputs_ProducesValidPatterns(string input)
    {
        var result = Code128Encoding.BuildPattern(input);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void BuildPattern_UnsupportedCharacter_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Code128Encoding.BuildPattern("Test\x00Invalid"));
    }
}
