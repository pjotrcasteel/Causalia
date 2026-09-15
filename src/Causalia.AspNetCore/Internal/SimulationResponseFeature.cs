using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Causalia.AspNetCore.Internal;

internal sealed class SimulationResponseFeature : IHttpResponseFeature
{
    private readonly List<(Func<object, Task> Callback, object State)> _completed = new();
    private readonly List<(Func<object, Task> Callback, object State)> _starting = new();

    public SimulationResponseFeature(Stream body)
    {
        Body = body;
    }

    public int StatusCode { get; set; } = StatusCodes.Status200OK;

    public string? ReasonPhrase { get; set; }

    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

    public Stream Body { get; set; }

    public bool HasStarted { get; private set; }

    public void OnStarting(Func<object, Task> callback, object state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (HasStarted)
        {
            throw new InvalidOperationException("The response has already started.");
        }

        _starting.Add((callback, state));
    }

    public void OnCompleted(Func<object, Task> callback, object state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _completed.Add((callback, state));
    }

    public async Task StartAsync()
    {
        if (HasStarted)
        {
            return;
        }

        for (var index = _starting.Count - 1; index >= 0; index--)
        {
            var registration = _starting[index];
            await registration.Callback(registration.State);
        }

        HasStarted = true;
    }

    public async Task CompleteAsync()
    {
        await StartAsync();

        for (var index = _completed.Count - 1; index >= 0; index--)
        {
            var registration = _completed[index];
            await registration.Callback(registration.State);
        }
    }
}
