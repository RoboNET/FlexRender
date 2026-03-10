namespace FlexRender.Layout;

/// <summary>
/// Diagnostic data attached to a <see cref="LayoutNode"/> for debugging text layout.
/// </summary>
/// <param name="IntrinsicWidth">Intrinsic width from IntrinsicMeasurer (before scaling).</param>
/// <param name="ShapedWidth">Shaped width from TextShaper at final font size.</param>
/// <param name="ContentWidth">Final content width used in layout calculation.</param>
/// <param name="ResolvedTypeface">Resolved typeface family name.</param>
public sealed record LayoutDiagnostics(
    float IntrinsicWidth,
    float ShapedWidth,
    float ContentWidth,
    string? ResolvedTypeface = null);
