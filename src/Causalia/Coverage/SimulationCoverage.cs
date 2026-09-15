using System.Globalization;

namespace Causalia.Coverage;

/// <summary>
/// Records stable coverage signals that can guide bounded schedule exploration toward novel execution states.
/// </summary>
public sealed class SimulationCoverage
{
    private readonly HashSet<string> _points = new(StringComparer.Ordinal);

    /// <summary>
    /// Records that a named execution point was reached.
    /// </summary>
    public void Hit(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Record($"user:hit:{name}");
    }

    /// <summary>
    /// Records a named string state value.
    /// </summary>
    public void Observe(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        Record($"user:state:{name}:{value}");
    }

    /// <summary>
    /// Records a named signed integer state value.
    /// </summary>
    public void Observe(string name, long value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Record($"user:state:{name}:{value.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>
    /// Records a named unsigned integer state value.
    /// </summary>
    public void Observe(string name, ulong value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Record($"user:state:{name}:{value.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>
    /// Records a named Boolean state value.
    /// </summary>
    public void Observe(string name, bool value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Record($"user:state:{name}:{value.ToString(CultureInfo.InvariantCulture)}");
    }

    internal void RecordAutomatic(string point)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(point);
        Record($"runtime:{point}");
    }

    internal CoverageSnapshot CreateSnapshot()
    {
        var ordered = _points.OrderBy(static point => point, StringComparer.Ordinal).ToList().AsReadOnly();
        return new CoverageSnapshot(ordered);
    }

    private void Record(string point)
    {
        _points.Add(point);
    }
}
