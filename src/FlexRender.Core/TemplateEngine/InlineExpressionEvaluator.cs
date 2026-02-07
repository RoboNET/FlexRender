using System.Globalization;

namespace FlexRender.TemplateEngine;

/// <summary>
/// Evaluates parsed <see cref="InlineExpression"/> AST nodes against a template context.
/// </summary>
/// <remarks>
/// <para>Evaluation rules:</para>
/// <list type="bullet">
///   <item>Arithmetic on <see cref="NumberValue"/> produces <see cref="NumberValue"/>.</item>
///   <item>Any operand being <see cref="NullValue"/> in arithmetic produces <see cref="NullValue"/>.</item>
///   <item>String + number (mixed types) produces <see cref="NullValue"/> (no implicit coercion).</item>
///   <item>Division by zero produces <see cref="NullValue"/> (no exception).</item>
///   <item>Null coalesce: returns right if left is <see cref="NullValue"/>.</item>
/// </list>
/// </remarks>
public sealed class InlineExpressionEvaluator
{
    private readonly FilterRegistry? _filterRegistry;
    private readonly CultureInfo _culture;

    /// <summary>
    /// Initializes a new instance of the <see cref="InlineExpressionEvaluator"/> class without filter support.
    /// Uses <see cref="CultureInfo.InvariantCulture"/> for formatting.
    /// </summary>
    public InlineExpressionEvaluator()
    {
        _culture = CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InlineExpressionEvaluator"/> class with filter support.
    /// Uses <see cref="CultureInfo.InvariantCulture"/> for formatting.
    /// </summary>
    /// <param name="filterRegistry">The filter registry for resolving filter names.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filterRegistry"/> is null.</exception>
    public InlineExpressionEvaluator(FilterRegistry filterRegistry)
        : this(filterRegistry, CultureInfo.InvariantCulture)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InlineExpressionEvaluator"/> class with filter support and culture.
    /// </summary>
    /// <param name="filterRegistry">The filter registry for resolving filter names.</param>
    /// <param name="culture">The culture to use for formatting operations in filters.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filterRegistry"/> or <paramref name="culture"/> is null.</exception>
    public InlineExpressionEvaluator(FilterRegistry filterRegistry, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(filterRegistry);
        ArgumentNullException.ThrowIfNull(culture);
        _filterRegistry = filterRegistry;
        _culture = culture;
    }

    /// <summary>
    /// Evaluates an expression AST node against the given template context.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="context">The template context providing variable values.</param>
    /// <returns>The result of evaluating the expression.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="TemplateEngineException">Thrown when an unknown filter is referenced.</exception>
    public TemplateValue Evaluate(InlineExpression expression, TemplateContext context)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(context);

        return expression switch
        {
            PathExpression path => ExpressionEvaluator.Resolve(path.Path, context),
            NumberLiteral num => new NumberValue(num.Value),
            StringLiteral str => new StringValue(str.Value),
            ArithmeticExpression arith => EvaluateArithmetic(arith, context),
            CoalesceExpression coal => EvaluateCoalesce(coal, context),
            FilterExpression filter => EvaluateFilter(filter, context),
            NegateExpression neg => EvaluateNegate(neg, context),
            _ => NullValue.Instance
        };
    }

    private TemplateValue EvaluateArithmetic(ArithmeticExpression expr, TemplateContext context)
    {
        var left = Evaluate(expr.Left, context);
        var right = Evaluate(expr.Right, context);

        // Both operands must be numbers
        if (left is not NumberValue leftNum || right is not NumberValue rightNum)
        {
            return NullValue.Instance;
        }

        return expr.Op switch
        {
            ArithmeticOperator.Add => new NumberValue(leftNum.Value + rightNum.Value),
            ArithmeticOperator.Subtract => new NumberValue(leftNum.Value - rightNum.Value),
            ArithmeticOperator.Multiply => new NumberValue(leftNum.Value * rightNum.Value),
            ArithmeticOperator.Divide => rightNum.Value == 0
                ? NullValue.Instance
                : new NumberValue(leftNum.Value / rightNum.Value),
            _ => NullValue.Instance
        };
    }

    private TemplateValue EvaluateCoalesce(CoalesceExpression expr, TemplateContext context)
    {
        var left = Evaluate(expr.Left, context);
        return left is NullValue ? Evaluate(expr.Right, context) : left;
    }

    private TemplateValue EvaluateFilter(FilterExpression expr, TemplateContext context)
    {
        if (_filterRegistry is null)
        {
            throw new TemplateEngineException(
                $"Filter '{expr.FilterName}' is not available. No filter registry has been configured.");
        }

        var input = Evaluate(expr.Input, context);

        var filter = _filterRegistry.Get(expr.FilterName)
            ?? throw new TemplateEngineException($"Unknown filter '{expr.FilterName}'");

        TemplateValue? argument = expr.Argument is not null
            ? new StringValue(expr.Argument)
            : null;

        return filter.Apply(input, argument, _culture);
    }

    private TemplateValue EvaluateNegate(NegateExpression expr, TemplateContext context)
    {
        var operand = Evaluate(expr.Operand, context);

        if (operand is NumberValue num)
        {
            return new NumberValue(-num.Value);
        }

        return NullValue.Instance;
    }
}
