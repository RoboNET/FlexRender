using System.Collections.Generic;

namespace FlexRender.Parsing.Nodes;

/// <summary>An ordered list of child nodes (the neutral analogue of a YAML sequence).</summary>
public sealed class TemplateSequence : TemplateNode
{
    private readonly List<TemplateNode> _items;

    /// <summary>Initializes an empty sequence.</summary>
    public TemplateSequence() => _items = [];

    /// <summary>Initializes a sequence with a starting capacity.</summary>
    /// <param name="capacity">The initial capacity.</param>
    public TemplateSequence(int capacity) => _items = new List<TemplateNode>(capacity);

    /// <summary>Gets the ordered child items.</summary>
    public IReadOnlyList<TemplateNode> Items => _items;

    /// <summary>Appends a child node to the sequence.</summary>
    /// <param name="node">The node to append.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
    public void Add(TemplateNode node)
    {
        System.ArgumentNullException.ThrowIfNull(node);
        _items.Add(node);
    }
}
