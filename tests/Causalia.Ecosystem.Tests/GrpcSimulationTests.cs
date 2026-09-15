using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.Grpc.Tests;

[TestClass]
public sealed class GrpcSimulationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task VirtualDeadlineShouldFailLongUnaryCall()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 13004 },
            async context =>
            {
                var network = context.CreateGrpcNetwork();
                var server = network.CreateServer("payments");
                server.RegisterUnary<string, string>(
                    "Payments",
                    "Authorize",
                    async (request, cancellationToken) =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), context.TimeProvider, cancellationToken);
                        return request;
                    });
                network.Register("payments", server);
                var client = network.CreateClient("orders", "payments");
                SimulationGrpcException? exception = null;

                try
                {
                    await client.UnaryAsync<string, string>(
                        "Payments",
                        "Authorize",
                        "request",
                        new SimulationGrpcCallOptions { Timeout = TimeSpan.FromSeconds(1) },
                        context.CancellationToken);
                }
                catch (SimulationGrpcException caught)
                {
                    exception = caught;
                }

                Assert.IsNotNull(exception);
                Assert.AreEqual(typeof(SimulationGrpcException), exception.GetType());
                Assert.AreEqual(SimulationGrpcStatusCode.DeadlineExceeded, exception.StatusCode);
            },
            TestContext.CancellationToken);
    }
}
