using System.Collections;

namespace FlexRender;

/// <summary>
/// Represents an array of template values.
/// </summary>
public sealed class ArrayValue : TemplateValue, IReadOnlyList<TemplateValue>
{
    private readonly IReadOnlyList<TemplateValue> _items;

    /// <summary>
    /// Gets the items in the array.
    /// </summary>
    public IReadOnlyList<TemplateValue> Items => _items;

    /// <summary>
    /// Gets the number of items in the array.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to get.</param>
    /// <returns>The item at the specified index.</returns>
    public TemplateValue this[int index] => _items[index];

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrayValue"/> class with the specified items.
    /// </summary>
    /// <param name="items">The items to store in the array.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
    public ArrayValue(IEnumerable<TemplateValue> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public override bool Equals(TemplateValue? other)
    {
        if (other is not ArrayValue arrayValue)
            return false;

        if (_items.Count != arrayValue._items.Count)
            return false;

        for (int i = 0; i < _items.Count; i++)
        {
            if (!_items[i].Equals(arrayValue._items[i]))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items)
        {
            hash.Add(item);
        }
        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"[{string.Join(", ", _items)}]";
    }

    /// <summary>
    /// Returns an enumerator that iterates through the array items.
    /// </summary>
    /// <returns>An enumerator for the array items.</returns>
    public IEnumerator<TemplateValue> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
