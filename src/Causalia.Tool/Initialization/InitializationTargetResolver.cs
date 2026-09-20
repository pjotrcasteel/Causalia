namespace Causalia.Tool.Initialization;

internal static class InitializationTargetResolver
{
    public static string? FindSolutionPath(string targetPath, string rootDirectory)
    {
        var fullPath = Path.GetFullPath(targetPath);

        if (File.Exists(fullPath)
            && (fullPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
                || fullPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)))
        {
            return fullPath;
        }

        var solutions = Directory
            .EnumerateFiles(rootDirectory, "*.slnx", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(rootDirectory, "*.sln", SearchOption.TopDirectoryOnly))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return solutions.Count == 1 ? solutions[0] : null;
    }
}
