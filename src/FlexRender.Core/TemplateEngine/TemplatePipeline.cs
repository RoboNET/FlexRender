using FlexRender.Parsing.Ast;

namespace FlexRender.TemplateEngine;

/// <summary>
/// Orchestrates the full template processing pipeline: Expand, Resolve, Materialize.
/// Replaces duplicated backend-specific preprocessors with a single Core implementation.
/// </summary>
public sealed class TemplatePipeline
{
    private readonly TemplateExpander _expander;
    private readonly TemplateProcessor _templateProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplatePipeline"/> class.
    /// </summary>
    /// <param name="expander">The template expander for control flow expansion (#if, #each, table).</param>
    /// <param name="templateProcessor">The template processor for expression resolution.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expander"/> or <paramref name="templateProcessor"/> is null.</exception>
    public TemplatePipeline(TemplateExpander expander, TemplateProcessor templateProcessor)
    {
        ArgumentNullException.ThrowIfNull(expander);
        ArgumentNullException.ThrowIfNull(templateProcessor);
        _expander = expander;
        _templateProcessor = templateProcessor;
    }

    /// <summary>
    /// Processes a template through the full pipeline: Expand, Resolve, Materialize.
    /// </summary>
    /// <param name="template">The parsed template to process.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    /// <returns>The expanded and resolved template with all expressions materialized.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="template"/> or <paramref name="data"/> is null.</exception>
    public Template Process(Template template, ObjectValue data)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        // Phase 1: Expand control flow (#if, #each, table)
        var expanded = _expander.Expand(template, data);

        // Phase 2: Resolve expressions in all ExprValue properties
        ResolveAll(expanded, data);

        // Phase 3: Materialize resolved strings into typed values
        MaterializeAll(expanded);

        return expanded;
    }

    /// <summary>
    /// Resolves template expressions in all <see cref="ExprValue{T}"/> properties across the template.
    /// </summary>
    /// <param name="template">The template whose properties to resolve.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    private void ResolveAll(Template template, ObjectValue data)
    {
        template.Canvas.ResolveExpressions(
            (raw, d) => _templateProcessor.Process(raw, d), data);

        foreach (var element in template.Elements)
        {
            ResolveElement(element, data);
        }
    }

    /// <summary>
    /// Resolves template expressions on a single element.
    /// </summary>
    /// <param name="element">The element to resolve.</param>
    /// <param name="data">The data context for expression evaluation.</param>
    private void ResolveElement(TemplateElement element, ObjectValue data)
    {
        element.ResolveExpressions(
            (raw, d) => _templateProcessor.Process(raw, d), data);
    }

    /// <summary>
    /// Materializes all resolved <see cref="ExprValue{T}"/> properties across the template.
    /// </summary>
    /// <param name="template">The template whose properties to materialize.</param>
    private static void MaterializeAll(Template template)
    {
        template.Canvas.Materialize();

        foreach (var element in template.Elements)
        {
            MaterializeElement(element);
        }
    }

    /// <summary>
    /// Materializes resolved properties on a single element.
    /// </summary>
    /// <param name="element">The element to materialize.</param>
    private static void MaterializeElement(TemplateElement element)
    {
        element.Materialize();
    }
}
