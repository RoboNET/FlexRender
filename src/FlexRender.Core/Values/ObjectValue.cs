namespace FlexRender;

/// <summary>
/// Represents an object with named properties in template data.
/// </summary>
public sealed class ObjectValue : TemplateValue
{
    private readonly Dictionary<string, TemplateValue> _properties = new();

    /// <summary>
    /// Gets or sets a property value by key.
    /// Returns <see cref="NullValue.Instance"/> if the key doesn't exist.
    /// </summary>
    /// <param name="key">The property key.</param>
    /// <returns>The property value, or <see cref="NullValue.Instance"/> if the key doesn't exist.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null on set.</exception>
    public TemplateValue this[string key]
    {
        get => _properties.GetValueOrDefault(key.Trim(), NullValue.Instance);
        set => _properties[key.Trim()] = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets all property names in this object.
    /// </summary>
    public IEnumerable<string> Keys => _properties.Keys;

    /// <summary>
    /// Gets the number of properties in this object.
    /// </summary>
    public int Count => _properties.Count;

    /// <summary>
    /// Determines whether this object contains a property with the specified key.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the property exists; otherwise, false.</returns>
    public bool ContainsKey(string key) => _properties.ContainsKey(key.Trim());

    /// <summary>
    /// Tries to get the value of a property.
    /// </summary>
    /// <param name="key">The key of the property to get.</param>
    /// <param name="value">When this method returns, contains the value if found; otherwise, null.</param>
    /// <returns>True if the property was found; otherwise, false.</returns>
    public bool TryGetValue(string key, out TemplateValue? value)
    {
        return _properties.TryGetValue(key.Trim(), out value);
    }

    /// <inheritdoc />
    public override bool Equals(TemplateValue? other)
    {
        if (other is not ObjectValue objectValue)
            return false;

        if (_properties.Count != objectValue._properties.Count)
            return false;

        foreach (var kvp in _properties)
        {
            if (!objectValue._properties.TryGetValue(kvp.Key, out var otherValue))
                return false;

            if (!kvp.Value.Equals(otherValue))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Uses order-independent XOR-based hashing for O(n) performance instead of
    /// sorting keys which would be O(n log n).
    /// </remarks>
    public override int GetHashCode()
    {
        // Use XOR for order-independent hashing
        // Combine count with XOR of all key-value pair hashes
        var hash = _properties.Count;
        foreach (var kvp in _properties)
        {
            // Combine key and value hashes, then XOR with running hash
            // Using unchecked to allow overflow
            unchecked
            {
                var pairHash = HashCode.Combine(kvp.Key, kvp.Value);
                hash ^= pairHash;
            }
        }
        return hash;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var props = string.Join(", ", _properties.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        return $"{{{props}}}";
    }
}
