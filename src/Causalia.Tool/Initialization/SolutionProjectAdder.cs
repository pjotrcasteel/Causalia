using System.Xml.Linq;

namespace Causalia.Tool.Initialization;

internal sealed class SolutionProjectAdder
{
    public async Task<bool> TryAddAsync(
        string? solutionPath,
        string rootDirectory,
        string projectPath,
        CancellationToken cancellationToken)
    {
        if (solutionPath is null || !solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        XDocument document;
        await using (var stream = File.OpenRead(solutionPath))
        {
            document = await XDocument.LoadAsync(stream, LoadOptions.PreserveWhitespace, cancellationToken);
        }

        var root = document.Root ?? throw new InvalidDataException($"Solution '{solutionPath}' has no XML root element.");
        var relativePath = Path.GetRelativePath(rootDirectory, projectPath).Replace('\\', '/');

        if (root.Descendants().Any(element =>
                string.Equals(element.Name.LocalName, "Project", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Path")?.Value, relativePath, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var testsFolder = root.Elements()
            .FirstOrDefault(element =>
                string.Equals(element.Name.LocalName, "Folder", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Name")?.Value, "/tests/", StringComparison.OrdinalIgnoreCase));

        if (testsFolder is null)
        {
            testsFolder = new XElement("Folder", new XAttribute("Name", "/tests/"));
            root.Add(testsFolder);
        }

        testsFolder.Add(new XElement("Project", new XAttribute("Path", relativePath)));
        await SaveAsync(document, solutionPath, cancellationToken);
        return true;
    }

    private static async Task SaveAsync(XDocument document, string solutionPath, CancellationToken cancellationToken)
    {
        var temporaryPath = solutionPath + "." + Path.GetRandomFileName() + ".tmp";
        var ownsTemporaryFile = false;

        try
        {
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemporaryFile = true;
                await document.SaveAsync(output, SaveOptions.None, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, solutionPath, overwrite: true);
        }
        finally
        {
            if (ownsTemporaryFile)
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
