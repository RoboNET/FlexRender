namespace FlexRender.Layout.Units;

/// <summary>
/// Represents resolved padding values for all four sides of an element.
/// Values are in absolute pixels after unit resolution.
/// </summary>
/// <param name="Top">The top padding in pixels.</param>
/// <param name="Right">The right padding in pixels.</param>
/// <param name="Bottom">The bottom padding in pixels.</param>
/// <param name="Left">The left padding in pixels.</param>
public readonly record struct PaddingValues(float Top, float Right, float Bottom, float Left)
{
    /// <summary>
    /// Total horizontal padding (Left + Right).
    /// </summary>
    public float Horizontal => Left + Right;

    /// <summary>
    /// Total vertical padding (Top + Bottom).
    /// </summary>
    public float Vertical => Top + Bottom;

    /// <summary>
    /// A padding with all sides set to zero.
    /// </summary>
    public static readonly PaddingValues Zero = new(0f, 0f, 0f, 0f);

    /// <summary>
    /// Creates a padding with the same value on all four sides.
    /// </summary>
    /// <param name="value">The uniform padding value in pixels.</param>
    /// <returns>A new <see cref="PaddingValues"/> with equal sides.</returns>
    public static PaddingValues Uniform(float value) => new(value, value, value, value);

    /// <summary>
    /// Returns a new <see cref="PaddingValues"/> with any negative values clamped to zero.
    /// </summary>
    /// <returns>A new instance with all values >= 0.</returns>
    public PaddingValues ClampNegatives() => new(
        Math.Max(0f, Top),
        Math.Max(0f, Right),
        Math.Max(0f, Bottom),
        Math.Max(0f, Left));
}
