using Causalia.Faults;

namespace Causalia.Storage.Faults;

internal sealed class StoragePhaseFaultPolicy : IFaultPolicy<StorageOperationContext, StorageFault>
{
    private readonly Func<StorageOperationContext, StorageFault> _effectFactory;
    private readonly StorageOperationPhase _phase;
    private readonly double _probability;

    public StoragePhaseFaultPolicy(
        string name,
        StorageOperationPhase phase,
        double probability,
        Func<StorageOperationContext, StorageFault> effectFactory)
    {
        Name = name;
        _phase = phase;
        _probability = probability;
        _effectFactory = effectFactory;
    }

    public string Name { get; }

    public bool TryEvaluate(StorageOperationContext context, FaultEvaluationContext evaluationContext, out StorageFault effect)
    {
        if (context.Phase != _phase || evaluationContext.NextDouble() >= _probability)
        {
            effect = default!;
            return false;
        }

        effect = _effectFactory(context);
        return true;
    }
}
