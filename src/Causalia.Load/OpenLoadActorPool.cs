namespace Causalia.Load;

internal sealed class OpenLoadActorPool
{
    private readonly Stack<int> _availableActors;

    public OpenLoadActorPool(int capacity)
    {
        _availableActors = new Stack<int>(capacity);

        for (var actorId = capacity; actorId >= 1; actorId--)
        {
            _availableActors.Push(actorId);
        }
    }

    public bool TryAcquire(out int actorId)
    {
        return _availableActors.TryPop(out actorId);
    }

    public void Release(int actorId)
    {
        _availableActors.Push(actorId);
    }
}
