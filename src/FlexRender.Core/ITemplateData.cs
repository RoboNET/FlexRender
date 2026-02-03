namespace FlexRender;

/// <summary>
/// Interface for types that can be converted to template data.
/// Implement this interface on your data classes to use them with FlexRender templates.
/// In the future, a source generator will auto-implement this interface.
/// </summary>
public interface ITemplateData
{
    /// <summary>
    /// Converts this object to a <see cref="ObjectValue"/> for use in template rendering.
    /// </summary>
    /// <returns>An <see cref="ObjectValue"/> containing the template data.</returns>
    ObjectValue ToTemplateValue();
}
