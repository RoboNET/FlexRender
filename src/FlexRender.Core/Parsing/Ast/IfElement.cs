namespace FlexRender.Parsing.Ast;

/// <summary>
/// An AST element that conditionally renders children based on a condition.
/// Supports truthy checks, equality comparisons, and else-if chains.
/// </summary>
public sealed class IfElement : TemplateElement
{
    /// <summary>
    /// Gets the element type.
    /// </summary>
    public override ElementType Type => ElementType.If;

    /// <summary>
    /// Gets or sets the path to the value used for condition evaluation.
    /// Example: "isPremium" or "order.status".
    /// </summary>
    public string ConditionPath { get; set; } = "";

    /// <summary>
    /// Gets or sets the comparison operator.
    /// When null, performs a truthy check on the value.
    /// </summary>
    public ConditionOperator? Operator { get; set; }

    /// <summary>
    /// Gets or sets the value to compare against.
    /// Supports different types based on the operator:
    /// - Equals/NotEquals: string, double, bool, or null
    /// - In/NotIn: IReadOnlyList&lt;string&gt;
    /// - Contains: string
    /// - GreaterThan/GreaterThanOrEqual/LessThan/LessThanOrEqual: double
    /// - HasItems: bool
    /// - CountEquals/CountGreaterThan: int
    /// </summary>
    public object? CompareValue { get; set; }

    /// <summary>
    /// Gets the elements to render when the condition is true.
    /// </summary>
    public IReadOnlyList<TemplateElement> ThenBranch { get; }

    /// <summary>
    /// Gets or sets the nested else-if element.
    /// Evaluated when the main condition is false.
    /// </summary>
    public IfElement? ElseIf { get; set; }

    /// <summary>
    /// Gets the elements to render when all conditions are false.
    /// </summary>
    public IReadOnlyList<TemplateElement> ElseBranch { get; }

    /// <summary>
    /// Creates a new IfElement with the specified branches.
    /// </summary>
    /// <param name="thenBranch">Elements to render when condition is true.</param>
    /// <param name="elseBranch">Elements to render when all conditions are false.</param>
    /// <exception cref="ArgumentNullException">Thrown when thenBranch is null.</exception>
    public IfElement(IReadOnlyList<TemplateElement> thenBranch, IReadOnlyList<TemplateElement>? elseBranch = null)
    {
        ThenBranch = thenBranch ?? throw new ArgumentNullException(nameof(thenBranch));
        ElseBranch = elseBranch ?? Array.Empty<TemplateElement>();
    }
}
