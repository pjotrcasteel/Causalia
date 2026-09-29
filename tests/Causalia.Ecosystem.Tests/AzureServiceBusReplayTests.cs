using Causalia.AzureServiceBus;
using Causalia.Exceptions;
using Causalia.Faults;
using Causalia.Minimization;

namespace Causalia.Ecosystem.Tests;

[TestClass]
public sealed class AzureServiceBusReplayTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task MinimizeAsync_RemovesUnnecessarySettlementFault_AndReproducesFailure()
    {
        var options = new SimulationOptions { Seed = 2200 };
        var failure = await Assert.ThrowsExactlyAsync<SimulationFailedException>(() =>
            Simulation.RunAsync(options, ScenarioAsync, TestContext.CancellationToken));
        Assert.AreEqual(2, failure.Faults.Count);

        var result = await Simulation.MinimizeAsync(
            options,
            new MinimizationOptions { MaxAttempts = 100 },
            failure,
            ScenarioAsync,
            TestContext.CancellationToken);
        Assert.AreEqual(1, result.EssentialFaultCount);
        Assert.AreEqual("reject-second", result.Reproduction.Faults[0].PolicyName);

        var replay = await Assert.ThrowsExactlyAsync<SimulationFailedException>(() =>
            Simulation.ReproduceAsync(options, result.Reproduction, ScenarioAsync, TestContext.CancellationToken));
        Assert.AreEqual("settlement rejected", replay.InnerException?.Message);
    }

    private static async Task ScenarioAsync(SimulationContext context)
    {
        var plan = new FaultPlan<ServiceBusSettlementEvent, ServiceBusSettlementFault>();
        plan.On<ServiceBusSettlementEvent>("lose-first")
            .Where(value => value.SequenceNumber == 1)
            .Once().Apply(_ => ServiceBusSettlementFault.LoseAcknowledgement);
        plan.On<ServiceBusSettlementEvent>("reject-second")
            .Where(value => value.SequenceNumber == 2)
            .Once().Apply(_ => ServiceBusSettlementFault.Reject);
        var broker = context.CreateAzureServiceBus(plan);
        broker.CreateQueue("orders");
        await broker.SendAsync("orders", new byte[] { 1 }, null, context.CancellationToken);
        await broker.SendAsync("orders", new byte[] { 2 }, null, context.CancellationToken);
        for (var index = 0; index < 2; index++)
        {
            var delivery = (await broker.ReceiveAsync("orders", context.CancellationToken))!;
            try
            {
                await broker.CompleteAsync("orders", delivery, context.CancellationToken);
            }
            catch (SimulationServiceBusException exception) when (exception.SettlementCommitted)
            {
                // A lost acknowledgement did not undo the broker settlement.
            }
            catch (SimulationServiceBusException)
            {
                throw new InvalidOperationException("settlement rejected");
            }
        }
    }
}
