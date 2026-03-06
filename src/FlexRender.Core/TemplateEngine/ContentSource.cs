namespace FlexRender.TemplateEngine;

/// <summary>
/// Represents a resolved content source — either text or binary data.
/// Used as the result of content source resolution to dispatch
/// to the appropriate content parser.
/// </summary>
public abstract record ContentSource;

/// <summary>
/// Text content to be parsed by an <see cref="Abstractions.IContentParser"/>.
/// </summary>
/// <param name="Text">The text content.</param>
public sealed record TextContent(string Text) : ContentSource;

/// <summary>
/// Binary content to be parsed by an <see cref="Abstractions.IBinaryContentParser"/>.
/// </summary>
/// <param name="Data">The binary data.</param>
/// <param name="MimeType">Optional MIME type of the data (e.g., "image/png").</param>
public sealed record BinaryContent(ReadOnlyMemory<byte> Data, string? MimeType = null) : ContentSource;
