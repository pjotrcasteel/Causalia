using Causalia.Tool.Inspection;

namespace Causalia.Tool.Tests;

[TestClass]
public sealed class ProjectInspectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task InspectAsync_WhenServiceUsesCommonBoundaries_ReturnsActionableReport()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = await directory.WriteFileAsync(
            "src/Orders/Orders.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore" />
                <PackageReference Include="Dapr.Client" />
              </ItemGroup>
            </Project>
            """,
            TestContext.CancellationToken);
        await directory.WriteFileAsync(
            "src/Orders/OrderHandler.cs",
            """
            using Dapr.Client;
            using Microsoft.EntityFrameworkCore;

            public sealed class OrderHandler
            {
                private readonly HttpClient _httpClient;
                private readonly DbContext _dbContext;
                private readonly DaprClient _daprClient;

                public async Task HandleAsync(CancellationToken cancellationToken)
                {
                    var now = DateTimeOffset.UtcNow;
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }
            """,
            TestContext.CancellationToken);

        var inspector = new ProjectInspector();

        var report = await inspector.InspectAsync(projectPath, TestContext.CancellationToken);

        CollectionAssert.Contains(report.DetectedTechnologies.ToList(), "ASP.NET Core");
        CollectionAssert.Contains(report.DetectedTechnologies.ToList(), "Entity Framework Core");
        CollectionAssert.Contains(report.DetectedTechnologies.ToList(), "Dapr");
        CollectionAssert.Contains(report.RecommendedPackages.ToList(), "Causalia.AspNetCore");
        CollectionAssert.Contains(report.RecommendedPackages.ToList(), "Causalia.EntityFrameworkCore");
        CollectionAssert.Contains(report.RecommendedPackages.ToList(), "Causalia.Dapr");
        Assert.IsTrue(report.Findings.Any(finding => finding.Code == "INSPECT001"));
        Assert.IsTrue(report.Findings.Any(finding => finding.Code == "INSPECT002"));
        Assert.IsTrue(report.AdoptionCapabilities.UsesCancellationToken);
        Assert.IsFalse(report.AdoptionCapabilities.UsesTimeProvider);
        Assert.IsTrue(report.BoundaryCandidates.Any(candidate => candidate.Category == "Storage"));
        Assert.IsTrue(report.BoundaryCandidates.Any(candidate => candidate.Category == "Dapr"));
        Assert.IsNotNull(report.SuggestedStartingPoint);
        Assert.AreEqual("Dapr", report.SuggestedStartingPoint.Category);
    }

    [TestMethod]
    public async Task InspectAsync_WhenSolutionContainsTests_ScansProductionSourcesOnly()
    {
        using var directory = new TemporaryDirectory();
        var solutionPath = await directory.WriteFileAsync(
            "App.slnx",
            """
            <Solution>
              <Project Path="src/App/App.csproj" />
              <Project Path="tests/App.Tests/App.Tests.csproj" />
            </Solution>
            """,
            TestContext.CancellationToken);
        await directory.WriteFileAsync(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """,
            TestContext.CancellationToken);
        await directory.WriteFileAsync(
            "src/App/Worker.cs",
            "public sealed class Worker { public DateTime Now => DateTime.UtcNow; }",
            TestContext.CancellationToken);
        await directory.WriteFileAsync(
            "tests/App.Tests/App.Tests.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
              <ItemGroup><PackageReference Include="Microsoft.NET.Test.Sdk" /></ItemGroup>
            </Project>
            """,
            TestContext.CancellationToken);
        await directory.WriteFileAsync(
            "tests/App.Tests/TestOnly.cs",
            "public sealed class TestOnly { public Guid Id => Guid.NewGuid(); }",
            TestContext.CancellationToken);

        var inspector = new ProjectInspector();

        var report = await inspector.InspectAsync(solutionPath, TestContext.CancellationToken);

        Assert.AreEqual(2, report.Projects.Count);
        Assert.AreEqual(1, report.SourceFilesScanned);
        Assert.IsTrue(report.Findings.Any(finding => finding.Code == "INSPECT001"));
        Assert.IsFalse(report.Findings.Any(finding => finding.Code == "INSPECT005"));
    }

    [TestMethod]
    public async Task InspectAsync_WhenCancellationIsRequested_Throws()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = await directory.WriteFileAsync(
            "App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            TestContext.CancellationToken);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var inspector = new ProjectInspector();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await inspector.InspectAsync(projectPath, cancellationTokenSource.Token));
    }
}
