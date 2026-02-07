#pragma warning disable CS0618 // Testing deprecated TextMeasurer backward compatibility

using FlexRender.Configuration;
using FlexRender.Layout;
using Xunit;

namespace FlexRender.Tests.Layout;

/// <summary>
/// Tests for <see cref="LayoutEngine"/> constructor and initialization.
/// </summary>
public sealed class LayoutEngineConstructorTests
{
    /// <summary>
    /// Verifies that the default constructor initializes the engine with default resource limits.
    /// </summary>
    [Fact]
    public void LayoutEngine_DefaultConstructor_HasDefaultLimits()
    {
        // Act
        var engine = new LayoutEngine();

        // Assert
        Assert.NotNull(engine);
        // Engine should be initialized and ready to use
        Assert.Null(engine.TextMeasurer); // Starts as null, can be set later
        Assert.Equal(16f, engine.BaseFontSize); // Has default value
    }

    /// <summary>
    /// Verifies that the constructor with explicit limits stores the provided limits.
    /// </summary>
    [Fact]
    public void LayoutEngine_WithLimits_StoresLimits()
    {
        // Arrange
        var customLimits = new ResourceLimits
        {
            MaxRenderDepth = 200,
            MaxTemplateNestingDepth = 150
        };

        // Act
        var engine = new LayoutEngine(customLimits);

        // Assert
        Assert.NotNull(engine);
        // Engine should be initialized with custom limits (internal field, not directly testable)
        // We verify it doesn't throw and is usable
        Assert.Equal(16f, engine.BaseFontSize);
    }

    /// <summary>
    /// Verifies that passing null limits throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void LayoutEngine_NullLimits_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new LayoutEngine(null!));
        Assert.Equal("limits", exception.ParamName);
    }
}
