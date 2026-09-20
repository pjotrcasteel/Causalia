using Causalia.Tool.Inspection;
using Causalia.Tool.Reporting;

namespace Causalia.Tool.Tests;

[TestClass]
public sealed class AdoptionAwareInspectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task InspectAsync_WhenDeterminismCandidatesExist_MapsThemToCompilerRules()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = await directory.WriteFileAsync(
            "App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            TestContext.CancellationToken);
        await directory.WriteFileAsync(
            "Worker.cs",
            """
            public sealed class Worker
            {
                public async Task RunAsync()
                {
                    var now = DateTimeOffset.UtcNow;
                    await Task.Delay(10, CancellationToken.None);
                }
            }
            """,
            TestContext.CancellationToken);

        var report = await new ProjectInspector().InspectAsync(projectPath, TestContext.CancellationToken);

        Assert.AreEqual("CAU1001", report.Findings.Single(finding => finding.Code == "INSPECT001").AnalyzerRuleId);
        Assert.AreEqual("CAU1001", report.Findings.Single(finding => finding.Code == "INSPECT002").AnalyzerRuleId);
        Assert.AreEqual("CAU1007", report.Findings.Single(finding => finding.Code == "INSPECT007").AnalyzerRuleId);
    }

    [TestMethod]
    public async Task ReportWriter_WhenFindingHasCompilerRule_ShowsAdoptionGuidance()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = await directory.WriteFileAsync(
            "App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            TestContext.CancellationToken);
        await directory.WriteFileAsync(
            "Worker.cs",
            "public sealed class Worker { public DateTimeOffset Now => DateTimeOffset.UtcNow; }",
            TestContext.CancellationToken);
        var report = await new ProjectInspector().InspectAsync(projectPath, TestContext.CancellationToken);
        using var writer = new StringWriter();

        await ReportWriter.WriteAsync(report, OutputFormat.Text, writer, TestContext.CancellationToken);

        StringAssert.Contains(writer.ToString(), "Compiler rule in deterministic code: CAU1001");
    }
}
