using FlexRender.Layout.Units;

namespace FlexRender.Layout;

/// <summary>
/// Intrinsic size measurements for an element computed during the measure pass.
/// MinWidth/MinHeight represent minimum content size (e.g., longest word for text).
/// MaxWidth/MaxHeight represent preferred natural size (e.g., full text width without wrapping).
/// </summary>
public readonly record struct IntrinsicSize(
    float MinWidth,
    float MaxWidth,
    float MinHeight,
    float MaxHeight)
{
    /// <summary>
    /// Returns a new IntrinsicSize with padding added to all dimensions.
    /// Padding is added twice (left+right or top+bottom).
    /// </summary>
    public IntrinsicSize WithPadding(float padding)
    {
        var p2 = Math.Max(0f, padding) * 2;
        return new IntrinsicSize(
            MinWidth + p2,
            MaxWidth + p2,
            MinHeight + p2,
            MaxHeight + p2);
    }

    /// <summary>
    /// Returns a new IntrinsicSize with non-uniform padding added.
    /// Horizontal (Left + Right) is added to widths; Vertical (Top + Bottom) to heights.
    /// </summary>
    /// <param name="padding">The per-side padding values.</param>
    /// <returns>A new IntrinsicSize with padding applied.</returns>
    public IntrinsicSize WithPadding(PaddingValues padding)
    {
        var h = Math.Max(0f, padding.Horizontal);
        var v = Math.Max(0f, padding.Vertical);
        return new IntrinsicSize(
            MinWidth + h,
            MaxWidth + h,
            MinHeight + v,
            MaxHeight + v);
    }

    /// <summary>
    /// Returns a new IntrinsicSize with margin added to all dimensions.
    /// Margin is added twice (left+right or top+bottom).
    /// </summary>
    public IntrinsicSize WithMargin(float margin)
    {
        var m2 = Math.Max(0f, margin) * 2;
        return new IntrinsicSize(
            MinWidth + m2,
            MaxWidth + m2,
            MinHeight + m2,
            MaxHeight + m2);
    }

    /// <summary>
    /// Returns a new IntrinsicSize with non-uniform margin added.
    /// </summary>
    /// <param name="margin">The per-side margin values.</param>
    /// <returns>A new IntrinsicSize with margin applied.</returns>
    public IntrinsicSize WithMargin(PaddingValues margin)
    {
        var h = Math.Max(0f, margin.Horizontal);
        var v = Math.Max(0f, margin.Vertical);
        return new IntrinsicSize(
            MinWidth + h,
            MaxWidth + h,
            MinHeight + v,
            MaxHeight + v);
    }
}
