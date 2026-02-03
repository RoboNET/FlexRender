using FlexRender.Layout;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// Image fitting modes for controlling how images are scaled within their bounds.
/// </summary>
public enum ImageFit
{
    /// <summary>
    /// Stretch the image to fill the entire bounds (may distort aspect ratio).
    /// </summary>
    Fill,

    /// <summary>
    /// Scale the image to fit within bounds while preserving aspect ratio (may have empty space).
    /// </summary>
    Contain,

    /// <summary>
    /// Scale the image to cover bounds while preserving aspect ratio (may be cropped).
    /// </summary>
    Cover,

    /// <summary>
    /// Use the image's natural size without scaling.
    /// </summary>
    None
}

/// <summary>
/// An image element in the template.
/// </summary>
public sealed class ImageElement : TemplateElement
{
    /// <inheritdoc />
    public override ElementType Type => ElementType.Image;

    /// <summary>
    /// The source of the image. Can be a file path or base64-encoded data URL.
    /// </summary>
    public string Src { get; set; } = "";

    /// <summary>
    /// The width of the image container in pixels. If null, uses the image's natural width.
    /// </summary>
    public int? ImageWidth { get; set; }

    /// <summary>
    /// The height of the image container in pixels. If null, uses the image's natural height.
    /// </summary>
    public int? ImageHeight { get; set; }

    /// <summary>
    /// How the image should be fitted within its container bounds.
    /// </summary>
    public ImageFit Fit { get; set; } = ImageFit.Contain;

    // Flex item properties

    /// <summary>Flex grow factor.</summary>
    public float Grow { get; set; }

    /// <summary>Flex shrink factor.</summary>
    public float Shrink { get; set; } = 1f;

    /// <summary>Flex basis (px, %, em, auto).</summary>
    public string Basis { get; set; } = "auto";

    /// <summary>Self alignment override.</summary>
    public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;

    /// <summary>Display order.</summary>
    public int Order { get; set; }

    /// <summary>Width (px, %, em, auto).</summary>
    public string? Width { get; set; }

    /// <summary>Height (px, %, em, auto).</summary>
    public string? Height { get; set; }
}
