using Causalia.Tool.Inspection;

namespace Causalia.Tool.Tests;

[TestClass]
public sealed class InspectionTargetResolverTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ResolveAsync_WhenClassicSolutionUsesWindowsSeparators_ResolvesProject()
    {
        using var directory = new TemporaryDirectory();
        var solutionPath = await directory.WriteFileAsync(
            "App.sln",
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{00000000-0000-0000-0000-000000000000}") = "App", "src\App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Global
            EndGlobal
            """,
            TestContext.CancellationToken);
        var projectPath = await directory.WriteFileAsync(
            "src/App/App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            TestContext.CancellationToken);
        var resolver = new InspectionTargetResolver();

        var target = await resolver.ResolveAsync(solutionPath, TestContext.CancellationToken);

        Assert.AreEqual(1, target.ProjectFiles.Count);
        Assert.AreEqual(Path.GetFullPath(projectPath), target.ProjectFiles[0]);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenDirectoryContainsBuildOutput_IgnoresObjProjects()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = await directory.WriteFileAsync(
            "src/App/App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            TestContext.CancellationToken);
        await directory.WriteFileAsync(
            "src/App/obj/Generated.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            TestContext.CancellationToken);
        var resolver = new InspectionTargetResolver();

        var target = await resolver.ResolveAsync(directory.Path, TestContext.CancellationToken);

        Assert.AreEqual(1, target.ProjectFiles.Count);
        Assert.AreEqual(Path.GetFullPath(projectPath), target.ProjectFiles[0]);
    }
}
