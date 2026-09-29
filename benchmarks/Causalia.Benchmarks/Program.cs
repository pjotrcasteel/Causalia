using System.Diagnostics;
using System.Text.Json;
using Causalia;
using Causalia.AzureServiceBus;

const int iterations = 1000;
var cancellationToken = CancellationToken.None;
await RunAsync("empty-simulation", async () =>
{
    await Simulation.RunAsync(_ => Task.CompletedTask, cancellationToken);
});
await RunAsync("servicebus-send-receive-complete", async () =>
{
    await Simulation.RunAsync(async context =>
    {
        var broker = context.CreateAzureServiceBus();
        broker.CreateQueue("orders");
        await broker.SendAsync("orders", new byte[] { 1 }, null, context.CancellationToken);
        var delivery = await broker.ReceiveAsync("orders", context.CancellationToken)
            ?? throw new InvalidOperationException("Message was not delivered.");
        await broker.CompleteAsync("orders", delivery, context.CancellationToken);
    }, cancellationToken);
});

static async Task RunAsync(string name, Func<Task> scenario)
{
    for (var index = 0; index < 100; index++)
        await scenario();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    var beforeBytes = GC.GetTotalAllocatedBytes(precise: true);
    var started = Stopwatch.GetTimestamp();
    for (var index = 0; index < iterations; index++)
        await scenario();
    var elapsed = Stopwatch.GetElapsedTime(started);
    var bytes = GC.GetTotalAllocatedBytes(precise: true) - beforeBytes;
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        name,
        iterations,
        elapsedMilliseconds = elapsed.TotalMilliseconds,
        microsecondsPerScenario = elapsed.TotalMilliseconds * 1000 / iterations,
        allocatedBytesPerScenario = bytes / iterations
    }));
}
