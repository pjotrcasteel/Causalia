using Causalia.Tool.Generation;
using Causalia.Tool.Inspection;

namespace Causalia.Tool.Tests;

[TestClass]
public sealed class ScenarioGeneratorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Generator_EmitsStableCompileableShape_ForDiscoveredBoundaries()
    {
        using var directory = new TemporaryDirectory();
        var project = await directory.WriteFileAsync("Orders.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            TestContext.CancellationToken);
        await directory.WriteFileAsync("OrderHandler.cs",
            "public sealed class OrderHandler { private HttpClient client; private ServiceBusClient bus; private TimeProvider clock; }",
            TestContext.CancellationToken);
        var report = await new ProjectInspector().InspectAsync(project, TestContext.CancellationToken);
        await ScenarioGenerator.WriteAsync(directory.Path, report, TestContext.CancellationToken);
        var path = Path.Combine(directory.Path, "GeneratedBoundarySimulationTests.cs");
        var first = await File.ReadAllTextAsync(path, TestContext.CancellationToken);
        await ScenarioGenerator.WriteAsync(directory.Path, report, TestContext.CancellationToken);
        var second = await File.ReadAllTextAsync(path, TestContext.CancellationToken);
        Assert.AreEqual(first, second);
        StringAssert.Contains(first, "AzureServiceBus_RedeliveryRequiresIdempotentHandler");
        StringAssert.Contains(first, "reject-first-complete");
        StringAssert.Contains(first, "HTTP_BoundaryCanRunDeterministically");
        StringAssert.Contains(first, "context.TimeProvider");
        StringAssert.Contains(first, "TODO: Inject the real application entry point");
    }
}
