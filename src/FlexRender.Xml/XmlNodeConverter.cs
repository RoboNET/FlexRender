using System.Xml.Linq;
using FlexRender.Parsing;
using FlexRender.Parsing.Nodes;

namespace FlexRender.Xml;

/// <summary>
/// Converts a FlexRender XML template tree into the format-neutral
/// <see cref="TemplateMapping"/> document root that the shared
/// <see cref="TemplateEngine"/> consumes, so all element/chart/shape parsing and
/// validation is reused without duplication and without any YAML dependency.
/// </summary>
internal static class XmlNodeConverter
{
    private const string RootName = "flexrender";

    private static readonly HashSet<string> WrapperNames = new(StringComparer.Ordinal)
    {
        "then", "else", "else-if", "columns", "rows",
        "categories", "x-labels", "y-labels", "palette", "shapes"
    };

    private static readonly HashSet<string> ListAttributes = new(StringComparer.Ordinal)
    {
        "points", "categories", "palette", "x-labels", "y-labels"
    };

    /// <summary>Parses the XML string and builds the neutral document-root mapping.</summary>
    /// <param name="xml">The raw XML template.</param>
    /// <returns>The equivalent neutral document root mapping.</returns>
    /// <exception cref="TemplateParseException">Thrown when the XML is malformed or the root element is wrong.</exception>
    internal static TemplateMapping Convert(string xml)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new TemplateParseException($"Invalid XML: {ex.Message}", ex);
        }

        var rootEl = doc.Root
            ?? throw new TemplateParseException("XML template has no root element.");

        if (!string.Equals(rootEl.Name.LocalName, RootName, StringComparison.Ordinal))
        {
            throw new TemplateParseException(
                $"XML template root must be <{RootName}>, but was <{rootEl.Name.LocalName}>.");
        }

        var root = new TemplateMapping();

        var metadata = new TemplateMapping();
        AddAttrIfPresent(rootEl, "name", metadata);
        AddAttrIfPresent(rootEl, "version", metadata);
        AddAttrIfPresent(rootEl, "culture", metadata);
        if (metadata.Keys.Count > 0)
            root.Add("template", metadata);

        var layout = new TemplateSequence();

        foreach (var child in rootEl.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "canvas":
                    root.Add("canvas", AttributesToMapping(child));
                    break;
                case "fonts":
                    root.Add("fonts", ConvertFonts(child));
                    break;
                default:
                    layout.Add(ConvertElement(child));
                    break;
            }
        }

        root.Add("layout", layout);
        return root;
    }

    private static TemplateMapping AttributesToMapping(XElement el)
    {
        var node = new TemplateMapping();
        foreach (var attr in el.Attributes())
            node.Add(attr.Name.LocalName, new TemplateScalar(attr.Value));
        return node;
    }

    private static TemplateMapping ConvertElement(XElement el)
    {
        var type = el.Name.LocalName;
        var node = new TemplateMapping();
        node.Add("type", new TemplateScalar(type));

        foreach (var attr in el.Attributes())
        {
            var name = attr.Name.LocalName;
            node.Add(name, ListAttributes.Contains(name)
                ? ExpandListAttribute(attr.Value)
                : new TemplateScalar(attr.Value));
        }

        if (el.Attribute("content") is null && !el.HasElements)
        {
            var inner = el.Value;
            if (!string.IsNullOrWhiteSpace(inner))
                node.Add("content", new TemplateScalar(inner.Trim()));
        }

        var naturalList = new TemplateSequence();
        var seriesList = new TemplateSequence();
        foreach (var child in el.Elements())
        {
            var childName = child.Name.LocalName;
            if (string.Equals(childName, "series", StringComparison.Ordinal))
                seriesList.Add(ConvertSeries(child));
            else if (WrapperNames.Contains(childName))
                AddWrapper(node, child);
            else
                naturalList.Add(ConvertElement(child));
        }

        if (seriesList.Items.Count > 0)
            node.Add("series", seriesList);

        if (naturalList.Items.Count > 0)
            node.Add("children", naturalList);

        return node;
    }

    private static void AddWrapper(TemplateMapping node, XElement wrapper)
    {
        var name = wrapper.Name.LocalName;
        switch (name)
        {
            case "then":
            case "else":
                node.Add(name, ConvertElementSequence(wrapper));
                break;
            case "else-if":
                var children = wrapper.Elements().ToList();
                if (children.Count != 1
                    || !string.Equals(children[0].Name.LocalName, "if", StringComparison.Ordinal))
                {
                    throw new TemplateParseException(
                        "An <else-if> must contain exactly one <if> child element.");
                }
                node.Add("elseIf", ConvertElement(children[0]));
                break;
            case "columns":
                node.Add("columns", ConvertAttributeItemSequence(wrapper));
                break;
            case "rows":
                node.Add("rows", ConvertAttributeItemSequence(wrapper));
                break;
            case "categories":
            case "x-labels":
            case "y-labels":
                node.Add(name, ConvertScalarItemSequence(wrapper));
                break;
            case "palette":
                node.Add("palette", ConvertScalarItemSequence(wrapper));
                break;
            case "shapes":
                node.Add("shapes", ConvertShapeSequence(wrapper));
                break;
        }
    }

    private static TemplateMapping ConvertSeries(XElement series)
    {
        var node = new TemplateMapping();
        foreach (var attr in series.Attributes())
        {
            var name = attr.Name.LocalName;
            switch (name)
            {
                case "data":
                case "points":
                    node.Add("data", ExpandListAttribute(attr.Value));
                    break;
                default:
                    node.Add(name, new TemplateScalar(attr.Value));
                    break;
            }
        }
        return node;
    }

    private static TemplateSequence ConvertElementSequence(XElement wrapper)
    {
        var seq = new TemplateSequence();
        foreach (var child in wrapper.Elements())
            seq.Add(ConvertElement(child));
        return seq;
    }

    private static TemplateSequence ConvertAttributeItemSequence(XElement wrapper)
    {
        var seq = new TemplateSequence();
        foreach (var child in wrapper.Elements())
            seq.Add(AttributesToMapping(child));
        return seq;
    }

    private static TemplateSequence ConvertScalarItemSequence(XElement wrapper)
    {
        var seq = new TemplateSequence();
        foreach (var child in wrapper.Elements())
            seq.Add(new TemplateScalar(child.Value.Trim()));
        return seq;
    }

    private static TemplateSequence ConvertShapeSequence(XElement wrapper)
    {
        var seq = new TemplateSequence();
        foreach (var shape in wrapper.Elements())
        {
            var shapeMapping = new TemplateMapping();
            foreach (var attr in shape.Attributes())
            {
                var name = attr.Name.LocalName;
                shapeMapping.Add(name, ListAttributes.Contains(name)
                    ? ExpandListAttribute(attr.Value)
                    : new TemplateScalar(attr.Value));
            }

            var wrapped = new TemplateMapping();
            wrapped.Add(shape.Name.LocalName, shapeMapping);
            seq.Add(wrapped);
        }
        return seq;
    }

    private static TemplateSequence ConvertFonts(XElement fonts)
    {
        var seq = new TemplateSequence();
        foreach (var font in fonts.Elements())
            seq.Add(AttributesToMapping(font));
        return seq;
    }

    private static TemplateNode ExpandListAttribute(string value)
    {
        if (value.Contains("{{", StringComparison.Ordinal))
            return new TemplateScalar(value);

        if (value.Contains(';', StringComparison.Ordinal))
        {
            var outer = new TemplateSequence();
            foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var inner = new TemplateSequence();
                foreach (var comp in part.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    inner.Add(new TemplateScalar(comp));
                outer.Add(inner);
            }
            return outer;
        }

        var seq = new TemplateSequence();
        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            seq.Add(new TemplateScalar(item));
        return seq;
    }

    private static void AddAttrIfPresent(XElement el, string name, TemplateMapping node)
    {
        var attr = el.Attribute(name);
        if (attr is not null && !string.IsNullOrEmpty(attr.Value))
            node.Add(name, new TemplateScalar(attr.Value));
    }
}
