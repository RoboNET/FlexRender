using System;
using System.Collections.Generic;

namespace FlexRender.Charts;

/// <summary>
/// A computed numeric axis scale with "nice" rounded bounds and evenly spaced tick values.
/// Renderer-agnostic; produced from raw data min/max by <see cref="AxisScale.Compute"/>.
/// </summary>
/// <param name="Min">The lower bound of the axis (a nice rounded value).</param>
/// <param name="Max">The upper bound of the axis (a nice rounded value).</param>
/// <param name="Step">The spacing between adjacent ticks.</param>
/// <param name="Ticks">The ordered tick values from <see cref="Min"/> to <see cref="Max"/> inclusive.</param>
public readonly record struct AxisScale(double Min, double Max, double Step, IReadOnlyList<double> Ticks)
{
    /// <summary>
    /// Computes a nice axis scale covering <paramref name="dataMin"/>..<paramref name="dataMax"/>.
    /// All-positive data is anchored at zero (bar/area baseline); all-negative data is anchored
    /// at zero above; data crossing zero keeps both sides and always includes a zero tick.
    /// Identical or empty inputs collapse to a unit range so a chart can still draw.
    /// </summary>
    /// <param name="dataMin">The smallest data value.</param>
    /// <param name="dataMax">The largest data value.</param>
    /// <param name="targetTicks">The desired approximate number of tick intervals (default 5).</param>
    /// <returns>The computed <see cref="AxisScale"/>.</returns>
    public static AxisScale Compute(double dataMin, double dataMax, int targetTicks = 5)
    {
        if (targetTicks < 1)
            targetTicks = 1;

        if (!double.IsFinite(dataMin) || !double.IsFinite(dataMax))
        {
            dataMin = 0d;
            dataMax = 1d;
        }

        if (dataMin > dataMax)
            (dataMin, dataMax) = (dataMax, dataMin);

        if (dataMin > 0d)
            dataMin = 0d;
        if (dataMax < 0d)
            dataMax = 0d;

        if (dataMin == dataMax)
        {
            if (dataMin == 0d)
            {
                dataMax = 1d;
            }
            else if (dataMin > 0d)
            {
                dataMin = 0d;
            }
            else
            {
                dataMax = 0d;
            }
        }

        var range = dataMax - dataMin;
        var rawStep = range / targetTicks;
        var step = NiceNumber(rawStep, round: true);
        if (step <= 0d)
            step = 1d;

        var niceMin = Math.Floor(dataMin / step) * step;
        var niceMax = Math.Ceiling(dataMax / step) * step;

        var ticks = new List<double>();
        var count = (int)Math.Round((niceMax - niceMin) / step);
        for (var i = 0; i <= count; i++)
        {
            ticks.Add(niceMin + (i * step));
        }

        return new AxisScale(niceMin, niceMax, step, ticks);
    }

    /// <summary>
    /// Rounds a positive number to a "nice" value (1, 2, 5, or 10 times a power of ten),
    /// the standard heuristic for readable axis ticks.
    /// </summary>
    /// <param name="value">The raw value to round (must be positive).</param>
    /// <param name="round">When true, rounds to the nearest nice value; otherwise rounds up.</param>
    /// <returns>The nice number.</returns>
    private static double NiceNumber(double value, bool round)
    {
        if (value <= 0d)
            return 1d;

        var exponent = Math.Floor(Math.Log10(value));
        var fraction = value / Math.Pow(10d, exponent);

        double niceFraction;
        if (round)
        {
            niceFraction = fraction < 1.5d ? 1d
                : fraction < 3d ? 2d
                : fraction < 7d ? 5d
                : 10d;
        }
        else
        {
            niceFraction = fraction <= 1d ? 1d
                : fraction <= 2d ? 2d
                : fraction <= 5d ? 5d
                : 10d;
        }

        return niceFraction * Math.Pow(10d, exponent);
    }
}
