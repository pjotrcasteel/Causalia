using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;

namespace Causalia.AspNetCore.Internal;

internal sealed class SimulationResponseBodyFeature : IHttpResponseBodyFeature
{
    private readonly SimulationResponseFeature _response;
    private PipeWriter? _writer;

    public SimulationResponseBodyFeature(Stream stream, SimulationResponseFeature response)
    {
        Stream = stream;
        _response = response;
    }

    public Stream Stream { get; }

    public PipeWriter Writer => _writer ??= PipeWriter.Create(Stream, new StreamPipeWriterOptions(leaveOpen: true));

    public void DisableBuffering()
    {
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _response.StartAsync();
    }

    public async Task SendFileAsync(
        string path,
        long offset,
        long? count,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await StartAsync(cancellationToken);
        await using var file = File.OpenRead(path);
        file.Seek(offset, SeekOrigin.Begin);

        if (count is null)
        {
            await file.CopyToAsync(Stream, cancellationToken);
            return;
        }

        var remaining = count.Value;
        var buffer = new byte[81920];

        while (remaining > 0)
        {
            var read = await file.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);

            if (read == 0)
            {
                break;
            }

            await Stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
    }

    public async Task CompleteAsync()
    {
        if (_writer is not null)
        {
            await _writer.CompleteAsync();
        }

        await Stream.FlushAsync();
        await _response.CompleteAsync();
    }
}
