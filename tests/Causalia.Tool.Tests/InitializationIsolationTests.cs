using System.Xml.Linq;
using Causalia.Tool.Initialization;
using Causalia.Tool.Inspection;

namespace Causalia.Tool.Tests;

[TestClass]
public sealed class InitializationIsolationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task InitializeAsync_WhenTargetIsStandaloneExecutable_LeavesProductionBuildIntact()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = await CreateExecutableAsync(directory, TestContext.CancellationToken);
        var before = await File.ReadAllTextAsync(projectPath, TestContext.CancellationToken);

        var result = await new ProjectInitializer().InitializeAsync(projectPath, null, false, TestContext.CancellationToken);

        Assert.AreEqual(".causalia/tests/Consumer.Causalia.Tests/Consumer.Causalia.Tests.csproj", result.TestProject);
        Assert.AreEqual(before, await File.ReadAllTextAsync(projectPath, TestContext.CancellationToken));
        await DotnetRunner.RunAsync(directory.Path, ["build", projectPath, "-c", "Release", "--nologo", "-m:1"], TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task InitializeAsync_WhenSolutionContainsRootProject_AddsIsolatedTestProject()
    {
        using var directory = new TemporaryDirectory();
        await CreateExecutableAsync(directory, TestContext.CancellationToken);
        var solutionPath = await directory.WriteFileAsync(
            "Consumer.slnx", "<Solution><Project Path=\"Consumer.csproj\" /></Solution>", TestContext.CancellationToken);

        var result = await new ProjectInitializer().InitializeAsync(solutionPath, null, false, TestContext.CancellationToken);

        Assert.IsTrue(result.AddedToSolution);
        Assert.AreEqual(".causalia/tests/Consumer.Causalia.Tests/Consumer.Causalia.Tests.csproj", result.TestProject);
        var solution = XDocument.Load(solutionPath);
        Assert.AreEqual(1, solution.Descendants("Project").Count(project => project.Attribute("Path")?.Value == result.TestProject));
        await DotnetRunner.RunAsync(directory.Path, ["build", "Consumer.csproj", "-c", "Release", "--nologo", "-m:1"], TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task InspectAsync_AfterRootProjectInitialization_DoesNotAnalyzeGeneratedTestSources()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = await CreateExecutableAsync(directory, TestContext.CancellationToken);
        var initializer = new ProjectInitializer();
        var inspector = new ProjectInspector();
        var before = await inspector.InspectAsync(projectPath, TestContext.CancellationToken);

        await initializer.InitializeAsync(projectPath, null, false, TestContext.CancellationToken);
        var after = await inspector.InspectAsync(directory.Path, TestContext.CancellationToken);

        Assert.AreEqual(1, after.Projects.Count);
        CollectionAssert.AreEqual(before.RecommendedPackages.ToList(), after.RecommendedPackages.ToList());
    }

    private static async Task<string> CreateExecutableAsync(TemporaryDirectory directory, CancellationToken cancellationToken)
    {
        var projectPath = await directory.WriteFileAsync(
            "Consumer.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
            </Project>
            """, cancellationToken);
        await directory.WriteFileAsync("Program.cs", "System.Console.WriteLine(\"Consumer\");", cancellationToken);
        return projectPath;
    }
}
