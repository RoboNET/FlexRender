namespace FlexRender.Parsing.Ast;

/// <summary>
/// Operators for conditional comparisons in IfElement.
/// </summary>
public enum ConditionOperator
{
    /// <summary>
    /// Value equals the comparison value (universal: strings, numbers, bool, arrays, null).
    /// </summary>
    Equals,

    /// <summary>
    /// Value does not equal the comparison value.
    /// </summary>
    NotEquals,

    /// <summary>
    /// Value is in the provided list.
    /// </summary>
    In,

    /// <summary>
    /// Value is not in the provided list.
    /// </summary>
    NotIn,

    /// <summary>
    /// Array contains the specified element.
    /// </summary>
    Contains,

    /// <summary>
    /// Numeric value is greater than comparison.
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Numeric value is greater than or equal to comparison.
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    /// Numeric value is less than comparison.
    /// </summary>
    LessThan,

    /// <summary>
    /// Numeric value is less than or equal to comparison.
    /// </summary>
    LessThanOrEqual,

    /// <summary>
    /// Array has items (true) or is empty (false).
    /// </summary>
    HasItems,

    /// <summary>
    /// Array count equals N.
    /// </summary>
    CountEquals,

    /// <summary>
    /// Array count is greater than N.
    /// </summary>
    CountGreaterThan
}
