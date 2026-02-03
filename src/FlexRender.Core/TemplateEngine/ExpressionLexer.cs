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
/// If profiling identifies this as a bottleneck in high-throughput scenarios,
/// consider pooling template parsing results or using a span-based tokenizer.
/// </para>
/// </remarks>
public sealed class ExpressionLexer
{
    private const string OpenTag = "{{";
    private const string CloseTag = "}}";

    /// <summary>
    /// Tokenizes the input template string.
    /// </summary>
    /// <param name="input">The template string to tokenize.</param>
    /// <returns>A sequence of tokens.</returns>
    /// <exception cref="TemplateEngineException">Thrown when the input contains invalid syntax.</exception>
    public IEnumerable<ExpressionToken> Tokenize(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            yield break;
        }

        var position = 0;

        while (position < input.Length)
        {
            var openIndex = input.IndexOf(OpenTag, position, StringComparison.Ordinal);

            if (openIndex == -1)
            {
                // Rest is plain text
                yield return new TextToken(input[position..]);
                yield break;
            }

            // Text before the expression
            if (openIndex > position)
            {
                yield return new TextToken(input[position..openIndex]);
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
            yield return ParseExpression(expressionContent);

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
