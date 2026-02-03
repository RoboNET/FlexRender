using System.Text;
using FlexRender.Configuration;

namespace FlexRender.TemplateEngine;

/// <summary>
/// Processes template strings by evaluating expressions and substituting values.
/// </summary>
public sealed class TemplateProcessor
{
    /// <summary>
    /// Maximum allowed nesting depth for control flow blocks to prevent stack overflow.
    /// </summary>
    /// <remarks>
    /// This constant is preserved for backward compatibility. The actual limit used
    /// at runtime comes from <see cref="ResourceLimits.MaxTemplateNestingDepth"/>.
    /// </remarks>
    public const int MaxNestingDepth = 100;

    private readonly ExpressionLexer _lexer = new();
    private readonly ExpressionEvaluator _evaluator = new();
    private readonly ResourceLimits _limits;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateProcessor"/> class with default resource limits.
    /// </summary>
    public TemplateProcessor() : this(new ResourceLimits())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateProcessor"/> class with custom resource limits.
    /// </summary>
    /// <param name="limits">The resource limits to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
    public TemplateProcessor(ResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
    }

    /// <summary>
    /// Processes a template string with the given data.
    /// </summary>
    /// <param name="template">The template string containing expressions.</param>
    /// <param name="data">The data to use for substitution.</param>
    /// <returns>The processed string with all expressions evaluated.</returns>
    public string Process(string template, ObjectValue data)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        var context = new TemplateContext(data);
        var tokens = _lexer.Tokenize(template).ToList();

