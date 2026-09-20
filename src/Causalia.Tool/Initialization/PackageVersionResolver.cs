using System.Reflection;

namespace Causalia.Tool.Initialization;

internal static class PackageVersionResolver
{
    private const string FallbackVersion = "2.1.1";

    public static string GetCausaliaVersion()
    {
        var informationalVersion = typeof(PackageVersionResolver).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var normalized = Normalize(informationalVersion);

        if (normalized is not null)
        {
            return normalized;
        }

        var assemblyVersion = typeof(PackageVersionResolver).Assembly.GetName().Version;

        if (assemblyVersion is not null && assemblyVersion.Major > 0)
        {
            return $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
        }

        return FallbackVersion;
    }

    private static string? Normalize(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var metadataIndex = version.IndexOf('+');
        return metadataIndex < 0 ? version : version[..metadataIndex];
    }
}
