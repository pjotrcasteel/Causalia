namespace Causalia.AspNetCore.Networking.Internal;

internal sealed class HttpContentSnapshot
{
    public HttpContentSnapshot(ReadOnlyMemory<byte> content, IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> headers)
    {
        Content = content;
        Headers = headers;
    }

    public ReadOnlyMemory<byte> Content { get; }

    public IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> Headers { get; }
}
