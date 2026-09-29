using System.Text;
using Causalia.Tool.Inspection;

namespace Causalia.Tool.Generation;

/// <summary>Writes conservative, deterministic boundary scenarios from inspection findings.</summary>
internal static class ScenarioGenerator
{
    public static async Task WriteAsync(string directory, InspectionReport report, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(report);
        var candidates = report.BoundaryCandidates
            .OrderBy(candidate => candidate.FilePath, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Line)
            .ThenBy(candidate => candidate.Category, StringComparer.Ordinal)
            .GroupBy(candidate => candidate.Category, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var builder = new StringBuilder();
        builder.AppendLine("using Causalia;");
        builder.AppendLine("using Microsoft.VisualStudio.TestTools.UnitTesting;");
        if (candidates.Any(candidate => candidate.Category == "Azure Service Bus"))
        {
            builder.AppendLine("using Causalia.AzureServiceBus;");
            builder.AppendLine("using Causalia.Faults;");
        }
        builder.AppendLine();
        builder.AppendLine("namespace GeneratedCausalia;");
        builder.AppendLine();
        builder.AppendLine("[TestClass]");
        builder.AppendLine("public sealed class GeneratedBoundarySimulationTests");
        builder.AppendLine("{");
        builder.AppendLine("    public TestContext TestContext { get; set; } = null!;");
        builder.AppendLine();
        if (candidates.Length == 0)
            AppendScenario(builder, "UnrecognizedBoundary", "No recognized distributed boundary. Add a narrow application adapter.");
        else
            foreach (var candidate in candidates)
            {
                if (candidate.Category == "Azure Service Bus")
                    AppendServiceBusScenario(builder, candidate);
                else
                    AppendScenario(builder, Identifier(candidate.Category),
                        $"{candidate.Name} ({candidate.Category}) at {candidate.FilePath}:{candidate.Line}. {candidate.SuggestedScenario}");
            }
        builder.AppendLine("}");
        await File.WriteAllTextAsync(Path.Combine(directory, "GeneratedBoundarySimulationTests.cs"),
            builder.ToString(), cancellationToken);
    }

    private static void AppendScenario(StringBuilder builder, string name, string description)
    {
        builder.AppendLine("    [TestMethod]");
        builder.AppendLine($"    public async Task {name}_BoundaryCanRunDeterministically()");
        builder.AppendLine("    {");
        builder.AppendLine("        // TODO: Inject the real application entry point and a simulated boundary.");
        builder.AppendLine($"        // {SafeComment(description)}");
        builder.AppendLine("        var result = await Simulation.RunAsync(async context =>");
        builder.AppendLine("        {");
        builder.AppendLine("            await Task.Delay(TimeSpan.FromMilliseconds(10), context.TimeProvider, context.CancellationToken);");
        builder.AppendLine("            // TODO: Add the application-specific invariant and a typed fault plan.");
        builder.AppendLine("        }, TestContext.CancellationToken);");
        builder.AppendLine("        Assert.AreEqual(TimeSpan.FromMilliseconds(10), result.VirtualElapsed);");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void AppendServiceBusScenario(StringBuilder builder, BoundaryCandidate candidate)
    {
        builder.AppendLine("    [TestMethod]");
        builder.AppendLine("    public async Task AzureServiceBus_RedeliveryRequiresIdempotentHandler()");
        builder.AppendLine("    {");
        builder.AppendLine($"        // Candidate: {SafeComment(candidate.Name)} at {SafeComment(candidate.FilePath)}:{candidate.Line}.");
        builder.AppendLine("        // TODO: Replace the demonstration send/receive with the real handler and its injected boundary.");
        builder.AppendLine("        // TODO: Assert the application's durable business effect occurs exactly once.");
        builder.AppendLine("        var result = await Simulation.RunAsync(async context =>");
        builder.AppendLine("        {");
        builder.AppendLine("            var plan = new FaultPlan<ServiceBusSettlementEvent, ServiceBusSettlementFault>();");
        builder.AppendLine("            plan.On<ServiceBusSettlementEvent>(\"reject-first-complete\")");
        builder.AppendLine("                .Where(e => e.Action == ServiceBusSettlementAction.Complete)");
        builder.AppendLine("                .Once().Apply(_ => ServiceBusSettlementFault.Reject);");
        builder.AppendLine("            var broker = context.CreateAzureServiceBus(plan);");
        builder.AppendLine("            broker.CreateQueue(\"orders\", TimeSpan.FromSeconds(1));");
        builder.AppendLine("            await broker.SendAsync(\"orders\", new byte[] { 1 }, null, context.CancellationToken);");
        builder.AppendLine("            var first = (await broker.ReceiveAsync(\"orders\", context.CancellationToken))!;");
        builder.AppendLine("            try");
        builder.AppendLine("            {");
        builder.AppendLine("                await broker.CompleteAsync(\"orders\", first, context.CancellationToken);");
        builder.AppendLine("                Assert.Fail(\"Expected settlement rejection.\");");
        builder.AppendLine("            }");
        builder.AppendLine("            catch (SimulationServiceBusException e) when (!e.SettlementCommitted)");
        builder.AppendLine("            {");
        builder.AppendLine("                await Task.Delay(TimeSpan.FromSeconds(2), context.TimeProvider, context.CancellationToken);");
        builder.AppendLine("                var again = (await broker.ReceiveAsync(\"orders\", context.CancellationToken))!;");
        builder.AppendLine("                Assert.AreEqual(2, again.DeliveryCount);");
        builder.AppendLine("                await broker.CompleteAsync(\"orders\", again, context.CancellationToken);");
        builder.AppendLine("            }");
        builder.AppendLine("        }, TestContext.CancellationToken);");
        builder.AppendLine("        Assert.IsTrue(result.Trace.Any(e => e.Message.Contains(\"servicebus:lock-expired\")));");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static string Identifier(string value)
    {
        var characters = value.Where(char.IsLetterOrDigit).ToArray();
        return characters.Length == 0 ? "Unknown" : new string(characters);
    }

    private static string SafeComment(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
}
