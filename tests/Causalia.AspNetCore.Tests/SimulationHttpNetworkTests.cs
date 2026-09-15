using System.Net;
using System.Text;
using Causalia.AspNetCore.Networking.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.AspNetCore.Tests;

[TestClass]
public sealed class SimulationHttpNetworkTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CreateClientShouldRouteToRegisteredDestination()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 13001 },
            async context =>
            {
                var network = context.CreateHttpNetwork();
                await using var orders = await StartHostAsync(context, "orders", "http://orders.local/", app => app.MapGet("/health", () => "orders"));
                await using var payments = await StartHostAsync(
                    context,
                    "payments",
                    "http://payments.local/",
                    app => app.MapGet("/health", () => "payments"));
                network.Register("orders", orders);
                network.Register("payments", payments);
                using var client = network.CreateClient("orders", "payments");
                using var response = await client.GetAsync("/health", context.CancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual("payments", await response.Content.ReadAsStringAsync(context.CancellationToken));
            },
            TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task SendAsyncShouldRetainTheCallersRequestOnTheResponse()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 130011 },
            async context =>
            {
                var network = context.CreateHttpNetwork();
                await using var source = await StartHostAsync(context, "source", "http://source.local/", _ => { });
                await using var destination = await StartHostAsync(
                    context,
                    "destination",
                    "http://destination.local/",
                    app => app.MapGet("/", () => "ok"));
                network.Register("source", source);
                network.Register("destination", destination);
                using var client = network.CreateClient("source", "destination");
                using var request = new HttpRequestMessage(HttpMethod.Get, "/");
                using var response = await client.SendAsync(request, context.CancellationToken);

                Assert.AreSame(request, response.RequestMessage);
            },
            TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task LatencyShouldAdvanceVirtualTime()
    {
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 13002 },
            async context =>
            {
                var network = context.CreateHttpNetwork();
                await using var source = await StartHostAsync(context, "source", "http://source.local/", _ => { });
                await using var destination = await StartHostAsync(
                    context,
                    "destination",
                    "http://destination.local/",
                    app => app.MapGet("/", () => "ok"));
                network.Register("source", source);
                network.Register("destination", destination);
                network.Between("source", "destination").Latency(TimeSpan.FromHours(6));
                using var client = network.CreateClient("source", "destination");
                using var response = await client.GetAsync("/", context.CancellationToken);
                Assert.IsTrue(response.IsSuccessStatusCode);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromHours(6), result.VirtualElapsed);
    }

    [TestMethod]
    public async Task PartitionShouldRejectTrafficUntilLinkIsHealed()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 13003 },
            async context =>
            {
                var network = context.CreateHttpNetwork();
                await using var source = await StartHostAsync(context, "source", "http://source.local/", _ => { });
                await using var destination = await StartHostAsync(
                    context,
                    "destination",
                    "http://destination.local/",
                    app => app.MapGet("/", () => "ok"));
                network.Register("source", source);
                network.Register("destination", destination);
                var link = network.Between("source", "destination").Partition();
                using var client = network.CreateClient("source", "destination");
                await Assert.ThrowsExactlyAsync<SimulationNetworkPartitionException>(async () =>
                    await client.GetAsync("/", context.CancellationToken));
                link.Heal();
                using var response = await client.GetAsync("/", context.CancellationToken);
                Assert.IsTrue(response.IsSuccessStatusCode);
            },
            TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task DropFaultShouldRejectSelectedRequest()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 13004 },
            async context =>
            {
                var network = context.CreateHttpNetwork();
                await using var source = await StartHostAsync(context, "source", "http://source.local/", _ => { });
                await using var destination = await StartHostAsync(
                    context,
                    "destination",
                    "http://destination.local/",
                    app => app.MapGet("/", () => "ok"));
                network.Register("source", source);
                network.Register("destination", destination);
                network.Between("source", "destination").Drop(1);
                using var client = network.CreateClient("source", "destination");
                await Assert.ThrowsExactlyAsync<SimulationNetworkRequestDroppedException>(async () =>
                    await client.GetAsync("/", context.CancellationToken));
            },
            TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task DuplicateFaultShouldDeliverIndependentCopiesWithRequestBody()
    {
        var bodies = new List<string>();
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 13005 },
            async context =>
            {
                var network = context.CreateHttpNetwork();
                await using var source = await StartHostAsync(context, "source", "http://source.local/", _ => { });
                await using var destination = await StartHostAsync(
                    context,
                    "destination",
                    "http://destination.local/",
                    app => app.MapPost(
                        "/work",
                        async (HttpRequest request, CancellationToken cancellationToken) =>
                        {
                            using var reader = new StreamReader(request.Body, Encoding.UTF8);
                            bodies.Add(await reader.ReadToEndAsync(cancellationToken));
                            return Results.Ok();
                        }));
                network.Register("source", source);
                network.Register("destination", destination);
                network.Between("source", "destination").Duplicate(1);
                using var client = network.CreateClient("source", "destination");
                using var content = new StringContent("payload", Encoding.UTF8, "text/plain");
                using var response = await client.PostAsync("/work", content, context.CancellationToken);
                Assert.IsTrue(response.IsSuccessStatusCode);
            },
            TestContext.CancellationToken);

        CollectionAssert.AreEquivalent(new List<string> { "payload", "payload" }, bodies);
    }


    [TestMethod]
    public async Task HostDisposalShouldWaitForSlowDuplicateDelivery()
    {
        var deliveries = 0;
        var result = await Simulation.RunAsync(
            new SimulationOptions { Seed = 130051 },
            async context =>
            {
                var network = context.CreateHttpNetwork();
                await using var source = await StartHostAsync(context, "source-slow", "http://source-slow.local/", _ => { });
                await using var destination = await StartHostAsync(
                    context,
                    "destination-slow",
                    "http://destination-slow.local/",
                    app => app.MapPost(
                        "/work",
                        async (TimeProvider timeProvider, CancellationToken cancellationToken) =>
                        {
                            deliveries++;

                            if (deliveries == 2)
                            {
                                await Task.Delay(TimeSpan.FromHours(1), timeProvider, cancellationToken);
                            }

                            return Results.Ok();
                        }));
                network.Register("source-slow", source);
                network.Register("destination-slow", destination);
                network.Between("source-slow", "destination-slow").Duplicate(1);
                using var client = network.CreateClient("source-slow", "destination-slow");
                using var response = await client.PostAsync("/work", content: null, context.CancellationToken);
                Assert.IsTrue(response.IsSuccessStatusCode);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(2, deliveries);
        Assert.AreEqual(TimeSpan.FromHours(1), result.VirtualElapsed);
    }

    [TestMethod]
    public async Task EndpointShouldCallAnotherHostedServiceThroughNetworkClient()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 13006 },
            async context =>
            {
                var network = context.CreateHttpNetwork();
                await using var payments = await StartHostAsync(
                    context,
                    "payments",
                    "http://payments.local/",
                    app => app.MapGet("/authorize/{id}", (int id) => Results.Text($"authorized:{id}")));
                network.Register("payments", payments);
                await using var orders = await context.StartAspNetCoreAsync(
                    new AspNetCoreSimulationHostOptions { NodeName = "orders", BaseAddress = new Uri("http://orders.local/") },
                    builder => builder.Services.AddSingleton(_ => network.CreateClient("orders", "payments")),
                    app => app.MapGet(
                        "/orders/{id}",
                        async (int id, HttpClient client, CancellationToken cancellationToken) =>
                        {
                            using var paymentResponse = await client.GetAsync($"/authorize/{id}", cancellationToken);
                            return Results.Text(await paymentResponse.Content.ReadAsStringAsync(cancellationToken));
                        }),
                    context.CancellationToken);
                network.Register("orders", orders);
                using var client = orders.CreateClient();
                using var response = await client.GetAsync("/orders/42", context.CancellationToken);
                Assert.AreEqual("authorized:42", await response.Content.ReadAsStringAsync(context.CancellationToken));
            },
            TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task CrashedDestinationShouldRejectRequestUntilRestarted()
    {
        await Simulation.RunAsync(
            new SimulationOptions { Seed = 13007 },
            async context =>
            {
                var network = context.CreateHttpNetwork();
                await using var source = await StartHostAsync(context, "source", "http://source.local/", _ => { });
                await using var destination = await StartHostAsync(
                    context,
                    "destination",
                    "http://destination.local/",
                    app => app.MapGet("/", () => "ok"));
                network.Register("source", source);
                network.Register("destination", destination);
                destination.Node.Crash();
                using var client = network.CreateClient("source", "destination");
                await Assert.ThrowsExactlyAsync<SimulationNetworkNodeUnavailableException>(async () =>
                    await client.GetAsync("/", context.CancellationToken));
                destination.Node.Restart();
                using var response = await client.GetAsync("/", context.CancellationToken);
                Assert.IsTrue(response.IsSuccessStatusCode);
            },
            TestContext.CancellationToken);
    }

    private static Task<AspNetCoreSimulationHost> StartHostAsync(
        SimulationContext context,
        string nodeName,
        string baseAddress,
        Action<WebApplication> configureApplication)
    {
        return context.StartAspNetCoreAsync(
            new AspNetCoreSimulationHostOptions { NodeName = nodeName, BaseAddress = new Uri(baseAddress) },
            null,
            configureApplication,
            context.CancellationToken);
    }
}
