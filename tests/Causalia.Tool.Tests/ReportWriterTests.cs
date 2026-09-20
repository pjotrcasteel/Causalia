using System.Text.Json;
using Causalia.Tool.Inspection;
using Causalia.Tool.Reporting;

namespace Causalia.Tool.Tests;

[TestClass]
public sealed class ReportWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAsync_WhenJsonIsRequested_ProducesMachineReadableReport()
    {
        var report = CreateReport();
        using var writer = new StringWriter();

        await ReportWriter.WriteAsync(report, OutputFormat.Json, writer, TestContext.CancellationToken);

        using var document = JsonDocument.Parse(writer.ToString());
        Assert.AreEqual("sample", document.RootElement.GetProperty("target").GetString());
        Assert.AreEqual(1, document.RootElement.GetProperty("projects").GetArrayLength());
        Assert.AreEqual("Causalia", document.RootElement.GetProperty("recommendedPackages")[0].GetString());
    }

    [TestMethod]
    public async Task WriteAsync_WhenTextIsRequested_IncludesSuggestedScenario()
    {
        var report = CreateReport();
        using var writer = new StringWriter();

        await ReportWriter.WriteAsync(report, OutputFormat.Text, writer, TestContext.CancellationToken);

        StringAssert.Contains(writer.ToString(), "Suggested starting point");
        StringAssert.Contains(writer.ToString(), "lost acknowledgement");
    }

    private static InspectionReport CreateReport()
    {
        var boundary = new BoundaryCandidate
        {
            Category = "Storage",
            Name = "OrderStore",
            FilePath = "OrderStore.cs",
            Line = 12,
            Reason = "Storage boundary.",
            Score = 90,
            SuggestedScenario = "Simulate a lost acknowledgement."
        };

        return new InspectionReport
        {
            Target = "sample",
            Projects = new List<ProjectSummary>
            {
                new()
                {
                    Name = "Sample",
                    Path = "Sample.csproj",
                    ProjectDirectory = ".",
                    Sdk = "Microsoft.NET.Sdk",
                    TargetFrameworks = new List<string> { "net10.0" }.AsReadOnly(),
                    PackageReferences = new List<string>().AsReadOnly(),
                    FrameworkReferences = new List<string>().AsReadOnly(),
                    IsTestProject = false
                }
            }.AsReadOnly(),
            SourceFilesScanned = 1,
            DetectedTechnologies = new List<string>().AsReadOnly(),
            AdoptionCapabilities = new AdoptionCapabilities
            {
                UsesTimeProvider = true,
                UsesCancellationToken = true,
                UsesRetryMechanisms = false
            },
            Findings = new List<SourceFinding>().AsReadOnly(),
            BoundaryCandidates = new List<BoundaryCandidate> { boundary }.AsReadOnly(),
            RecommendedPackages = new List<string> { "Causalia" }.AsReadOnly(),
            SuggestedStartingPoint = boundary
        };
    }
}
