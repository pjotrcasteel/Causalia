using System.Xml.Linq;

namespace Causalia.Tool.Initialization;

internal sealed class CentralPackageManagement
{
    private CentralPackageManagement(string propsPath, IReadOnlySet<string> packageIds)
    {
        PropsPath = propsPath;
        PackageIds = packageIds;
    }

    public string PropsPath { get; }

    public IReadOnlySet<string> PackageIds { get; }

    public static async Task<CentralPackageManagement?> FindAsync(
        string startDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = Path.Combine(current.FullName, "Directory.Packages.props");

            if (File.Exists(candidate))
            {
                // MSBuild searches above a directly selected project and imports only the nearest file.
                return await TryReadAsync(candidate, cancellationToken);
            }

            current = current.Parent;
        }

        return null;
    }

    private static async Task<CentralPackageManagement?> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var enabled = document
            .Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "ManagePackageVersionsCentrally", StringComparison.Ordinal))
            ?.Value;

        if (!bool.TryParse(enabled, out var isEnabled) || !isEnabled)
        {
            return null;
        }

        var packageIds = document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "PackageVersion", StringComparison.Ordinal))
            .Select(element => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new CentralPackageManagement(path, packageIds);
    }
}
