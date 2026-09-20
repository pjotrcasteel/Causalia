using System.Collections.Immutable;
using Causalia.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Causalia.Analyzers.Tests;

[TestClass]
public sealed class CancellationPropagationAnalyzerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CancellationTokenNone_InsideSimulationScenario_ReportsCau1007()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using Causalia;

            public static class Scenario
            {
                [DeterministicSimulation]
                public static Task RunAsync(SimulationContext context)
                {
                    return Task.Delay(1, CancellationToken.None);
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(1, diagnostics.Length);
        Assert.AreEqual("CAU1007", diagnostics[0].Id);
    }

    [TestMethod]
    public async Task CancellationTokenNone_OutsideSimulationBoundary_DoesNotReport()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using System.Threading;

            public static class OrdinaryCode
            {
                public static CancellationToken GetToken() => CancellationToken.None;
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(0, diagnostics.Length);
    }

    [TestMethod]
    public async Task ContextCancellationToken_InsideSimulationScenario_DoesNotReport()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using System.Threading.Tasks;
            using Causalia;

            public static class Scenario
            {
                [DeterministicSimulation]
                public static Task RunAsync(SimulationContext context)
                {
                    return Task.Delay(1, context.TimeProvider, context.CancellationToken);
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(0, diagnostics.Length);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        CancellationToken cancellationToken)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
        var compilation = CSharpCompilation.Create(
            "AnalyzerTest",
            [syntaxTree],
            CreateReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new CancellationPropagationAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync(cancellationToken);
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Trusted platform assemblies were not available.");
        var references = trustedAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(Simulation).Assembly.Location));
        return references;
    }
}
