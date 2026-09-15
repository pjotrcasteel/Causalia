namespace Causalia.AspNetCore.Networking.Internal;

internal sealed class HttpRequestSnapshot
{
    private readonly HttpContentSnapshot? _content;
    private readonly IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> _requestHeaders;

    private HttpRequestSnapshot(
        HttpMethod method,
        Uri requestUri,
        Version version,
        HttpVersionPolicy versionPolicy,
        IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> requestHeaders,
        HttpContentSnapshot? content)
    {
        Method = method;
        RequestUri = requestUri;
        Version = version;
        VersionPolicy = versionPolicy;
        _requestHeaders = requestHeaders;
        _content = content;
    }

    public HttpMethod Method { get; }

    public Uri RequestUri { get; }

    public Version Version { get; }

    public HttpVersionPolicy VersionPolicy { get; }

    public static async Task<HttpRequestSnapshot> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestUri = request.RequestUri ?? throw new InvalidOperationException("A simulated network request must have a request URI.");
        var requestHeaders = request.Headers.Select(header => Pair(header.Key, header.Value)).ToList().AsReadOnly();
        HttpContentSnapshot? content = null;

        if (request.Content is not null)
        {
            var contentHeaders = request.Content.Headers.Select(header => Pair(header.Key, header.Value)).ToList().AsReadOnly();
            var contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            content = new HttpContentSnapshot(contentBytes, contentHeaders);
        }

        return new HttpRequestSnapshot(request.Method, requestUri, request.Version, request.VersionPolicy, requestHeaders, content);
    }

    public HttpRequestMessage CreateMessage()
    {
        var request = new HttpRequestMessage(Method, RequestUri)
        {
            Version = Version,
            VersionPolicy = VersionPolicy
        };

        foreach (var header in _requestHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (_content is null)
        {
            return request;
        }

        request.Content = new ByteArrayContent(_content.Content.ToArray());

        foreach (var header in _content.Headers)
        {
            request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return request;
    }

    private static KeyValuePair<string, IReadOnlyList<string>> Pair(string key, IEnumerable<string> values)
    {
        return new KeyValuePair<string, IReadOnlyList<string>>(key, values.ToList().AsReadOnly());
    }
}
