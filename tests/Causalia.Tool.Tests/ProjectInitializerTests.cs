using System.Xml.Linq;
using Causalia.Tool.Initialization;

namespace Causalia.Tool.Tests;

[TestClass]
public sealed class ProjectInitializerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task InitializeAsync_WhenSolutionHasDaprService_GeneratesScopedGoldenPathProject()
    {
        using var directory = new TemporaryDirectory();
        var solutionPath = await CreateSolutionAsync(directory, TestContext.CancellationToken);
        await CreateCentralPackagesAsync(directory, TestContext.CancellationToken);
        await directory.WriteFileAsync(
            "src/Orders/OrderHandler.cs",
            """
            using Dapr.Client;

            public sealed class OrderHandler
            {
                private readonly DaprClient _client;

                public OrderHandler(DaprClient client)
                {
                    _client = client;
                }

                public Task HandleAsync(CancellationToken cancellationToken)
                {
                    return _client.PublishEventAsync("pubsub", "orders", new { Id = "42" }, cancellationToken);
                }
            }
            """,
            TestContext.CancellationToken);
        var initializer = new ProjectInitializer();

        var result = await initializer.InitializeAsync(
            solutionPath,
            projectPath: null,
            force: false,
            TestContext.CancellationToken);

        var testProject = Path.Combine(directory.Path, "tests/Orders.Causalia.Tests/Orders.Causalia.Tests.csproj");
        var testFile = Path.Combine(directory.Path, "tests/Orders.Causalia.Tests/GoldenPathSimulationTests.cs");
        var centralProps = Path.Combine(directory.Path, "tests/Orders.Causalia.Tests/Directory.Packages.props");
        Assert.IsTrue(File.Exists(testProject));
        Assert.IsTrue(File.Exists(testFile));
        Assert.IsTrue(File.Exists(centralProps));
        Assert.IsTrue(result.AddedToSolution);
        CollectionAssert.Contains(result.PackageReferences.ToList(), "Causalia");
        CollectionAssert.Contains(result.PackageReferences.ToList(), "Causalia.Dapr");
        CollectionAssert.DoesNotContain(result.PackageReferences.ToList(), "Causalia.RabbitMQ");

        var projectContent = await File.ReadAllTextAsync(testProject, TestContext.CancellationToken);
        StringAssert.Contains(projectContent, "<PackageReference Include=\"Causalia.Dapr\" />");
        StringAssert.Contains(projectContent, "<ProjectReference Include=\"../../src/Orders/Orders.csproj\" />");

        var testContent = await File.ReadAllTextAsync(testFile, TestContext.CancellationToken);
        StringAssert.Contains(testContent, "Simulation.RunAsync(");
        StringAssert.Contains(testContent, "context.Invariant(\"operation happens once\"");
        StringAssert.Contains(testContent, "Suggested production boundary:");

        var solution = XDocument.Load(solutionPath);
        Assert.IsTrue(solution.Descendants("Project").Any(project =>
            project.Attribute("Path")?.Value == "tests/Orders.Causalia.Tests/Orders.Causalia.Tests.csproj"));
    }

    [TestMethod]
    public async Task InitializeAsync_WhenProjectIsSpecified_UsesThatProjectOnly()
    {
        using var directory = new TemporaryDirectory();
        var solutionPath = await CreateSolutionAsync(directory, TestContext.CancellationToken);
        var initializer = new ProjectInitializer();

        var result = await initializer.InitializeAsync(
            solutionPath,
            "src/Notifications/Notifications.csproj",
            force: false,
            TestContext.CancellationToken);

        Assert.AreEqual("src/Notifications/Notifications.csproj", result.TargetProject);
        CollectionAssert.Contains(result.PackageReferences.ToList(), "Causalia.RabbitMQ");
        CollectionAssert.DoesNotContain(result.PackageReferences.ToList(), "Causalia.Dapr");
    }

    [TestMethod]
    public async Task InitializeAsync_WhenProjectAlreadyExists_RequiresForce()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = await directory.WriteFileAsync(
            "Orders.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """,
            TestContext.CancellationToken);
        var initializer = new ProjectInitializer();
        await initializer.InitializeAsync(projectPath, null, false, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await initializer.InitializeAsync(projectPath, null, false, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task InitializeAsync_WhenForceIsSpecified_ReplacesGeneratedProject()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = await directory.WriteFileAsync(
            "Orders.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """,
            TestContext.CancellationToken);
        var initializer = new ProjectInitializer();
        var first = await initializer.InitializeAsync(projectPath, null, false, TestContext.CancellationToken);
        var generatedFile = Path.Combine(directory.Path, first.TestFile.Replace('/', Path.DirectorySeparatorChar));
        await File.WriteAllTextAsync(generatedFile, "stale", TestContext.CancellationToken);

        await initializer.InitializeAsync(projectPath, null, true, TestContext.CancellationToken);

        var content = await File.ReadAllTextAsync(generatedFile, TestContext.CancellationToken);
        Assert.AreNotEqual("stale", content);
        StringAssert.Contains(content, "GoldenPathSimulationRunsDeterministically");
    }

    [TestMethod]
    public async Task InitializeAsync_WhenTargetIsNotNet10_ThrowsActionableError()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = await directory.WriteFileAsync(
            "Orders.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>
            </Project>
            """,
            TestContext.CancellationToken);
        var initializer = new ProjectInitializer();

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await initializer.InitializeAsync(projectPath, null, false, TestContext.CancellationToken));

        StringAssert.Contains(exception.Message, "require net10.0");
    }

    private static async Task<string> CreateSolutionAsync(
        TemporaryDirectory directory,
        CancellationToken cancellationToken)
    {
        var solution = await directory.WriteFileAsync(
            "Service.slnx",
            """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/Orders/Orders.csproj" />
                <Project Path="src/Notifications/Notifications.csproj" />
              </Folder>
              <Folder Name="/tests/" />
            </Solution>
            """,
            cancellationToken);
        await directory.WriteFileAsync(
            "src/Orders/Orders.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="Dapr.Client" /></ItemGroup>
            </Project>
            """,
            cancellationToken);
        await directory.WriteFileAsync(
            "src/Notifications/Notifications.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="RabbitMQ.Client" /></ItemGroup>
            </Project>
            """,
            cancellationToken);
        await directory.WriteFileAsync(
            "src/Notifications/NotificationPublisher.cs",
            "public sealed class NotificationPublisher { private readonly RabbitMQ.Client.IChannel _channel = null!; }",
            cancellationToken);
        return solution;
    }

    private static Task<string> CreateCentralPackagesAsync(
        TemporaryDirectory directory,
        CancellationToken cancellationToken)
    {
        return directory.WriteFileAsync(
            "Directory.Packages.props",
            """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.10.0" />
                <PackageVersion Include="MSTest.TestAdapter" Version="4.4.0" />
                <PackageVersion Include="MSTest.TestFramework" Version="4.4.0" />
              </ItemGroup>
            </Project>
            """,
            cancellationToken);
    }
}
