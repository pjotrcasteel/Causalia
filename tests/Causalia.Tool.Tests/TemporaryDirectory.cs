namespace Causalia.Tool.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"causalia-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }

    public async Task<string> WriteFileAsync(
        string relativePath,
        string content,
        CancellationToken cancellationToken)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
        return fullPath;
    }
}
