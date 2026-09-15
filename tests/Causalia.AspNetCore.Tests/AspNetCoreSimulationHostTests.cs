using System.Net;
using Causalia.Scheduling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.AspNetCore.Tests;

[TestClass]
public sealed class AspNetCoreSimulationHostTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SendAsyncShouldExecuteMinimalApiInMemory()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 12001 },
            async context =>
            {
                await using var host = await context.StartAspNetCoreAsync(
                    app => app.MapGet("/hello/{name}", (string name) => Results.Text($"hello {name}")),
                    context.CancellationToken);
                using var request = new HttpRequestMessage(HttpMethod.Get, "/hello/causalia");
                using var response = await host.SendAsync(request, context.CancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual("hello causalia", await response.Content.ReadAsStringAsync(context.CancellationToken));
            },
            TestContext.CancellationToken);

        Assert.IsTrue(result.Trace.Any(entry => entry.Message.Contains("aspnetcore:request:completed", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task InjectedTimeProviderShouldUseVirtualTime()
    {
        var start = new DateTimeOffset(2040, 4, 5, 6, 7, 8, TimeSpan.Zero);
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 12002, StartTime = start },
            async context =>
            {
                await using var host = await context.StartAspNetCoreAsync(
                    app => app.MapGet(
                        "/wait",
                        async (TimeProvider timeProvider, HttpContext httpContext) =>
                        {
                            await Task.Delay(TimeSpan.FromHours(4), timeProvider, httpContext.RequestAborted);
                            return Results.Ok(timeProvider.GetUtcNow());
                        }),
                    context.CancellationToken);
                using var request = new HttpRequestMessage(HttpMethod.Get, "/wait");
                using var response = await host.SendAsync(request, context.CancellationToken);
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(start.AddHours(4), context.TimeProvider.GetUtcNow());
            },
            TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromHours(4), result.VirtualElapsed);
    }

    [TestMethod]
    public async Task CrashShouldInterruptRequestAndRestartShouldAllowNextRequest()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 12003 },
            async context =>
            {
                await using var host = await context.StartAspNetCoreAsync(
                    new AspNetCoreSimulationHostOptions { NodeName = "api" },
                    null,
                    app => app.MapGet(
                        "/slow",
                        async (TimeProvider timeProvider, HttpContext httpContext) =>
                        {
                            await Task.Delay(TimeSpan.FromDays(1), timeProvider, httpContext.RequestAborted);
                            return Results.Ok();
                        }),
                    context.CancellationToken);
                using var firstRequest = new HttpRequestMessage(HttpMethod.Get, "/slow");
                var firstRequestTask = host.SendAsync(firstRequest, context.CancellationToken);
                await Task.Yield();
                Assert.IsTrue(host.Node.Crash());
                TaskCanceledException? cancellation = null;

                try
                {
                    await firstRequestTask;
                }
                catch (TaskCanceledException exception)
                {
                    cancellation = exception;
                }

                Assert.IsNotNull(cancellation);
                Assert.AreEqual(typeof(TaskCanceledException), cancellation.GetType());
                Assert.IsTrue(host.Node.Restart());
                using var secondRequest = new HttpRequestMessage(HttpMethod.Get, "/slow");
                using var secondResponse = await host.SendAsync(secondRequest, context.CancellationToken);
                Assert.IsTrue(secondResponse.IsSuccessStatusCode);
            },
            TestContext.CancellationToken);
    }


    [TestMethod]
    public async Task CreateClientShouldRemainDeterministicAcrossVirtualDelay()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 12005 },
            async context =>
            {
                await using var host = await context.StartAspNetCoreAsync(
                    app => app.MapGet(
                        "/delay",
                        async (TimeProvider timeProvider, CancellationToken cancellationToken) =>
                        {
                            await Task.Delay(TimeSpan.FromHours(2), timeProvider, cancellationToken);
                            return Results.Ok();
                        }),
                    context.CancellationToken);
                using var client = host.CreateClient();
                using var response = await client.GetAsync("/delay", context.CancellationToken);
                Assert.IsTrue(response.IsSuccessStatusCode);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromHours(2), result.VirtualElapsed);
    }

    [TestMethod]
    public async Task ConcurrentRequestsShouldParticipateInSystematicExploration()
    {
        var observedOrders = new HashSet<string>(StringComparer.Ordinal);
        var result = await Simulation.ExploreAsync(
            new ExplorationOptions
            {
                Simulation = new SimulationOptions { Seed = 12004 },
                MaxSchedules = 50,
                MaxDecisionDepth = 20
            },
            async context =>
            {
                var order = new List<string>();
                await using var host = await context.StartAspNetCoreAsync(
                    app => app.MapGet(
                        "/work/{id}",
                        async (string id) =>
                        {
                            order.Add($"{id}:start");
                            await Task.Yield();
                            order.Add($"{id}:end");
                            return Results.Ok();
                        }),
                    context.CancellationToken);
                await context.ConcurrentAsync(
                    cancellationToken => SendAsync(host, "/work/a", cancellationToken),
                    cancellationToken => SendAsync(host, "/work/b", cancellationToken),
                    context.CancellationToken);
                observedOrders.Add(string.Join(",", order));
            },
            TestContext.CancellationToken);

        Assert.IsTrue(result.SchedulesExplored > 1);
        Assert.IsTrue(observedOrders.Count > 1);
    }

    private static async Task SendAsync(AspNetCoreSimulationHost host, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await host.SendAsync(request, cancellationToken);
        Assert.IsTrue(response.IsSuccessStatusCode);
    }
}
