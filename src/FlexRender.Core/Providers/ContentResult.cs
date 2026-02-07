namespace FlexRender.Providers;

/// <summary>
/// Contains the result of a content provider's raster generation: PNG-encoded image bytes
/// along with the image dimensions. This is the backend-neutral exchange format between
/// content providers and rendering engines.
/// </summary>
/// <param name="PngBytes">The PNG-encoded image bytes.</param>
/// <param name="Width">The image width in pixels.</param>
/// <param name="Height">The image height in pixels.</param>
public readonly record struct ContentResult(byte[] PngBytes, int Width, int Height);
