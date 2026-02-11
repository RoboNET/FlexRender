using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FlexRender.TemplateEngine;

/// <summary>
/// Pratt parser for inline expressions within <c>{{...}}</c> blocks.
/// Supports arithmetic operators, comparison operators, logical NOT, null coalesce,
/// filter pipes, and parenthesized grouping.
/// </summary>
/// <remarks>
/// <para>Operator precedence (lowest to highest):</para>
/// <list type="number">
///   <item><c>|</c> (filter pipe)</item>
///   <item><c>??</c> (null coalesce)</item>
///   <item><c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c>, <c>&gt;=</c> (comparison)</item>
///   <item><c>+</c>, <c>-</c> (add, subtract)</item>
///   <item><c>*</c>, <c>/</c> (multiply, divide)</item>
///   <item>Unary <c>-</c> (negation), <c>!</c> (logical NOT)</item>
///   <item><c>()</c> (grouping)</item>
/// </list>
/// <para>
/// Includes a fast path: if the expression is a simple variable path (no operators),
/// the full parser is bypassed and a <see cref="PathExpression"/> is returned directly.
/// </para>
/// <para>
/// Parsed AST nodes are cached in a thread-safe <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// keyed by expression string. All AST nodes are immutable sealed records, making caching safe.
/// The cache is capped at <see cref="MaxCacheSize"/> entries to prevent unbounded memory growth.
/// </para>
/// </remarks>
public sealed partial class InlineExpressionParser
{
    /// <summary>
    /// Maximum expression nesting depth to prevent stack overflow from deeply nested expressions.
    /// </summary>
    public const int MaxExpressionDepth = 50;

    /// <summary>
    /// Maximum expression length to prevent excessive parsing time.
    /// </summary>
    public const int MaxExpressionLength = 2000;

    /// <summary>
    /// Maximum number of cached expression ASTs. When exceeded, the cache is cleared.
    /// </summary>
    internal const int MaxCacheSize = 1024;

    private static readonly ConcurrentDictionary<string, InlineExpression> Cache = new(StringComparer.Ordinal);

