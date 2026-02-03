using FlexRender.Layout.Units;
using FlexRender.Parsing.Ast;

namespace FlexRender.Layout;

/// <summary>
/// Context for layout calculations containing parent dimensions and font size.
/// </summary>
public sealed class LayoutContext
{
    /// <summary>Available width from container.</summary>
    public float ContainerWidth { get; }

    /// <summary>Available height from container.</summary>
    public float ContainerHeight { get; }

    /// <summary>Current font size for em calculations.</summary>
    public float FontSize { get; }

    /// <summary>
    /// Pre-computed intrinsic sizes from the measure pass, keyed by element reference identity.
    /// May be null if no measure pass was performed.
    /// </summary>
    public IReadOnlyDictionary<TemplateElement, IntrinsicSize>? IntrinsicSizes { get; }

    /// <summary>
    /// Creates a new layout context.
    /// </summary>
    /// <param name="containerWidth">Available width from container.</param>
    /// <param name="containerHeight">Available height from container.</param>
    /// <param name="fontSize">Current font size for em calculations.</param>
    /// <param name="intrinsicSizes">Optional pre-computed intrinsic sizes from the measure pass.</param>
    public LayoutContext(float containerWidth, float containerHeight, float fontSize, IReadOnlyDictionary<TemplateElement, IntrinsicSize>? intrinsicSizes = null)
    {
        ContainerWidth = containerWidth;
        ContainerHeight = containerHeight;
        FontSize = fontSize;
        IntrinsicSizes = intrinsicSizes;
    }

    /// <summary>
    /// Creates a new context with different container size.
    /// </summary>
    /// <param name="width">New container width.</param>
    /// <param name="height">New container height.</param>
    /// <returns>A new LayoutContext with the specified size.</returns>
    public LayoutContext WithSize(float width, float height)
    {
        return new LayoutContext(width, height, FontSize, IntrinsicSizes);
    }

    /// <summary>
    /// Creates a new context with different font size.
    /// </summary>
    /// <param name="fontSize">New font size.</param>
    /// <returns>A new LayoutContext with the specified font size.</returns>
    public LayoutContext WithFontSize(float fontSize)
    {
        return new LayoutContext(ContainerWidth, ContainerHeight, fontSize, IntrinsicSizes);
    }

    /// <summary>
    /// Resolves a width value string to pixels.
    /// </summary>
    /// <param name="value">The value string to parse.</param>
    /// <returns>The resolved pixel value, or null for auto.</returns>
    public float? ResolveWidth(string? value)
    {
        var unit = UnitParser.Parse(value);
        return unit.Resolve(ContainerWidth, FontSize);
    }

    /// <summary>
    /// Resolves a height value string to pixels.
    /// </summary>
    /// <param name="value">The value string to parse.</param>
    /// <returns>The resolved pixel value, or null for auto.</returns>
    public float? ResolveHeight(string? value)
    {
        var unit = UnitParser.Parse(value);
        return unit.Resolve(ContainerHeight, FontSize);
    }

    /// <summary>
    /// Resolves a unit to pixels using width as the reference.
    /// </summary>
    /// <param name="unit">The unit to resolve.</param>
    /// <returns>The resolved pixel value, or null for auto.</returns>
    public float? ResolveUnit(Unit unit)
    {
        return unit.Resolve(ContainerWidth, FontSize);
    }

    /// <summary>
    /// Retrieves the pre-computed intrinsic size for an element, if available.
    /// </summary>
    /// <param name="element">The element to look up.</param>
    /// <returns>The intrinsic size if found; otherwise, null.</returns>
    public IntrinsicSize? GetIntrinsicSize(TemplateElement element)
    {
        if (IntrinsicSizes != null && IntrinsicSizes.TryGetValue(element, out var size))
            return size;
        return null;
    }
}
