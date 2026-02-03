using FlexRender.Layout.Units;

namespace FlexRender.Layout;

/// <summary>
/// Properties for a flex container.
/// </summary>
/// <param name="Direction">Direction of the main axis.</param>
/// <param name="Wrap">Whether items wrap to new lines.</param>
/// <param name="Gap">Gap between items.</param>
/// <param name="Padding">Padding inside the container.</param>
/// <param name="Justify">Main axis alignment.</param>
/// <param name="Align">Cross axis alignment.</param>
/// <param name="AlignContent">Alignment of wrapped lines.</param>
public readonly record struct FlexContainerProperties(
    FlexDirection Direction,
    FlexWrap Wrap,
    Unit Gap,
    Unit Padding,
    JustifyContent Justify,
    AlignItems Align,
    AlignContent AlignContent
)
{
    /// <summary>
    /// Default flex container properties.
    /// </summary>
    public static FlexContainerProperties Default { get; } = new(
        Direction: FlexDirection.Row,
        Wrap: FlexWrap.NoWrap,
        Gap: Unit.Pixels(0),
        Padding: Unit.Pixels(0),
        Justify: JustifyContent.Start,
        Align: AlignItems.Stretch,
        AlignContent: AlignContent.Stretch
    );

    /// <summary>
    /// Returns true if the main axis is horizontal (row direction).
    /// </summary>
    public bool IsMainAxisHorizontal => Direction == FlexDirection.Row;
}
