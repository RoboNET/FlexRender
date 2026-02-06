namespace FlexRender.Layout.Units;

/// <summary>
/// Represents a single margin value that can be either a fixed pixel amount or auto.
/// Auto margins consume free space during flexbox layout, overriding justify-content
/// on the main axis and align-items on the cross axis.
/// </summary>
/// <param name="Pixels">The pixel value (null for auto margins before distribution).</param>
/// <param name="IsAuto">Whether this margin is auto-distributed.</param>
public readonly record struct MarginValue(float? Pixels, bool IsAuto)
{
    /// <summary>Creates an auto margin that will consume available free space.</summary>
    public static MarginValue Auto => new(null, true);

    /// <summary>Creates a fixed margin with a specific pixel value.</summary>
    /// <param name="px">The margin size in pixels.</param>
    /// <returns>A fixed <see cref="MarginValue"/> with the specified pixel value.</returns>
    public static MarginValue Fixed(float px) => new(px, false);

    /// <summary>The resolved pixel value (0 for auto margins before distribution).</summary>
    public float ResolvedPixels => Pixels ?? 0f;
}

/// <summary>
/// Represents resolved margin values for all four sides, supporting auto margins.
/// Auto margins on the main axis consume free space before justify-content is applied.
/// Auto margins on the cross axis override align-items/align-self behavior.
/// </summary>
/// <param name="Top">Top margin.</param>
/// <param name="Right">Right margin.</param>
/// <param name="Bottom">Bottom margin.</param>
/// <param name="Left">Left margin.</param>
public readonly record struct MarginValues(
    MarginValue Top, MarginValue Right, MarginValue Bottom, MarginValue Left)
{
    /// <summary>Whether any side has an auto margin.</summary>
    public bool HasAuto => Top.IsAuto || Right.IsAuto || Bottom.IsAuto || Left.IsAuto;

    /// <summary>
    /// Counts auto margins on the main axis.
    /// For column containers, the main axis is vertical (Top/Bottom).
    /// For row containers, the main axis is horizontal (Left/Right).
    /// </summary>
    /// <param name="isColumn">True for column direction, false for row direction.</param>
    /// <returns>The number of auto margins on the main axis (0, 1, or 2).</returns>
    public int MainAxisAutoCount(bool isColumn) => isColumn
        ? (Top.IsAuto ? 1 : 0) + (Bottom.IsAuto ? 1 : 0)
        : (Left.IsAuto ? 1 : 0) + (Right.IsAuto ? 1 : 0);

    /// <summary>
    /// Counts auto margins on the cross axis.
    /// For column containers, the cross axis is horizontal (Left/Right).
    /// For row containers, the cross axis is vertical (Top/Bottom).
    /// </summary>
    /// <param name="isColumn">True for column direction, false for row direction.</param>
    /// <returns>The number of auto margins on the cross axis (0, 1, or 2).</returns>
    public int CrossAxisAutoCount(bool isColumn) => isColumn
        ? (Left.IsAuto ? 1 : 0) + (Right.IsAuto ? 1 : 0)
        : (Top.IsAuto ? 1 : 0) + (Bottom.IsAuto ? 1 : 0);

    /// <summary>All sides zero, no auto margins.</summary>
    public static readonly MarginValues Zero = new(
        MarginValue.Fixed(0), MarginValue.Fixed(0),
        MarginValue.Fixed(0), MarginValue.Fixed(0));
}
