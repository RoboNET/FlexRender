namespace FlexRender.TemplateEngine;

/// <summary>
/// Types of tokens in template expressions.
/// </summary>
public enum TokenType
{
    /// <summary>Plain text content.</summary>
    Text,
    /// <summary>Variable substitution: {{path}}.</summary>
    Variable,
    /// <summary>Start of if block: {{#if condition}}.</summary>
    IfStart,
    /// <summary>Else clause: {{else}}.</summary>
    Else,
    /// <summary>End of if block: {{/if}}.</summary>
    IfEnd,
    /// <summary>Start of each loop: {{#each array}}.</summary>
    EachStart,
    /// <summary>End of each loop: {{/each}}.</summary>
    EachEnd,
    /// <summary>Inline expression with operators/filters: {{price * quantity | currency}}.</summary>
    InlineExpression
}

/// <summary>
/// Base class for all expression tokens.
/// </summary>
/// <param name="Type">The token type.</param>
public abstract record ExpressionToken(TokenType Type);

/// <summary>
/// Plain text content outside of template expressions.
/// </summary>
/// <param name="Value">The text content.</param>
public sealed record TextToken(string Value) : ExpressionToken(TokenType.Text);

/// <summary>
/// Variable substitution token: {{path}}.
/// </summary>
/// <param name="Path">The variable path (e.g., "user.name" or "items[0]").</param>
public sealed record VariableToken(string Path) : ExpressionToken(TokenType.Variable);

/// <summary>
/// Start of conditional block: {{#if condition}}.
/// </summary>
/// <param name="Condition">The condition expression to evaluate.</param>
public sealed record IfStartToken(string Condition) : ExpressionToken(TokenType.IfStart);

/// <summary>
/// Else clause in conditional: {{else}}.
/// </summary>
public sealed record ElseToken() : ExpressionToken(TokenType.Else);

/// <summary>
/// End of conditional block: {{/if}}.
/// </summary>
public sealed record IfEndToken() : ExpressionToken(TokenType.IfEnd);

/// <summary>
/// Start of loop block: {{#each arrayPath}}.
/// </summary>
/// <param name="ArrayPath">The path to the array to iterate.</param>
public sealed record EachStartToken(string ArrayPath) : ExpressionToken(TokenType.EachStart);

/// <summary>
/// End of loop block: {{/each}}.
/// </summary>
public sealed record EachEndToken() : ExpressionToken(TokenType.EachEnd);

/// <summary>
/// Inline expression token containing a parsed expression AST.
/// Created when the expression content contains operators, filters, or other non-path syntax.
/// </summary>
/// <param name="Expression">The parsed expression AST.</param>
/// <param name="RawContent">The original expression text for error reporting.</param>
public sealed record InlineExpressionToken(InlineExpression Expression, string RawContent) : ExpressionToken(TokenType.InlineExpression);
