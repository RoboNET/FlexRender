using System.Xml.Linq;
using FlexRender.Parsing;
using YamlDotNet.RepresentationModel;

namespace FlexRender.Xml;

/// <summary>
/// Converts a FlexRender XML template tree into the equivalent
/// <see cref="YamlMappingNode"/> document root that the YAML
/// <see cref="TemplateParser"/> consumes, so all element/chart/shape parsing and
/// validation is reused without duplication.
/// </summary>
internal static class XmlToYamlNodeConverter
{
    /// <summary>The root element local-name.</summary>
    private const string RootName = "flexrender";

    /// <summary>
    /// Wrapper child element names that map to dedicated YAML list/branch keys rather than
    /// being treated as nested layout elements.
    /// </summary>
    private static readonly HashSet<string> WrapperNames = new(StringComparer.Ordinal)
    {
        "then", "else", "else-if", "columns", "rows", "series",
        "categories", "x-labels", "y-labels", "palette", "shapes"
    };

    /// <summary>
    /// Attribute names whose comma/semicolon values expand into YAML sequences.
    /// </summary>
    private static readonly HashSet<string> ListAttributes = new(StringComparer.Ordinal)
    {
        "data", "points", "categories", "palette", "x-labels", "y-labels"
    };

    /// <summary>
    /// Parses the XML string and builds the YAML document root mapping.
    /// </summary>
    /// <param name="xml">The raw XML template.</param>
    /// <returns>The equivalent document root mapping node.</returns>
    /// <exception cref="TemplateParseException">Thrown when the XML is malformed or the root element is wrong.</exception>
    internal static YamlMappingNode Convert(string xml)
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

        var root = new YamlMappingNode();

        // template metadata from root attributes
        var metadata = new YamlMappingNode();
        AddAttrIfPresent(rootEl, "name", metadata);
        AddAttrIfPresent(rootEl, "version", metadata);
        AddAttrIfPresent(rootEl, "culture", metadata);
        if (metadata.Children.Count > 0)
        {
            root.Add("template", metadata);
        }

        var layout = new YamlSequenceNode();

