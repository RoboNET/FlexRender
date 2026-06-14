using System.Collections.Generic;

namespace FlexRender.Parsing.Nodes;

/// <summary>
/// An ordered, string-keyed mapping of child nodes (the neutral analogue of a YAML mapping).
/// Preserves insertion order and supports key lookup and key enumeration for validation.
/// </summary>
public sealed class TemplateMapping : TemplateNode
{
    // Insertion-ordered keys + value lookup. Keys are compared ordinally (YAML/XML keys are case-sensitive).
    private readonly List<string> _keys = [];
    private readonly Dictionary<string, TemplateNode> _values = new(System.StringComparer.Ordinal);

    /// <summary>Gets the keys in insertion order.</summary>
    public IReadOnlyList<string> Keys => _keys;

    /// <summary>
    /// Adds or replaces a child node by key. If the key already exists its value is overwritten
    /// but its position in the key order is preserved (last-wins on value, matching YAML semantics).
    /// </summary>
    /// <param name="key">The child key.</param>
    /// <param name="node">The child node.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="node"/> is null.</exception>
    public void Add(string key, TemplateNode node)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        System.ArgumentNullException.ThrowIfNull(node);
        if (!_values.ContainsKey(key))
            _keys.Add(key);
        _values[key] = node;
    }

    /// <summary>Tries to get any child node by key.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="node">The child node when found.</param>
    /// <returns>True when the key exists; otherwise false.</returns>
    public bool TryGet(string key, out TemplateNode node) => _values.TryGetValue(key, out node!);

    /// <summary>Tries to get a child mapping by key.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="mapping">The child mapping when the key exists and is a mapping.</param>
    /// <returns>True when the key exists and is a <see cref="TemplateMapping"/>; otherwise false.</returns>
    public bool TryGetMapping(string key, out TemplateMapping mapping)
    {
        if (_values.TryGetValue(key, out var n) && n is TemplateMapping m)
        {
            mapping = m;
            return true;
        }
        mapping = null!;
        return false;
    }

    /// <summary>Tries to get a child sequence by key.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="sequence">The child sequence when the key exists and is a sequence.</param>
    /// <returns>True when the key exists and is a <see cref="TemplateSequence"/>; otherwise false.</returns>
    public bool TryGetSequence(string key, out TemplateSequence sequence)
    {
        if (_values.TryGetValue(key, out var n) && n is TemplateSequence s)
        {
            sequence = s;
            return true;
        }
        sequence = null!;
        return false;
    }

    /// <summary>Gets the scalar string value for a key, or null when absent or not a scalar.</summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The scalar value, or null.</returns>
    public string? GetScalar(string key)
        => _values.TryGetValue(key, out var n) && n is TemplateScalar s ? s.Value : null;
}
