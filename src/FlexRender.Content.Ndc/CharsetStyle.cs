namespace FlexRender.Content.Ndc;

/// <summary>
/// Style properties for an NDC character set designator.
/// </summary>
/// <param name="Font">Font registration name (e.g., "bold", "default"). When set, used as the <c>Font</c> property on the text element.</param>
/// <param name="FontFamily">Explicit font family name (e.g., "JetBrains Mono"). Overrides the global font family for this charset.</param>
/// <param name="FontStyle">Font style string (e.g., "bold", "italic", "regular"). Maps to font weight and style on the text element.</param>
/// <param name="FontSize">Explicit font size in pixels. When set, overrides auto-calculated font size.</param>
/// <param name="Color">Text color in hex format (e.g., "#333").</param>
/// <param name="Encoding">Character encoding for this charset (e.g., "qwerty-jcuken", "none").</param>
/// <param name="Uppercase">Whether to convert text to uppercase.</param>
internal sealed record CharsetStyle(
    string? Font = null,
    string? FontFamily = null,
    string? FontStyle = null,
    int? FontSize = null,
    string? Color = null,
    string Encoding = "none",
    bool Uppercase = false);
