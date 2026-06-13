using System;

namespace FlexRender.Charts;

/// <summary>
/// A 2D point in pixel space, returned by <see cref="RadarGeometry"/> so the Core math layer stays
/// independent of any rendering library.
/// </summary>
/// <param name="X">The pixel X.</param>
/// <param name="Y">The pixel Y.</param>
public readonly record struct RadarPoint(float X, float Y);

/// <summary>
/// Pure, renderer-agnostic geometry for radar (spider) charts. Spoke 0 points straight up and
/// successive spokes advance clockwise; a value's radial fraction (0 at center, 1 at the outer
/// radius) projects onto its spoke.
/// </summary>
public static class RadarGeometry
{
    /// <summary>
    /// Returns the angle (in radians) of a spoke, measured from the positive X axis with screen Y
    /// growing downward. Spoke 0 is at <c>-PI/2</c> (straight up); spokes advance clockwise.
    /// </summary>
    /// <param name="spokeIndex">The zero-based spoke index.</param>
    /// <param name="spokeCount">The total number of spokes (must be positive).</param>
    /// <returns>The spoke angle in radians.</returns>
    public static float SpokeAngle(int spokeIndex, int spokeCount)
    {
        if (spokeCount <= 0)
            return -MathF.PI / 2f;
        return (-MathF.PI / 2f) + ((2f * MathF.PI) * spokeIndex / spokeCount);
    }

    /// <summary>
    /// Projects a radial fraction onto a spoke, returning the pixel point.
    /// </summary>
    /// <param name="centerX">The radar center X in pixels.</param>
    /// <param name="centerY">The radar center Y in pixels.</param>
    /// <param name="radius">The outer radius in pixels (fraction 1 maps here).</param>
    /// <param name="spokeIndex">The zero-based spoke index.</param>
    /// <param name="spokeCount">The total number of spokes.</param>
    /// <param name="fraction">The radial fraction in <c>[0, 1]</c> (clamped).</param>
    /// <returns>The projected pixel point.</returns>
    public static RadarPoint Project(
        float centerX, float centerY, float radius, int spokeIndex, int spokeCount, float fraction)
    {
        var f = Math.Clamp(fraction, 0f, 1f);
        var angle = SpokeAngle(spokeIndex, spokeCount);
        var r = radius * f;
        return new RadarPoint(
            centerX + (r * MathF.Cos(angle)),
            centerY + (r * MathF.Sin(angle)));
    }
}