        return ProcessTokens(tokens, context, currentDepth: 0);
    }

    private string ProcessTokens(List<ExpressionToken> tokens, TemplateContext context, int currentDepth)
    {
        var result = new StringBuilder();
        var index = 0;

        while (index < tokens.Count)
        {
            var token = tokens[index];

            switch (token)
            {
                case TextToken text:
                    result.Append(text.Value);
                    index++;
                    break;

                case VariableToken variable:
                    var value = _evaluator.Resolve(variable.Path, context);
                    result.Append(ValueToString(value));
                    index++;
                    break;

                case IfStartToken ifStart:
                    index = ProcessIfBlock(tokens, index, context, result, currentDepth);
                    break;

                case EachStartToken eachStart:
                    index = ProcessEachBlock(tokens, index, context, result, currentDepth);
                    break;

                default:
                    index++;
                    break;
            }
        }

        return result.ToString();
    }

    private int ProcessIfBlock(List<ExpressionToken> tokens, int startIndex, TemplateContext context, StringBuilder result, int currentDepth)
    {
        var newDepth = currentDepth + 1;
        if (newDepth > _limits.MaxTemplateNestingDepth)
        {
            throw new TemplateEngineException(
                $"Maximum nesting depth ({_limits.MaxTemplateNestingDepth}) exceeded. Templates cannot be nested more than {_limits.MaxTemplateNestingDepth} levels deep.");
        }

        var ifStart = (IfStartToken)tokens[startIndex];
        var conditionValue = _evaluator.Resolve(ifStart.Condition, context);
        var condition = ExpressionEvaluator.IsTruthy(conditionValue);

        var (thenTokens, elseTokens, endIndex) = ExtractIfBlockParts(tokens, startIndex);

        if (condition)
        {
            result.Append(ProcessTokens(thenTokens, context, newDepth));
        }
        else if (elseTokens.Count > 0)
        {
            result.Append(ProcessTokens(elseTokens, context, newDepth));
        }

        return endIndex + 1;
    }

    private (List<ExpressionToken> thenTokens, List<ExpressionToken> elseTokens, int endIndex) ExtractIfBlockParts(
        List<ExpressionToken> tokens, int startIndex)
    {
        var thenTokens = new List<ExpressionToken>();
        var elseTokens = new List<ExpressionToken>();
        var currentList = thenTokens;
        var depth = 1;
        var index = startIndex + 1;

        while (index < tokens.Count && depth > 0)
        {
            var token = tokens[index];

            switch (token)
            {
                case IfStartToken:
                    depth++;
                    currentList.Add(token);
                    break;

                case IfEndToken when depth == 1:
                    depth = 0;
                    break;

                case IfEndToken:
                    depth--;
                    currentList.Add(token);
                    break;

                case ElseToken when depth == 1:
                    currentList = elseTokens;
                    break;

                default:
                    currentList.Add(token);
                    break;
            }

            if (depth > 0)
            {
                index++;
            }
        }

        // Validate that the block was properly closed
        if (depth > 0)
        {
            throw new TemplateEngineException("Unclosed {{#if}} block detected. Missing {{/if}} tag.");
        }

        return (thenTokens, elseTokens, index);
    }

    private int ProcessEachBlock(List<ExpressionToken> tokens, int startIndex, TemplateContext context, StringBuilder result, int currentDepth)
    {
        var newDepth = currentDepth + 1;
        if (newDepth > _limits.MaxTemplateNestingDepth)
        {
            throw new TemplateEngineException(
                $"Maximum nesting depth ({_limits.MaxTemplateNestingDepth}) exceeded. Templates cannot be nested more than {_limits.MaxTemplateNestingDepth} levels deep.");
        }

        var eachStart = (EachStartToken)tokens[startIndex];
        var arrayValue = _evaluator.Resolve(eachStart.ArrayPath, context);

        var (bodyTokens, endIndex) = ExtractEachBlockBody(tokens, startIndex);

        if (arrayValue is ArrayValue array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                context.PushScope(array[i]);
                context.SetLoopVariables(i, array.Count);

                result.Append(ProcessTokens(bodyTokens.ToList(), context, newDepth));

                context.ClearLoopVariables();
                context.PopScope();
            }
        }

        return endIndex + 1;
    }

    private (List<ExpressionToken> bodyTokens, int endIndex) ExtractEachBlockBody(
        List<ExpressionToken> tokens, int startIndex)
    {
        var bodyTokens = new List<ExpressionToken>();
        var depth = 1;
        var index = startIndex + 1;

        while (index < tokens.Count && depth > 0)
        {
            var token = tokens[index];

            switch (token)
            {
                case EachStartToken:
                    depth++;
                    bodyTokens.Add(token);
                    break;

                case EachEndToken when depth == 1:
                    depth = 0;
                    break;

                case EachEndToken:
                    depth--;
                    bodyTokens.Add(token);
                    break;

                default:
                    bodyTokens.Add(token);
                    break;
            }

            if (depth > 0)
            {
                index++;
            }
        }

        // Validate that the block was properly closed
        if (depth > 0)
        {
            throw new TemplateEngineException("Unclosed {{#each}} block detected. Missing {{/each}} tag.");
        }

        return (bodyTokens, index);
    }

    private static string ValueToString(TemplateValue value)
    {
        return value switch
        {
            NullValue => string.Empty,
            StringValue s => s.Value,
            NumberValue n => n.Value.ToString("G"),
            BoolValue b => b.Value ? "true" : "false",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Processes a parsed template, resolving all expressions.
    /// </summary>
    /// <param name="template">The parsed template.</param>
    /// <param name="data">The data to use for substitution.</param>
    /// <returns>A new template with all expressions resolved.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="template"/> or <paramref name="data"/> is null.</exception>
    public Parsing.Ast.Template ProcessTemplate(Parsing.Ast.Template template, ObjectValue data)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        var resolved = new Parsing.Ast.Template
        {
            Name = template.Name,
            Version = template.Version,
            Canvas = template.Canvas // CanvasSettings is a reference type; sharing is intentional as it's not modified
        };

        foreach (var element in template.Elements)
        {
            var processedElement = ProcessElement(element, data);
            if (processedElement != null)
            {
                resolved.AddElement(processedElement);
            }
        }

        return resolved;
    }

    private Parsing.Ast.TemplateElement? ProcessElement(Parsing.Ast.TemplateElement element, ObjectValue data)
    {
        return element switch
        {
            Parsing.Ast.TextElement text => ProcessTextElement(text, data),
            _ => element // Return other element types unchanged for now
        };
    }

    private Parsing.Ast.TextElement ProcessTextElement(Parsing.Ast.TextElement original, ObjectValue data)
    {
        return new Parsing.Ast.TextElement
        {
            Content = Process(original.Content, data),
            Font = original.Font,
            Size = original.Size,
            Color = original.Color,
            Align = original.Align,
            Wrap = original.Wrap,
            Overflow = original.Overflow,
            MaxLines = original.MaxLines,
            Rotate = original.Rotate
        };
    }
}
