using FlexRender.TemplateEngine;

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
    public ExprValue<string> Src { get; set; } = "";

    /// <summary>
    /// The width of the image container in pixels. If null, uses the image's natural width.
    /// </summary>
    public ExprValue<int?> ImageWidth { get; set; }

    /// <summary>
    /// The height of the image container in pixels. If null, uses the image's natural height.
    /// </summary>
    public ExprValue<int?> ImageHeight { get; set; }

    /// <summary>
    /// How the image should be fitted within its container bounds.
    /// </summary>
    public ExprValue<ImageFit> Fit { get; set; } = ImageFit.Contain;

    /// <inheritdoc />
    public override TemplateElement CloneWithSubstitution(Func<string?, string?> substitutor)
    {
        ArgumentNullException.ThrowIfNull(substitutor);

        var clone = new ImageElement
        {
            Src = Src,
            ImageWidth = ImageWidth,
            ImageHeight = ImageHeight,
            Fit = Fit
        };
        CopyBasePropertiesTo(clone, substitutor);
        return clone;
    }

    /// <summary>
    /// Creates a clone of this element with a different <see cref="Src"/> value.
    /// Used by the expansion pipeline to attach resolved binary data.
    /// </summary>
    /// <param name="src">The resolved source expression with optional bytes attached.</param>
    /// <param name="substitutor">Function to substitute template variables in string values.</param>
    /// <returns>A new image element with the specified source.</returns>
    public ImageElement WithSrc(ExprValue<string> src, Func<string?, string?> substitutor)
    {
        var clone = (ImageElement)CloneWithSubstitution(substitutor);
        clone.Src = src;
        return clone;
    }

    /// <inheritdoc />
    public override void ResolveExpressions(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        base.ResolveExpressions(resolver, data);
        Src = Src.Resolve(resolver, data);
        ImageWidth = ImageWidth.Resolve(resolver, data);
        ImageHeight = ImageHeight.Resolve(resolver, data);
        Fit = Fit.Resolve(resolver, data);
    }

    /// <inheritdoc />
    public override void Materialize()
    {
        base.Materialize();
        Src = Src.Materialize(nameof(Src), ValueKind.Path);
        ImageWidth = ImageWidth.Materialize(nameof(ImageWidth));
        ImageHeight = ImageHeight.Materialize(nameof(ImageHeight));
        Fit = Fit.Materialize(nameof(Fit));
    }
}