    [GeneratedRegex(@"^[a-zA-Z_@][a-zA-Z0-9_.\[\]]*$")]
    private static partial Regex SimplePathRegex();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9]*$")]
    private static partial Regex FilterNameRegex();

    private readonly string _input;
    private int _pos;
    private int _depth;

    private InlineExpressionParser(string input)
    {
        _input = input;
        _pos = 0;
        _depth = 0;
    }

    /// <summary>
    /// Determines whether an expression content string requires full parsing.
    /// Simple variable paths (e.g., <c>user.name</c>) do not need the Pratt parser.
    /// </summary>
    /// <param name="content">The trimmed expression content from inside <c>{{...}}</c>.</param>
    /// <returns><c>true</c> if the expression contains operators requiring full parsing; otherwise, <c>false</c>.</returns>
    public static bool NeedsFullParsing(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        // Fast check: scan for operator characters
        foreach (var c in content)
        {
            if (c is '+' or '*' or '/' or '|' or '?' or '(' or ')' or '"' or '\'' or '=' or '<' or '>' or '!')
            {
                return true;
            }

            // Minus with surrounding spaces indicates subtraction, not a path like "my-var"
            // We need to check the broader context, so we check for any minus
            // and rely on the regex for simple path detection below
        }

        // Check if it's a simple path (no operators)
        // Minus in paths is not an operator: "my-var" is a valid path
        if (content.Contains('-'))
        {
            // If it contains spaces around the minus, it's subtraction
            if (content.Contains(" - ", StringComparison.Ordinal))
            {
                return true;
            }

            // Leading minus is unary negation when followed by space, digit, letter, or underscore
            if (content.StartsWith('-') && content.Length > 1 && (content[1] == ' ' || char.IsDigit(content[1]) || char.IsLetter(content[1]) || content[1] == '_'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Parses an expression string into an <see cref="InlineExpression"/> AST node.
    /// Results are cached for repeated calls with the same expression string.
    /// </summary>
    /// <param name="content">The trimmed expression content from inside <c>{{...}}</c>.</param>
    /// <returns>The parsed expression AST.</returns>
    /// <exception cref="TemplateEngineException">Thrown when the expression is malformed or exceeds resource limits.</exception>
    public static InlineExpression Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new TemplateEngineException("Empty expression", expression: content);
        }

        if (content.Length > MaxExpressionLength)
        {
            throw new TemplateEngineException(
                $"Expression length ({content.Length}) exceeds maximum ({MaxExpressionLength})",
                expression: content.Length > 100 ? content[..100] + "..." : content);
        }

        // Fast path: simple variable path (no operators, no cache needed)
        if (!NeedsFullParsing(content) && SimplePathRegex().IsMatch(content))
        {
            return new PathExpression(content);
        }

        // Cache lookup for complex expressions
        if (Cache.TryGetValue(content, out var cached))
        {
            return cached;
        }

        var result = ParseCore(content);

        // Evict all entries when cache is full to prevent unbounded growth
        if (Cache.Count >= MaxCacheSize)
        {
            Cache.Clear();
        }

        Cache.TryAdd(content, result);
        return result;
    }

    /// <summary>
    /// Clears the expression cache. Intended for testing and resource management.
    /// </summary>
    internal static void ClearCache() => Cache.Clear();

    /// <summary>
    /// Gets the current number of cached expressions. Intended for testing.
    /// </summary>
    internal static int CacheCount => Cache.Count;

    private static InlineExpression ParseCore(string content)
    {
        var parser = new InlineExpressionParser(content);
        var result = parser.ParseExpression(Precedence.None);

        parser.SkipWhitespace();
        if (parser._pos < parser._input.Length)
        {
            throw new TemplateEngineException(
                $"Unexpected character '{parser._input[parser._pos]}' at position {parser._pos}",
                position: parser._pos,
                expression: content);
        }

        return result;
    }

    private InlineExpression ParseExpression(Precedence minPrecedence)
    {
        _depth++;
        if (_depth > MaxExpressionDepth)
        {
            throw new TemplateEngineException(
                $"Expression nesting depth exceeds maximum ({MaxExpressionDepth})",
                expression: _input);
        }

        try
        {
            var left = ParsePrefix();

            while (true)
            {
                SkipWhitespace();
                if (_pos >= _input.Length)
                {
                    break;
                }

                var (precedence, isRightAssociative) = GetInfixPrecedence();
                if (precedence == Precedence.None || precedence < minPrecedence ||
                    (!isRightAssociative && precedence == minPrecedence))
                {
                    break;
                }

                left = ParseInfix(left, precedence);
            }

            return left;
        }
        finally
        {
            _depth--;
        }
    }

    private InlineExpression ParsePrefix()
    {
        SkipWhitespace();

        if (_pos >= _input.Length)
        {
            throw new TemplateEngineException("Unexpected end of expression", expression: _input);
        }

        var c = _input[_pos];

        // Parenthesized grouping
        if (c == '(')
        {
            _pos++;
            var expr = ParseExpression(Precedence.None);
            SkipWhitespace();
            if (_pos >= _input.Length || _input[_pos] != ')')
            {
                throw new TemplateEngineException(
                    "Missing closing parenthesis",
                    position: _pos,
                    expression: _input);
            }
            _pos++;
            return expr;
        }

        // Unary negation
        if (c == '-')
        {
            _pos++;
            var operand = ParseExpression(Precedence.Unary);
            return new NegateExpression(operand);
        }

        // Logical NOT
        if (c == '!')
        {
            _pos++;
            var operand = ParseExpression(Precedence.Unary);
            return new NotExpression(operand);
        }

        // String literal (double or single quotes)
        if (c is '"' or '\'')
        {
            return ParseStringLiteral();
        }

        // Number literal
        if (char.IsDigit(c))
        {
            return ParseNumberLiteral();
        }

        // Path expression (variable reference)
        if (char.IsLetter(c) || c == '_' || c == '@')
        {
            return ParsePath();
        }

        throw new TemplateEngineException(
            $"Unexpected character '{c}'",
            position: _pos,
            expression: _input);
    }

    private InlineExpression ParseInfix(InlineExpression left, Precedence precedence)
    {
        SkipWhitespace();
        var c = _input[_pos];

        return c switch
        {
            '|' when !IsDoubleChar('|') => ParseFilter(left),
            '?' when IsDoubleChar('?') => ParseCoalesce(left),
            '=' when IsDoubleChar('=') => ParseComparison(left, ComparisonOperator.Equal, 2),
            '!' when _pos + 1 < _input.Length && _input[_pos + 1] == '=' => ParseComparison(left, ComparisonOperator.NotEqual, 2),
            '<' when _pos + 1 < _input.Length && _input[_pos + 1] == '=' => ParseComparison(left, ComparisonOperator.LessThanOrEqual, 2),
            '>' when _pos + 1 < _input.Length && _input[_pos + 1] == '=' => ParseComparison(left, ComparisonOperator.GreaterThanOrEqual, 2),
            '<' => ParseComparison(left, ComparisonOperator.LessThan, 1),
            '>' => ParseComparison(left, ComparisonOperator.GreaterThan, 1),
            '+' => ParseArithmetic(left, ArithmeticOperator.Add),
            '-' => ParseArithmetic(left, ArithmeticOperator.Subtract),
            '*' => ParseArithmetic(left, ArithmeticOperator.Multiply),
            '/' => ParseArithmetic(left, ArithmeticOperator.Divide),
            _ => throw new TemplateEngineException(
                $"Unexpected operator '{c}'",
                position: _pos,
                expression: _input)
        };
    }

    private ArithmeticExpression ParseArithmetic(InlineExpression left, ArithmeticOperator op)
    {
        _pos++; // skip operator
        var right = ParseExpression(op switch
        {
            ArithmeticOperator.Add => Precedence.Additive,
            ArithmeticOperator.Subtract => Precedence.Additive,
            ArithmeticOperator.Multiply => Precedence.Multiplicative,
            ArithmeticOperator.Divide => Precedence.Multiplicative,
            _ => Precedence.Additive
        });
        return new ArithmeticExpression(left, op, right);
    }

    private ComparisonExpression ParseComparison(InlineExpression left, ComparisonOperator op, int operatorLength)
    {
        _pos += operatorLength; // skip operator chars
        var right = ParseExpression(Precedence.Comparison);
        return new ComparisonExpression(left, op, right);
    }

    private CoalesceExpression ParseCoalesce(InlineExpression left)
    {
        _pos += 2; // skip ??
        var right = ParseExpression(Precedence.Coalesce);
        return new CoalesceExpression(left, right);
    }

    private InlineExpression ParseFilter(InlineExpression input)
    {
        _pos++; // skip |
        SkipWhitespace();

        var filterName = ReadFilterName();
        string? argument = null;

        if (_pos < _input.Length && _input[_pos] == ':')
        {
            _pos++; // skip :
            argument = ReadFilterArgument();
        }

        var filterExpr = new FilterExpression(input, filterName, argument);

        // Allow chaining: {{name | trim | upper}}
        SkipWhitespace();
        if (_pos < _input.Length && _input[_pos] == '|' && !IsDoubleChar('|'))
        {
            return ParseFilter(filterExpr);
        }

        return filterExpr;
    }

    private string ReadFilterName()
    {
        var start = _pos;
        while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_'))
        {
            _pos++;
        }

        if (_pos == start)
        {
            throw new TemplateEngineException(
                "Expected filter name after '|'",
                position: _pos,
                expression: _input);
        }

        var name = _input[start.._pos];

        if (!FilterNameRegex().IsMatch(name))
        {
            throw new TemplateEngineException(
                $"Invalid filter name '{name}'. Filter names must be alphanumeric.",
                position: start,
                expression: _input);
        }

        return name;
    }

    private string ReadFilterArgument()
    {
        SkipWhitespace();

        if (_pos >= _input.Length)
        {
            throw new TemplateEngineException(
                "Expected filter argument after ':'",
                position: _pos,
                expression: _input);
        }

        // Quoted string argument (double or single quotes)
        if (_input[_pos] is '"' or '\'')
        {
            var quoteChar = _input[_pos];
            _pos++; // skip opening quote
            var start = _pos;
            var hasEscape = false;

            while (_pos < _input.Length)
            {
                var c = _input[_pos];
                if (c == '\\' && _pos + 1 < _input.Length)
                {
                    hasEscape = true;
                    _pos += 2;
                }
                else if (c == quoteChar)
                {
                    break;
                }
                else
                {
                    _pos++;
                }
            }

            if (_pos >= _input.Length)
            {
                throw new TemplateEngineException(
                    "Unterminated string in filter argument",
                    position: start,
                    expression: _input);
            }

            var value = _input[start.._pos];
            _pos++; // skip closing quote
            return hasEscape ? UnescapeString(value) : value;
        }

        // Unquoted argument (number or identifier)
        var argStart = _pos;
        while (_pos < _input.Length && _input[_pos] != '|' && _input[_pos] != '}' && !char.IsWhiteSpace(_input[_pos]))
        {
            _pos++;
        }

        if (_pos == argStart)
        {
            throw new TemplateEngineException(
                "Expected filter argument after ':'",
                position: _pos,
                expression: _input);
        }

        return _input[argStart.._pos];
    }

    private StringLiteral ParseStringLiteral()
    {
        var quoteChar = _input[_pos];
        _pos++; // skip opening quote
        var start = _pos;
        var hasEscape = false;

        while (_pos < _input.Length)
        {
            var c = _input[_pos];
            if (c == '\\' && _pos + 1 < _input.Length)
            {
                hasEscape = true;
                _pos += 2; // skip escape sequence
            }
            else if (c == quoteChar)
            {
                break;
            }
            else
            {
                _pos++;
            }
        }

        if (_pos >= _input.Length)
        {
            throw new TemplateEngineException(
                "Unterminated string literal",
                position: start,
                expression: _input);
        }

        var raw = _input[start.._pos];
        _pos++; // skip closing quote

        return new StringLiteral(hasEscape ? UnescapeString(raw) : raw);
    }

    private static string UnescapeString(string raw)
    {
        var sb = new System.Text.StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length)
            {
                i++;
                sb.Append(raw[i] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '\\' => '\\',
                    '\'' => '\'',
                    '"' => '"',
                    _ => raw[i]
                });
            }
            else
            {
                sb.Append(raw[i]);
            }
        }

        return sb.ToString();
    }

    private NumberLiteral ParseNumberLiteral()
    {
        var start = _pos;

        while (_pos < _input.Length && (char.IsDigit(_input[_pos]) || _input[_pos] == '.'))
        {
            _pos++;
        }

        var text = _input[start.._pos];

        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            throw new TemplateEngineException(
                $"Invalid number literal '{text}'",
                position: start,
                expression: _input);
        }

        return new NumberLiteral(value);
    }

    private PathExpression ParsePath()
    {
        var start = _pos;

        while (_pos < _input.Length)
        {
            var c = _input[_pos];

            if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '[' || c == ']' || c == '@')
            {
                _pos++;
                continue;
            }

            // Allow hyphens in path segments ONLY when not followed by whitespace
            // "my-var" -> path, "a - b" -> subtraction
            if (c == '-' && _pos + 1 < _input.Length && !char.IsWhiteSpace(_input[_pos + 1]))
            {
                // Also check that the preceding char is not whitespace (already consumed)
                if (_pos > start && !char.IsWhiteSpace(_input[_pos - 1]))
                {
                    _pos++;
                    continue;
                }
            }

            break;
        }

        return new PathExpression(_input[start.._pos]);
    }

    private (Precedence precedence, bool isRightAssociative) GetInfixPrecedence()
    {
        if (_pos >= _input.Length)
        {
            return (Precedence.None, false);
        }

        var c = _input[_pos];

        return c switch
        {
            '|' when !IsDoubleChar('|') => (Precedence.Filter, false),
            '?' when IsDoubleChar('?') => (Precedence.Coalesce, true),
            '=' when IsDoubleChar('=') => (Precedence.Comparison, false),
            '!' when _pos + 1 < _input.Length && _input[_pos + 1] == '=' => (Precedence.Comparison, false),
            '<' => (Precedence.Comparison, false),
            '>' => (Precedence.Comparison, false),
            '+' or '-' => (Precedence.Additive, false),
            '*' or '/' => (Precedence.Multiplicative, false),
            _ => (Precedence.None, false)
        };
    }

    private bool IsDoubleChar(char c)
    {
        return _pos + 1 < _input.Length && _input[_pos] == c && _input[_pos + 1] == c;
    }

    private void SkipWhitespace()
    {
        while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
        {
            _pos++;
        }
    }

    private enum Precedence
    {
        None = 0,
        Filter = 1,
        Coalesce = 2,
        Comparison = 3,
        Additive = 4,
        Multiplicative = 5,
        Unary = 6
    }
}