        foreach (var child in rootEl.Elements())
        {
            var localName = child.Name.LocalName;
            switch (localName)
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

    /// <summary>
    /// Builds a mapping node containing only the element's attributes (no type, no children).
    /// </summary>
    private static YamlMappingNode AttributesToMapping(XElement el)
    {
        var node = new YamlMappingNode();
        foreach (var attr in el.Attributes())
        {
            node.Add(attr.Name.LocalName, new YamlScalarNode(attr.Value));
        }
        return node;
    }

    /// <summary>
    /// Converts a single layout element (recursively) into a YAML mapping node with a
    /// <c>type</c> entry, scalar attributes, and child-derived list properties.
    /// </summary>
    private static YamlMappingNode ConvertElement(XElement el)
    {
        var type = el.Name.LocalName;
        var node = new YamlMappingNode();
        node.Add("type", new YamlScalarNode(type));

        // Attributes -> scalar or list entries.
        foreach (var attr in el.Attributes())
        {
            var name = attr.Name.LocalName;
            if (ListAttributes.Contains(name))
            {
                node.Add(name, ExpandListAttribute(attr.Value));
            }
            else
            {
                node.Add(name, new YamlScalarNode(attr.Value));
            }
        }

        // Inner text -> content (only when no content attribute and there are no child elements).
        if (el.Attribute("content") is null && !el.HasElements)
        {
            var inner = el.Value;
            if (!string.IsNullOrWhiteSpace(inner))
            {
                node.Add("content", new YamlScalarNode(inner.Trim()));
            }
        }

        // Child elements.
        var naturalList = new YamlSequenceNode();
        foreach (var child in el.Elements())
        {
            var childName = child.Name.LocalName;
            if (WrapperNames.Contains(childName))
            {
                AddWrapper(node, type, child);
            }
            else
            {
                naturalList.Add(ConvertElement(child));
            }
        }

        if (naturalList.Children.Count > 0)
        {
            node.Add(NaturalListKey(type), naturalList);
        }

        return node;
    }

    /// <summary>
    /// Maps an element type to the YAML key its directly-nested layout children belong under.
    /// </summary>
    private static string NaturalListKey(string type) => type switch
    {
        "each" => "children",
        _ => "children" // flex and any other container use 'children'
    };

    /// <summary>
    /// Adds a recognised wrapper child (then/else/columns/series/...) to the node under its YAML key.
    /// </summary>
    private static void AddWrapper(YamlMappingNode node, string parentType, XElement wrapper)
    {
        var name = wrapper.Name.LocalName;
        switch (name)
        {
            case "then":
            case "else":
                node.Add(name, ConvertElementSequence(wrapper));
                break;
            case "else-if":
                var inner = wrapper.Elements().FirstOrDefault();
                if (inner is not null)
                {
                    node.Add("elseIf", ConvertElement(inner));
                }
                break;
            case "columns":
                node.Add("columns", ConvertAttributeItemSequence(wrapper));
                break;
            case "rows":
                node.Add("rows", ConvertAttributeItemSequence(wrapper));
                break;
            case "series":
                // Handled at element level for charts; placeholder (overridden in chart task).
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
                // Handled in the draw task.
                break;
        }
    }

    /// <summary>Converts child layout elements of a wrapper into a YAML sequence of mappings.</summary>
    private static YamlSequenceNode ConvertElementSequence(XElement wrapper)
    {
        var seq = new YamlSequenceNode();
        foreach (var child in wrapper.Elements())
        {
            seq.Add(ConvertElement(child));
        }
        return seq;
    }

    /// <summary>Converts child elements whose attributes become mapping fields (table column/row).</summary>
    private static YamlSequenceNode ConvertAttributeItemSequence(XElement wrapper)
    {
        var seq = new YamlSequenceNode();
        foreach (var child in wrapper.Elements())
        {
            seq.Add(AttributesToMapping(child));
        }
        return seq;
    }

    /// <summary>Converts <c>&lt;item&gt;value&lt;/item&gt;</c> / <c>&lt;color&gt;</c> children into a scalar sequence.</summary>
    private static YamlSequenceNode ConvertScalarItemSequence(XElement wrapper)
    {
        var seq = new YamlSequenceNode();
        foreach (var child in wrapper.Elements())
        {
            seq.Add(new YamlScalarNode(child.Value.Trim()));
        }
        return seq;
    }

    /// <summary>Converts a &lt;fonts&gt; wrapper into a YAML sequence of font entry mappings.</summary>
    private static YamlSequenceNode ConvertFonts(XElement fonts)
    {
        var seq = new YamlSequenceNode();
        foreach (var font in fonts.Elements())
        {
            seq.Add(AttributesToMapping(font));
        }
        return seq;
    }

    /// <summary>
    /// Expands a comma-separated (or "x,y; x,y" tuple) attribute value into a YAML sequence.
    /// A value containing a template expression is left as a scalar.
    /// </summary>
    private static YamlNode ExpandListAttribute(string value)
    {
        if (value.Contains("{{", StringComparison.Ordinal))
        {
            return new YamlScalarNode(value);
        }

        // Tuple list: "1,2; 3,4" -> [[1,2],[3,4]]
        if (value.Contains(';', StringComparison.Ordinal))
        {
            var outer = new YamlSequenceNode();
            foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var inner = new YamlSequenceNode();
                foreach (var comp in part.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    inner.Add(new YamlScalarNode(comp));
                }
                outer.Add(inner);
            }
            return outer;
        }

        var seq = new YamlSequenceNode();
        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            seq.Add(new YamlScalarNode(item));
        }
        return seq;
    }

    /// <summary>Adds an XML attribute to a mapping node when present and non-empty.</summary>
    private static void AddAttrIfPresent(XElement el, string name, YamlMappingNode node)
    {
        var attr = el.Attribute(name);
        if (attr is not null && !string.IsNullOrEmpty(attr.Value))
        {
            node.Add(name, new YamlScalarNode(attr.Value));
        }
    }
}
