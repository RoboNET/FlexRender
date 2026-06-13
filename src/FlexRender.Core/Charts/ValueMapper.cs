namespace FlexRender.Charts;

/// <summary>
/// Maps a numeric data value to a pixel Y within a plot band, where the scale minimum maps to
/// the plot bottom and the scale maximum maps to the plot top (screen Y grows downward).
/// </summary>
public readonly struct ValueMapper
{
    private readonly double _min;
    private readonly double _max;
    private readonly float _plotTop;
    private readonly float _plotBottom;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueMapper"/> struct.
    /// </summary>
    /// <param name="min">The scale minimum (maps to the plot bottom).</param>
    /// <param name="max">The scale maximum (maps to the plot top).</param>
    /// <param name="plotTop">The plot top pixel Y.</param>
    /// <param name="plotBottom">The plot bottom pixel Y.</param>
    public ValueMapper(double min, double max, float plotTop, float plotBottom)
    {
        _min = min;
        _max = max;
        _plotTop = plotTop;
        _plotBottom = plotBottom;
    }

    /// <summary>
    /// Maps a data value to its pixel Y within the plot band.
    /// </summary>
    /// <param name="value">The data value.</param>
    /// <returns>The pixel Y. Returns the plot bottom when the scale is degenerate.</returns>
    public float MapY(double value)
    {
        var span = _max - _min;
        if (span <= 0d)
            return _plotBottom;

        var t = (value - _min) / span;
        return _plotBottom - (float)(t * (_plotBottom - _plotTop));
    }
}
