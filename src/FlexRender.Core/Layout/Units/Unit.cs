using System.Globalization;

namespace FlexRender.Layout.Units;

/// <summary>
/// Types of measurement units.
/// </summary>
public enum UnitType
{
    /// <summary>Absolute pixels.</summary>
    Pixels,
    /// <summary>Percentage of parent size.</summary>
    Percent,
    /// <summary>Relative to font size.</summary>
    Em,
    /// <summary>Automatic sizing.</summary>
    Auto
}

/// <summary>
/// Represents a measurement unit value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>The type of unit.</summary>
    public UnitType Type { get; }

    /// <summary>The numeric value.</summary>
    public float Value { get; }

    private Unit(UnitType type, float value = 0)
    {
        Type = type;
        Value = value;
    }

    /// <summary>Creates a pixel unit.</summary>
    /// <param name="value">The value in pixels.</param>
    /// <returns>A new Unit with pixel type.</returns>
    public static Unit Pixels(float value) => new(UnitType.Pixels, value);

    /// <summary>Creates a percentage unit.</summary>
    /// <param name="value">The percentage value.</param>
    /// <returns>A new Unit with percent type.</returns>
    public static Unit Percent(float value) => new(UnitType.Percent, value);

    /// <summary>Creates an em unit.</summary>
    /// <param name="value">The em value.</param>
    /// <returns>A new Unit with em type.</returns>
    public static Unit Em(float value) => new(UnitType.Em, value);

    /// <summary>Gets the auto unit.</summary>
    public static Unit Auto { get; } = new(UnitType.Auto);

    /// <summary>
    /// Resolves the unit to an absolute pixel value.
    /// </summary>
    /// <param name="parentSize">The parent container size for percentage calculations.</param>
    /// <param name="fontSize">The current font size for em calculations.</param>
    /// <returns>The resolved pixel value, or null for Auto.</returns>
    public float? Resolve(float parentSize, float fontSize)
    {
        return Type switch
        {
            UnitType.Pixels => Value,
            UnitType.Percent => parentSize * Value / 100f,
            UnitType.Em => fontSize * Value,
            UnitType.Auto => null,
            _ => null
        };
    }

    /// <inheritdoc />
    public bool Equals(Unit other) => Type == other.Type && Value.Equals(other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Type, Value);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Unit left, Unit right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Unit left, Unit right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => Type switch
    {
        UnitType.Pixels => $"{Value}px",
        UnitType.Percent => $"{Value}%",
        UnitType.Em => $"{Value}em",
        UnitType.Auto => "auto",
        _ => Value.ToString(CultureInfo.InvariantCulture)
    };
}
