using Causalia.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.Grpc.Tests;

[TestClass]
public sealed class GrpcDeadlineAndCancellationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Deadline_WhenHandlerIgnoresCallCancellation_EndsClientWaitAndTracksLateEffects()
    {
        var effects = 0;
        var result = await Simulation.RunAsync(async context =>
        {
            var network = context.CreateGrpcNetwork();
            var server = network.CreateServer("payments");
            server.RegisterUnary<string, string>("Payments", "Charge", async (request, cancellationToken) =>
            {
                // Model a server that continues committing after the client abandons the request.
                await Task.Delay(TimeSpan.FromSeconds(10), context.TimeProvider, context.CancellationToken);
                effects++;
                return request;
            });
            network.Register("payments", server);
            var client = network.CreateClient("orders", "payments");
            var startedAt = context.TimeProvider.GetTimestamp();
            SimulationGrpcException? failure = null;

            try
            {
                await client.UnaryAsync<string, string>("Payments", "Charge", "order",
                    new SimulationGrpcCallOptions { Timeout = TimeSpan.FromSeconds(1) }, context.CancellationToken);
            }
            catch (SimulationGrpcException exception)
            {
                failure = exception;
            }

            Assert.IsNotNull(failure);
            Assert.AreEqual(SimulationGrpcStatusCode.DeadlineExceeded, failure.StatusCode);
            Assert.AreEqual(TimeSpan.FromSeconds(1), context.TimeProvider.GetElapsedTime(startedAt));
            Assert.AreEqual(0, effects);
        }, TestContext.CancellationToken);

        Assert.AreEqual(1, effects);
        Assert.AreEqual(TimeSpan.FromSeconds(10), result.VirtualElapsed);
    }

    [TestMethod]
    public async Task Deadline_WhenAbandonedServerFails_ObservesFailureWithoutReplacingDeadline()
    {
        var result = await Simulation.RunAsync(async context =>
        {
            var network = context.CreateGrpcNetwork();
            var server = network.CreateServer("payments");
            server.RegisterUnary<string, string>("Payments", "Charge", async (request, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), context.TimeProvider, context.CancellationToken);
                throw new InvalidOperationException("Late server failure.");
            });
            network.Register("payments", server);
            SimulationGrpcException? failure = null;

            try
            {
                await network.CreateClient("orders", "payments").UnaryAsync<string, string>("Payments", "Charge", "order",
                    new SimulationGrpcCallOptions { Timeout = TimeSpan.FromSeconds(1) }, context.CancellationToken);
            }
            catch (SimulationGrpcException exception)
            {
                failure = exception;
            }

            Assert.IsNotNull(failure);
            Assert.AreEqual(SimulationGrpcStatusCode.DeadlineExceeded, failure.StatusCode);
        }, TestContext.CancellationToken);

        Assert.IsTrue(result.Trace.Any(entry => entry.Message.Contains("server-failed-after-cancellation", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task UnaryAsync_WhenAlreadyCancelled_DoesNotInvokeHandler()
    {
        await Simulation.RunAsync(async context =>
        {
            var network = context.CreateGrpcNetwork();
            var server = network.CreateServer("payments");
            var calls = 0;
            server.RegisterUnary<string, string>("Payments", "Charge", (request, cancellationToken) =>
            {
                calls++;
                return Task.FromResult(request);
            });
            network.Register("payments", server);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cancellation.Cancel();
            var cancelled = false;

            try
            {
                await network.CreateClient("orders", "payments").UnaryAsync<string, string>("Payments", "Charge", "order", null, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled);
            Assert.AreEqual(0, calls);
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task UnaryAsync_WhenCallerCancelsNonCooperativeHandler_StopsWaitingAtCancellation()
    {
        await Simulation.RunAsync(async context =>
        {
            var network = context.CreateGrpcNetwork();
            var server = network.CreateServer("payments");
            server.RegisterUnary<string, string>("Payments", "Charge", async (request, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), context.TimeProvider, context.CancellationToken);
                return request;
            });
            network.Register("payments", server);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1), context.TimeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellation.Token);
            var startedAt = context.TimeProvider.GetTimestamp();
            var cancelled = false;

            try
            {
                await network.CreateClient("orders", "payments").UnaryAsync<string, string>("Payments", "Charge", "order", null, linked.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled);
            Assert.AreEqual(TimeSpan.FromSeconds(1), context.TimeProvider.GetElapsedTime(startedAt));
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task UnaryAsync_WhenDestinationBecomesUnavailableDuringLatency_DoesNotDispatch(bool crashNode)
    {
        await Simulation.RunAsync(async context =>
        {
            var network = context.CreateGrpcNetwork();
            var node = context.CreateNode("payments");
            var server = network.CreateServer("payments", node);
            var calls = 0;
            server.RegisterUnary<string, string>("Payments", "Charge", (request, cancellationToken) =>
            {
                calls++;
                return Task.FromResult(request);
            });
            network.Register("payments", server);
            var link = network.Between("orders", "payments");
            link.Latency = TimeSpan.FromSeconds(2);
            var call = network.CreateClient("orders", "payments").UnaryAsync<string, string>(
                "Payments", "Charge", "order", null, context.CancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), context.TimeProvider, context.CancellationToken);

            if (crashNode)
            {
                node.Crash();
            }
            else
            {
                link.Partition();
            }

            SimulationGrpcException? failure = null;

            try
            {
                await call;
            }
            catch (SimulationGrpcException exception)
            {
                failure = exception;
            }

            Assert.IsNotNull(failure);
            Assert.AreEqual(SimulationGrpcStatusCode.Unavailable, failure.StatusCode);
            Assert.AreEqual(0, calls);
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task GrpcNetwork_WhenObjectsBelongToAnotherContext_RejectsCrossContextUse()
    {
        SimulationNode? foreignNode = null;
        SimulationGrpcServer? foreignServer = null;
        await Simulation.RunAsync(context =>
        {
            foreignNode = context.CreateNode("foreign");
            foreignServer = context.CreateGrpcNetwork().CreateServer("foreign");
            return Task.CompletedTask;
        }, TestContext.CancellationToken);

        await Simulation.RunAsync(context =>
        {
            var network = context.CreateGrpcNetwork();
            Assert.ThrowsExactly<ArgumentException>(() => network.CreateServer("foreign", foreignNode));
            Assert.ThrowsExactly<ArgumentException>(() => network.Register("foreign", foreignServer!));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => network.Between("a", "b").Latency = TimeSpan.FromTicks(-1));
            return Task.CompletedTask;
        }, TestContext.CancellationToken);
    }
}
