using System.Collections.Immutable;
using Causalia.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Causalia.Analyzers.Tests;

[TestClass]
public sealed class DeterminismAnalyzerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Analyzer_DirectScenarioUsingUtcNow_ReportsVirtualTimeDiagnostic()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public static class Scenario
            {
                public static Task RunAsync(CancellationToken cancellationToken)
                {
                    return Simulation.RunAsync(
                        new SimulationOptions(),
                        context =>
                        {
                            _ = DateTime.UtcNow;
                            return Task.CompletedTask;
                        },
                        cancellationToken);
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1001"));
    }

    [TestMethod]
    public async Task Analyzer_DeterministicAlternatives_DoNotReportDiagnostics()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public static class Scenario
            {
                public static Task RunAsync(CancellationToken cancellationToken)
                {
                    return Simulation.RunAsync(
                        new SimulationOptions(),
                        async context =>
                        {
                            _ = context.TimeProvider.GetUtcNow();
                            _ = context.Random.NextInt32(10);
                            _ = context.Random.NextGuid();
                            await Task.Delay(TimeSpan.FromSeconds(1), context.TimeProvider, context.CancellationToken);
                        },
                        cancellationToken);
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(0, diagnostics.Length);
    }

    [TestMethod]
    public async Task Analyzer_RandomSharedAndTaskRun_ReportDedicatedDiagnostics()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public static class Scenario
            {
                public static Task RunAsync(CancellationToken cancellationToken)
                {
                    return Simulation.RunAsync(
                        new SimulationOptions(),
                        async context =>
                        {
                            _ = Random.Shared.Next();
                            await Task.Run(() => 42, context.CancellationToken);
                        },
                        cancellationToken);
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1002"));
        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1003"));
    }

    [TestMethod]
    public async Task Analyzer_ConfigureAwaitFalse_ReportsEscapedAwaitDiagnostic()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public static class Scenario
            {
                public static Task RunAsync(CancellationToken cancellationToken)
                {
                    return Simulation.RunAsync(
                        new SimulationOptions(),
                        async context =>
                        {
                            await Task.Delay(
                                TimeSpan.FromSeconds(1),
                                context.TimeProvider,
                                context.CancellationToken).ConfigureAwait(false);
                        },
                        cancellationToken);
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1006"));
    }

    [TestMethod]
    public async Task Analyzer_ReqnrollProviderScenario_IsRecognizedAutomatically()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia.Reqnroll;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public static class Scenario
            {
                public static Task RunAsync(ReqnrollSimulationProvider provider, CancellationToken cancellationToken)
                {
                    return provider.RunAsync(
                        context =>
                        {
                            _ = DateTime.UtcNow;
                            return Task.CompletedTask;
                        },
                        cancellationToken);
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1001"));
    }

    [TestMethod]
    public async Task Analyzer_ModelSystemFactoryAndCommandExecution_AreRecognizedAutomatically()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia;
            using Causalia.ModelBased;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public static class Scenario
            {
                public static ModelBasedSpecification<int, object> Create()
                {
                    return new ModelBasedSpecification<int, object>(
                        "model",
                        static () => 0,
                        (context, cancellationToken) =>
                        {
                            _ = DateTime.UtcNow;
                            return Task.FromResult(new object());
                        },
                        state =>
                        [
                            ModelCommand.Create<int, object, int>(
                                "command",
                                current => current + 1,
                                async (_, context, cancellationToken) =>
                                {
                                    await Task.Run(() => 42, cancellationToken);
                                    return 1;
                                },
                                static (_, expected, observed) => ModelCommandVerification.Equal(expected, observed))
                        ]);
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1001"));
        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1003"));
    }

    [TestMethod]
    public async Task Analyzer_ModelDefinitionDelegates_AreRecognizedAutomatically()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia;
            using Causalia.ModelBased;
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public static class Scenario
            {
                public static ModelBasedSpecification<int, object> Create()
                {
                    return new ModelBasedSpecification<int, object>(
                        "model",
                        () => Random.Shared.Next(),
                        static (_, _) => Task.FromResult(new object()),
                        state =>
                        {
                            _ = Random.Shared.Next();
                            return
                            [
                                ModelCommand.Create<int, object, int>(
                                    "command",
                                    current => current + Random.Shared.Next(),
                                    static (_, _, _) => Task.FromResult(1),
                                    static (_, expected, observed) =>
                                    {
                                        var now = DateTime.UtcNow;
                                        return ModelCommandVerification.Equal(expected + now.Year - now.Year, observed);
                                    },
                                    _ => DateTime.UtcNow.Year > 2000)
                            ];
                        });
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(3, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1002"));
        Assert.AreEqual(2, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1001"));
    }

    [TestMethod]
    public async Task Analyzer_TimeTravelProbeDelegates_AreRecognizedAutomatically()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public static class Scenario
            {
                public static Task RunAsync(CancellationToken cancellationToken)
                {
                    return Simulation.RunAsync(
                        new SimulationOptions(),
                        context =>
                        {
                            context.TimeTravel.Watch(
                                "state",
                                () => DateTime.UtcNow,
                                value => value.ToString() + Random.Shared.Next());
                            context.TimeTravel.Checkpoint();
                            return Task.CompletedTask;
                        },
                        cancellationToken);
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1001"));
        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1002"));
    }

    [TestMethod]
    public async Task Analyzer_LoadIterationUsingWallClockAndTaskRun_ReportsDiagnostics()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia;
            using Causalia.Load;
            using Causalia.Load.Profiles;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public static class Scenario
            {
                public static Task RunAsync(CancellationToken cancellationToken)
                {
                    return Simulation.RunAsync(
                        new SimulationOptions(),
                        async context =>
                        {
                            await context.CreateLoadRunner().RunAsync(
                                new BurstLoadProfile { Iterations = 1 },
                                async iteration =>
                                {
                                    _ = DateTime.UtcNow;
                                    await Task.Run(() => 42, iteration.CancellationToken);
                                },
                                context.CancellationToken);
                        },
                        cancellationToken);
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1001"));
        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1003"));
    }

    [TestMethod]
    public async Task Analyzer_LoadIterationMethodGroupWithoutAttribute_ReportsCoverageDiagnostic()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia;
            using Causalia.Load;
            using Causalia.Load.Profiles;
            using System.Threading;
            using System.Threading.Tasks;

            public static class Scenario
            {
                public static Task RunAsync(CancellationToken cancellationToken)
                {
                    return Simulation.RunAsync(
                        new SimulationOptions(),
                        context => context.CreateLoadRunner().RunAsync(
                            new BurstLoadProfile { Iterations = 1 },
                            ExecuteAsync,
                            context.CancellationToken),
                        cancellationToken);
                }

                private static Task ExecuteAsync(LoadIterationContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1005"));
    }

    [TestMethod]
    public async Task Analyzer_MethodGroupScenarioWithoutAttribute_ReportsCoverageDiagnostic()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia;
            using System.Threading;
            using System.Threading.Tasks;

            public static class Scenario
            {
                public static Task RunAsync(CancellationToken cancellationToken)
                {
                    return Simulation.RunAsync(new SimulationOptions(), ExecuteAsync, cancellationToken);
                }

                private static Task ExecuteAsync(SimulationContext context)
                {
                    return Task.CompletedTask;
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1005"));
    }

    [TestMethod]
    public async Task Analyzer_DeterministicSimulationAttribute_AnalyzesHelperMethod()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia;
            using System;

            public static class Scenario
            {
                [DeterministicSimulation]
                public static Guid CreateId()
                {
                    return Guid.NewGuid();
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(1, diagnostics.Count(diagnostic => diagnostic.Id == "CAU1002"));
    }

    [TestMethod]
    public async Task Analyzer_AllowNondeterminismAttribute_SuppressesMethodDiagnostics()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using Causalia;
            using System;

            public static class Scenario
            {
                [DeterministicSimulation]
                [AllowNondeterminism("external clock is intentionally part of this boundary")]
                public static DateTime ReadClock()
                {
                    return DateTime.UtcNow;
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(0, diagnostics.Length);
    }

    [TestMethod]
    public async Task Analyzer_OrdinaryCodeOutsideSimulationBoundary_DoesNotReportDiagnostics()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using System;

            public static class OrdinaryCode
            {
                public static DateTime ReadClock()
                {
                    return DateTime.UtcNow;
                }
            }
            """,
            TestContext.CancellationToken);

        Assert.AreEqual(0, diagnostics.Length);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, CancellationToken cancellationToken)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
        var compilation = CSharpCompilation.Create(
            "AnalyzerTest",
            [syntaxTree],
            CreateReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzer = ImmutableArray.Create<DiagnosticAnalyzer>(new DeterminismAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzer);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
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
        references.Add(MetadataReference.CreateFromFile(typeof(Causalia.Load.SimulationLoadRunner).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Causalia.Testing.SimulationProvider).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Causalia.Reqnroll.ReqnrollSimulationProvider).Assembly.Location));
        return references;
    }
}
