namespace Causalia.Linearizability;

/// <summary>
/// Creates deterministic concurrent-operation histories within a simulation.
/// </summary>
public sealed class SimulationLinearizability
{
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly SimulationContext _context;

    internal SimulationLinearizability(SimulationContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a uniquely named concurrent-operation history.
    /// </summary>
    public LinearizabilityHistory<TInput, TOutput> CreateHistory<TInput, TOutput>(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_names.Add(name))
        {
            throw new InvalidOperationException($"A linearizability history named '{name}' already exists in this simulation.");
        }

        return new LinearizabilityHistory<TInput, TOutput>(_context, name);
    }
}
