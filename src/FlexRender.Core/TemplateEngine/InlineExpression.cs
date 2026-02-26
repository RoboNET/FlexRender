namespace FlexRender.TemplateEngine;

/// <summary>
/// Arithmetic operators supported in inline expressions.
/// </summary>
public enum ArithmeticOperator
{
    /// <summary>Addition operator (+).</summary>
    Add,

    /// <summary>Subtraction operator (-).</summary>
    Subtract,

    /// <summary>Multiplication operator (*).</summary>
    Multiply,

    /// <summary>Division operator (/).</summary>
    Divide
}

/// <summary>
/// Base class for all inline expression AST nodes.
/// Used by the Pratt parser to represent parsed expressions within <c>{{...}}</c> blocks.
/// </summary>
public abstract record InlineExpression;

/// <summary>
/// A path expression referencing a variable (e.g., <c>user.name</c>, <c>items[0].price</c>).
/// </summary>
/// <param name="Path">The dot-separated path to the variable.</param>
public sealed record PathExpression(string Path) : InlineExpression;

/// <summary>
/// A numeric literal (e.g., <c>42</c>, <c>3.14</c>).
/// </summary>
/// <param name="Value">The decimal value of the literal.</param>
public sealed record NumberLiteral(decimal Value) : InlineExpression;

/// <summary>
/// A string literal enclosed in double quotes (e.g., <c>"hello"</c>).
/// </summary>
/// <param name="Value">The string value without quotes.</param>
public sealed record StringLiteral(string Value) : InlineExpression;

/// <summary>
/// A boolean literal (<c>true</c> or <c>false</c>).
/// </summary>
/// <param name="Value">The boolean value.</param>
public sealed record BoolLiteral(bool Value) : InlineExpression;

/// <summary>
/// A null literal (<c>null</c>).
/// </summary>
public sealed record NullLiteral() : InlineExpression;

/// <summary>
/// A binary arithmetic expression (e.g., <c>price * quantity</c>).
/// </summary>
/// <param name="Left">The left operand.</param>
/// <param name="Op">The arithmetic operator.</param>
/// <param name="Right">The right operand.</param>
public sealed record ArithmeticExpression(InlineExpression Left, ArithmeticOperator Op, InlineExpression Right) : InlineExpression;

/// <summary>
/// A null-coalesce expression (e.g., <c>name ?? "Guest"</c>).
/// Returns <paramref name="Right"/> when <paramref name="Left"/> evaluates to null.
/// </summary>
/// <param name="Left">The primary expression.</param>
/// <param name="Right">The fallback expression.</param>
public sealed record CoalesceExpression(InlineExpression Left, InlineExpression Right) : InlineExpression;

/// <summary>
/// A named argument for a filter (e.g., <c>suffix:'...'</c>). A null <paramref name="Value"/>
/// indicates a boolean flag (e.g., <c>fromEnd</c>).
/// </summary>
/// <param name="Name">The argument name.</param>
/// <param name="Value">The argument value, or null for boolean flags.</param>
public sealed record FilterNamedArgument(string Name, string? Value);

/// <summary>
/// A filter pipe expression (e.g., <c>price | currency</c>, <c>name | truncate:30 suffix:'...' fromEnd</c>).
/// Applies a named filter to the input expression.
/// </summary>
/// <param name="Input">The expression whose result is passed to the filter.</param>
/// <param name="FilterName">The name of the filter to apply.</param>
/// <param name="Argument">An optional positional string argument (after the colon).</param>
/// <param name="NamedArguments">Optional named arguments and flags.</param>
public sealed record FilterExpression(
    InlineExpression Input,
    string FilterName,
    string? Argument,
    IReadOnlyList<FilterNamedArgument>? NamedArguments = null
) : InlineExpression;

/// <summary>
/// A unary negation expression (e.g., <c>-price</c>).
/// </summary>
/// <param name="Operand">The expression to negate.</param>
public sealed record NegateExpression(InlineExpression Operand) : InlineExpression;

/// <summary>
/// Comparison operators supported in inline expressions.
/// </summary>
public enum ComparisonOperator
{
    /// <summary>Equality operator (==).</summary>
    Equal,

    /// <summary>Inequality operator (!=).</summary>
    NotEqual,

    /// <summary>Less than operator (&lt;).</summary>
    LessThan,

    /// <summary>Greater than operator (&gt;).</summary>
    GreaterThan,

    /// <summary>Less than or equal operator (&lt;=).</summary>
    LessThanOrEqual,

    /// <summary>Greater than or equal operator (&gt;=).</summary>
    GreaterThanOrEqual
}

/// <summary>
/// A binary comparison expression (e.g., <c>price &gt; 100</c>, <c>status == "paid"</c>).
/// </summary>
/// <param name="Left">The left operand.</param>
/// <param name="Op">The comparison operator.</param>
/// <param name="Right">The right operand.</param>
public sealed record ComparisonExpression(InlineExpression Left, ComparisonOperator Op, InlineExpression Right) : InlineExpression;

/// <summary>
/// A logical NOT expression (e.g., <c>!isActive</c>).
/// Returns true when the operand is falsy, false when truthy.
/// </summary>
/// <param name="Operand">The expression to negate logically.</param>
public sealed record NotExpression(InlineExpression Operand) : InlineExpression;

/// <summary>
/// A truthy coalescing expression using <c>||</c> (e.g., <c>name || 'Guest'</c>).
/// Returns <paramref name="Left"/> if truthy, otherwise evaluates and returns <paramref name="Right"/>.
/// </summary>
/// <param name="Left">The primary expression.</param>
/// <param name="Right">The fallback expression.</param>
public sealed record LogicalOrExpression(InlineExpression Left, InlineExpression Right) : InlineExpression;

/// <summary>
/// A logical AND expression using <c>&amp;&amp;</c> (e.g., <c>a &amp;&amp; b</c>).
/// Returns <paramref name="Left"/> if falsy, otherwise evaluates and returns <paramref name="Right"/>.
/// </summary>
/// <param name="Left">The left expression.</param>
/// <param name="Right">The right expression.</param>
public sealed record LogicalAndExpression(InlineExpression Left, InlineExpression Right) : InlineExpression;

/// <summary>
/// A computed index/key access expression (e.g., <c>dict[lang]</c>, <c>arr[idx]</c>).
/// Evaluates <see cref="Index"/> and uses the result as a key (for <see cref="ObjectValue"/>)
/// or numeric index (for <see cref="ArrayValue"/>).
/// </summary>
/// <param name="Target">The expression being indexed (the object or array).</param>
/// <param name="Index">The expression whose result is used as the key or index.</param>
public sealed record IndexAccessExpression(InlineExpression Target, InlineExpression Index) : InlineExpression;
