using Causalia.AspNetCore;
using Causalia.AspNetCore.Networking;
using Causalia.Processes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.AspNetCore.Tests;

[TestClass]
public sealed class AspNetCoreSimulationProcessTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RestartRebuildsDiAndExistingClientTargetsNewGeneration()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 1203 },
            async context =>
            {
                await using var process = await context.StartAspNetCoreProcessAsync(
                    new AspNetCoreSimulationHostOptions
                    {
                        NodeName = "api",
                        BaseAddress = new Uri("http://api.causalia.local/")
                    },
                    builder => builder.Services.AddSingleton<VolatileMarker>(),
                    app =>
                    {
                        app.MapGet(
                            "/generation",
                            (Causalia.Processes.SimulationProcessGenerationContext generation) => Results.Ok(generation.Generation));
                    },
                    TestContext.CancellationToken);

                using var client = process.CreateClient();
                var first = await client.GetStringAsync("/generation", TestContext.CancellationToken);
                var firstProvider = process.Services;
                var firstMarker = firstProvider.GetRequiredService<VolatileMarker>();

                await process.CrashAsync(TestContext.CancellationToken);
                await process.RestartAsync(TestContext.CancellationToken);

                var second = await client.GetStringAsync("/generation", TestContext.CancellationToken);
                var secondProvider = process.Services;
                var secondMarker = secondProvider.GetRequiredService<VolatileMarker>();
                Assert.AreNotEqual(first, second);
                Assert.AreNotSame(firstProvider, secondProvider);
                Assert.AreNotSame(firstMarker, secondMarker);
                Assert.AreEqual("2", second);
            },
            TestContext.CancellationToken);

        Assert.IsTrue(result.Trace.Any(entry => entry.Message.StartsWith("process:crashed:api", StringComparison.Ordinal)));
        Assert.IsTrue(result.Trace.Any(entry => entry.Message.StartsWith("process:running:api:generation:2", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task CrashSkipsHostedServiceStopButGracefulStopInvokesIt()
    {
        var observer = new LifecycleObserver();

        await Simulation.RunAsync(
            new SimulationOptions { Seed = 1204 },
            async context =>
            {
                await using var process = await context.StartAspNetCoreProcessAsync(
                    new AspNetCoreSimulationHostOptions { NodeName = "api" },
                    builder =>
                    {
                        builder.Services.AddSingleton(observer);
                        builder.Services.AddHostedService<LifecycleProbe>();
                    },
                    app => app.MapGet("/", () => Results.Ok()),
                    TestContext.CancellationToken);

                Assert.AreEqual(1, observer.StartCount);
                await process.CrashAsync(TestContext.CancellationToken);
                Assert.AreEqual(0, observer.StopCount);
                Assert.AreEqual(1, observer.DisposeCount);

                await process.RestartAsync(TestContext.CancellationToken);
                Assert.AreEqual(2, observer.StartCount);
                await process.StopAsync(TestContext.CancellationToken);
                Assert.AreEqual(1, observer.StopCount);
                Assert.AreEqual(2, observer.DisposeCount);
            },
            TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task NetworkRegistrationSurvivesDestinationProcessRestart()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 1205 },
            async context =>
            {
                await using var source = await context.StartAspNetCoreProcessAsync(
                    new AspNetCoreSimulationHostOptions
                    {
                        NodeName = "source",
                        BaseAddress = new Uri("http://source.causalia.local/")
                    },
                    null,
                    app => app.MapGet("/", () => Results.Ok()),
                    TestContext.CancellationToken);

                await using var destination = await context.StartAspNetCoreProcessAsync(
                    new AspNetCoreSimulationHostOptions
                    {
                        NodeName = "destination",
                        BaseAddress = new Uri("http://destination.causalia.local/")
                    },
                    null,
                    app => app.MapGet(
                        "/generation",
                        (Causalia.Processes.SimulationProcessGenerationContext generation) => Results.Ok(generation.Generation)),
                    TestContext.CancellationToken);

                var network = context.CreateHttpNetwork();
                network.Register("source", source);
                network.Register("destination", destination);
                network.Between("source", "destination").Latency(TimeSpan.FromMilliseconds(10));
                using var client = network.CreateClient("source", "destination");
                var first = await client.GetStringAsync("/generation", TestContext.CancellationToken);
                await destination.CrashAsync(TestContext.CancellationToken);
                await destination.RestartAsync(TestContext.CancellationToken);
                var second = await client.GetStringAsync("/generation", TestContext.CancellationToken);
                Assert.AreNotEqual(first, second);
                Assert.AreEqual("2", second);
            },
            TestContext.CancellationToken);
    }

    private sealed class VolatileMarker
    {
    }

    private sealed class LifecycleObserver
    {
        public int StartCount { get; set; }

        public int StopCount { get; set; }

        public int DisposeCount { get; set; }
    }

    private sealed class LifecycleProbe : IHostedService, IDisposable
    {
        private readonly LifecycleObserver _observer;

        public LifecycleProbe(LifecycleObserver observer)
        {
            _observer = observer;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _observer.StartCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _observer.StopCount++;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _observer.DisposeCount++;
        }
    }
}
