using Causalia.Faults;

namespace Causalia.Storage.Faults;

/// <summary>
/// Composes deterministic latency and failure policies around durable-storage boundaries.
/// </summary>
public sealed class StorageFaultPlan
{
    private readonly FaultPlan<StorageOperationContext, StorageFault> _plan = new();
    private int _policyId;

    internal FaultPlan<StorageOperationContext, StorageFault> Plan => _plan;

    /// <summary>
    /// Adds deterministic additional latency at the selected storage-operation phase.
    /// </summary>
    public StorageFaultPlan Delay(StorageOperationPhase phase, double probability, TimeSpan delay)
    {
        ValidateProbability(probability);

        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay cannot be negative.");
        }

        var name = $"storage.delay.{phase}.{++_policyId}";
        _plan.Add(new StoragePhaseFaultPolicy(name, phase, probability, _ => new StorageDelayFault(delay)));
        return this;
    }

    /// <summary>
    /// Adds a deterministic storage failure at the selected operation phase.
    /// </summary>
    public StorageFaultPlan Fail(StorageOperationPhase phase, double probability, string code)
    {
        ValidateProbability(probability);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var name = $"storage.fail.{phase}.{++_policyId}";
        _plan.Add(new StoragePhaseFaultPolicy(name, phase, probability, _ => new StorageFailureFault(code)));
        return this;
    }

    /// <summary>
    /// Adds a failure before durable commit is applied.
    /// </summary>
    public StorageFaultPlan FailBeforeCommit(double probability, string code = "commit-rejected")
    {
        return Fail(StorageOperationPhase.BeforeCommit, probability, code);
    }

    /// <summary>
    /// Adds an ambiguous failure after durable commit has already been applied.
    /// </summary>
    public StorageFaultPlan FailAfterCommit(double probability, string code = "commit-ack-lost")
    {
        return Fail(StorageOperationPhase.AfterCommit, probability, code);
    }

    private static void ValidateProbability(double probability)
    {
        if (double.IsNaN(probability) || probability is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(probability), probability, "Probability must be between zero and one inclusive.");
        }
    }
}
