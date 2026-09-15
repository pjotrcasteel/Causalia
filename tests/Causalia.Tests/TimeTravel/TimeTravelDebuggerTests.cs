using Causalia.Exceptions;
using Causalia.TimeTravel;

namespace Causalia.Tests.TimeTravel;

[TestClass]
public sealed class TimeTravelDebuggerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_WithManualCheckpoint_CapturesRegisteredState()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 1801 },
            context =>
            {
                var value = 41;
                context.TimeTravel.Watch("counter", () => value.ToString());
                var token = context.TimeTravel.Checkpoint("before-increment");
                value++;

                Assert.AreEqual("tt1:0", token);
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.AreEqual(1, result.TimeTravel.Checkpoints.Count);
        var checkpoint = result.TimeTravel.Checkpoints[0];
        Assert.AreEqual("before-increment", checkpoint.Label);
        Assert.AreEqual("41", checkpoint.FindState("counter")?.Value);
    }

    [TestMethod]
    public async Task RunAsync_WithSchedulerCapture_CreatesNavigableStateHistory()
    {
        var result = await Simulation.RunAsync(
            CreateAutomaticOptions(1802),
            RunChangingScenarioAsync,
            TestContext.CancellationToken);

        Assert.IsTrue(result.TimeTravel.Checkpoints.Count >= 6);
        Assert.AreEqual(TimeTravelCheckpointKind.Start, result.TimeTravel.First?.Kind);
        Assert.AreEqual(TimeTravelCheckpointKind.Completed, result.TimeTravel.Last?.Kind);
        var debugger = result.TimeTravel.CreateDebugger();
        Assert.IsTrue(debugger.MoveToPreviousChange("counter"));
        Assert.IsNotNull(debugger.Current);
        Assert.IsTrue(debugger.MoveToNextChange("counter"));
        Assert.AreEqual("2", debugger.Current?.FindState("counter")?.Value);
    }

    [TestMethod]
    public async Task ReplayAsync_WithTimeTravel_ProducesSameCheckpointTokensAndState()
    {
        var options = CreateAutomaticOptions(1803);
        var first = await Simulation.RunAsync(options, RunChangingScenarioAsync, TestContext.CancellationToken);
        var replay = await Simulation.ReplayAsync(
            options,
            first.Schedule,
            RunChangingScenarioAsync,
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            first.TimeTravel.Checkpoints.Select(value => value.Token).ToList(),
            replay.TimeTravel.Checkpoints.Select(value => value.Token).ToList());
        CollectionAssert.AreEqual(
            first.TimeTravel.Checkpoints.Select(value => value.FindState("counter")?.Value).ToList(),
            replay.TimeTravel.Checkpoints.Select(value => value.FindState("counter")?.Value).ToList());
    }

    [TestMethod]
    public async Task RunAsync_WhenProbeThrows_RecordsProbeFailureWithoutFailingSimulation()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 1804 },
            context =>
            {
                context.TimeTravel.Watch("broken", static () => throw new InvalidOperationException("probe-bug"));
                context.TimeTravel.Checkpoint();
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        var snapshot = result.TimeTravel.Checkpoints[0].FindState("broken");
        Assert.IsNotNull(snapshot);
        Assert.IsFalse(snapshot.Succeeded);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, snapshot.ErrorType);
        Assert.AreEqual("probe-bug", snapshot.ErrorMessage);
    }

    [TestMethod]
    public async Task RunAsync_WithRetentionLimit_DropsOldestCheckpointsWithoutChangingTokens()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions
            {
                Seed = 1805,
                TimeTravel = new TimeTravelOptions { MaxRetainedCheckpoints = 2 }
            },
            context =>
            {
                context.TimeTravel.Checkpoint("one");
                context.TimeTravel.Checkpoint("two");
                context.TimeTravel.Checkpoint("three");
                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.AreEqual(3L, result.TimeTravel.TotalCheckpointCount);
        Assert.AreEqual(1L, result.TimeTravel.DroppedCheckpointCount);
        Assert.IsTrue(result.TimeTravel.Truncated);
        CollectionAssert.AreEqual(new[] { "tt1:1", "tt1:2" }, result.TimeTravel.Checkpoints.Select(value => value.Token).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_WhenScenarioFails_RetainsFailureCheckpoint()
    {
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(async () =>
            await Simulation.RunAsync(
                CreateAutomaticOptions(1806),
                async context =>
                {
                    var state = "before";
                    context.TimeTravel.Watch("state", () => state);
                    await Task.Yield();
                    state = "failed";
                    throw new InvalidOperationException("boom");
                },
                TestContext.CancellationToken));

        Assert.AreEqual(TimeTravelCheckpointKind.Failure, failure.TimeTravel.Last?.Kind);
        Assert.AreEqual("failed", failure.TimeTravel.Last?.FindState("state")?.Value);
    }

    [TestMethod]
    public async Task Debugger_SeekTraceEvent_NavigatesBeforeAndAfterCausalEvent()
    {
        var result = await Simulation.RunAsync(
            CreateAutomaticOptions(1807),
            async context =>
            {
                var value = 0;
                context.TimeTravel.Watch("counter", () => value.ToString());
                await Task.Yield();
                context.TraceEvent("counter:changing");
                value = 1;
                await Task.Yield();
            },
            TestContext.CancellationToken);
        var eventIndex = result.Trace.ToList().FindIndex(value => value.Message == "counter:changing");
        var debugger = result.TimeTravel.CreateDebugger();

        Assert.IsTrue(debugger.SeekTraceEvent(eventIndex, TimeTravelTraceSeekMode.BeforeEvent));
        var before = debugger.Current;
        Assert.IsTrue(debugger.SeekTraceEvent(eventIndex, TimeTravelTraceSeekMode.AfterEvent));
        var after = debugger.Current;

        Assert.IsNotNull(before);
        Assert.IsNotNull(after);
        Assert.IsTrue(before.TraceCount <= eventIndex);
        Assert.IsTrue(after.TraceCount > eventIndex);
    }

    private static SimulationOptions CreateAutomaticOptions(ulong seed)
    {
        return new SimulationOptions
        {
            Seed = seed,
            TimeTravel = new TimeTravelOptions
            {
                CaptureMode = TimeTravelCaptureMode.SchedulerSteps
            }
        };
    }

    [DeterministicSimulation]
    private static async Task RunChangingScenarioAsync(SimulationContext context)
    {
        var value = 0;
        context.TimeTravel.Watch("counter", () => value.ToString());
        await Task.Yield();
        value = 1;
        await Task.Yield();
        value = 2;
    }
}
