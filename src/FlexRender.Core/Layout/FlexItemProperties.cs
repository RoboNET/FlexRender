using FlexRender.Layout.Units;

namespace FlexRender.Layout;

/// <summary>
/// Flex properties for an individual item within a flex container.
/// </summary>
/// <param name="Grow">Flex grow factor.</param>
/// <param name="Shrink">Flex shrink factor.</param>
/// <param name="Basis">Flex basis size.</param>
/// <param name="AlignSelf">Self alignment override.</param>
/// <param name="Order">Display order.</param>
public readonly record struct FlexItemProperties(
    float Grow,
    float Shrink,
    Unit Basis,
    AlignSelf AlignSelf,
    int Order
)
{
    /// <summary>
    /// Default flex item properties: grow=0, shrink=1, basis=auto, alignSelf=auto, order=0.
    /// </summary>
    public static FlexItemProperties Default { get; } = new(
        Grow: 0f,
        Shrink: 1f,
        Basis: Unit.Auto,
        AlignSelf: AlignSelf.Auto,
        Order: 0
    );
}
