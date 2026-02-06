namespace FlexRender.Layout;

/// <summary>
/// Direction of the main axis in a flex container.
/// </summary>
public enum FlexDirection
{
    /// <summary>Main axis is horizontal (left to right).</summary>
    Row = 0,
    /// <summary>Main axis is vertical (top to bottom).</summary>
    Column = 1,
    /// <summary>Main axis is horizontal (right to left).</summary>
    RowReverse = 2,
    /// <summary>Main axis is vertical (bottom to top).</summary>
    ColumnReverse = 3
}

/// <summary>
/// Controls whether flex items wrap to new lines.
/// </summary>
public enum FlexWrap
{
    /// <summary>All items on one line.</summary>
    NoWrap = 0,
    /// <summary>Items wrap to additional lines.</summary>
    Wrap = 1,
    /// <summary>Items wrap in reverse order.</summary>
    WrapReverse = 2
}

/// <summary>
/// Alignment of items along the main axis.
/// </summary>
public enum JustifyContent
{
    /// <summary>Items packed toward start.</summary>
    Start = 0,
    /// <summary>Items centered.</summary>
    Center = 1,
    /// <summary>Items packed toward end.</summary>
    End = 2,
    /// <summary>Items evenly distributed; first at start, last at end.</summary>
    SpaceBetween = 3,
    /// <summary>Items evenly distributed with equal space around.</summary>
    SpaceAround = 4,
    /// <summary>Items evenly distributed with equal space between.</summary>
    SpaceEvenly = 5
}

/// <summary>
/// Alignment of items along the cross axis.
/// </summary>
public enum AlignItems
{
    /// <summary>Items aligned to start of cross axis.</summary>
    Start = 0,
    /// <summary>Items centered on cross axis.</summary>
    Center = 1,
    /// <summary>Items aligned to end of cross axis.</summary>
    End = 2,
    /// <summary>Items stretch to fill container.</summary>
    Stretch = 3,
    /// <summary>Items aligned by their baselines.</summary>
    Baseline = 4
}

/// <summary>
/// Alignment of lines when wrapping (cross axis).
/// </summary>
public enum AlignContent
{
    /// <summary>Lines packed toward start.</summary>
    Start = 0,
    /// <summary>Lines centered.</summary>
    Center = 1,
    /// <summary>Lines packed toward end.</summary>
    End = 2,
    /// <summary>Lines stretch to fill.</summary>
    Stretch = 3,
    /// <summary>Lines evenly distributed.</summary>
    SpaceBetween = 4,
    /// <summary>Lines evenly distributed with space around.</summary>
    SpaceAround = 5,
    /// <summary>Lines evenly distributed with equal space between slots.</summary>
    SpaceEvenly = 6
}

/// <summary>
/// Controls whether an element participates in layout.
/// </summary>
public enum Display
{
    /// <summary>Element participates in flex layout.</summary>
    Flex = 0,
    /// <summary>Element is hidden and removed from layout flow.</summary>
    None = 1
}

/// <summary>
/// Individual item alignment override.
/// </summary>
public enum AlignSelf
{
    /// <summary>Use parent's align-items value.</summary>
    Auto = 0,
    /// <summary>Align to start.</summary>
    Start = 1,
    /// <summary>Center.</summary>
    Center = 2,
    /// <summary>Align to end.</summary>
    End = 3,
    /// <summary>Stretch to fill.</summary>
    Stretch = 4,
    /// <summary>Align by baseline.</summary>
    Baseline = 5
}

/// <summary>
/// CSS positioning mode for an element.
/// </summary>
public enum Position
{
    /// <summary>Normal flow (default).</summary>
    Static = 0,
    /// <summary>Offset from normal flow position.</summary>
    Relative = 1,
    /// <summary>Removed from flow, positioned relative to container.</summary>
    Absolute = 2
}

/// <summary>
/// Controls how content that overflows the element's bounds is handled.
/// </summary>
public enum Overflow
{
    /// <summary>Content is visible outside bounds.</summary>
    Visible = 0,
    /// <summary>Content is clipped at bounds.</summary>
    Hidden = 1
}
