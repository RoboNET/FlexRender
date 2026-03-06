using FlexRender.Parsing.Ast;

namespace FlexRender.Abstractions;

/// <summary>
/// Provides template metadata and tree context to content parsers.
/// Passed by the template expander so parsers can access canvas settings,
/// parent elements, and other metadata without special injection hacks.
/// </summary>
public sealed record ContentParserContext
{
    /// <summary>
    /// Canvas settings from the template (width, height, background, etc.).
    /// </summary>
    public CanvasSettings? Canvas { get; init; }

    /// <summary>
    /// The template being rendered.
    /// </summary>
    public Template? Template { get; init; }

    /// <summary>
    /// The computed effective width (in pixels) of the parent element containing
    /// this content element. Used by content parsers for auto-sizing calculations
    /// (e.g., fitting N characters into the available width).
    /// Null if the parent's width cannot be determined.
    /// </summary>
    public int? ParentWidth { get; init; }
}
