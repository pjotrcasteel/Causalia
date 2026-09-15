using Microsoft.AspNetCore.Http.Features;

namespace Causalia.AspNetCore.Internal;

internal sealed class SimulationRequestLifetimeFeature : IHttpRequestLifetimeFeature, IDisposable
{
    private readonly CancellationToken _requestCancellationToken;
    private CancellationTokenSource _cancellation;

    public SimulationRequestLifetimeFeature(CancellationToken cancellationToken)
    {
        _requestCancellationToken = cancellationToken;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    public CancellationToken RequestAborted
    {
        get => _cancellation.Token;
        set
        {
            var replacement = CancellationTokenSource.CreateLinkedTokenSource(_requestCancellationToken, value);
            var previous = _cancellation;
            _cancellation = replacement;
            previous.Dispose();
        }
    }

    public void Abort()
    {
        _cancellation.Cancel();
    }

    public void Dispose()
    {
        _cancellation.Dispose();
    }
}
