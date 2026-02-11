using System.Globalization;
using FlexRender.TemplateEngine;

namespace FlexRender.Parsing.Ast;

/// <summary>
/// Semantic hint for string validation during materialization.
/// </summary>
public enum ValueKind
{
    /// <summary>No validation -- any string accepted.</summary>
    Any,

    /// <summary>Color value -- hex (#RGB, #RRGGBB) or rgba().</summary>
    Color,

    /// <summary>Size value -- number with unit (px, em, %).</summary>
    Size,

    /// <summary>Path value -- file/url path, must not be empty.</summary>
    Path
}

/// <summary>
/// A universal property value that can hold either a typed literal or a raw expression string.
/// Used by all AST element properties to support template expressions in any property type.
/// </summary>
/// <typeparam name="T">The typed value type (string, float, int?, enum, bool).</typeparam>
public readonly struct ExprValue<T>
{
    /// <summary>
    /// Gets the raw string from YAML or resolved expression result.
    /// </summary>
    public string? RawValue { get; private init; }

    /// <summary>
    /// Gets the typed value (populated for literals at parse time, or after materialization).
    /// </summary>
    public T Value { get; private init; }

    /// <summary>
    /// Gets a value indicating whether the raw value contains template expressions ({{ }}).
    /// </summary>
    public bool IsExpression { get; private init; }

    /// <summary>
    /// Gets a value indicating whether the value has been through the full pipeline (resolve + materialize).
    /// </summary>
    public bool IsResolved { get; private init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExprValue{T}"/> struct from a typed literal.
    /// No expression, value is set directly.
    /// </summary>
    /// <param name="value">The typed literal value.</param>
    public ExprValue(T value)
    {
        Value = value;
        RawValue = null;
        IsExpression = false;
        IsResolved = false;
    }

    /// <summary>
    /// Creates an <see cref="ExprValue{T}"/> from a raw expression string containing {{ }} placeholders.
    /// </summary>
    /// <param name="raw">The raw expression string (e.g., "{{theme.opacity}}").</param>
    /// <returns>A new <see cref="ExprValue{T}"/> marked as an expression.</returns>
#pragma warning disable CA1000 // Static factory method on generic type is intentional for value type construction
    public static ExprValue<T> Expression(string raw)
    {
        return new ExprValue<T>
        {
            RawValue = raw,
            Value = default!,
            IsExpression = true,
            IsResolved = false
        };
    }

    /// <summary>
    /// Creates an <see cref="ExprValue{T}"/> from a raw literal string with its pre-parsed typed value.
    /// Used when the YAML parser has already parsed the value but the raw string is still needed.
    /// </summary>
    /// <param name="raw">The raw string from YAML.</param>
    /// <param name="value">The pre-parsed typed value.</param>
    /// <returns>A new <see cref="ExprValue{T}"/> with both raw and typed values set.</returns>
    public static ExprValue<T> RawLiteral(string raw, T value)
#pragma warning restore CA1000
    {
        return new ExprValue<T>
        {
            RawValue = raw,
            Value = value,
            IsExpression = false,
            IsResolved = false
        };
    }

    /// <summary>
    /// Implicit conversion from <typeparamref name="T"/> to <see cref="ExprValue{T}"/> for literal assignment.
    /// </summary>
    /// <param name="value">The typed literal value.</param>
    public static implicit operator ExprValue<T>(T value) => new(value);

    /// <summary>
    /// Resolves template expressions in the raw value. If this is not an expression, returns self unchanged.
    /// </summary>
    /// <param name="resolver">Function that resolves a raw template string to a concrete string value.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>A new <see cref="ExprValue{T}"/> with the resolved raw value and IsExpression set to false.</returns>
    public ExprValue<T> Resolve(Func<string, ObjectValue, string> resolver, ObjectValue data)
    {
        if (!IsExpression || RawValue is null)
            return this;

        var resolved = resolver(RawValue, data);
        return new ExprValue<T>
        {
            RawValue = resolved,
            Value = default!,
            IsExpression = false,
            IsResolved = false
        };
    }

    /// <summary>
    /// Materializes the raw value into a typed value with validation.
    /// For literals created without expressions, simply marks as resolved.
    /// </summary>
    /// <param name="propertyName">The property name for error messages.</param>
    /// <param name="kind">Optional semantic validation hint (reserved for future use).</param>
    /// <returns>A new <see cref="ExprValue{T}"/> with the materialized typed value and IsResolved set to true.</returns>
    /// <exception cref="TemplateEngineException">Thrown when the raw value cannot be parsed to <typeparamref name="T"/>.</exception>
    public ExprValue<T> Materialize(string propertyName, ValueKind kind = ValueKind.Any)
    {
        if (IsResolved)
            return this;

        // Literal with no raw value -- Value was set at construction
        if (!IsExpression && RawValue is null)
        {
            return this with { IsResolved = true };
        }

        // Raw literal (parsed at YAML time) -- Value already set, not default
        if (!IsExpression && RawValue is not null && !EqualityComparer<T>.Default.Equals(Value, default!))
        {
            return this with { IsResolved = true };
        }

        // Need to parse RawValue -> T
        var parsed = ParseRawValue(RawValue, propertyName);
        return new ExprValue<T>
        {
            RawValue = RawValue,
            Value = parsed,
            IsExpression = false,
            IsResolved = true
        };
    }

    private static T ParseRawValue(string? raw, string propertyName)
    {
        var targetType = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        var isNullable = underlyingType is not null;
        var effectiveType = underlyingType ?? targetType;

        // Null/empty handling for nullable types
        if (string.IsNullOrEmpty(raw))
        {
            if (isNullable)
                return default!;

            if (effectiveType == typeof(string))
                return (T)(object)(raw ?? "");

            throw new TemplateEngineException(
                $"Property '{propertyName}': expected {effectiveType.Name}, got empty value.");
        }

        // String passthrough
        if (effectiveType == typeof(string))
            return (T)(object)raw;

        // Float
        if (effectiveType == typeof(float))
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                return isNullable ? (T)(object)(float?)f : (T)(object)f;

            throw new TemplateEngineException(
                $"Property '{propertyName}': expected float, got '{raw}'.");
        }

        // Int
        if (effectiveType == typeof(int))
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                return isNullable ? (T)(object)(int?)i : (T)(object)i;

            throw new TemplateEngineException(
                $"Property '{propertyName}': expected int, got '{raw}'.");
        }

        // Bool
        if (effectiveType == typeof(bool))
        {
            if (bool.TryParse(raw, out var b))
                return isNullable ? (T)(object)(bool?)b : (T)(object)b;

            throw new TemplateEngineException(
                $"Property '{propertyName}': expected bool, got '{raw}'.");
        }

        // Double
        if (effectiveType == typeof(double))
        {
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return isNullable ? (T)(object)(double?)d : (T)(object)d;

            throw new TemplateEngineException(
                $"Property '{propertyName}': expected double, got '{raw}'.");
        }

        // Enums
        if (effectiveType.IsEnum)
        {
            if (Enum.TryParse(effectiveType, raw, ignoreCase: true, out var e) && e is not null)
                return (T)e;

            throw new TemplateEngineException(
                $"Property '{propertyName}': expected {effectiveType.Name}, got '{raw}'. " +
                $"Valid values: {string.Join(", ", Enum.GetNames(effectiveType))}.");
        }

        throw new TemplateEngineException(
            $"Property '{propertyName}': unsupported type {targetType.Name}.");
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsExpression)
            return $"Expr({RawValue})";
        if (RawValue is not null)
            return $"Raw({RawValue})={Value}";
        return $"{Value}";
    }
}
