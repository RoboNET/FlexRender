namespace FlexRender.TemplateEngine;

/// <summary>
/// Holds the current evaluation context including data scope and loop variables.
/// </summary>
public sealed class TemplateContext
{
    private readonly Stack<TemplateValue> _scopeStack = new();

    /// <summary>
    /// Gets the current data scope.
    /// </summary>
    public TemplateValue CurrentScope => _scopeStack.Peek();

    /// <summary>
    /// Gets a read-only view of the scope stack for scope walking during variable resolution.
    /// The first element is the current (innermost) scope, the last is the root.
    /// </summary>
    internal IReadOnlyList<TemplateValue> Scopes => _scopeList;

    /// <summary>
    /// Cached list view of the scope stack for efficient scope walking.
    /// Kept in sync with the stack via PushScope/PopScope.
    /// </summary>
    private readonly List<TemplateValue> _scopeList = [];

    /// <summary>
    /// Gets the current loop index, or null if not in a loop.
    /// </summary>
    public int? LoopIndex { get; private set; }

    /// <summary>
    /// Gets whether the current iteration is the first item.
    /// </summary>
    public bool IsFirst { get; private set; }

    /// <summary>
    /// Gets whether the current iteration is the last item.
    /// </summary>
    public bool IsLast { get; private set; }

    /// <summary>
    /// Gets the current loop key when iterating over an ObjectValue, or null if not in an object loop.
    /// </summary>
    public string? LoopKey { get; private set; }

    /// <summary>
    /// Initializes a new context with root data.
    /// </summary>
    /// <param name="rootData">The root data object.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootData"/> is null.</exception>
    public TemplateContext(TemplateValue rootData)
    {
        ArgumentNullException.ThrowIfNull(rootData);
        _scopeStack.Push(rootData);
        _scopeList.Add(rootData);
    }

    /// <summary>
    /// Pushes a new scope onto the stack.
    /// </summary>
    /// <param name="scope">The new scope data.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> is null.</exception>
    public void PushScope(TemplateValue scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _scopeStack.Push(scope);
        _scopeList.Add(scope);
    }

    /// <summary>
    /// Pops the current scope and returns to the previous one.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when attempting to pop the root scope.</exception>
    public void PopScope()
    {
        if (_scopeStack.Count <= 1)
        {
            throw new InvalidOperationException("Cannot pop the root scope.");
        }
        _scopeStack.Pop();
        _scopeList.RemoveAt(_scopeList.Count - 1);
    }

    /// <summary>
    /// Sets the loop variables for the current iteration.
    /// </summary>
    /// <param name="index">The current loop index (zero-based). Must be non-negative and less than count.</param>
    /// <param name="count">The total number of items in the loop. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when index is negative, count is less than or equal to zero, or index is greater than or equal to count.
    /// </exception>
    public void SetLoopVariables(int index, int count)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Loop index cannot be negative.");
        }

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Loop count must be greater than zero.");
        }

        if (index >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index,
                $"Loop index ({index}) must be less than count ({count}).");
        }

        LoopIndex = index;
        IsFirst = index == 0;
        IsLast = index == count - 1;
    }

    /// <summary>
    /// Sets the loop key for the current object iteration.
    /// </summary>
    /// <param name="key">The current key. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    public void SetLoopKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        LoopKey = key.Trim();
    }

    /// <summary>
    /// Clears all loop variables.
    /// </summary>
    public void ClearLoopVariables()
    {
        LoopIndex = null;
        IsFirst = false;
        IsLast = false;
        LoopKey = null;
    }
}
