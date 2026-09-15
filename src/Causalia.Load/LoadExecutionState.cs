using System.Runtime.ExceptionServices;

namespace Causalia.Load;

internal sealed class LoadExecutionState
{
    private ExceptionDispatchInfo? _fatalFailure;

    public required Func<LoadIterationContext, Task> Iteration { get; init; }

    public required LoadRunOptions Options { get; init; }

    public required LoadMetricsAccumulator Metrics { get; init; }

    public required CancellationToken CancellationToken { get; init; }

    public void CaptureFatalFailure(Exception exception)
    {
        _fatalFailure ??= ExceptionDispatchInfo.Capture(exception);
    }

    public void ThrowIfFatalFailure()
    {
        _fatalFailure?.Throw();
    }
}
