using System.Text;

namespace Causalia.Tool.Initialization;

internal sealed class ProjectTemplateWriter
{
    private const string TestSdkVersion = "18.10.0";
    private const string MstestVersion = "4.4.0";

    public async Task WriteAsync(
        GeneratedProjectDefinition definition,
        string? suggestedBoundary,
        string? suggestedScenario,
        bool force,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        EnsureCanWrite(definition.TestProjectDirectory, force);
        Directory.CreateDirectory(definition.TestProjectDirectory);

        var centralManagement = await CentralPackageManagement.FindAsync(
            Path.GetDirectoryName(definition.TargetProjectPath)!,
            cancellationToken);
        var packageVersions = CreatePackageVersions(definition.CausaliaPackages);
        var project = BuildProject(definition, packageVersions, centralManagement is not null);
        var test = BuildTest(definition.Namespace, suggestedBoundary, suggestedScenario);

        await File.WriteAllTextAsync(definition.TestProjectPath, project, cancellationToken);
        await File.WriteAllTextAsync(definition.TestFilePath, test, cancellationToken);

        if (centralManagement is not null)
        {
            var props = BuildCentralPackageProps(
                definition.TestProjectDirectory,
                centralManagement,
                packageVersions);
            await File.WriteAllTextAsync(
                Path.Combine(definition.TestProjectDirectory, "Directory.Packages.props"),
                props,
                cancellationToken);
        }
    }

    private static void EnsureCanWrite(string directory, bool force)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        if (!force)
        {
            throw new ArgumentException(
                $"Generated test project '{directory}' already exists. Use --force to replace it.");
        }

        Directory.Delete(directory, recursive: true);
    }

    private static IReadOnlyDictionary<string, string> CreatePackageVersions(IReadOnlyList<string> causaliaPackages)
    {
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Microsoft.NET.Test.Sdk"] = TestSdkVersion,
            ["MSTest.TestAdapter"] = MstestVersion,
            ["MSTest.TestFramework"] = MstestVersion
        };
        var causaliaVersion = PackageVersionResolver.GetCausaliaVersion();

        foreach (var package in causaliaPackages)
        {
            versions[package] = causaliaVersion;
        }

        return versions;
    }

    private static string BuildProject(
        GeneratedProjectDefinition definition,
        IReadOnlyDictionary<string, string> packageVersions,
        bool usesCentralPackageManagement)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        builder.AppendLine("  <PropertyGroup>");
        builder.AppendLine("    <TargetFramework>net10.0</TargetFramework>");
        builder.AppendLine("    <IsPackable>false</IsPackable>");
        builder.AppendLine("    <IsTestProject>true</IsTestProject>");
        builder.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        builder.AppendLine("    <Nullable>enable</Nullable>");
        builder.AppendLine($"    <ManagePackageVersionsCentrally>{usesCentralPackageManagement.ToString().ToLowerInvariant()}</ManagePackageVersionsCentrally>");
        builder.AppendLine("  </PropertyGroup>");
        builder.AppendLine("  <ItemGroup>");

        foreach (var package in packageVersions.Keys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            AppendPackageReference(builder, package, packageVersions[package], usesCentralPackageManagement);
        }

        var projectReference = Path.GetRelativePath(definition.TestProjectDirectory, definition.TargetProjectPath)
            .Replace('\\', '/');
        builder.AppendLine($"    <ProjectReference Include=\"{EscapeXml(projectReference)}\" />");
        builder.AppendLine("  </ItemGroup>");
        builder.AppendLine("</Project>");
        return builder.ToString();
    }

    private static void AppendPackageReference(
        StringBuilder builder,
        string package,
        string version,
        bool usesCentralPackageManagement)
    {
        var versionAttribute = usesCentralPackageManagement ? string.Empty : $" Version=\"{EscapeXml(version)}\"";

        if (string.Equals(package, "Causalia.Analyzers", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"    <PackageReference Include=\"{package}\"{versionAttribute}>");
            builder.AppendLine("      <PrivateAssets>all</PrivateAssets>");
            builder.AppendLine("      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>");
            builder.AppendLine("    </PackageReference>");
            return;
        }

        builder.AppendLine($"    <PackageReference Include=\"{package}\"{versionAttribute} />");
    }

    private static string BuildCentralPackageProps(
        string testProjectDirectory,
        CentralPackageManagement management,
        IReadOnlyDictionary<string, string> packageVersions)
    {
        var relativeImport = Path.GetRelativePath(testProjectDirectory, management.PropsPath).Replace('\\', '/');
        var builder = new StringBuilder();
        builder.AppendLine("<Project>");
        builder.AppendLine($"  <Import Project=\"{EscapeXml(relativeImport)}\" />");
        var missing = packageVersions
            .Where(pair => !management.PackageIds.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missing.Count > 0)
        {
            builder.AppendLine("  <ItemGroup>");

            foreach (var package in missing)
            {
                builder.AppendLine(
                    $"    <PackageVersion Include=\"{EscapeXml(package.Key)}\" Version=\"{EscapeXml(package.Value)}\" />");
            }

            builder.AppendLine("  </ItemGroup>");
        }

        builder.AppendLine("</Project>");
        return builder.ToString();
    }

    private static string BuildTest(string namespaceName, string? suggestedBoundary, string? suggestedScenario)
    {
        var boundary = string.IsNullOrWhiteSpace(suggestedBoundary)
            ? "No specific production boundary was detected. Replace the demonstration effect with one dependency seam."
            : $"Suggested production boundary: {suggestedBoundary}";
        var scenario = string.IsNullOrWhiteSpace(suggestedScenario)
            ? "Start by simulating one retry, ambiguous outcome, duplicate delivery, or concurrent operation."
            : $"Suggested first real scenario: {suggestedScenario}";

        return $$"""
            using Causalia;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace {{namespaceName}};

            [TestClass]
            public sealed class GoldenPathSimulationTests
            {
                public TestContext TestContext { get; set; } = null!;

                [TestMethod]
                public async Task GoldenPathSimulationRunsDeterministically()
                {
                    // {{boundary}}
                    // {{scenario}}
                    var observedEffects = 0;

                    var result = await Simulation.RunAsync(
                        async context =>
                        {
                            context.Invariant("operation happens once", () => observedEffects <= 1);
                            observedEffects++;
                            await Task.Delay(
                                TimeSpan.FromMilliseconds(10),
                                context.TimeProvider,
                                context.CancellationToken);
                        },
                        TestContext.CancellationToken);

                    Assert.AreEqual(1, observedEffects);
                    Assert.AreEqual(TimeSpan.FromMilliseconds(10), result.VirtualElapsed);
                }
            }
            """ + Environment.NewLine;
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }
}
