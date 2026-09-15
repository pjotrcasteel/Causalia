namespace Causalia.Runtime;

internal sealed class DporFrontier
{
    private readonly HashSet<string> _knownPrefixes = new(StringComparer.Ordinal) { string.Empty };
    private readonly Stack<IReadOnlyList<int>> _stack = new();

    public int Count => _stack.Count;

    public bool TryAdd(IReadOnlyList<int> prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var normalized = Normalize(prefix);
        var key = normalized.Count == 0 ? string.Empty : string.Join(',', normalized);

        if (!_knownPrefixes.Add(key))
        {
            return false;
        }

        _stack.Push(normalized);
        return true;
    }

    public IReadOnlyList<int> Pop()
    {
        return _stack.Pop();
    }

    private static IReadOnlyList<int> Normalize(IReadOnlyList<int> prefix)
    {
        var count = prefix.Count;

        while (count > 0 && prefix[count - 1] == 0)
        {
            count--;
        }

        if (count == 0)
        {
            return Array.Empty<int>();
        }

        if (count == prefix.Count)
        {
            return prefix;
        }

        var normalized = new int[count];

        for (var index = 0; index < count; index++)
        {
            normalized[index] = prefix[index];
        }

        return Array.AsReadOnly(normalized);
    }
}
