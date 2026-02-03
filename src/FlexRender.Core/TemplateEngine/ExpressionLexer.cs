namespace FlexRender.TemplateEngine;

/// <summary>
/// Tokenizes template strings into a sequence of expression tokens.
/// </summary>
/// <remarks>
/// <para>
/// Implementation note: This lexer uses string slicing which allocates substrings.
/// For most template processing scenarios, this is acceptable because:
/// </para>
/// <list type="bullet">
///   <item>Templates are typically processed once and the tokens are reused</item>
///   <item>The token records need to store the string values anyway</item>
///   <item>A span-based approach would require significant API changes</item>
/// </list>
/// <para>
/// For high-throughput scenarios, use the <see cref="Tokenize(string, List{ExpressionToken})"/>
/// overload with a reusable list to avoid per-call list allocations.
/// </para>
/// </remarks>
public sealed class ExpressionLexer
{
    private const string OpenTag = "{{";
    private const string CloseTag = "}}";

    /// <summary>
    /// Tokenizes the input template string into a new list.
    /// </summary>
    /// <param name="input">The template string to tokenize.</param>
    /// <returns>A list of tokens.</returns>
    /// <exception cref="TemplateEngineException">Thrown when the input contains invalid syntax.</exception>
    /// <remarks>
    /// This method allocates a new list on each call. For high-throughput scenarios,
    /// use <see cref="Tokenize(string, List{ExpressionToken})"/> with a reusable list.
    /// </remarks>
    public static List<ExpressionToken> Tokenize(string input)
    {
        var tokens = new List<ExpressionToken>();
        Tokenize(input, tokens);
        return tokens;
    }

    /// <summary>
    /// Tokenizes the input template string into the provided list.
    /// </summary>
    /// <param name="input">The template string to tokenize.</param>
    /// <param name="tokens">The list to populate with tokens. The list is cleared before tokenization.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tokens"/> is null.</exception>
    /// <exception cref="TemplateEngineException">Thrown when the input contains invalid syntax.</exception>
    /// <remarks>
    /// This overload allows reusing a list across multiple tokenization calls to reduce GC pressure.
    /// The list is cleared at the start of each call.
    /// </remarks>
    public static void Tokenize(string input, List<ExpressionToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        tokens.Clear();

        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        var position = 0;

        while (position < input.Length)
        {
            var openIndex = input.IndexOf(OpenTag, position, StringComparison.Ordinal);

            if (openIndex == -1)
            {
                // Rest is plain text
                tokens.Add(new TextToken(input[position..]));
                return;
            }

            // Text before the expression
            if (openIndex > position)
            {
                tokens.Add(new TextToken(input[position..openIndex]));
            }

            // Find closing tag
            var closeIndex = input.IndexOf(CloseTag, openIndex + OpenTag.Length, StringComparison.Ordinal);
            if (closeIndex == -1)
            {
                throw new TemplateEngineException("Unclosed expression", position: openIndex);
            }

            // Extract expression content
            var expressionContent = input[(openIndex + OpenTag.Length)..closeIndex].Trim();

            // Parse the expression type
            tokens.Add(ParseExpression(expressionContent));

            position = closeIndex + CloseTag.Length;
        }
    }

    private static ExpressionToken ParseExpression(string content)
    {
        if (content.StartsWith("#if ", StringComparison.Ordinal))
        {
            return new IfStartToken(content[4..].Trim());
        }

        if (content.StartsWith("#each ", StringComparison.Ordinal))
        {
            return new EachStartToken(content[6..].Trim());
        }

        if (content == "else")
        {
            return new ElseToken();
        }

        if (content == "/if")
        {
            return new IfEndToken();
        }

        if (content == "/each")
        {
            return new EachEndToken();
        }

        // Default: variable substitution
        return new VariableToken(content);
    }
}
