using System.Globalization;

namespace FlexRender.Rendering;

/// <summary>
/// Helper for parsing and working with rotation values.
/// </summary>
public static class RotationHelper
{
    /// <summary>
    /// Parses a rotation string to degrees.
    /// Supports: "none", "left", "right", "flip", or numeric degrees.
    /// </summary>
    /// <param name="rotation">The rotation string.</param>
    /// <returns>Rotation in degrees (0 if invalid or null).</returns>
    public static float ParseRotation(string? rotation)
    {
        if (string.IsNullOrEmpty(rotation))
            return 0f;

        // Handle named rotations (case-insensitive)
        switch (rotation.ToLowerInvariant())
        {
            case "none":
                return 0f;
            case "right":
                return 90f;
            case "flip":
                return 180f;
            case "left":
                return 270f;
        }

        // Try parsing as numeric degrees
        if (float.TryParse(rotation, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees))
            return degrees;

        return 0f;
    }

    /// <summary>
    /// Checks if the rotation value represents any rotation (non-zero).
    /// </summary>
    /// <param name="degrees">Rotation in degrees.</param>
    /// <returns>True if there is rotation; otherwise, false.</returns>
    public static bool HasRotation(float degrees)
    {
        return Math.Abs(degrees) > float.Epsilon;
    }

    /// <summary>
    /// Checks if the rotation swaps width and height dimensions.
    /// This occurs at 90 or 270 degrees (and their negatives).
    /// </summary>
    /// <param name="degrees">Rotation in degrees.</param>
    /// <returns>True if dimensions should be swapped; otherwise, false.</returns>
    public static bool SwapsDimensions(float degrees)
    {
        // Normalize to 0-360 range
        var normalized = ((degrees % 360) + 360) % 360;

        // Check if close to 90 or 270 degrees
        const float tolerance = 0.1f;
        return Math.Abs(normalized - 90f) < tolerance ||
               Math.Abs(normalized - 270f) < tolerance;
    }
}
