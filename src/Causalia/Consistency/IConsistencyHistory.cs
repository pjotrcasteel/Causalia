namespace Causalia.Consistency;

internal interface IConsistencyHistory
{
    ConsistencyOutcome Complete();
}
