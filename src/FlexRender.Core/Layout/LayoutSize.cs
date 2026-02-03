namespace FlexRender.Layout;

/// <summary>
/// Represents a size with width and height in pixels.
/// Backend-agnostic replacement for SKSize in the layout engine.
/// </summary>
public readonly record struct LayoutSize(float Width, float Height);
