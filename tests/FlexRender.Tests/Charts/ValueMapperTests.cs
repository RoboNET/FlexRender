using FlexRender.Charts;
using Xunit;

namespace FlexRender.Tests.Charts;

/// <summary>
/// Tests for mapping data values to pixel positions within a plot band.
/// </summary>
public sealed class ValueMapperTests
{
    [Fact]
    public void MapY_MinMapsToBottom_MaxMapsToTop()
    {
        // plot spans pixel Y 0 (top) .. 100 (bottom); scale 0..50.
        var mapper = new ValueMapper(0d, 50d, plotTop: 0f, plotBottom: 100f);

        Assert.Equal(100f, mapper.MapY(0d), 3);
        Assert.Equal(0f, mapper.MapY(50d), 3);
        Assert.Equal(50f, mapper.MapY(25d), 3);
    }

    [Fact]
    public void MapY_ZeroBaseline_WhenScaleCrossesZero()
    {
        var mapper = new ValueMapper(-50d, 50d, plotTop: 0f, plotBottom: 100f);
        Assert.Equal(50f, mapper.MapY(0d), 3);
    }

    [Fact]
    public void MapY_DegenerateScale_DoesNotDivideByZero()
    {
        var mapper = new ValueMapper(10d, 10d, plotTop: 0f, plotBottom: 100f);
        var yy = mapper.MapY(10d);
        Assert.True(float.IsFinite(yy));
    }
}
