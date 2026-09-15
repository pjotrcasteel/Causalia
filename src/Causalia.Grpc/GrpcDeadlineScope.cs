namespace Causalia.Grpc;

internal sealed class GrpcDeadlineScope : IDisposable
{
    private readonly CancellationTokenSource _deadline;
    private readonly CancellationTokenSource _linked;

    public GrpcDeadlineScope(TimeSpan timeout, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        _deadline = new CancellationTokenSource(timeout, timeProvider);
        _linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _deadline.Token);
    }

    public CancellationToken Token => _linked.Token;

    public bool IsDeadlineExceeded => _deadline.IsCancellationRequested;

    public void Dispose()
    {
        _linked.Dispose();
        _deadline.Dispose();
    }
}
