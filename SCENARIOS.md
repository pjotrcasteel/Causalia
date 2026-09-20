# Causalia 2.1.1 — Scenarios and advanced reference

This is the scenario cookbook and complete advanced reference for Causalia. If you are new to the project, start with
[README.md](README.md): install the tool, run `inspect`, generate the first test with `init`, and get one deterministic simulation green.

Use this document when you already know the production symptom you want to reproduce or when you need the deeper verification APIs.
The complete source-backed example later in this document is checked by `eng/verify.py` against the real files under `examples/` and `tests/`.

<a id="scenario-index"></a>

## Scenario index

| Production symptom | Scenario to model | Causalia area |
| --- | --- | --- |
| Database call failed but may already have committed | [Lost acknowledgement after commit](#scenario-lost-db-ack) | `Causalia.Storage` |
| A message can be delivered more than once | [Duplicate delivery](#scenario-duplicate-delivery) | core messaging / broker adapters |
| Two operations update the same state | [Concurrent update / ETag race](#scenario-concurrent-update) | scheduling + storage |
| Inbox/outbox records are replayed | [Inbox/outbox replay](#scenario-outbox-replay) | storage + messaging |
| Dapr/Kafka/RabbitMQ retries after acknowledgement loss | [Broker redelivery](#scenario-broker-redelivery) | ecosystem adapters |
| A process dies halfway through work | [Service restart](#scenario-service-restart) | process lifecycle |
| HTTP timed out after the remote side may have completed | [Ambiguous HTTP outcome](#scenario-http-timeout) | ASP.NET Core networking |
| Replicas converge later | [Eventual consistency](#scenario-eventual-consistency) | invariants + consistency |
| Two workers compete for a lease | [Lease ownership race](#scenario-lease-race) | scheduling + storage |
| A race only appears under some interleavings | [Schedule exploration / DPOR](#scenario-exploration) | exploration |
| The failure is hard to explain | [Replay, minimization and failure intelligence](#scenario-failure-analysis) | failure intelligence |

<a id="scenario-lost-db-ack"></a>

## Lost acknowledgement after a database commit

**Production failure.** The durable write succeeds, but the caller never observes the success because the acknowledgement or connection is lost.
A retry is therefore ambiguous: repeating the operation blindly can duplicate a business effect.

**Why a normal unit test misses it.** A mock usually returns either success or failure before persistence. The important state — durable success plus observed
failure — is neither of those.

```csharp
var database = context.CreateStorageDatabase("orders");
var client = database.CreateClient(options: new SimulationStorageClientOptions
{
    Faults = new StorageFaultPlan().FailAfterCommit(1)
});

var service = new OrderService(new SimulatedOrderStore(client), context.TimeProvider);
await service.AcceptAsync("order-42", context.CancellationToken);
```

Assert the **domain guarantee**, not the number of calls: one accepted order, one reservation, one payment, one durable operation marker. A safe retry
re-reads durable state or uses an idempotency/version rule before repeating an external effect.

<a id="scenario-duplicate-delivery"></a>

## Duplicate message delivery

At-least-once delivery means the same logical command/event may reach the handler multiple times. Drive the real handler through a simulated message
boundary and make the invariant describe the externally visible result.

```csharp
context.Invariant("reservation is unique", () => reservationCount <= 1);
await bus.SendAsync("inventory", command, context.CancellationToken);
await bus.SendAsync("inventory", command, context.CancellationToken);
```

Multiple deliveries may be correct. Multiple business effects usually are not. Prefer a durable idempotency key, inbox record, state transition guard,
or another production mechanism instead of suppressing the second call only inside the test.

<a id="scenario-concurrent-update"></a>

## Concurrent update / ETag race

Run the competing application operations under the deterministic scheduler. Keep the production concurrency rule — ETag, version, CAS, or equivalent —
inside the application/storage boundary.

```csharp
await context.ConcurrentAsync(
    cancellationToken => service.UpdateAsync(first, cancellationToken),
    cancellationToken => service.UpdateAsync(second, cancellationToken),
    context.CancellationToken);
```

Useful invariants include “no lost update”, “exactly one compare-and-swap succeeds”, and “the final state is reachable through a legal sequence of
transitions”. Do not fix a race by adding a test-only global lock.

<a id="scenario-outbox-replay"></a>

## Inbox/outbox replay

Model the durable inbox/outbox record and the externally visible effect as separate facts. Inject failure between persistence, publication/handling,
and acknowledgement. Then replay the record and verify that the domain result remains idempotent.

This is especially valuable when the database gives atomicity only locally while the broker is at-least-once.

<a id="scenario-broker-redelivery"></a>

## Dapr, Kafka and RabbitMQ redelivery

Use the adapter that matches the production boundary: `Causalia.Dapr`, `Causalia.Kafka`, or `Causalia.RabbitMQ`. Model acknowledgement loss, offset or
publisher-confirm ambiguity, delayed delivery, rebalance/restart, and redelivery. Keep the assertion in domain language.

The adapter models the **observable semantics** needed by the application test. It does not prove that a real sidecar or broker implements those
semantics; keep real integration tests for infrastructure wiring and provider-specific behavior.

<a id="scenario-service-restart"></a>

## Service restart during work

Put restartable state behind the normal production interfaces, interrupt a simulated process at a meaningful boundary, and rebuild the process from
durable state. Completed effects should not happen again solely because volatile process state disappeared.

Exercise both “crash before durable progress” and “durable progress succeeded but completion was not observed”.

<a id="scenario-http-timeout"></a>

## HTTP timeout after a possible remote side effect

The difficult case is not a clean failure before work starts. It is: the remote service may have completed the request, but the response never reaches
the caller. Make the remote operation identifiable or queryable, inject the ambiguous outcome, retry, and assert that the effect is not duplicated.

Use `Causalia.AspNetCore` when you want the service host and logical HTTP network under deterministic control.

<a id="scenario-eventual-consistency"></a>

## Eventual consistency

Use virtual time instead of sleeping in a test and express the user-visible convergence requirement directly.

```csharp
context.Invariants.Eventually(
    "replicas converge",
    TimeSpan.FromSeconds(30),
    () => primaryVersion == replicaVersion);
```

For stronger distributed guarantees, continue to the consistency-verification reference later in this document.

<a id="scenario-lease-race"></a>

## Lease ownership race

Run competing workers, model acquire/renew/release through a narrow storage boundary, advance virtual time through expiry, and assert that only a valid
owner can perform protected work. Include stale owners and delayed renewal — those are commonly more valuable than the simple simultaneous-acquire case.

<a id="scenario-exploration"></a>

## Schedule exploration and DPOR

Once one deterministic scenario is meaningful, explore alternate schedules. Use bounded exploration when the number of possible interleavings matters;
use DPOR to avoid re-running schedules that are equivalent with respect to declared conflicts. A failing schedule remains replayable.

Do not start adoption here. First make one production boundary and one invariant useful, then expand the schedule space.

<a id="scenario-failure-analysis"></a>

## Replay, minimization and failure intelligence

A first failure should be reproducible before it is clever. Keep the replay token, rerun the exact execution, then use minimization to determine which
scheduler choices and injected faults are essential. `FailureAnalyzer` can classify whether the reduced reproduction depends on ordering, fault
injection, both, or neither.

The beginner-facing `CAUSALIA FAILURE` output deliberately distinguishes **evidence observed before failure** from **proven causal triggers**.

---

<a id="complete-reference"></a>

# Complete source-backed reference

The remainder of this document preserves the full 2.0 reference and examples, updated for the 2.1.1 package train.

## Implementation guide for agents

**Runtime support: .NET 10 only.** Install the SDK selected by `global.json` (10.0.400).
All runtime libraries, tests, and the example application target `net10.0`. EF Core uses version 10.
The Roslyn analyzer targets `netstandard2.0` because it is loaded by the compiler, not by your application.
No older .NET runtime or targeting pack is required. Removing previous target frameworks is a compatibility break for existing consumers;
upgrade their applications and test projects to .NET 10 before using this source revision.

This README contains the complete project documentation. Its examples are either complete repository files (marked with their paths below)
or API fragments that must be placed inside a test with the shown application-specific types and an explicit cancellation token.
Example domain classes elsewhere in this reference are illustrative, not APIs supplied by Causalia.

1. Inspect the target service's entry points, retries, idempotency keys, transaction boundaries, clocks, and cancellation flow.
2. Keep domain/application code independent of Causalia. Inject `TimeProvider` and narrow interfaces for storage, HTTP, messaging, and external effects.
   Register `TimeProvider.System` and real adapters in production. Register simulation-scoped adapters in tests.
3. Add the Causalia packages needed by the **test project**, plus `Causalia.Analyzers` with `PrivateAssets="all"`.
   Keep all Causalia packages on the same version. Package installation snippets assume the release is available from your chosen feed;
   before public publication, pack this repository and use its output directory as a local NuGet source.
4. Create fresh application instances, simulated databases and fault plans inside each `Simulation.RunAsync` or exploration callback.
   Reusing mutable state across runs makes replay invalid. Inject `context.TimeProvider` and pass `context.CancellationToken` throughout the run.
5. Map the real adapter's observable guarantees: atomic writes, version checks, duplicate handling, error translation and durability after restart.
   The simulator cannot prove a real database, broker or network provider satisfies those guarantees; retain real integration tests.
6. Test successful execution, duplicate/concurrent requests, failure before persistence, lost acknowledgement after persistence, bounded retries,
   cancellation and process restart. Assert domain outcomes, fault counts and virtual time so faults cannot silently go unexercised.
7. Explore bounded schedules and retain failure/replay tokens. A passed bounded exploration is not proof of every possible execution.
   Record `ExhaustedWithinBounds` and depth/schedule limits; replay with the same scenario code and compatible package versions.
8. Run restore, build, tests and package verification before merging. Never disable warnings or vulnerability auditing to obtain a green build.

### Complete application and test example

The application below depends only on .NET. It accepts an order once using an atomic create-if-absent storage operation.
The test adapter uses transactions deliberately: `FailBeforeCommit` and `FailAfterCommit` apply to transaction/EF commit boundaries.
A direct `WriteAsync` uses `BeforeWrite`/`AfterWrite`; a direct `DeleteAsync` uses `BeforeDelete`/`AfterDelete`.
A failure after a durable write/delete/commit is ambiguous: the caller cannot assume that nothing changed.
Retries therefore recheck the idempotency key and must not blindly repeat non-idempotent side effects.

The sample assumes an existing key always represents the same accepted order. A real implementation must reject a reused key with conflicting payload.
Only exceptions classified as transient or ambiguous by the real adapter should map to `IOException` in this illustrative contract.
Permanent validation/authentication errors must propagate without retry.

Within a simulation callback, use normal `try`/`catch` and synchronous assertions for expected errors.
MSTest async assertion helpers can resume on a thread-pool thread and escape the deterministic scheduler.
Use `Assert.ThrowsExactlyAsync<SimulationFailedException>` around the outer simulation call when asserting an uncaught scenario failure.

These complete files are built and tested as part of `Causalia.slnx`; CI checks that the copies below match the source.

**examples/Causalia.ExampleApp/Causalia.ExampleApp.csproj**

<!-- source: examples/Causalia.ExampleApp/Causalia.ExampleApp.csproj -->
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

**examples/Causalia.ExampleApp/IOrderStore.cs**

<!-- source: examples/Causalia.ExampleApp/IOrderStore.cs -->
```csharp
namespace Causalia.ExampleApp;

/// <summary>
/// Stores an order marker atomically and idempotently by order identifier.
/// </summary>
public interface IOrderStore
{
    /// <summary>
    /// Returns whether the order marker already exists.
    /// </summary>
    Task<bool> ExistsAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the marker if absent; throws IOException for transient or ambiguous failures.
    /// </summary>
    Task CreateIfMissingAsync(string orderId, CancellationToken cancellationToken);
}
```

**examples/Causalia.ExampleApp/OrderService.cs**

<!-- source: examples/Causalia.ExampleApp/OrderService.cs -->
```csharp
namespace Causalia.ExampleApp;

/// <summary>
/// Demonstrates retrying an idempotent operation using an injected clock and storage port.
/// </summary>
public sealed class OrderService
{
    private readonly IOrderStore _store;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes the service with its storage boundary and clock.
    /// </summary>
    public OrderService(IOrderStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Accepts an order once, retrying recoverable storage failures at most twice.
    /// </summary>
    public async Task AcceptAsync(string orderId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!await _store.ExistsAsync(orderId, cancellationToken))
                {
                    await _store.CreateIfMissingAsync(orderId, cancellationToken);
                }

                return;
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), _timeProvider, cancellationToken);
            }
        }
    }
}
```

**tests/Causalia.Examples.Tests/Causalia.Examples.Tests.csproj**

<!-- source: tests/Causalia.Examples.Tests/Causalia.Examples.Tests.csproj -->
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="MSTest.TestAdapter" />
    <PackageReference Include="MSTest.TestFramework" />
    <ProjectReference Include="../../examples/Causalia.ExampleApp/Causalia.ExampleApp.csproj" />
    <ProjectReference Include="../../src/Causalia.Storage/Causalia.Storage.csproj" />
    <ProjectReference Include="../../src/Causalia.Analyzers/Causalia.Analyzers.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

**tests/Causalia.Examples.Tests/SimulatedOrderStore.cs**

<!-- source: tests/Causalia.Examples.Tests/SimulatedOrderStore.cs -->
```csharp
using System.Text;
using Causalia;
using Causalia.ExampleApp;
using Causalia.Storage;
using Causalia.Storage.Exceptions;

namespace Causalia.Examples.Tests;

/// <summary>
/// Maps the application storage port to deterministic transactional storage.
/// </summary>
[DeterministicSimulation]
public sealed class SimulatedOrderStore : IOrderStore
{
    private readonly SimulationStorageClient _client;

    /// <summary>
    /// Initializes the adapter for a simulation-scoped client.
    /// </summary>
    public SimulatedOrderStore(SimulationStorageClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string orderId, CancellationToken cancellationToken)
    {
        try
        {
            return (await _client.ReadAsync(orderId, cancellationToken)).Exists;
        }
        catch (SimulationStorageTransientException exception)
        {
            throw new IOException("Storage temporarily unavailable.", exception);
        }
    }

    /// <inheritdoc />
    public async Task CreateIfMissingAsync(string orderId, CancellationToken cancellationToken)
    {
        try
        {
            var transaction = _client.BeginTransaction();
            transaction.Write(orderId, Encoding.UTF8.GetBytes("accepted"), expectedVersion: 0);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SimulationStorageConcurrencyException)
        {
            // A competing acceptance already created the same marker atomically.
        }
        catch (SimulationStorageTransientException exception)
        {
            throw new IOException("Storage temporarily unavailable.", exception);
        }
        catch (SimulationStorageAmbiguousCommitException exception)
        {
            throw new IOException("Storage response lost; the write may have committed.", exception);
        }
    }
}
```

**tests/Causalia.Examples.Tests/OrderServiceSimulationTests.cs**

<!-- source: tests/Causalia.Examples.Tests/OrderServiceSimulationTests.cs -->
```csharp
using Causalia.ExampleApp;
using Causalia.Storage;
using Causalia.Storage.Faults;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.Examples.Tests;

[TestClass]
public sealed class OrderServiceSimulationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LostAcknowledgementShouldBeRetriedWithoutDuplicatingOrder()
    {
        var result = await Simulation.RunAsync(new SimulationOptions { Seed = 202601 }, async context =>
        {
            var database = context.CreateStorageDatabase("orders");
            var client = database.CreateClient(options: new SimulationStorageClientOptions
            {
                Faults = new StorageFaultPlan().FailAfterCommit(1)
            });
            var service = new OrderService(new SimulatedOrderStore(client), context.TimeProvider);
            await service.AcceptAsync("order-42", context.CancellationToken);
            await service.AcceptAsync("order-42", context.CancellationToken);
            var actual = await database.CreateClient().ReadAsync("order-42", context.CancellationToken);
            Assert.IsTrue(actual.Exists);
            Assert.AreEqual(1L, actual.Version);
        }, TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromMilliseconds(250), result.VirtualElapsed);
        Assert.AreEqual(1, result.Faults.Count);
    }

    [TestMethod]
    public async Task PersistentFailureShouldStopAfterThreeAttempts()
    {
        var result = await Simulation.RunAsync(new SimulationOptions { Seed = 202602 }, async context =>
        {
            var database = context.CreateStorageDatabase("orders");
            var client = database.CreateClient(options: new SimulationStorageClientOptions
            {
                Faults = new StorageFaultPlan().FailBeforeCommit(1)
            });
            var service = new OrderService(new SimulatedOrderStore(client), context.TimeProvider);
            IOException? failure = null;
            try
            {
                await service.AcceptAsync("order-42", context.CancellationToken);
            }
            catch (IOException exception)
            {
                failure = exception;
            }

            Assert.AreEqual(typeof(IOException), failure?.GetType());
            Assert.IsFalse((await database.CreateClient().ReadAsync("order-42", context.CancellationToken)).Exists);
        }, TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromMilliseconds(500), result.VirtualElapsed);
        Assert.AreEqual(3, result.Faults.Count);
    }

    [TestMethod]
    public async Task CancellationShouldInterruptRetryDelay()
    {
        var result = await Simulation.RunAsync(new SimulationOptions { Seed = 202603 }, async context =>
        {
            var database = context.CreateStorageDatabase("orders");
            var client = database.CreateClient(options: new SimulationStorageClientOptions
            {
                Faults = new StorageFaultPlan().FailBeforeCommit(1)
            });
            var service = new OrderService(new SimulatedOrderStore(client), context.TimeProvider);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100), context.TimeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, context.CancellationToken);
            OperationCanceledException? failure = null;
            try
            {
                await service.AcceptAsync("order-42", linked.Token);
            }
            catch (OperationCanceledException exception)
            {
                failure = exception;
            }

            Assert.AreEqual(typeof(TaskCanceledException), failure?.GetType());
            Assert.IsFalse((await database.CreateClient().ReadAsync("order-42", context.CancellationToken)).Exists);
        }, TestContext.CancellationToken);

        Assert.AreEqual(TimeSpan.FromMilliseconds(100), result.VirtualElapsed);
    }
}
```

<a id="release-checks"></a>

## Build, test and release checks

From the repository root, with Python 3 and the .NET SDK available:

```bash
dotnet restore Causalia.slnx --locked-mode
dotnet build Causalia.slnx -c Release --no-restore
dotnet test Causalia.slnx -c Release --no-build --no-restore --logger "trx;LogFilePrefix=tests"
dotnet pack Causalia.slnx -c Release --no-build --no-restore -o artifacts/packages
python eng/verify.py --packages artifacts/packages --consumer
```

`eng/verify.py` checks that the repository contains exactly `README.md` and `SCENARIOS.md`, that the source-backed examples match their compiled source,
and that runtime projects and packages target only .NET 10. It verifies all 14 NuGet packages and 12 symbol packages. Its isolated consumer restores the
runtime packages into a fresh cache, builds and runs a durable-storage and virtual-time smoke test, confirms the packaged analyzer rejects wall-clock
access with CAU1001, then installs `Causalia.Tool`, runs `inspect` and `init`, and builds/tests the generated project.
The intentionally rejected build is part of that verification and is considered successful only when the expected diagnostic is reported.

On constrained build machines, use `-m:1 /nr:false` for restore/build/test/pack and `-p:UseSharedCompilation=false` for build.
When changing dependencies intentionally, run `dotnet restore Causalia.slnx --force-evaluate` and commit the regenerated lock files.
Resolve NU1507 at the source configuration: the checked-in `NuGet.Config` clears inherited sources and maps public packages to nuget.org.
For an intentional private dependency, add its source and explicit package mapping; do not suppress NU1507.
After a restore failure, fix that error before investigating missing targeting packs. Reopen Visual Studio after installing the selected SDK.

## GitHub open-source readiness

The source includes the MIT license, NuGet license/readme metadata, package lock files, dependency auditing, Dependabot and a Windows/Linux CI workflow.
No repository URL is invented: package metadata obtains it from GitHub Actions when the repository exists.
Before the first public release, the owner must create the canonical repository, verify both OS jobs, require passing PR checks and review package identity/versioning.
Enable GitHub private vulnerability reporting so the [security policy](#security) has a working private reporting channel.
Until that channel exists, do not instruct reporters to post sensitive details in public issues.
Contribution and security policies are sections of this scenario/reference document; GitHub may not display the dedicated policy tabs that it provides for separate policy files.

Passing this repository's tests validates the simulated model and tested behaviors. It does not certify arbitrary integrations, production performance,
or provider-specific behavior. Keep deployment, real database/broker and end-to-end tests in each consuming system.

<a id="overview"></a>

<a id="overview-causalia"></a>

# Causalia

> Explore controlled schedules and replay failures before deployment.

Causalia is a deterministic simulation-testing toolkit for concurrent and distributed .NET software.
It lets you control time, scheduling, failures, messaging, service-to-service HTTP and durable storage so race conditions and partial failures
become reproducible instead of probabilistic.

> Repository version: `2.1.1`. Runtime packages and tests target **net10.0 only**.
> Causalia 2.0 completes the Verification Platform as DS-26, unifying the deterministic engines from 1.0 through 1.9.

<a id="overview-why-causalia"></a>

## Why Causalia?

Normal integration tests usually execute one timing outcome. A production system can execute thousands of other interleavings when requests
overlap, messages are retried, nodes restart or acknowledgements disappear.

Causalia lets a test say:

- run with virtual time instead of wall-clock time;
- deliberately explore alternative async schedules;
- inject deterministic faults;
- state invariants that must always hold;
- explore executable model-state command sequences and compare the real system with the model oracle;
- capture the exact failing schedule;
- replay the same failure;
- minimize it to the decisions and faults that actually matter;
- export the execution as a standalone HTML timeline.

A failure therefore becomes something you can keep:

```text
Seed:         729381
Replay token: v1:...
Faults:       2
Virtual time: 00:10:00

Replay the exact same timeline instead of hoping the race happens again.
```

<a id="overview-packages"></a>

## Packages

Start with `Causalia`. Add only the integrations you need.

| Package | Purpose |
| --- | --- |
| `Causalia` | Core deterministic runtime, verification platform, replay, fault injection and distributed-system test primitives |
| `Causalia.Analyzers` | Roslyn diagnostics for nondeterministic APIs such as wall-clock time, `Random.Shared`, `Task.Run` and blocking waits |
| `Causalia.Testing` | Framework-neutral scenario-scoped provider |
| `Causalia.Reqnroll` | Scenario-scoped provider for Reqnroll constructor injection |
| `Causalia.AspNetCore` | In-memory deterministic ASP.NET Core host and service-to-service HTTP network |
| `Causalia.Storage` | Provider-independent durable storage simulation, transactions and ambiguous commits |
| `Causalia.EntityFrameworkCore` | EF Core `SaveChanges` commit-boundary integration |
| `Causalia.Visualization` | Normalized causal traces and standalone interactive HTML export |
| `Causalia.Load` | Deterministic fixed-concurrency, arrival-rate, ramping and burst load simulation |
| `Causalia.Dapr` | Dapr state, ETag, pub/sub redelivery, dead-lettering and service-invocation semantics |
| `Causalia.Kafka` | Kafka partitions, consumer groups, rebalances and committed-offset semantics |
| `Causalia.RabbitMQ` | RabbitMQ exchanges, queues, prefetch, ack/nack and redelivery semantics |
| `Causalia.Grpc` | gRPC unary calls, canonical statuses, virtual deadlines, partitions and retries |

For a normal test project, the recommended starting point is:

```bash
dotnet add package Causalia
dotnet add package Causalia.Analyzers
```

For the repository version shown here:

```xml
<ItemGroup>
  <PackageReference Include="Causalia" Version="2.1.1" />
  <PackageReference Include="Causalia.Analyzers" Version="2.1.1" />
</ItemGroup>
```

The repository maps its public dependencies to `nuget.org`. This keeps Central Package Management restore-safe on developer machines that also have
corporate or private package sources configured.

<a id="overview-five-minute-quick-start"></a>

## Five-minute quick start

<a id="overview-1-run-code-with-deterministic-virtual-time"></a>

### 1. Run code with deterministic virtual time

Use `context.TimeProvider` for delays and time-dependent code. A ten-minute delay advances virtual time without waiting ten real minutes.

```csharp
using Causalia;

var result = await Simulation.RunAsync(
    new SimulationOptions
    {
        Seed = 729381
    },
    async context =>
    {
        await Task.Delay(
            TimeSpan.FromMinutes(10),
            context.TimeProvider,
            context.CancellationToken);
    },
    cancellationToken);

Console.WriteLine(result.VirtualElapsed); // 00:10:00
```

The same context also exposes deterministic user randomness:

```csharp
var value = context.Random.NextInt32(100);
var id = context.Random.NextGuid();
```

Do not use `DateTime.UtcNow`, `Random.Shared`, `Task.Run` or other uncontrolled runtime primitives inside deterministic simulation code.
`Causalia.Analyzers` reports these at compile time.

<a id="overview-2-explore-alternative-concurrency-schedules"></a>

### 2. Explore alternative concurrency schedules

`RunAsync` executes one deterministic schedule. `ExploreAsync` deliberately explores different runnable-continuation orders.

The example below contains a race: one operation writes `1`, while another can observe the old value first.

```csharp
using Causalia;
using Causalia.Exceptions;
using Causalia.Scheduling;

Func<SimulationContext, Task> scenario = async context =>
{
    var value = 0;
    var observedStaleValue = false;

    context.Invariants.Never(
        "reader observes the pre-write value",
        () => observedStaleValue);

    await context.ConcurrentAsync(
        async operationCancellationToken =>
        {
            await Task.Yield();
            operationCancellationToken.ThrowIfCancellationRequested();
            value = 1;
        },
        async operationCancellationToken =>
        {
            await Task.Yield();
            operationCancellationToken.ThrowIfCancellationRequested();
            observedStaleValue = value == 0;
        },
        context.CancellationToken);
};

try
{
    await Simulation.ExploreAsync(
        new ExplorationOptions
        {
            Strategy = ExplorationStrategy.CoverageGuided,
            Simulation = new SimulationOptions { Seed = 729381 },
            MaxSchedules = 1_000,
            MaxDecisionDepth = 100
        },
        scenario,
        cancellationToken);
}
catch (SimulationExplorationFailedException failure)
{
    Console.WriteLine(failure.Schedule.ReplayToken);
}
```

The failure contains the exact schedule that exposed the bug.

<a id="overview-3-replay-the-exact-failing-timeline"></a>

### 3. Replay the exact failing timeline

Persist the replay token in CI logs, a test artifact or a bug report:

```csharp
var schedule = SimulationSchedule.Parse(replayToken);

await Simulation.ReplayAsync(
    new SimulationOptions { Seed = 729381 },
    schedule,
    scenario,
    cancellationToken);
```

Strict replay validates the deterministic execution shape. If the code changed so much that the schedule no longer describes the same branching
structure, Causalia throws `SimulationScheduleReplayException` rather than silently running another timeline.

<a id="overview-4-minimize-a-failure"></a>

### 4. Minimize a failure

A discovered failure may contain many scheduling and fault decisions. Causalia can reduce it to a sparse reproduction.

```csharp
SimulationExplorationFailedException discovered;

try
{
    await Simulation.ExploreAsync(
        explorationOptions,
        scenario,
        cancellationToken);

    throw new InvalidOperationException("Expected a failure.");
}
catch (SimulationExplorationFailedException failure)
{
    discovered = failure;
}

var minimized = await Simulation.MinimizeAsync(
    explorationOptions.Simulation,
    new Causalia.Minimization.MinimizationOptions
    {
        MaxAttempts = 1_000
    },
    discovered.Failure,
    scenario,
    cancellationToken);

Console.WriteLine(minimized.Reproduction.ReplayToken); // m1:...
```

A minimized `m1:` reproduction can later be parsed and executed with `Simulation.ReproduceAsync`.

<a id="overview-deterministic-simulated-load"></a>

## Deterministic simulated load

Add `Causalia.Load` when you want to search for correctness failures under virtual concurrency pressure rather than measure real-machine throughput.

```bash
dotnet add package Causalia.Load
```

A constant open-model arrival rate starts work independently from iteration completion:

```csharp
using Causalia.Load;
using Causalia.Load.Profiles;
using Causalia.Load.Thresholds;

var loadResult = await context.CreateLoadRunner().RunAsync(
    new ConstantArrivalRateLoadProfile
    {
        Rate = 500,
        TimeUnit = TimeSpan.FromSeconds(1),
        Duration = TimeSpan.FromMinutes(10),
        MaxConcurrentIterations = 2_000
    },
    iteration => ExecuteCheckoutAsync(iteration.CancellationToken),
    new LoadRunOptions
    {
        Thresholds = new LoadThresholds
        {
            MaximumDroppedRate = 0.001,
            MaximumPercentile99 = TimeSpan.FromSeconds(2)
        }
    },
    context.CancellationToken);
```

`LoadRunResult` reports scheduled, started, completed, failed and dropped iterations, peak concurrency, virtual throughput and exact virtual
latency percentiles. Fixed-concurrency, piecewise-linear ramping and same-instant burst profiles are also included. Load iterations remain normal
deterministic scheduler work, so races discovered under load retain Causalia exploration, replay and minimization. For very large virtual workloads,
set `SimulationOptions.TraceSchedulerEvents = false` to reduce trace volume without disabling schedule capture or replay. See [Load](#load).

<a id="overview-ecosystem-adapters"></a>

## Ecosystem adapters

Causalia 1.3 can place common distributed-system semantics directly inside the deterministic world without starting real brokers or sidecars.
Install only the adapters your tests use:

```bash
dotnet add package Causalia.Dapr --version 2.0.0
dotnet add package Causalia.Kafka --version 2.0.0
dotnet add package Causalia.RabbitMQ --version 2.0.0
dotnet add package Causalia.Grpc --version 2.0.0
```

```csharp
var dapr = context.CreateDaprEnvironment();
dapr.AddStateStore("state");

var kafka = context.CreateKafkaCluster();
kafka.CreateTopic("orders", partitions: 3);

var rabbit = context.CreateRabbitMqBroker();
rabbit.DeclareExchange("events", RabbitExchangeType.Direct);

var grpc = context.CreateGrpcNetwork();
```

These are semantic test adapters: production can keep using the official SDKs while tests bind the same application ports to deterministic Dapr,
Kafka, RabbitMQ or gRPC behavior. Lost acknowledgements, ambiguous commits, lost publisher confirms and gRPC `Unavailable` retries participate in
the normal Causalia fault, replay and minimization pipeline. See [EcosystemAdapters](#ecosystemadapters).

<a id="overview-invariants"></a>

## Invariants

Use invariants for rules that must hold across every explored timeline rather than asserting one expected execution order.

```csharp
context.Invariants.Always(
    "money is conserved",
    () => totalBalance == expectedBalance);

context.Invariants.Never(
    "resource has multiple owners",
    () => ownerCount > 1);

context.Invariants.Eventually(
    "replicas converge",
    TimeSpan.FromSeconds(30),
    () => replicasConverged);
```

`Always` and `Never` are safety properties. `Eventually` is a virtual-time liveness obligation; Causalia keeps the simulation alive until the
condition succeeds or its deadline expires.

<a id="overview-messaging-and-deterministic-faults"></a>

## Messaging and deterministic faults

Create a point-to-point in-memory message bus inside the simulation:

```csharp
using Causalia.Messaging;
using Causalia.Messaging.Faults;

var faults = new MessageFaultPlan()
    .Drop(0.01)
    .Duplicate(0.02)
    .Delay(0.05, TimeSpan.FromSeconds(5))
    .Reorder(0.03);

var bus = context.CreateMessageBus(
    new MessageBusOptions
    {
        DeliveryLatency = TimeSpan.FromMilliseconds(25),
        Faults = faults
    });

bus.RegisterHandler<OrderSubmitted>(
    "orders",
    async (message, delivery, handlerCancellationToken) =>
    {
        await HandleAsync(message, handlerCancellationToken);
    });

await bus.SendAsync(
    "orders",
    new OrderSubmitted("order-42"),
    context.CancellationToken);
```

A duplicate keeps the same logical `MessageId` and receives a new delivery `Attempt`, which makes idempotency and at-least-once behavior testable.

<a id="overview-simulated-nodes-and-crashrestart"></a>

## Simulated nodes and crash/restart

```csharp
var worker = context.CreateNode("worker");
var bus = context.CreateMessageBus();

bus.RegisterNodeHandler<OrderSubmitted>(
    worker,
    async (message, delivery, handlerCancellationToken) =>
    {
        await HandleAsync(message, handlerCancellationToken);
    });

worker.Crash();
worker.Restart();
```

A crash cancels work bound to the current node generation. Queued node-bound messages wait while the node is down; interrupted messages can be
redelivered after restart.

<a id="overview-full-process-lifecycle"></a>

## Full process lifecycle

Use a `SimulationProcess<TGeneration>` when restart must replace volatile process state rather than only rotating the node cancellation token:

```csharp
using Causalia.Processes;

await using var process = await context.StartProcessAsync(
    new SimulationProcessOptions
    {
        Name = "orders"
    },
    (generation, cancellationToken) =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new OrdersGeneration());
    },
    context.CancellationToken);

await process.CrashAsync(context.CancellationToken);
await process.RestartAsync(context.CancellationToken);
```

Every restart reruns the generation factory. Graceful stop invokes the generation `StopAsync` hook; crash deliberately skips it. Use
`generation.RunBackgroundAsync(...)` for long-lived generation work so it is cancelled and recreated with the process. Durable resources such as a
`SimulationStorageDatabase` should live outside the generation factory.

ASP.NET Core applications can opt into the same behavior with `StartAspNetCoreProcessAsync`. That rebuilds the `WebApplication`, DI container,
singletons and deterministic hosted workers on every restart while stable HTTP network registrations continue to target the logical process. See
[ProcessLifecycle](#processlifecycle).

<a id="overview-aspnet-core-and-service-to-service-http"></a>

## ASP.NET Core and service-to-service HTTP

Install:

```bash
dotnet add package Causalia.AspNetCore
```

Run a real ASP.NET Core middleware/routing/DI pipeline without Kestrel, TCP, DNS or a real port:

```csharp
using Causalia.AspNetCore;
using Microsoft.AspNetCore.Http;

await using var host = await context.StartAspNetCoreAsync(
    app =>
    {
        app.MapGet(
            "/clock",
            async (TimeProvider timeProvider, HttpContext httpContext) =>
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(10),
                    timeProvider,
                    httpContext.RequestAborted);

                return Results.Ok(timeProvider.GetUtcNow());
            });
    },
    context.CancellationToken);

using var client = host.CreateClient();
using var response = await client.GetAsync(
    "/clock",
    context.CancellationToken);
```

Multiple simulated services can be connected through a deterministic logical network:

```csharp
var network = context.CreateHttpNetwork();
network.Register("orders", ordersHost);
network.Register("payments", paymentsHost);

var link = network.Between("orders", "payments")
    .Latency(TimeSpan.FromMilliseconds(25))
    .Drop(0.01)
    .Duplicate(0.02)
    .Delay(0.05, TimeSpan.FromSeconds(2));

using var payments = network.CreateClient("orders", "payments");
```

Partitions are stateful:

```csharp
link.Partition();
link.Heal();
```

See [ASP.NET Core simulation](#aspnetcore) and [HTTP networking](#networking).

<a id="overview-durable-storage"></a>

## Durable storage

Install:

```bash
dotnet add package Causalia.Storage
```

```csharp
using Causalia.Storage;
using Causalia.Storage.Faults;

var node = context.CreateNode("orders");
var database = context.CreateStorageDatabase("orders-db");

var storage = database.CreateClient(
    node,
    new SimulationStorageClientOptions
    {
        OperationLatency = TimeSpan.FromMilliseconds(5),
        CommitLatency = TimeSpan.FromMilliseconds(20),
        Faults = new StorageFaultPlan()
            .FailAfterCommit(0.01, "commit-ack-lost")
    });
```

The storage model distinguishes failures before commit from ambiguous failures after the durable commit. That makes retry-after-unknown-commit
scenarios reproducible.

For EF Core, add `Causalia.EntityFrameworkCore` and attach the commit-boundary interceptor to the normal provider you already use. See
[storage](#storage) and [EF Core](#entityframeworkcore).

<a id="overview-reqnroll"></a>

## Reqnroll

Install `Causalia.Reqnroll` in the Reqnroll test project and inject `ReqnrollSimulationProvider` like any other scenario context object.

```csharp
using Causalia;
using Causalia.Reqnroll;
using Causalia.Scheduling;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

[Binding]
public sealed class CheckoutSteps
{
    private readonly ReqnrollSimulationProvider _causalia;
    private readonly TestContext _testContext;

    public CheckoutSteps(
        ReqnrollSimulationProvider causalia,
        TestContext testContext)
    {
        _causalia = causalia;
        _testContext = testContext;
    }

    [Given("Causalia uses seed {long}")]
    public void GivenCausaliaUsesSeed(long seed)
    {
        _causalia.Configure(
            new SimulationOptions
            {
                Seed = checked((ulong)seed)
            });
    }

    [When("all checkout schedules are explored")]
    public async Task WhenAllCheckoutSchedulesAreExplored()
    {
        await _causalia.ExploreAsync(
            new ExplorationOptions
            {
                Strategy = ExplorationStrategy.CoverageGuided,
                Simulation = _causalia.Options,
                MaxSchedules = 1_000,
                MaxDecisionDepth = 100
            },
            RunCheckoutScenarioAsync,
            _testContext.CancellationToken);
    }

    [DeterministicSimulation]
    private static Task RunCheckoutScenarioAsync(
        SimulationContext context)
    {
        // Build all state that belongs to this run inside this method.
        return Task.CompletedTask;
    }
}
```

The provider is scenario-scoped and retains `LastResult`, `LastFailure`, `LastExplorationResult`, `LastExplorationFailure`,
`LastMinimizationResult`, `LastFailureAnalysis`, `LastInvariantFailure`, `LastLinearizabilityFailure`, `LastConsistencyFailure`, `LastModelFailure` and model-based
exploration/minimization results for later steps.

See [Reqnroll integration](#reqnroll).

<a id="overview-visualize-a-failure"></a>

## Visualize a failure

Install:

```bash
dotnet add package Causalia.Visualization
```

Then export any successful result or deterministic failure to one standalone HTML file:

```csharp
using Causalia.Visualization;

var html = failure.ToTraceHtml(
    new TraceHtmlOptions
    {
        Title = "Checkout race"
    });

await File.WriteAllTextAsync(
    "causalia-trace.html",
    html,
    cancellationToken);
```

The viewer contains no CDN or external assets and can be attached directly to a CI run. It supports category, lane, severity, text and
causal-correlation filtering.

See [trace visualization](#visualization).

<a id="overview-coverage-guided-exploration"></a>

## Coverage-guided exploration

For larger bounded state spaces, use coverage guidance:

```csharp
await Simulation.ExploreAsync(
    new ExplorationOptions
    {
        Strategy = ExplorationStrategy.CoverageGuided,
        Simulation = new SimulationOptions { Seed = 729381 },
        MaxSchedules = 10_000,
        MaxDecisionDepth = 100
    },
    async context =>
    {
        await ExecuteScenarioAsync(context);

        context.Coverage.Hit("checkout-authorized");
        context.Coverage.Observe(
            "checkout-state",
            checkout.State.ToString());
    },
    cancellationToken);
```

Causalia automatically records scheduler and applied-fault coverage. User coverage points let the explorer prioritize domain states that matter
to the test without coupling the runtime to your domain model.


<a id="overview-dynamic-partial-order-reduction"></a>

## Dynamic Partial Order Reduction

Causalia 1.4 adds conservative Dynamic Partial Order Reduction (DPOR). DPOR avoids schedules that are provably equivalent, while unmodeled
operations remain dependent by default so Causalia never guesses that arbitrary user code is independent.

```csharp
var result = await Simulation.ExploreAsync(
    new ExplorationOptions
    {
        Strategy = ExplorationStrategy.DynamicPartialOrderReduction,
        Simulation = new SimulationOptions { Seed = 729381 },
        MaxSchedules = 100_000,
        MaxDecisionDepth = 250,
        MaxPreemptions = 2
    },
    async context =>
    {
        await Task.WhenAll(
            context.Exploration.RunAsync(
                "orders",
                [context.Exploration.Write("orders:42")],
                ProcessOrderAsync,
                context.CancellationToken),
            context.Exploration.RunAsync(
                "inventory",
                [context.Exploration.Write("inventory:17")],
                ReserveInventoryAsync,
                context.CancellationToken));
    },
    cancellationToken);
```

Resource declarations are a correctness contract. Declare `Read`, `Write` or `Synchronize` only when they completely describe the shared resources
touched by that operation. If you cannot safely make that guarantee, use ordinary Causalia scheduling and the operation remains conservatively
dependent.

Useful result metrics include `EquivalentSchedulesPruned`, `PreemptionBoundPruned` and `MaximumPreemptionsObserved`. Exact replay tokens are
unchanged, so a DPOR-discovered failure can still be replayed and minimized normally. See [AdvancedExploration](#advancedexploration).

<a id="overview-distributed-consistency-verification"></a>

## Distributed consistency verification

Causalia 1.5 can verify client-centric and causal consistency without forcing an eventually consistent system into a linearizable model. A history
records logical write versions, the version returned by each read and optional complete replica snapshots.

```csharp
using Causalia.Consistency;

var history = context.Consistency.CreateHistory<string>("orders")
    .Require(
        ConsistencyGuarantees.Session |
        ConsistencyGuarantees.CausalVisibility |
        ConsistencyGuarantees.ReplicaReadAgreement);

var created = history.Write("checkout", "primary", "order:42");
history.Read("checkout", "primary", "order:42", created);

history.ObserveReplica(
    "secondary",
    new Dictionary<string, ConsistencyVersion<string>?>
    {
        ["order:42"] = created
    });

history.RequireConvergence(
    "order replicas converge",
    TimeSpan.FromSeconds(30),
    ["primary", "secondary"]);
```

The verifier supports `ReadYourWrites`, `MonotonicReads`, `MonotonicWrites`, `WritesFollowReads`, `CausalVisibility` and `ReplicaReadAgreement`.
Bounded convergence uses virtual time and consistency failures retain the normal schedule, replay and minimization pipeline. See
[Consistency](#consistency).

<a id="overview-model-based-testing"></a>

## Model-based testing

Causalia 1.6 can systematically drive a real system under test from an executable state-machine model. A model command declares when it is valid,
how the expected model state changes, how the real system executes that command and how the observed result is verified.

```csharp
using Causalia.ModelBased;

var specification = new ModelBasedSpecification<CounterState, Counter>(
    "counter",
    () => new CounterState(0),
    static (_, cancellationToken) =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new Counter());
    },
    state =>
    [
        ModelCommand.Create<CounterState, Counter, int>(
            "increment",
            current => current with { Value = current.Value + 1 },
            static (counter, _, cancellationToken) => counter.IncrementAsync(cancellationToken),
            static (_, expected, observed) => ModelCommandVerification.Equal(expected.Value, observed),
            current => current.Value < 3)
    ]);

var result = await Simulation.CheckModelAsync(
    new ModelBasedOptions
    {
        Simulation = new SimulationOptions { Seed = 729381 },
        MaxSequences = 10_000,
        MaxCommandDepth = 20,
        ScheduleExploration = new ModelBasedScheduleExplorationOptions
        {
            Strategy = ExplorationStrategy.DynamicPartialOrderReduction,
            MaxSchedules = 1_000,
            MaxDecisionDepth = 100
        }
    },
    specification,
    cancellationToken);
```

Command exploration and scheduler exploration are deliberately separate dimensions. Causalia first chooses a valid business-command sequence and
then, when configured, explores deterministic scheduler timelines for that exact sequence. Every simulation run constructs a fresh system under test.

A discovered failure retains both identities: an `mb1:` model-sequence replay token and the normal `v1:` scheduler replay token. `ReplayModelAsync`
can replay the command sequence alone or combine it with the exact failing scheduler schedule. `MinimizeModelAsync` delta-debugs a failing command
sequence while preserving the same deterministic failure class.

See [model-based testing](#modelbasedtesting).

<a id="overview-failure-intelligence"></a>

## Failure intelligence

Causalia 1.7 turns a deterministic failure into a stable semantic identity plus reproducible causal evidence. Every
`SimulationFailedException` exposes a `FailureKind` and versioned `fi2:` signature that deliberately excludes seed and concrete scheduler order.

```csharp
using Causalia.FailureIntelligence;

var analysis = await Simulation.AnalyzeFailureAsync(
    simulationOptions,
    new FailureAnalysisOptions
    {
        Minimization = new MinimizationOptions { MaxAttempts = 1_000 }
    },
    discoveredFailure,
    RunScenarioAsync,
    cancellationToken);

Console.WriteLine(analysis.Signature.Token);
Console.WriteLine(analysis.Kind);
Console.WriteLine(analysis.Trigger);
Console.WriteLine(analysis.Explanation);
Console.WriteLine(analysis.ReproductionToken);
```

Causal reduction reuses deterministic minimization and classifies the essential trigger as `Deterministic`, `SchedulerOrdering`, `FaultInjection`
or `SchedulerOrderingAndFaultInjection`. The surviving scheduler choices and fault occurrences remain directly inspectable.

Multiple analyses can be grouped with `FailureAnalyzer.CreateReport(...)`. Equivalent semantic failures share one `fi2:` cluster even when different
seeds or `v1:` schedules exposed them; clusters are triaged deterministically by occurrence count, analysis confidence and reproduction size.
`Causalia.Visualization` can render a `FailureAnalysis` directly and includes signature, trigger, confidence and the compact `m1:` token. See
[Failure intelligence](#failureintelligence).

<a id="overview-time-travel-debugger"></a>

## Time-travel debugger

Causalia 1.8 can retain deterministic state checkpoints alongside the normal causal trace. Register stable textual probes and enable scheduler-step
capture when you want to inspect state immediately before and after controlled asynchronous work.

```csharp
using Causalia.TimeTravel;

var options = new SimulationOptions
{
    Seed = 729381,
    TimeTravel = new TimeTravelOptions
    {
        CaptureMode = TimeTravelCaptureMode.SchedulerSteps
    }
};

var result = await Simulation.RunAsync(
    options,
    async context =>
    {
        var order = new OrderState();
        context.TimeTravel.Watch(
            "order",
            () => $"status={order.Status};version={order.Version}");

        await ExecuteAsync(order, context.CancellationToken);
    },
    cancellationToken);

var debugger = result.TimeTravel.CreateDebugger();
var latest = debugger.Current;
var previous = debugger.MovePrevious() ? debugger.Current : null;
```

Every retained checkpoint has a stable `tt1:` token, scheduler step, virtual timestamp, trace position and immutable probe snapshots. The debugger can
seek by token, scheduler step or before/after a trace event, jump between changes of one probe and diff arbitrary checkpoints. Deterministic failures
carry the timeline too, so the final watched state is available directly from `SimulationFailedException.TimeTravel`.

Automatic retention is bounded through `MaxRetainedCheckpoints`; older checkpoints are dropped without renumbering surviving tokens. Probe capture
exceptions are recorded as debugger data rather than changing simulation behavior. `Causalia.Analyzers` treats capture and formatter delegates as
deterministic boundaries.

`Causalia.Visualization` exports the checkpoints into the standalone HTML viewer with an interactive previous/next state inspector. See
[time-travel debugging](#timetravel).

<a id="overview-production-reality-bridge"></a>

## Production Reality Bridge

Causalia 1.9 can turn immutable production observations into deterministic simulation evidence without connecting a test to a live production system.
Normalize application telemetry or stopped .NET `Activity` objects into a `ProductionRealityDataset`, persist its stable `pr2:` JSON artifact, then use
real observed durations/outcomes for sampling or replay the relative timing of one correlated incident.

```csharp
var dataset = ProductionRealityDataset.ParseJson(productionEvidenceJson);
var incident = dataset.GetIncident("checkout-42");

await Simulation.RunAsync(
    new SimulationOptions { Seed = 729381 },
    async context =>
    {
        context.ProductionReality.Use(dataset);

        var sample = await context.ProductionReality.ApplyAsync(
            "payments.authorize",
            context.CancellationToken);

        await context.ProductionReality.ReplayIncidentAsync(
            incident,
            (observation, operationCancellationToken) =>
                ExecuteObservedOperationAsync(observation, operationCancellationToken),
            context.CancellationToken);
    },
    cancellationToken);
```

Production sampling has its own deterministic random stream and never perturbs scheduler, fault or user randomness. Both successful results and failures
retain the exact observations consumed by the run. See [Production Reality Bridge](#productionreality).

<a id="overview-verification-platform"></a>

## Verification Platform

Causalia 2.0 composes the engines from 1.0 through 1.9 into one ordered verification plan. A release gate can run seeded simulation, DPOR exploration
and executable model checking together, retain each engine's evidence, and cluster failures across all steps by their stable `fi2:` identity.

```csharp
using Causalia.Verification;

var plan = VerificationPlan.Create(
    "checkout-release-gate",
    builder => builder
        .AddRun("smoke", new SimulationOptions { Seed = 729381 }, RunSmokeAsync)
        .AddExploration("dpor", explorationOptions, RunConcurrentCheckoutAsync)
        .AddModel("state-machine", modelOptions, checkoutSpecification));

var verification = await Simulation.VerifyAsync(
    plan,
    new VerificationRunOptions(),
    cancellationToken);

await File.WriteAllTextAsync(
    "causalia-verification.json",
    verification.Report.ToJson(),
    cancellationToken);

verification.EnsurePassed();
```

The portable report has a content-addressed `vp1:` fingerprint, exact schedule tokens where available, `fi2:` failure identities, time-travel
checkpoint metadata and referenced `pr2:` production evidence. `Causalia.Visualization` can export the same result as a standalone HTML dashboard.
See [Verification Platform](#verificationplatform).

<a id="overview-linearizability"></a>

## Linearizability

For concurrent APIs where every observed result must be explainable by a legal sequential history, Causalia can record operations and check them
against an executable sequential specification.

```csharp
var history = context.Linearizability
    .CreateHistory<RegisterInput, RegisterOutput>("register");

var write = history.Begin(
    "writer",
    RegisterInput.Write(200));

var read = history.Begin(
    "reader",
    RegisterInput.Read());

read.Complete(RegisterOutput.FromValue(0));
write.Complete(RegisterOutput.Ack());

history.RequireLinearizable(specification);
```

The bounded checker respects real-time precedence and returns `Inconclusive` rather than pretending success when configured search limits are
exhausted.

<a id="overview-determinism-rules"></a>

## Determinism rules

A deterministic scenario should use Causalia-controlled primitives wherever behavior can affect scheduling or outcomes.

Prefer:

```csharp
context.TimeProvider
context.Random
context.CancellationToken
Task.Delay(delay, context.TimeProvider, context.CancellationToken)
```

Avoid inside simulation code:

```csharp
DateTime.Now
DateTime.UtcNow
Random.Shared
Task.Run(...)
new Thread(...)
Thread.Sleep(...)
task.Result
task.Wait()
task.ConfigureAwait(false)
```

Install `Causalia.Analyzers` so these mistakes become compiler diagnostics. For helper methods outside a scenario lambda, annotate deterministic
code with `[DeterministicSimulation]`. Deliberate external boundaries can be marked with `[AllowNondeterminism("reason")]`.

`ConfigureAwait(false)` deliberately escapes the simulation synchronization context and is unsupported inside a deterministic boundary. Use the
default `await` behavior. If an external library must escape the context, isolate that call behind an explicitly documented
`[AllowNondeterminism]` boundary; its behavior is outside the replay guarantee.

See [analyzers](#analyzers).

<a id="overview-important-testing-rule-create-run-state-inside-the-scenario"></a>

## Important testing rule: create run state inside the scenario

`ExploreAsync` and `MinimizeAsync` execute the scenario multiple times. Mutable state belonging to one run must therefore be created inside the
scenario delegate or method.

Correct:

```csharp
async Task ScenarioAsync(SimulationContext context)
{
    var state = new MyState();
    await ExecuteAsync(state, context);
}
```

Do not reuse mutable per-run state captured outside that method unless resetting it is explicitly part of the test.

<a id="overview-what-causalia-does-not-try-to-be"></a>

## What Causalia does not try to be

Causalia is not a replacement for every test type.

Keep normal unit tests for local logic. Keep real integration/end-to-end tests for deployment wiring and provider-specific behavior. Use normal
performance tooling when you need real CPU, memory, kernel, database-server or network throughput measurements.

Causalia is aimed at the correctness space between those layers: timing, concurrency, retries, partial failure, ordering, crash/restart and
distributed invariants.

`Causalia.Load` adds deterministic simulated load for correctness under concurrency pressure. Real throughput benchmarking remains a separate
concern because virtual time cannot tell you how many requests per second production hardware can execute.

<a id="overview-versioning-and-20-platform-stability"></a>

## Versioning and 2.1 adoption stability

Causalia `1.0.0` established the stable DS-1 through DS-16 deterministic runtime baseline. Releases 1.1 through 1.9 added deterministic load, process
lifecycle, ecosystem adapters, DPOR, consistency verification, model testing, failure intelligence, time travel and Production Reality. Causalia
`2.0.0` adds DS-26: the Verification Platform that composes those engines into ordered plans with unified `vp1:` CI reports.

Causalia 2.1.1 keeps the 2.0 verification semantics and executable `v1:`, `m1:` and `mb1:` token families unchanged. The 2.1 adoption work adds
`inspect`/`init`, adoption-aware diagnostics, beginner-facing failure presentation, and the README/SCENARIOS documentation split without redefining
existing replay or verification semantics. The collision-safe `fi2:` and `pr2:` formats introduced in 2.0.1 remain unchanged. See
[versioning policy](#versioning) and [changelog](#changelog) for migration details.

<a id="overview-documentation"></a>

## Documentation

Start with [README.md](README.md) for installation, `inspect`, `init`, and the first deterministic simulation.
This document contains the scenario cookbook and full reference.

- [Detailed feature reference](#features)
- [Reqnroll](#reqnroll)
- [Process lifecycle](#processlifecycle)
- [ASP.NET Core](#aspnetcore)
- [Service-to-service networking](#networking)
- [Storage](#storage)
- [Entity Framework Core](#entityframeworkcore)
- [Determinism analyzer](#analyzers)
- [Trace visualization](#visualization)
- [Deterministic simulated load](#load)
- [Ecosystem adapters](#ecosystemadapters)
- [Advanced exploration / DPOR](#advancedexploration)
- [Consistency verification](#consistency)
- [Model-based testing](#modelbasedtesting)
- [Failure intelligence](#failureintelligence)
- [Time-travel debugger](#timetravel)
- [Production Reality Bridge](#productionreality)
- [Verification Platform](#verificationplatform)
- [Versioning policy](#versioning)
- [Changelog](#changelog)

<a id="overview-current-architecture"></a>

## Current architecture

```text
Causalia                         core deterministic runtime
├── Verification Platform + vp1 CI reports
├── virtual time
├── scheduler + exploration
├── executable model/state-machine exploration
├── replay + minimization + failure intelligence
├── time-travel checkpoints + state diffing
├── invariants + consistency + linearizability
├── messaging + generic faults
├── nodes + restartable processes
└── coverage

Causalia.Testing                test-runner-neutral provider
Causalia.Reqnroll               Reqnroll scenario provider
Causalia.Analyzers              compile-time determinism checks
Causalia.AspNetCore             ASP.NET Core + logical HTTP network
Causalia.Storage                deterministic durable storage
Causalia.EntityFrameworkCore    EF Core commit-boundary adapter
Causalia.Visualization          causal trace model + HTML viewer
Causalia.Load                   deterministic simulated load
Causalia.Tool                   inspect/init adoption CLI
```

The `Causalia` core package has no external runtime dependencies.

<a id="overview-license"></a>

## License

MIT. See [LICENSE](LICENSE).


<a id="advancedexploration"></a>

<a id="advancedexploration-advanced-exploration-and-dpor"></a>

# Advanced exploration and DPOR

Causalia 1.4 adds conservative Dynamic Partial Order Reduction (DPOR) for deterministic schedule exploration.

<a id="advancedexploration-why-dpor"></a>

## Why DPOR

Systematic exploration can grow factorially. Six independent logical operations have 720 possible execution orders, even though all are equivalent
when the operations touch disjoint state. DPOR uses declared resource dependencies plus scheduler causality to avoid equivalent orders.

In the Causalia validation suite, six independent operations reduce from 720 DFS schedules to one DPOR schedule. Three operations that all write the
same resource remain six schedules.

<a id="advancedexploration-configure-dpor"></a>

## Configure DPOR

```csharp
var result = await Simulation.ExploreAsync(
    new ExplorationOptions
    {
        Strategy = ExplorationStrategy.DynamicPartialOrderReduction,
        Simulation = new SimulationOptions { Seed = 729381 },
        MaxSchedules = 100_000,
        MaxDecisionDepth = 250,
        MaxPreemptions = 2
    },
    RunScenarioAsync,
    cancellationToken);
```

`MaxPreemptions` is optional. A value of zero explores only non-preemptive executions that satisfy the other bounds.

<a id="advancedexploration-declare-logical-operations"></a>

## Declare logical operations

```csharp
await context.Exploration.RunAsync(
    "update-order",
    [
        context.Exploration.Read("customer:17"),
        context.Exploration.Write("order:42"),
        context.Exploration.Synchronize("orders-lock")
    ],
    async cancellationToken =>
    {
        await UpdateOrderAsync(cancellationToken);
    },
    context.CancellationToken);
```

Two operations can be treated as independent only when their declared accesses prove that they do not conflict. Read/read access is independent. A
write conflicts with another read or write of the same resource. Synchronize access is treated as a dependency boundary.

<a id="advancedexploration-correctness-rule"></a>

## Correctness rule

Resource declarations are a correctness contract. They must completely describe the shared dependency footprint of the operation. Never omit shared
mutable state merely to make DPOR prune more schedules.

If an operation is not modeled, Causalia treats it conservatively as dependent. This can reduce pruning efficiency but cannot hide a race by assuming independence.

<a id="advancedexploration-happens-before-lineage"></a>

## Happens-before lineage

Causalia tracks logical operation ids through async continuations and virtual-time wakeups. A timer captures the operation that scheduled it, so
operations waking at the same virtual instant keep their original dependency profiles. DPOR can reason about those wakeups without falling back to
an unmodeled dependency.

<a id="advancedexploration-metrics"></a>

## Metrics

`ExplorationResult` exposes:

- `EquivalentSchedulesPruned` for alternatives removed because declared dependencies proved them equivalent;
- `PreemptionBoundPruned` for alternatives rejected by the configured preemption limit;
- `MaximumPreemptionsObserved` for the largest preemption count among executed schedules.

<a id="advancedexploration-replay-and-minimization"></a>

## Replay and minimization

DPOR changes which schedules Causalia chooses to execute, not the replay format. A failure found by DPOR still produces the normal
`SimulationSchedule` and `v1:` replay token. `ReplayAsync`, `MinimizeAsync` and reproduction tokens continue to work unchanged.


<a id="analyzers"></a>

<a id="analyzers-causaliaanalyzers"></a>

# Causalia.Analyzers

`Causalia.Analyzers` protects deterministic simulation code from APIs that escape Causalia's virtual world.
The analyzer targets `netstandard2.0` and uses the Roslyn 4.8 API baseline for compiler-host compatibility. It does not require an older .NET runtime.

Install it in the project that contains simulation scenarios:

```xml
<PackageReference Include="Causalia.Analyzers" Version="2.1.1" PrivateAssets="all" />
```

The analyzer automatically recognizes lambdas passed as `Func<SimulationContext, Task>` scenario arguments to Causalia runtime and provider APIs.
Helpers outside those lambdas can opt into the same analysis with `[DeterministicSimulation]`.

<a id="analyzers-diagnostics"></a>

## Diagnostics

| ID | Meaning | Preferred replacement |
| --- | --- | --- |
| `CAU1001` | Real time, delay or timer escapes virtual time | `context.TimeProvider` and TimeProvider-aware APIs |
| `CAU1002` | Nondeterministic or runtime-dependent randomness | `context.Random` |
| `CAU1003` | Work escapes the deterministic scheduler | cooperative async code on the Causalia synchronization context |
| `CAU1004` | Blocking wait can prevent simulation progress | `await` the asynchronous operation |
| `CAU1005` | Method-group scenario body is not automatically in analysis scope | mark the scenario method `[DeterministicSimulation]` |
| `CAU1006` | `ConfigureAwait(false)` escapes the simulation context | keep the default `await` behavior |

Examples that are reported inside deterministic code include `DateTime.UtcNow`, `Task.Delay` without a `TimeProvider`, `Random.Shared`,
`Guid.NewGuid()`, `Task.Run`, raw threads, `ThreadPool` queueing, `Task.Wait()`, `Task<T>.Result` and `ConfigureAwait(false)`.

Safe alternatives remain explicit:

```csharp
await Task.Delay(
    TimeSpan.FromSeconds(5),
    context.TimeProvider,
    context.CancellationToken);

var number = context.Random.NextInt32(100);
var id = context.Random.NextGuid();
var now = context.TimeProvider.GetUtcNow();
```

The deterministic scheduler depends on continuations returning through the simulation synchronization context. `ConfigureAwait(false)` bypasses
that context, so it is unsupported in deterministic code. Use ordinary `await`. If an external library cannot preserve the context, isolate the
call behind an `[AllowNondeterminism("reason")]` method and treat its behavior as outside the replay guarantee.

<a id="analyzers-model-based-testing-boundaries"></a>

## Model-based testing boundaries

Since Causalia 1.6, model definitions are part of the deterministic boundary rather than only the SUT execution delegate. The analyzer recognizes
`ModelBasedSpecification` initial-state factories, system factories and command providers, plus `ModelCommand.Create` transitions, execution delegates,
verifiers and preconditions. Uncontrolled time, randomness, scheduler escape and blocking waits inside any of those delegates receive the same
CAU1001-CAU1004 diagnostics as ordinary simulation code.

<a id="analyzers-helper-methods"></a>

## Helper methods

Annotate helpers that must remain deterministic:

```csharp
[DeterministicSimulation]
private static async Task ProcessAsync(
    SimulationContext context,
    CancellationToken cancellationToken)
{
    await Task.Delay(
        TimeSpan.FromSeconds(1),
        context.TimeProvider,
        cancellationToken);
}
```

`[AllowNondeterminism("reason")]` is the explicit escape hatch for a method or type that intentionally crosses the deterministic boundary.
Use it sparingly and document why replay correctness is still acceptable. Standard diagnostic suppression through `.editorconfig` or `#pragma` also works.

<a id="analyzers-aspnet-core-adapter"></a>

## ASP.NET Core adapter

Endpoint and middleware lambdas declared structurally inside a recognized Causalia scenario remain inside that deterministic boundary. The existing
analyzer rules therefore apply to inline `Causalia.AspNetCore` endpoint code without a separate analyzer package. Endpoint helpers declared outside
the scenario should use `[DeterministicSimulation]` like any other simulation helper.


<a id="analyzers-deterministic-load-boundaries"></a>

## Deterministic load boundaries

Since Causalia 1.1, inline `Func<LoadIterationContext, Task>` delegates are recognized passed to `Causalia.Load` as deterministic boundaries as well.
Wall-clock time, uncontrolled randomness, `Task.Run` and blocking waits inside load-iteration lambdas therefore produce the same CAU1001-CAU1004
diagnostics as ordinary simulation scenarios. A method group used as a load iteration should be marked `[DeterministicSimulation]` so the analyzer
can verify the referenced method body.


<a id="analyzers-process-lifecycle-boundaries"></a>

## Process lifecycle boundaries

Causalia 1.2 also recognizes inline `Func<SimulationProcessGenerationContext, CancellationToken, Task<TGeneration>>` generation factories as
deterministic boundaries. Overrides of `SimulationBackgroundService.ExecuteAsync` are treated as deterministic code as well, so process startup and
long-lived generation workers receive the same CAU1001-CAU1004 protections without requiring an extra attribute.


Causalia 1.9 also treats handlers supplied to `SimulationProductionReality.ReplayIncidentAsync` as deterministic simulation code.


<a id="aspnetcore"></a>

<a id="aspnetcore-causaliaaspnetcore"></a>

# Causalia.AspNetCore

`Causalia.AspNetCore` runs a real ASP.NET Core request pipeline inside a deterministic Causalia simulation without opening a network socket.

<a id="aspnetcore-start-an-application"></a>

## Start an application

```csharp
await Simulation.RunAsync(
    new SimulationOptions { Seed = 729381 },
    async context =>
    {
        await using var host = await context.StartAspNetCoreAsync(
            app =>
            {
                app.MapGet(
                    "/clock",
                    async (TimeProvider timeProvider, HttpContext httpContext) =>
                    {
                        await Task.Delay(
                            TimeSpan.FromMinutes(10),
                            timeProvider,
                            httpContext.RequestAborted);

                        return Results.Ok(timeProvider.GetUtcNow());
                    });
            },
            context.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/clock");
        using var response = await host.SendAsync(request, context.CancellationToken);
    },
    cancellationToken);
```

Causalia registers the simulation `TimeProvider`, `SimulationContext`, and the host's `SimulationNode` in ASP.NET Core DI after user service
configuration so deterministic services cannot be accidentally replaced.

<a id="aspnetcore-httpclient"></a>

## HttpClient

```csharp
using var client = host.CreateClient();
using var response = await client.GetAsync("/orders/42", context.CancellationToken);
```

The client uses an in-memory `HttpMessageHandler`; no TCP listener, port, DNS lookup, or socket is involved. Causalia sets the client timeout to
`Timeout.InfiniteTimeSpan` so `HttpClient` does not introduce a hidden wall-clock timer; deterministic timeouts should be expressed with a cancellation
token driven by the simulation `TimeProvider`.

<a id="aspnetcore-service-to-service-network"></a>

## Service-to-service network

DS-13 can connect multiple hosted applications in one deterministic HTTP topology:

```csharp
var network = context.CreateHttpNetwork();
network.Register("orders", ordersHost);
network.Register("payments", paymentsHost);

var link = network.Between("orders", "payments")
    .Latency(TimeSpan.FromMilliseconds(25))
    .Drop(0.01)
    .Duplicate(0.02)
    .Delay(0.05, TimeSpan.FromSeconds(2));

using var client = network.CreateClient("orders", "payments");
using var response = await client.GetAsync("/authorize/42", context.CancellationToken);
```

Links are directional. `link.Partition()` rejects traffic until `link.Heal()` is called. Fixed latency and explicit partition state can change during a
scenario; probabilistic fault policies are frozen after the first request on that link so replay semantics cannot change mid-run.

A duplicate is a real additional ASP.NET Core delivery with the same logical network request id and a different delivery attempt. Request content and
headers are snapshotted before delivery so duplicate POST/PUT requests receive independent readable bodies. Duplicate deliveries are tracked by the
simulation runtime, so the scenario cannot silently finish while an extra delivery is still running.

A destination that is already crashed rejects a network delivery with `SimulationNetworkNodeUnavailableException`. A destination crash during an
active request still flows through the normal DS-12 `RequestAborted` behavior. A source crash during virtual transport latency cancels that outbound
request through the source node generation.

See [Networking](#networking) for the full DS-13 contract.

<a id="aspnetcore-host-versus-process-lifecycle"></a>

## Host versus process lifecycle

The lightweight host API remains available:

```csharp
host.Node.Crash();
host.Node.Restart();
```

That rotates node generation cancellation while preserving the same `WebApplication` and DI container.

Causalia 1.2 adds a full process lifecycle when volatile application state must be reconstructed:

```csharp
await using var process = await context.StartAspNetCoreProcessAsync(
    options,
    configureBuilder,
    configureApplication,
    context.CancellationToken);

await process.CrashAsync(context.CancellationToken);
await process.RestartAsync(context.CancellationToken);
```

A process restart creates a fresh `WebApplication`, service provider and singleton graph. Requests active during a crash receive cancellation through
`HttpContext.RequestAborted`. Existing clients and `SimulationHttpNetwork` registrations follow the logical process to its replacement generation.

Use `SimulationBackgroundService` for deterministic long-running hosted work that must be cancelled and recreated with each process generation. See
[ProcessLifecycle](#processlifecycle) for the full lifecycle contract.

<a id="aspnetcore-systematic-exploration"></a>

## Systematic exploration

Multiple in-memory HTTP requests participate in the normal Causalia scheduler:

```csharp
await context.ConcurrentAsync(
    token => client.GetAsync("/work/a", token),
    token => client.GetAsync("/work/b", token),
    context.CancellationToken);
```

When the containing scenario is executed with `Simulation.ExploreAsync`, asynchronous endpoint continuations become part of the explored schedule.

<a id="aspnetcore-reqnroll"></a>

## Reqnroll

No special Reqnroll bridge is required. `ReqnrollSimulationProvider` already supplies a normal `SimulationContext`, so a binding can start an
ASP.NET Core host inside `RunAsync` or `ExploreAsync`.

<a id="aspnetcore-current-boundaries"></a>

## Current boundaries

Causalia does not simulate WebSockets, HTTP/2 frame behavior, TLS handshakes, incremental network streaming or kernel socket buffers. Full process
reconstruction is available through `StartAspNetCoreProcessAsync`, but it still runs inside the test process rather than forking an operating-system
process. Standard `BackgroundService` follows normal .NET hosting behavior; use `SimulationBackgroundService` when long-running work must remain under
Causalia scheduling and generation cancellation.


<a id="consistency"></a>

<a id="consistency-consistency-verification"></a>

# Consistency verification

Causalia 1.5 adds deterministic verification for distributed read/write histories that are weaker than a single linearizable register but still
need explicit client and replica guarantees.

Use this API when the system intentionally permits replication lag or eventual consistency and the test still needs to prove properties such as
read-your-writes, monotonic reads, causal visibility or bounded convergence.

<a id="consistency-create-a-logical-history"></a>

## Create a logical history

A consistency history records logical write versions rather than inspecting a particular database protocol. The test tells Causalia which logical
write a later read observed.

```csharp
var history = context.Consistency.CreateHistory<string>("orders")
    .Require(
        ConsistencyGuarantees.Session |
        ConsistencyGuarantees.CausalVisibility |
        ConsistencyGuarantees.ReplicaReadAgreement);

var created = history.Write(
    "checkout-session",
    "primary",
    "order:42");

history.Read(
    "checkout-session",
    "primary",
    "order:42",
    created);
```

`ConsistencyVersion<TKey>` is a simulation-side logical version token. It is not required to match an ETag, database sequence number or provider
version. Application adapters can keep their real version type and map the observation back to the logical write that produced it. Tokens are scoped
to one simulation run, so histories and provider-to-logical-version mappings must be rebuilt inside a scenario that exploration or minimization reruns.

<a id="consistency-session-guarantees"></a>

## Session guarantees

The four client-centric session guarantees are available individually or through `ConsistencyGuarantees.Session`:

- `ReadYourWrites` requires a later read of a key to observe the session's last write or a causally newer version;
- `MonotonicReads` prevents one session from moving behind a version it has already observed;
- `MonotonicWrites` requires a replica that exposes a later session write to also expose the earlier writes from that session;
- `WritesFollowReads` requires a replica that exposes a write to also expose the versions the writing session had previously read.

The first two guarantees can be checked directly from read/write observations. `MonotonicWrites` and `WritesFollowReads` require replica snapshots
because they describe what another observer is allowed to expose.

<a id="consistency-replica-snapshots-and-causal-visibility"></a>

## Replica snapshots and causal visibility

Record a complete logical snapshot for each replica that matters to the test:

```csharp
var customer = history.Write(
    "crm",
    "primary",
    "customer:42");

history.Read(
    "checkout",
    "primary",
    "customer:42",
    customer);

var order = history.Write(
    "checkout",
    "primary",
    "order:99");

history.ObserveReplica(
    "secondary",
    new Dictionary<string, ConsistencyVersion<string>?>
    {
        ["customer:42"] = customer,
        ["order:99"] = order
    });
```

Every write captures the complete causal context already known by its session. With `CausalVisibility` enabled, exposing a version on a replica
requires that the replica snapshot also contains each causal predecessor or a causally newer descendant for the predecessor's key.

Snapshots are deliberately explicit. A missing key and a key mapped to `null` both mean that no logical version is visible; Causalia normalizes
those forms before comparison. Causalia does not infer replica state from network calls, storage reads or message delivery because doing so would
turn a correctness proof into a provider-specific guess.

<a id="consistency-replicaread-agreement"></a>

## Replica/read agreement

`ReplicaReadAgreement` verifies that a read matches the latest snapshot explicitly recorded for that replica:

```csharp
history.ObserveReplica(
    "secondary",
    new Dictionary<string, ConsistencyVersion<string>?>
    {
        ["order:42"] = created
    });

history.Read(
    "browser",
    "secondary",
    "order:42",
    created);
```

This guarantee is useful when a test separately models replication and then wants to assert that the read path did not return data different from
the modeled replica state.

<a id="consistency-bounded-eventual-convergence"></a>

## Bounded eventual convergence

A history can keep the simulation alive until selected replicas converge or a virtual-time deadline expires:

```csharp
history.RequireConvergence(
    "inventory replicas converge",
    TimeSpan.FromSeconds(30),
    ["primary", "secondary"]);
```

The requirement is satisfied when all selected replicas have recorded snapshots with exactly the same logical keys and version ids. If they remain
different, Causalia advances virtual time to the deadline and fails with `SimulationConsistencyViolationException` whose `Kind` is `Convergence`.

This uses virtual time. A thirty-second convergence window does not wait thirty wall-clock seconds.

<a id="consistency-failure-diagnostics-and-replay"></a>

## Failure diagnostics and replay

Consistency violations fail the simulation through the normal `SimulationFailedException` pipeline. The convenience property
`SimulationFailedException.ConsistencyFailure` exposes the specific violation and framework-neutral providers retain it as
`LastConsistencyFailure`.

The failure still contains the exact schedule and trace, so replay, systematic exploration, DPOR and minimization continue to work without a
separate consistency-specific execution model.

Successful runs expose one `ConsistencyOutcome` per history through `SimulationResult.Consistency`, including the enabled guarantees, read/write
counts, replica observations and completed convergence requirements.

<a id="consistency-soundness-boundary"></a>

## Soundness boundary

Consistency verification depends on accurate logical observations:

- a read must reference the logical write it actually observed;
- `ObserveReplica` must represent the complete logical snapshot relevant to the guarantees being checked;
- session ids must represent real client/session ordering boundaries;
- replica ids must represent the replica or materialized view whose state was actually observed;
- when DPOR is enabled, exploration resource declarations must still include every application resource whose ordering can change these observations.

Consistency instrumentation does not repair an incomplete DPOR dependency profile. If declarations or observations are incomplete, the verifier can
only prove the history that was supplied. This is the same explicit-modeling principle used by 1.4 DPOR resource declarations.


<a id="ecosystemadapters"></a>

<a id="ecosystemadapters-ecosystem-adapters"></a>

# Ecosystem adapters

Causalia 1.3 adds deterministic semantic adapters for Dapr, Kafka, RabbitMQ and gRPC. These packages do not start real sidecars, brokers or sockets.
They reproduce the failure-sensitive semantics that application code depends on so Causalia can explore them with the same scheduler, virtual time,
fault engine, replay and minimization.

Install only the adapter packages required by the system under test:

```bash
dotnet add package Causalia.Dapr --version 2.0.0
dotnet add package Causalia.Kafka --version 2.0.0
dotnet add package Causalia.RabbitMQ --version 2.0.0
dotnet add package Causalia.Grpc --version 2.0.0
```

<a id="ecosystemadapters-dapr"></a>

## Dapr

Install `Causalia.Dapr` and create an environment inside the simulation:

```csharp
var dapr = context.CreateDaprEnvironment();
dapr.AddStateStore("state");
var pubsub = dapr.AddPubSub("events");
var client = dapr.CreateClient("orders");
```

State stores provide typed JSON values, ETags, conditional save/delete and atomic transactions. Pub/sub supports `Success`, `Retry` and `Drop`,
virtual retry delay, dead-letter topics and lost-acknowledgement fault injection for at-least-once redelivery.

When the environment is created with `context.CreateHttpNetwork()`, `SimulationDaprClient.InvokeMethodAsync` routes service invocation over Causalia's deterministic HTTP network.

<a id="ecosystemadapters-kafka"></a>

## Kafka

```csharp
var kafka = context.CreateKafkaCluster();
kafka.CreateTopic("orders", partitions: 3);
var producer = kafka.CreateProducer();
var consumer = kafka.CreateConsumer("workers", "worker-1");
consumer.Subscribe("orders");
```

Records are appended to durable partition logs with deterministic key hashing or round-robin partitioning. Consumer-group membership causes
deterministic rebalances. Committed offsets survive member replacement; uncommitted records are redelivered after a member leaves and another member
takes ownership.

Offset commits are fenced to the consumer-group generation in which they started. A rebalance during injected commit latency rejects the stale
commit, including when a member ID is reused. Committing stored offsets does not erase newer offsets stored while that commit was in flight.

`KafkaFaultPlan` can reject a commit before it is applied or lose the acknowledgement after the offset is durably committed. The latter
intentionally creates the classic ambiguous-commit case.

<a id="ecosystemadapters-rabbitmq"></a>

## RabbitMQ

```csharp
var rabbit = context.CreateRabbitMqBroker();
rabbit.DeclareExchange("events", RabbitExchangeType.Direct);
rabbit.DeclareQueue("orders");
rabbit.BindQueue("events", "orders", "order.created");
```

The 1.3 broker models direct and fanout routing, queue durability inside the simulation, manual ack/nack, requeue, redelivery and consumer prefetch.
Closing a consumer automatically requeues outstanding unacknowledged deliveries with `Redelivered = true`.

`RabbitFaultPlan` can reject a publish before routing or lose the publisher confirm after routing, which makes publisher retry/idempotency logic testable.

<a id="ecosystemadapters-grpc"></a>

## gRPC

```csharp
var grpc = context.CreateGrpcNetwork();
var server = grpc.CreateServer("payments");
server.RegisterUnary<AuthorizeRequest, AuthorizeResponse>(
    "Payments",
    "Authorize",
    HandleAuthorizeAsync);
grpc.Register("payments", server);

var client = grpc.CreateClient("orders", "payments");
var response = await client.UnaryAsync<AuthorizeRequest, AuthorizeResponse>(
    "Payments",
    "Authorize",
    request,
    new SimulationGrpcCallOptions
    {
        Timeout = TimeSpan.FromSeconds(2),
        MaxAttempts = 3,
        RetryBackoff = TimeSpan.FromMilliseconds(100)
    },
    context.CancellationToken);
```

Calls use canonical gRPC status codes, virtual deadlines, directed partitions, virtual link latency and explicit retry policies. `GrpcFaultPlan` can
inject latency and `Unavailable` failures. Causalia does not inject arbitrary duplicate RPC calls at transport level; repeated application execution
must come from an explicit retry policy.

Client cancellation and deadlines stop the client wait even when a handler ignores its call token. Any late server work remains tracked by the
simulation so durable effects after a timeout can still be observed. A server task that never completes can therefore leave the overall simulation
deadlocked even though its client call has already timed out. Synchronous blocking work cannot be preempted by the cooperative scheduler.

<a id="ecosystemadapters-production-integration"></a>

## Production integration

The ecosystem packages are semantic test adapters, not replacements for the official production SDKs. Keep application logic behind small ports or
adapter seams. Production uses the real Dapr, Kafka, RabbitMQ or gRPC client; deterministic tests bind those same application seams to Causalia.

This avoids simulation-specific branches in production code while still exercising real application handlers, state transitions and idempotency rules.


<a id="entityframeworkcore"></a>

<a id="entityframeworkcore-entity-framework-core-adapter"></a>

# Entity Framework Core adapter

`Causalia.EntityFrameworkCore` maps EF Core `SaveChangesAsync` onto Causalia's deterministic storage commit boundary.
The adapter targets `net10.0` with EF Core 10.

<a id="entityframeworkcore-configuration"></a>

## Configuration

```csharp
var databaseNode = context.CreateNode("orders");
var boundary = context.CreateStorageBoundary(
    "orders-db",
    databaseNode,
    new SimulationStorageClientOptions
    {
        CommitLatency = TimeSpan.FromMilliseconds(50),
        Faults = new StorageFaultPlan()
            .FailAfterCommit(0.01, "commit-ack-lost")
    });

var options = new DbContextOptionsBuilder<OrdersDbContext>()
    .UseSqlServer(connectionString)
    .AddCausaliaStorageSimulation(boundary)
    .Options;
```

The extension registers `CausaliaSaveChangesInterceptor` through the normal EF Core interceptor mechanism.

<a id="entityframeworkcore-commit-semantics"></a>

## Commit semantics

`SavingChangesAsync` opens the Causalia `BeforeCommit` boundary. If that boundary fails, EF never reaches the backing provider.
`SavedChangesAsync` runs only after the backing provider reports success and opens the `AfterCommit` boundary.

That gives Causalia two materially different failures:

```text
BeforeCommit failure
    -> backing provider did not commit
    -> SimulationStorageTransientException

AfterCommit failure
    -> backing provider already reported success
    -> caller observes failure
    -> SimulationStorageAmbiguousCommitException
```

The second case is useful for testing idempotency when application retry logic cannot know whether the previous commit reached durable storage.

<a id="entityframeworkcore-asynchronous-api-only"></a>

## Asynchronous API only

The first adapter slice requires `SaveChangesAsync`. Synchronous `SaveChanges` throws `SynchronousEfCoreOperationNotSupportedException` because a
synchronous call cannot advance Causalia virtual time without blocking the cooperative simulation thread.

<a id="entityframeworkcore-current-boundary"></a>

## Current boundary

The adapter controls the deterministic commit boundary; it does not virtualize the backing EF provider itself. A provider that performs real socket,
file or thread-pool I/O can still introduce nondeterminism outside Causalia. For fully deterministic tests, use a controlled provider or place the
external persistence system behind a later Causalia adapter.

DS-14 does not yet intercept individual relational commands or query materialization. Command-level latency/fault injection can be added later through
EF Core relational interceptors without changing the provider-independent `Causalia.Storage` contract.


<a id="failureintelligence"></a>

<a id="failureintelligence-failure-intelligence"></a>

# Failure intelligence

Causalia 1.7 turns deterministic failures into stable, groupable and causally reduced diagnostics.
A replay token answers **how do I execute this exact timeline again?** Failure intelligence adds three different questions:

1. **What semantic failure is this?**
2. **Is this the same defect as another occurrence under a different seed or schedule?**
3. **Which controlled simulation dimensions are actually necessary for the failure?**

The feature is DS-23 and lives in the core `Causalia` package.

<a id="failureintelligence-stable-semantic-signatures"></a>

## Stable semantic signatures

Every `SimulationFailedException` now exposes:

```csharp
failure.Kind
failure.Signature.Token
```

The signature token is versioned separately and currently starts with `fi2:`.
It deliberately excludes the seed, concrete `v1:` schedule and minimized `m1:` reproduction.
Causalia 2.0.1 moved from `fi1:` to `fi2:` so every semantic field is length-prefixed and delimiter placement cannot create collisions. Previously
stored `fi1:` values remain historical evidence, but should not be compared directly with newly emitted `fi2:` signatures.
Known Causalia failures use semantic identity instead of incidental diagnostics:

- invariants use invariant name and kind;
- linearizability uses history name and result status;
- consistency uses history name and violated consistency rule;
- model-based failures use model name, command name and verifier message;
- deadlocks have one semantic deadlock identity;
- step-limit failures include the configured maximum step count;
- ordinary application exceptions fall back to exception type and message.

That makes the same invariant, consistency or model defect cluster together even when a different seed or scheduler timeline exposed it.
A user exception with changing identifiers in its message remains a different signature by design; prefer stable exception messages or a structured
Causalia verification primitive when cross-run grouping matters.

<a id="failureintelligence-semantic-classification"></a>

## Semantic classification

`FailureKind` distinguishes:

- `UnhandledException`;
- `InvariantViolation` and `InvariantEvaluation`;
- `LinearizabilityViolation` and `LinearizabilityInconclusive`;
- `ConsistencyViolation`;
- `ModelViolation`;
- `Deadlock`;
- `StepLimitExceeded`.

Classification is descriptive and does not guess business severity.

<a id="failureintelligence-describe-without-rerunning"></a>

## Describe without rerunning

When you only have a captured failure, create a descriptive analysis immediately:

```csharp
using Causalia.FailureIntelligence;

var analysis = FailureAnalyzer.Describe(failure);

Console.WriteLine(analysis.Signature.Token);
Console.WriteLine(analysis.Kind);
Console.WriteLine(analysis.Summary);
Console.WriteLine(analysis.Explanation);
```

A descriptive analysis has `FailureTrigger.Unknown` because no causal reduction has been executed yet.
The final trace window is still retained in `TraceContext` for quick diagnostics.

<a id="failureintelligence-prove-the-triggering-dimensions"></a>

## Prove the triggering dimensions

When the scenario can be executed again, use `Simulation.AnalyzeFailureAsync`:

```csharp
var analysis = await Simulation.AnalyzeFailureAsync(
    simulationOptions,
    new FailureAnalysisOptions
    {
        Minimization = new MinimizationOptions
        {
            MaxAttempts = 1_000
        },
        TraceContextEntries = 25
    },
    failure,
    RunScenarioAsync,
    cancellationToken);
```

The analysis reuses DS-9 bounded delta debugging. It first proves that the failure can be reproduced and then removes non-essential
scheduler choices and fault occurrences while preserving the same semantic `fi2:` signature.

`FailureTrigger` reports the remaining controlled dimensions:

- `Deterministic` — canonical scheduling and no injected faults still fail;
- `SchedulerOrdering` — one or more non-canonical scheduler choices are required;
- `FaultInjection` — one or more fault occurrences are required;
- `SchedulerOrderingAndFaultInjection` — both dimensions are required;
- `Unknown` — no minimization proof was requested.

The exact evidence remains available through `EssentialSchedulerChoices`, `EssentialFaults` and `Reproduction`.
`Explanation` names the first essential scheduler decisions and fault policies instead of only reporting counts.

If the minimization budget is exhausted, `Confidence` is `BoundedMinimization`; otherwise it is `Minimized`.
The resulting `m1:` token remains executable with `Simulation.ReproduceAsync`.

<a id="failureintelligence-cluster-repeated-failures"></a>

## Cluster repeated failures

A CI run, load campaign or collection of independently discovered failures can be grouped after analysis:

```csharp
IReadOnlyList<FailureAnalysis> analyses = collectedAnalyses;
var report = FailureAnalyzer.CreateReport(analyses);

foreach (var cluster in report.Clusters)
{
    Console.WriteLine(
        $"#{cluster.TriageRank} {cluster.Signature.Token} " +
        $"{cluster.Kind} x{cluster.OccurrenceCount}");
}
```

Clusters use the semantic `fi2:` token, not the concrete scheduler token.
The report order is deterministic and intended for engineering triage:

1. higher occurrence count first;
2. stronger analysis confidence first;
3. smaller essential reproductions first;
4. signature token as the stable final tie-breaker.

`TriageRank` is therefore an actionability/prevalence order, not a statement about customer or business impact.
Each cluster exposes distinct seeds and observed trigger classes plus the most actionable representative occurrence.

<a id="failureintelligence-testing-and-reqnroll-providers"></a>

## Testing and Reqnroll providers

`SimulationProvider` and `ReqnrollSimulationProvider` populate `LastFailureAnalysis` immediately with a descriptive analysis when a normal run,
replay or exploration fails.

For causal reduction, call the provider directly:

```csharp
var analysis = await causalia.AnalyzeFailureAsync(
    new FailureAnalysisOptions(),
    causalia.LastFailure!,
    RunScenarioAsync,
    cancellationToken);
```

The provider then keeps the minimized analysis in `LastFailureAnalysis` and the underlying minimization result in `LastMinimizationResult`.
`ISimulationProvider` uses default interface members for the new API so existing custom 1.x provider implementations remain source-compatible.

<a id="failureintelligence-visualization"></a>

## Visualization

`Causalia.Visualization` accepts a `FailureAnalysis` directly:

```csharp
var html = analysis.ToTraceHtml(
    new TraceHtmlOptions
    {
        Title = "Checkout failure intelligence"
    });
```

The trace summary includes the semantic signature, failure kind, trigger attribution, analysis confidence and minimized `m1:` reproduction token.
The underlying timeline remains the deterministic representative failure trace.

<a id="failureintelligence-relationship-to-replay-and-minimization"></a>

## Relationship to replay and minimization

The three token types answer different questions:

- `v1:` — exact concrete scheduler timeline;
- `m1:` — sparse deterministic reproduction containing only forced scheduler choices and allowed faults;
- `fi2:` — semantic failure identity for grouping and triage.

A failure can therefore have many `v1:` schedules and multiple minimized `m1:` reproductions while still belonging to one `fi2:` semantic cluster.
Changing `fi2:` semantics in a future release requires a new token-version prefix rather than silently changing the meaning of existing signatures.

<a id="failureintelligence-scope"></a>

## Scope

Failure intelligence only attributes dimensions that Causalia controls and can replay. It does not claim to infer a business root cause from arbitrary
application state, nor does it assign product severity. Uncontrolled wall-clock time, raw threads, external services and other nondeterministic escapes
can invalidate causal reduction just as they can invalidate replay; use `Causalia.Analyzers` and deterministic adapters to keep the boundary explicit.


<a id="features"></a>

<a id="features-causalia-feature-reference"></a>

# Causalia feature reference

This document preserves the detailed DS-1 through DS-26 capability reference. Start with the root `README.md` if you are new to Causalia.

Causalia is a deterministic simulation-testing runtime for concurrent and distributed .NET software.
The goal is to make timing, concurrency, failures, retries and message ordering reproducible instead of probabilistic.

<a id="features-current-vertical-slice-ds-1-through-ds-26"></a>

## Current vertical slice: DS-1 through DS-26

The repository currently provides:

- deterministic virtual time through `TimeProvider`;
- deterministic cooperative async scheduling through a custom `SynchronizationContext`;
- a runtime-independent seeded PRNG based on xoshiro256**;
- a user-facing deterministic random stream through `SimulationContext.Random` that does not perturb scheduler or fault randomness;
- stable seeded runs plus exact branching-schedule replay;
- a timestamped execution trace;
- bounded depth-first and coverage-guided exploration of runnable continuation orders;
- dependency-aware DPOR with conservative fallback, resource access profiles and optional preemption bounding;
- stable runtime/user coverage signals for scheduler branches, applied faults and scenario state;
- portable replay tokens for exact failing schedules;
- deterministic `Always`, `Never` and bounded `Eventually` invariants;
- invariant outcomes and dedicated invariant failure metadata;
- bounded delta-debugging minimization of failing scheduler choices and triggered fault occurrences;
- portable minimized-reproduction tokens that suppress non-essential faults and use canonical scheduler choices by default;
- deterministic concurrent-operation histories with call/return boundaries;
- bounded linearizability checking against executable sequential specifications;
- real-time precedence enforcement, model-state caching and longest-valid-prefix diagnostics;
- dedicated linearizability failure metadata that survives exploration, replay and minimization;
- session, causal and replica consistency verification with bounded eventual convergence;
- executable state-machine model exploration with preconditions, typed observations and stable `mb1:` replay;
- optional scheduler exploration inside each model sequence plus bounded model-sequence minimization;
- semantic `fi2:` failure signatures, failure classification and minimized trigger attribution;
- deterministic clustering and triage ordering across repeated failure occurrences;
- failure exceptions that retain the seed and trace;
- deterministic in-memory point-to-point messaging;
- logical endpoints with typed handlers;
- deterministic logical message identifiers and delivery attempts;
- fixed virtual delivery latency and FIFO processing per endpoint;
- a generic composable deterministic fault engine;
- isolated fault random streams that do not perturb scheduler randomness;
- messaging faults for drop, duplicate, additional delay and reorder;
- simulated nodes with deterministic crash/restart lifecycle and generation-scoped cancellation;
- node-bound message handlers that pause while a node is down and redeliver interrupted work after restart;
- framework-agnostic scenario providers through `Causalia.Testing`;
- a scenario-scoped Reqnroll adapter through `Causalia.Reqnroll`;
- Roslyn determinism diagnostics through `Causalia.Analyzers`;
- deterministic in-memory ASP.NET Core hosting through `Causalia.AspNetCore`;
- deterministic service-to-service HTTP networking with virtual latency, drop, duplicate and directional partitions;
- provider-independent durable storage simulation with transactions, virtual latency, version checks and ambiguous commits;
- an optional EF Core commit-boundary adapter through `Causalia.EntityFrameworkCore`;
- normalized causal trace documents and self-contained interactive HTML export through `Causalia.Visualization`;
- deterministic fixed-concurrency, constant-arrival, ramping-arrival and burst load simulation through `Causalia.Load`;
- deterministic Dapr, Kafka, RabbitMQ and gRPC semantic adapters for common distributed-system failure modes.
- ordered Verification Platform plans with unified Failure Intelligence, portable `vp1:` CI reports and standalone HTML dashboards.

The `Causalia` core package has **zero external runtime dependencies** and targets `net10.0`.

<a id="features-deterministic-scheduling"></a>

## Deterministic scheduling

```csharp
var result = await Simulation.RunAsync(
    new SimulationOptions { Seed = 729381 },
    context => context.ConcurrentAsync(
        async operationCancellationToken =>
        {
            context.TraceEvent("writer:start");
            await Task.Yield();
            await Task.Delay(
                TimeSpan.FromSeconds(10),
                context.TimeProvider,
                operationCancellationToken);
            context.TraceEvent("writer:complete");
        },
        async operationCancellationToken =>
        {
            operationCancellationToken.ThrowIfCancellationRequested();
            context.TraceEvent("reader:start");
            await Task.Yield();
            context.TraceEvent("reader:complete");
        },
        cancellationToken),
    cancellationToken);
```

A seeded run is reproducible, and DS-7 now also captures the exact branching schedule. This separates the random seed used by a normal run
from the concrete continuation order required to replay one discovered failure.

<a id="features-systematic-schedule-exploration"></a>

## Systematic schedule exploration

`ExploreAsync` deliberately walks alternative runnable-continuation orders instead of hoping that different random seeds eventually expose a race:

```csharp
var result = await Simulation.ExploreAsync(
    new ExplorationOptions
    {
        Simulation = new SimulationOptions { Seed = 729381 },
        MaxSchedules = 1_000,
        MaxDecisionDepth = 100
    },
    async context =>
    {
        var value = 0;

        await context.ConcurrentAsync(
            async operationCancellationToken =>
            {
                await Task.Yield();
                operationCancellationToken.ThrowIfCancellationRequested();
                value = 1;
            },
            async operationCancellationToken =>
            {
                await Task.Yield();
                operationCancellationToken.ThrowIfCancellationRequested();

                if (value == 0)
                {
                    throw new InvalidOperationException("Reader ran before writer.");
                }
            },
            context.CancellationToken);
    },
    cancellationToken);
```

When exploration finds a failing schedule it throws `SimulationExplorationFailedException`. The exception contains the exact `SimulationSchedule` and a stable text token such as:

```text
v1:1,1,2,1
```

The schedule can be replayed directly or reconstructed from a logged token:

```csharp
var schedule = SimulationSchedule.Parse(replayToken);

await Simulation.ReplayAsync(
    new SimulationOptions { Seed = 729381 },
    schedule,
    scenario,
    cancellationToken);
```

Replay validates the scheduler step, candidate count and selected work-item identity at every branching point. If the execution shape changed,
Causalia throws `SimulationScheduleReplayException` instead of silently running a different schedule.

Exploration is bounded by both `MaxSchedules` and `MaxDecisionDepth`. `ExplorationResult.ExhaustedWithinBounds` reports whether all schedules
reachable inside the configured depth bound were exhausted, while `DepthLimitReached` reports that deeper branching existed.


<a id="features-coverage-guided-exploration"></a>

## Coverage-guided exploration

DS-15 adds an optional coverage-guided frontier for large bounded state spaces. Depth-first exploration remains the default and keeps its previous
ordering, while `CoverageGuided` probes shallow alternatives and then prioritizes descendants of executions that discovered new coverage.

```csharp
var result = await Simulation.ExploreAsync(
    new ExplorationOptions
    {
        Strategy = ExplorationStrategy.CoverageGuided,
        Simulation = new SimulationOptions { Seed = 729381 },
        MaxSchedules = 10_000,
        MaxDecisionDepth = 100
    },
    async context =>
    {
        await ExecuteScenarioAsync(context);

        context.Coverage.Hit("checkout-authorized");
        context.Coverage.Observe("order-state", order.State.ToString());
        context.Coverage.Observe("retry-count", retryCount);
    },
    cancellationToken);
```

Causalia records scheduler-branch shape and applied fault-policy coverage automatically. User coverage adds stable domain state without coupling the
core runtime to any business model. `SimulationResult.Coverage` exposes the coverage for one run. `ExplorationResult` reports the aggregate
`CoveragePointsDiscovered` and `SchedulesWithNewCoverage` metrics across an exploration.

Coverage signals only influence which unexplored prefix runs next. They do not consume scheduler randomness, change the simulation seed or weaken
exact DS-7 replay. The same failure still carries the concrete `SimulationSchedule` required for strict replay and DS-9 minimization.

<a id="features-invariants"></a>

## Invariants

DS-8 adds temporal correctness rules that are evaluated inside the deterministic runtime rather than only after a scenario finishes:

```csharp
var result = await Simulation.ExploreAsync(
    new ExplorationOptions
    {
        Simulation = new SimulationOptions { Seed = 729381 },
        MaxSchedules = 1_000,
        MaxDecisionDepth = 100
    },
    async context =>
    {
        var balance = 100;
        var ownerCount = 1;
        var converged = false;

        context.Invariants.Always(
            "money is conserved",
            () => balance == 100);

        context.Invariants.Never(
            "resource has multiple owners",
            () => ownerCount > 1);

        context.Invariants.Eventually(
            "replicas converge",
            TimeSpan.FromSeconds(30),
            () => converged);

        await RunScenarioAsync(context);
    },
    cancellationToken);
```

`Always` must remain true and `Never` must remain false at every deterministic observation point after registration. Causalia observes safety
invariants after registration, after each scheduler work item and at successful completion. A synchronous continuation is therefore one atomic
observation segment; Causalia does not inspect intermediate assignments inside one uninterrupted continuation.

`Eventually` starts a liveness obligation at registration time. The simulation remains alive until the condition is satisfied or its virtual
deadline is reached. Work scheduled exactly at the deadline is allowed to run before Causalia decides the liveness obligation, so an invariant
cannot fail merely because the internal deadline wake-up raced with user work at the same virtual instant.

A violation is wrapped by the normal deterministic failure pipeline:

```text
SimulationExplorationFailedException
  -> SimulationFailedException
       -> SimulationInvariantViolationException
```

That means a violation discovered during `ExploreAsync` automatically retains the exact `SimulationSchedule`, replay token, seed and trace.
Predicate exceptions are reported separately as `SimulationInvariantEvaluationException`.

Successful runs expose completed invariant metadata through `SimulationResult.Invariants`. Invariant names are unique within one simulation run
so traces and BDD assertions remain unambiguous.


<a id="features-failure-minimization"></a>

## Failure minimization

DS-9 reduces a discovered failure instead of forcing a developer to inspect every scheduler and fault decision from the original run.
The minimizer starts from the concrete failing execution, keeps only non-canonical scheduler choices plus triggered fault occurrences, and then
uses bounded delta debugging to remove chunks that are not required for the same failure identity.

```csharp
SimulationExplorationFailedException discovered;

try
{
    await Simulation.ExploreAsync(
        explorationOptions,
        scenario,
        cancellationToken);

    throw new InvalidOperationException("Expected exploration to find a failure.");
}
catch (SimulationExplorationFailedException exception)
{
    discovered = exception;
}

var minimized = await Simulation.MinimizeAsync(
    explorationOptions.Simulation,
    new MinimizationOptions
    {
        MaxAttempts = 1_000
    },
    discovered.Failure,
    scenario,
    cancellationToken);

Console.WriteLine(minimized.Reproduction.ReplayToken);
```

A minimized reproduction contains two explicit inputs:

- non-canonical scheduler choices that must still be forced; all omitted scheduler decisions choose runnable candidate `0`;
- triggered fault occurrences that are still allowed to take effect; naturally triggered faults not present in the reproduction are suppressed.

The reproduction is portable through a versioned `m1:` token:

```csharp
var reproduction = SimulationReproduction.Parse(replayToken);

await Simulation.ReproduceAsync(
    new SimulationOptions { Seed = reproduction.Seed },
    reproduction,
    scenario,
    cancellationToken);
```

`ReplayAsync` and `ReproduceAsync` deliberately have different contracts. Exact schedule replay remains strict and detects execution-shape drift.
A minimized reproduction is sparse: it forces only the scheduler choices that survived minimization and suppresses faults that were proven
unnecessary. The concrete run still captures a normal full `SimulationSchedule`, trace and applied fault list for diagnostics.

Causalia 1.7 uses the same semantic `fi2:` identity for minimization and failure intelligence. Invariants use name and kind; linearizability uses
history and status; consistency uses history and violated guarantee; model failures use model, command and verifier message; ordinary exceptions fall
back to exception type and message. `MinimizationOptions.MaxAttempts` bounds the amount of additional execution spent
reducing a failure, and `MinimizationResult.ExhaustedBudget` reports whether that bound stopped reduction early.

A minimization scenario must be safe to execute repeatedly. State that belongs to one simulated run should therefore be created inside the
scenario delegate rather than retained in mutable captured variables between invocations.

<a id="features-linearizability-checking"></a>

## Linearizability checking

DS-10 records concurrent operation histories and checks whether the observed results can be explained by any legal sequential execution that also
respects real-time precedence. If operation A completed before operation B was invoked, every candidate linearization must place A before B.

```csharp
var history = context.Linearizability
    .CreateHistory<RegisterInput, RegisterOutput>("register");

var write = history.Begin("writer", RegisterInput.Write(200));
var read = history.Begin("reader", RegisterInput.Read());

read.Complete(RegisterOutput.FromValue(0));
write.Complete(RegisterOutput.Ack());

var specification = new LinearizabilitySpecification<int, RegisterInput, RegisterOutput>(
    0,
    (state, input, output) => input.IsRead
        ? output.HasValue && output.Value == state
            ? LinearizabilityStep<int>.Accept(state)
            : LinearizabilityStep<int>.Reject(state)
        : !output.HasValue
            ? LinearizabilityStep<int>.Accept(input.Value)
            : LinearizabilityStep<int>.Reject(state));

history.RequireLinearizable(specification);
```

The specification is executable: each candidate operation either rejects the current model state or produces a next model state. Model state
should be treated as immutable by the step function because the checker explores speculative branches and caches equivalent `(operation set, state)`
pairs. A custom `IEqualityComparer<TState>` can be supplied when the default state equality is not appropriate.

The first bounded implementation supports at most 63 completed operations per history and is additionally limited by `MaxSearchStates`. Reaching
either bound returns `Inconclusive`; `RequireLinearizable` turns that into `SimulationLinearizabilityInconclusiveException` instead of silently
passing the test. Pending operations are reported and omitted from the completed-history check, which is conservative for the current slice.

A proven violation throws `SimulationLinearizabilityViolationException` through the normal deterministic failure pipeline:

```text
SimulationExplorationFailedException
  -> SimulationFailedException
       -> SimulationLinearizabilityViolationException
```

`SimulationFailedException.LinearizabilityFailure` exposes the history name, status, search-state count and longest valid partial linearization.
Because the violation uses the same simulation failure pipeline, schedule exploration, exact replay and DS-9 minimization work without a second
execution model. Linearizability minimization fingerprints are semantic: history name plus status, not incidental search-state counts.

<a id="features-deterministic-messaging-with-faults"></a>

## Deterministic messaging with faults

```csharp
var faults = new MessageFaultPlan()
    .Drop(0.01)
    .Duplicate(0.02)
    .Delay(0.05, TimeSpan.FromSeconds(5))
    .Reorder(0.03);

var result = await Simulation.RunAsync(
    new SimulationOptions { Seed = 729381 },
    async context =>
    {
        var bus = context.CreateMessageBus(
            new MessageBusOptions
            {
                DeliveryLatency = TimeSpan.FromMilliseconds(25),
                Faults = faults
            });

        bus.RegisterHandler<OrderSubmitted>(
            "orders",
            async (message, delivery, handlerCancellationToken) =>
            {
                Console.WriteLine($"Handling {delivery.MessageId}/{delivery.Attempt}: {message.OrderId}");
                await Task.Yield();
                handlerCancellationToken.ThrowIfCancellationRequested();
            });

        await bus.SendAsync(
            "orders",
            new OrderSubmitted("order-42"),
            context.CancellationToken);
    },
    cancellationToken);
```

A duplicate delivery retains the same logical message identifier and receives an incrementing `Attempt` value.
Fault decisions are deterministic for the simulation seed and use a random stream isolated from scheduler selection.

The trace can contain entries such as:

```text
fault:messaging:duplicate:1:1:orders
messaging:enqueued:1:1:orders:Example.OrderSubmitted
messaging:enqueued:1:2:orders:Example.OrderSubmitted
messaging:delivered:1:1:orders:Example.OrderSubmitted
messaging:completed:1:1:orders:Example.OrderSubmitted
```

`SendAsync` models broker acceptance rather than consumer completion. Causalia tracks delivery pumps as part of the simulation.
Use `SendAndWaitAsync` when the simulated caller explicitly needs to wait for all generated deliveries to settle.
A deterministically dropped message generates no consumer delivery, so `SendAndWaitAsync` completes immediately for that send.


<a id="features-simulated-nodes-and-crashrestart"></a>

## Simulated nodes and crash/restart

Nodes model the availability and execution lifetime of a logical process or service instance without coupling Causalia to any hosting framework.

```csharp
var result = await Simulation.RunAsync(
    new SimulationOptions { Seed = 729381 },
    async context =>
    {
        var worker = context.CreateNode("worker");
        var bus = context.CreateMessageBus();

        bus.RegisterNodeHandler<OrderSubmitted>(
            worker,
            async (message, delivery, handlerCancellationToken) =>
            {
                await ProcessAsync(message, handlerCancellationToken);
            });

        var completion = bus.SendAndWaitAsync(
            "worker",
            new OrderSubmitted("order-42"),
            context.CancellationToken);

        await Task.Delay(
            TimeSpan.FromSeconds(5),
            context.TimeProvider,
            context.CancellationToken);

        worker.Crash();

        await Task.Delay(
            TimeSpan.FromSeconds(10),
            context.TimeProvider,
            context.CancellationToken);

        worker.Restart();
        await completion;
    },
    cancellationToken);
```

A node starts in generation `1`. `Crash()` cancels the node generation token, which interrupts cooperative in-flight work.
`Restart()` creates a fresh generation token and increments `Generation`.

For a node-bound message handler:

- queued messages do not start while the node is crashed;
- a cooperative in-flight handler is interrupted when the node crashes;
- the interrupted logical message is redelivered after restart;
- the logical `MessageId` stays stable;
- each delivery start receives a unique incrementing `Attempt`;
- lifecycle and interruption decisions are written to the deterministic trace.

Example trace:

```text
node:created:worker:generation:1
messaging:delivered:1:1:worker:Example.OrderSubmitted
node:crashed:worker:generation:1
messaging:interrupted:1:1:worker:Example.OrderSubmitted
node:restarted:worker:generation:2
messaging:delivered:1:2:worker:Example.OrderSubmitted
messaging:completed:1:2:worker:Example.OrderSubmitted
```

`SimulationNode.RunAsync` can also bind arbitrary cooperative work to the current node generation. The node abstraction intentionally does not
reconstruct arbitrary application memory. Causalia 1.2 adds `SimulationProcess<TGeneration>` for tests that require volatile generation state to be
discarded and rebuilt while durable resources remain outside the process generation.

<a id="features-generic-fault-policies"></a>

## Generic fault policies

Fault injection is not implemented as messaging-specific logic in the scheduler. Any simulation domain can define an event context and an effect type:

```csharp
var plan = new FaultPlan<StorageWrite, StorageFault>()
    .Add(
        new ProbabilityFaultPolicy<StorageWrite, StorageFault>(
            "storage.timeout",
            0.01,
            write => new StorageTimeoutFault(write.Key)));

var injector = context.CreateFaultInjector("storage", plan);
var effects = injector.Evaluate(write);
```

Messaging simply supplies a fluent domain adapter over this generic engine.

<a id="features-determinism-analyzer"></a>

## Determinism analyzer

DS-11 adds the optional `Causalia.Analyzers` package. It recognizes direct Causalia scenario delegates and code explicitly marked with
`[DeterministicSimulation]`, then reports APIs that escape deterministic replay.

```csharp
await Simulation.RunAsync(
    new SimulationOptions { Seed = 729381 },
    async context =>
    {
        var now = context.TimeProvider.GetUtcNow();
        var id = context.Random.NextGuid();
        var value = context.Random.NextInt32(100);

        await Task.Delay(
            TimeSpan.FromSeconds(1),
            context.TimeProvider,
            context.CancellationToken);
    },
    cancellationToken);
```

The analyzer reports six warning categories:

- `CAU1001` — wall-clock time, real delays and real timers;
- `CAU1002` — nondeterministic or runtime-dependent randomness;
- `CAU1003` — raw threads, thread-pool scheduling, `Task.Run` and similar scheduler escapes;
- `CAU1004` — synchronous waits that can block Causalia's cooperative execution thread;
- `CAU1005` — a method-group scenario must be marked `[DeterministicSimulation]` so its body is analyzed;
- `CAU1006` — `ConfigureAwait(false)` escapes the simulation synchronization context.

`SimulationContext.Random` uses its own deterministic stream derived from the simulation seed. Consuming user randomness therefore does not change
the scheduler choices or the fault-injection streams for the same simulation.

Helpers outside a direct scenario delegate can opt into analysis explicitly:

```csharp
[DeterministicSimulation]
private static Task ProcessAsync(
    SimulationContext context,
    CancellationToken cancellationToken)
{
    return Task.Delay(
        TimeSpan.FromSeconds(1),
        context.TimeProvider,
        cancellationToken);
}
```

A method or type can use `[AllowNondeterminism("reason")]` when crossing the deterministic boundary is intentional. Standard Roslyn diagnostic
suppression remains available as well. The analyzer is deliberately scoped: it does not claim to make arbitrary external libraries deterministic,
and helper methods that are neither structurally inside a scenario lambda nor marked `[DeterministicSimulation]` are outside the DS-11 boundary.

Use ordinary `await` within deterministic boundaries. A dependency that must use `ConfigureAwait(false)` should be isolated behind an explicitly
documented `[AllowNondeterminism]` boundary and is outside Causalia's replay guarantee.

See [Analyzers](#analyzers) for installation and the complete first-slice rule set.

<a id="features-aspnet-core-simulation-host"></a>

## ASP.NET Core simulation host

DS-12 and DS-13 use the optional `Causalia.AspNetCore` package. It runs a real ASP.NET Core middleware, routing and endpoint pipeline on an in-memory
`IServer` implementation instead of a network socket. The adapter registers the simulation `TimeProvider`, `SimulationContext` and host
`SimulationNode` in ASP.NET Core DI.

```csharp
await using var host = await context.StartAspNetCoreAsync(
    app =>
    {
        app.MapGet(
            "/clock",
            async (TimeProvider timeProvider, HttpContext httpContext) =>
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(10),
                    timeProvider,
                    httpContext.RequestAborted);

                return Results.Ok(timeProvider.GetUtcNow());
            });
    },
    context.CancellationToken);

using var request = new HttpRequestMessage(HttpMethod.Get, "/clock");
using var response = await host.SendAsync(request, context.CancellationToken);
```

`host.CreateClient()` provides a normal `HttpClient` backed by the same in-memory deterministic transport. Multiple requests can be run with
`ConcurrentAsync` and explored by DS-7 like any other asynchronous Causalia workload. DS-13 also adds a multi-host HTTP network:

```csharp
var network = context.CreateHttpNetwork();
network.Register("orders", ordersHost);
network.Register("payments", paymentsHost);

network.Between("orders", "payments")
    .Latency(TimeSpan.FromMilliseconds(25))
    .Drop(0.01)
    .Duplicate(0.02)
    .Delay(0.05, TimeSpan.FromSeconds(2));

using var payments = network.CreateClient("orders", "payments");
```

`Between` is directional. `Partition()` keeps that directed link unavailable until `Heal()` is called. Network fault decisions use the same isolated
fault engine as DS-5, so they participate in deterministic replay and DS-9 fault minimization without perturbing scheduler randomness.

Every ASP.NET Core host owns a `SimulationNode`. Crashing that node cancels in-flight HTTP work through `HttpContext.RequestAborted`. The original
`StartAspNetCoreAsync` API retains this lightweight node-only lifecycle. Causalia 1.2 adds `StartAspNetCoreProcessAsync` for full `WebApplication` and
DI reconstruction, deterministic generation-scoped background services, and process-aware network registrations. WebSockets, TLS handshakes, HTTP/2
frames, kernel buffers and incremental network streaming remain outside the model.

See [AspNetCore](#aspnetcore) for the full adapter contract and current boundaries.

<a id="features-deterministic-storage-and-ef-core"></a>

## Deterministic storage and EF Core

DS-14 adds the optional `Causalia.Storage` package. Durable state is owned by a simulated database and can be accessed by node-bound clients:

```csharp
var node = context.CreateNode("orders");
var database = context.CreateStorageDatabase("orders-db");
var client = database.CreateClient(
    node,
    new SimulationStorageClientOptions
    {
        OperationLatency = TimeSpan.FromMilliseconds(5),
        CommitLatency = TimeSpan.FromMilliseconds(20),
        Faults = new StorageFaultPlan()
            .FailAfterCommit(0.01, "commit-ack-lost")
    });

var transaction = client.BeginTransaction();
transaction.Write("orders/42", payload);
await transaction.CommitAsync(context.CancellationToken);
```

Committed state survives a simulated node restart. A failure before commit leaves staged changes non-durable; a failure after commit throws
`SimulationStorageAmbiguousCommitException` after the state is already durable. This models the retry hazard where a caller loses the commit
acknowledgement and cannot know whether retrying will duplicate work. Reads expose per-key versions, and writes or staged transaction changes can
require an expected version to detect stale updates.

`Causalia.EntityFrameworkCore` maps the same boundary onto EF Core `SaveChangesAsync` through `CausaliaSaveChangesInterceptor`:

```csharp
var boundary = context.CreateStorageBoundary(
    "orders-db",
    node,
    new SimulationStorageClientOptions
    {
        CommitLatency = TimeSpan.FromMilliseconds(50),
        Faults = new StorageFaultPlan().FailAfterCommit(0.01)
    });

var options = new DbContextOptionsBuilder<OrdersDbContext>()
    .UseSqlServer(connectionString)
    .AddCausaliaStorageSimulation(boundary)
    .Options;
```

The adapter deliberately requires `SaveChangesAsync`; synchronous `SaveChanges` cannot advance virtual time safely and is rejected. The commit
boundary is deterministic, but DS-14 does not claim to virtualize arbitrary I/O performed by the underlying EF provider.

See [Storage](#storage) and [EntityFrameworkCore](#entityframeworkcore) for the full contract and boundaries.


<a id="features-deterministic-simulated-load"></a>

## Deterministic simulated load

DS-17 adds the optional `Causalia.Load` package. It runs virtual closed-model and open-model workloads inside the normal deterministic scheduler.
This means concurrency pressure is reproducible and participates in exploration, exact replay, minimization, invariants and coverage guidance.

The public profiles are `FixedConcurrencyLoadProfile`, `ConstantArrivalRateLoadProfile`, `RampingArrivalRateLoadProfile` and `BurstLoadProfile`.
`LoadRunResult` reports virtual latency percentiles, peak concurrency, failures and dropped arrivals. Optional thresholds can fail the simulation
with a replayable `SimulationLoadThresholdException`. See [Load](#load) for the complete usage contract.

<a id="features-trace-visualization"></a>

## Trace visualization

DS-16 adds the optional `Causalia.Visualization` package. It consumes existing deterministic trace data and does not participate in scheduling,
fault evaluation or replay. Successful runs and deterministic failures can both be normalized and exported:

```csharp
using Causalia.Visualization;

var document = result.ToTraceDocument();
var html = result.ToTraceHtml(
    new TraceHtmlOptions
    {
        Title = "Checkout race",
        ShowSchedulerEvents = false
    });

await File.WriteAllTextAsync(
    "checkout-race.html",
    html,
    cancellationToken);
```

`SimulationTraceDocument` classifies known trace protocols into scheduler, node, messaging, ASP.NET Core, network, storage, fault, invariant,
linearizability and load categories. It derives logical lanes and correlation IDs for message, HTTP-network, storage and linearizability operations while
leaving unknown `TraceEvent` messages visible as user events.

The HTML export is one self-contained file with no external scripts, stylesheets or web assets. It includes seed, replay token, virtual time,
failure metadata, category/lane/severity filters, full-text search and clickable correlation-chain filters, making it suitable as a CI artifact.
See [Visualization](#visualization) for the public model and export contract.

<a id="features-reqnroll-adapter"></a>

## Reqnroll adapter

`Causalia.Reqnroll` is intentionally thin. Reqnroll can create ordinary context classes with scenario lifetime through constructor injection,
so `ReqnrollSimulationProvider` has no dependency on Reqnroll runtime types and does not require a custom Reqnroll plugin.

```csharp
[Binding]
public sealed class CheckoutSteps
{
    private readonly ReqnrollSimulationProvider _causalia;
    private readonly TestContext _testContext;

    public CheckoutSteps(ReqnrollSimulationProvider causalia, TestContext testContext)
    {
        _causalia = causalia;
        _testContext = testContext;
    }

    [Given("Causalia uses seed {long}")]
    public void GivenCausaliaUsesSeed(long seed)
    {
        _causalia.Configure(new SimulationOptions { Seed = checked((ulong)seed) });
    }

    [When("the deterministic checkout scenario runs")]
    public async Task WhenTheDeterministicCheckoutScenarioRuns()
    {
        await _causalia.RunAsync(
            async context =>
            {
                // Arrange and execute deterministic concurrent work here.
                await Task.Yield();
                context.CancellationToken.ThrowIfCancellationRequested();
            },
            _testContext.CancellationToken);
    }

    [Then("the simulation used seed {long}")]
    public void ThenTheSimulationUsedSeed(long seed)
    {
        Assert.AreEqual(checked((ulong)seed), _causalia.LastResult!.Seed);
    }
}
```

Because the provider instance is scenario-scoped, configuration, `LastResult`, `LastFailure`, `LastInvariantFailure`, `LastLinearizabilityFailure`, `LastConsistencyFailure` and
`LastMinimizationResult` can be shared across multiple binding classes in the same scenario.
The current adapter runs a deterministic simulation per `RunAsync` call. A later adapter milestone can add a long-lived simulation session if step-by-step
control of one continuously running virtual world proves useful.

<a id="features-current-boundary"></a>

## Current boundary

DS-1 through DS-26 deliberately use a **cooperative simulation model**. Code is deterministic when asynchronous continuations flow through the
simulation `SynchronizationContext` and time flows through the supplied `TimeProvider`.

The runtime does not intercept arbitrary `Task.Run`, raw threads, wall-clock calls, cryptographic randomness, sockets, file I/O or other sources
of nondeterminism. DS-11 now catches a useful set of these escapes at compile time inside recognized deterministic boundaries, but the runtime still
does not virtualize them. External libraries and unannotated helper methods remain the application's responsibility until later adapters or analysis
capabilities model those boundaries explicitly.

<a id="features-roadmap"></a>

## Roadmap

1. **DS-1** — virtual deterministic time
2. **DS-2** — deterministic async scheduler
3. **DS-3** — stable seeded replay and trace
4. **DS-4** — deterministic simulated messaging primitives
5. **DS-5** — generic fault engine plus messaging delay, drop, duplicate and reorder
6. **DS-6** — simulated node crash and restart ✓
7. **DS-7** — systematic schedule exploration ✓
8. **DS-8** — invariant engine ✓
9. **DS-9** — failing-trace minimization ✓
10. **DS-10** — linearizability checking ✓
11. **DS-11** — Roslyn nondeterminism analyzer ✓
12. **DS-12** — ASP.NET Core simulation host ✓
13. **DS-13** — deterministic service-to-service HTTP network ✓
14. **DS-14** — deterministic durable storage plus EF Core commit-boundary adapter ✓
15. **DS-15** — coverage-guided exploration ✓
16. **DS-16** — trace visualizer and debugger ✓
17. **DS-17** — deterministic simulated load ✓
18. **DS-18** — full simulated process lifecycle ✓
19. **DS-19** — deterministic ecosystem adapters ✓
20. **DS-20** — advanced exploration with DPOR ✓
21. **DS-21** — distributed consistency verification ✓
22. **DS-22** — executable model-based testing ✓
23. **DS-23** — deterministic failure intelligence ✓
24. **DS-24** — deterministic time-travel debugger ✓
25. **DS-25** — Production Reality Bridge ✓
26. **DS-26** — Verification Platform ✓

Testing adapters such as Reqnroll are intentionally introduced earlier because they do not couple application-runtime frameworks to the core.
Real-world throughput benchmarking remains outside the deterministic runtime; future adapters can bridge Causalia scenarios to dedicated performance tools.

<a id="features-design-rules"></a>

## Design rules

- `Causalia` core does not depend on ASP.NET Core, Entity Framework Core, Dapr, Kafka, RabbitMQ, Redis, Reqnroll or another application/test framework.
- Integrations depend on Causalia, never the reverse.
- Fault randomness must not perturb scheduler randomness.
- A logical message identifier remains stable across duplicate/redelivery attempts.
- Every nondeterministic choice introduced by Causalia must be reproducible from simulation inputs and seed.


<a id="features-ds-18-full-simulated-process-lifecycle"></a>

## DS-18: full simulated process lifecycle

Causalia 1.2 layers `SimulationProcess<TGeneration>` over the lower-level node primitive. Each restart replaces the volatile generation object,
cancels generation-scoped work and reruns the generation factory, while durable resources deliberately created outside the generation remain intact.

`StartAspNetCoreProcessAsync` applies the same model to ASP.NET Core by rebuilding the `WebApplication`, service provider and singleton graph after
restart. `SimulationHttpNetwork` can register the logical process so existing clients follow the replacement generation, and
`SimulationBackgroundService` keeps long-running hosted work under deterministic generation cancellation.


<a id="features-ds-19-deterministic-ecosystem-adapters"></a>

## DS-19: deterministic ecosystem adapters

Causalia 1.3 adds optional semantic adapters for Dapr, Kafka, RabbitMQ and gRPC. They model state/ETag, redelivery, partition/offset, acknowledgement,
deadline and retry behavior without adding those production SDKs to Causalia core. See [EcosystemAdapters](#ecosystemadapters).


<a id="features-ds-20-advanced-exploration-with-dpor"></a>

## DS-20: advanced exploration with DPOR

Causalia 1.4 adds conservative Dynamic Partial Order Reduction. Tests can declare complete resource-access sets for logical exploration operations
using `Read`, `Write` and `Synchronize`; DPOR removes only schedules proven equivalent by those declarations. Unmodeled operations remain
conservatively dependent.

The scheduler records causal parent/work-item lineage across continuations and virtual-time wakeups, supports an optional preemption bound through
`ExplorationOptions.MaxPreemptions`, and reports equivalent/pruned schedule metrics. Exact replay and failure minimization remain compatible with
DPOR-discovered schedules. See [AdvancedExploration](#advancedexploration).


<a id="features-ds-21-distributed-consistency-verification"></a>

## DS-21: distributed consistency verification

Causalia 1.5 adds logical distributed-consistency histories through `SimulationContext.Consistency`. Tests can record the write version observed by
each read and complete replica snapshots, then require read-your-writes, monotonic reads, monotonic writes, writes-follow-reads, causal visibility and
replica/read agreement.

`RequireConvergence` adds bounded eventual convergence on virtual time. Consistency violations flow through the deterministic failure, replay,
exploration and minimization pipeline, while successful runs expose `ConsistencyOutcome` metadata. See [Consistency](#consistency).

<a id="features-ds-22-executable-model-based-testing"></a>

## DS-22: executable model-based testing

Causalia 1.6 adds bounded executable state-machine exploration through `ModelBasedSpecification<TState, TSystem>`. Each model command has a stable
name, state precondition, pure expected-state transition, deterministic system-under-test execution and typed observation verifier.

`Simulation.CheckModelAsync` explores valid non-empty command sequences up to `MaxSequences` and `MaxCommandDepth`. An optional
`ModelBasedScheduleExplorationOptions` runs normal depth-first, coverage-guided or DPOR scheduler exploration independently for every model sequence,
so business-state exploration and concurrency-timeline exploration remain orthogonal.

Failures retain an `mb1:` model-sequence token plus the normal `v1:` scheduler token. `ReplayModelAsync` can replay only the command sequence or
combine it with one exact scheduler schedule. `MinimizeModelAsync` removes unnecessary contiguous command ranges while preserving the same failure
class. Model traces use dedicated visualization category/lane metadata, and the framework-neutral provider retains structured model failures and
model exploration/minimization results. See [ModelBasedTesting](#modelbasedtesting).

<a id="features-ds-23-deterministic-failure-intelligence"></a>

## DS-23: deterministic failure intelligence

Causalia 1.7 promotes failure identity from an internal minimization concern into public diagnostics. Every `SimulationFailedException` now exposes a
semantic `FailureKind` and versioned `fi2:` signature that excludes seed and concrete scheduler order. Known Causalia verification failures use stable
semantic fields so equivalent invariants, consistency violations, model mismatches and linearizability results cluster across different timelines.

`Simulation.AnalyzeFailureAsync` combines this semantic identity with DS-9 delta debugging. The resulting `FailureAnalysis` classifies the essential
controlled trigger as deterministic logic, scheduler ordering, injected faults or a required combination of ordering and faults, and exposes the exact
surviving scheduler choices/fault occurrences plus an `m1:` reproduction. `FailureAnalyzer.CreateReport` groups repeated analyses by `fi2:` signature
and orders clusters deterministically for triage. Testing/Reqnroll providers retain the latest analysis and Visualization can render the signature,
trigger, confidence and compact reproduction directly. See [FailureIntelligence](#failureintelligence).


<a id="features-ds-24-deterministic-time-travel-debugger"></a>

## DS-24: deterministic time-travel debugger

Causalia 1.8 adds replay-stable debugger checkpoints through `SimulationContext.TimeTravel`. Scenarios register named textual state probes and can
capture explicit checkpoints in the default `ManualOnly` mode. `TimeTravelCaptureMode.SchedulerSteps` additionally captures start, before-step,
after-step, completion and failure boundaries without creating scheduler decisions or consuming deterministic random streams.

Every checkpoint exposes a stable `tt1:` token, completed scheduler-step count, virtual timestamp, trace-event count, kind, optional label and immutable
probe snapshots. Probe capture failures are recorded as debugger data instead of altering system-under-test behavior. Bounded retention discards the
oldest checkpoints while preserving monotonic token indexes and reporting total/dropped counts.

`TimeTravelDebugger` navigates previous/next checkpoints, seeks by `tt1:` token or scheduler step, positions immediately before or after a causal trace
event and jumps between changes of one probe. `TimeTravelTimeline.Diff` reports added, removed and changed watched values. Successful results and
deterministic failures both retain the timeline, Testing/Reqnroll providers expose `LastTimeTravel`, and `Causalia.Visualization` renders an interactive
checkpoint/state inspector in the standalone HTML viewer. See [TimeTravel](#timetravel).


<a id="features-ds-25-production-reality-bridge"></a>

## DS-25: Production Reality Bridge

Causalia 1.9 normalizes immutable production observations into content-addressed `pr2:` evidence. Production-derived operation profiles can drive exact
observed duration/outcome sampling through an isolated deterministic random stream, while correlated incidents can replay production relative start
timing inside virtual time. Successful and failed executions retain the exact production observations they consumed. See [ProductionReality](#productionreality).


<a id="features-ds-26-verification-platform"></a>

## DS-26: Verification Platform

Causalia 2.0 composes the deterministic engines from DS-1 through DS-25 into ordered `VerificationPlan` executions. Seeded simulation, bounded
scheduler exploration and executable model verification retain their native evidence while contributing to one Failure Intelligence report.

`VerificationReport` provides a portable content-addressed `vp1:` CI artifact with ordered step status, seeds, `v1:` replay references, `fi2:`
semantic identities, time-travel counts and referenced `pr2:` production evidence. `Causalia.Visualization` can render the same result as a standalone
HTML dashboard. Plan orchestration is sequential by design; concurrency remains inside controlled simulation steps.


<a id="load"></a>

<a id="load-deterministic-simulated-load"></a>

# Deterministic simulated load

`Causalia.Load` adds virtual load generation to the same deterministic scheduler used for schedule exploration, replay, faults and invariants.
It is intended to answer correctness questions under concurrency pressure, not to replace wall-clock throughput tools such as k6 or NBomber.

Because load runs on virtual time, a profile can model hours or days of arrivals without sleeping for the corresponding real duration. Iteration
continuations remain visible to Causalia's scheduler, so a race discovered under load retains the normal replay and minimization behavior.

<a id="load-install"></a>

## Install

```bash
dotnet add package Causalia.Load
```

The package depends only on `Causalia` and targets the same runtime frameworks.

<a id="load-fixed-concurrency"></a>

## Fixed concurrency

A closed-model workload keeps a fixed number of logical actors active. Each actor executes the configured number of iterations sequentially.

```csharp
using Causalia;
using Causalia.Load;
using Causalia.Load.Profiles;

var result = await Simulation.RunAsync(
    new SimulationOptions { Seed = 729381 },
    async context =>
    {
        var load = context.CreateLoadRunner();

        var loadResult = await load.RunAsync(
            new FixedConcurrencyLoadProfile
            {
                VirtualUsers = 100,
                IterationsPerUser = 50,
                ThinkTime = TimeSpan.FromMilliseconds(250)
            },
            iteration => ExecuteCheckoutAsync(iteration.CancellationToken),
            context.CancellationToken);

        Console.WriteLine(loadResult.PeakConcurrency);
        Console.WriteLine(loadResult.Latency.Percentile99);
    },
    cancellationToken);
```

`ThinkTime` uses `SimulationContext.TimeProvider`, so it advances deterministic virtual time rather than wall-clock time.

<a id="load-constant-arrival-rate"></a>

## Constant arrival rate

An open-model workload schedules iterations independently of how quickly previous iterations finish.

```csharp
var loadResult = await context.CreateLoadRunner().RunAsync(
    new ConstantArrivalRateLoadProfile
    {
        Rate = 500,
        TimeUnit = TimeSpan.FromSeconds(1),
        Duration = TimeSpan.FromMinutes(10),
        MaxConcurrentIterations = 2_000
    },
    iteration => ExecuteCheckoutAsync(iteration.CancellationToken),
    context.CancellationToken);
```

Arrivals are fractionally spaced on the deterministic virtual arrival clock. If all logical actor slots are busy when an arrival occurs, that
arrival is recorded as dropped instead of waiting for an existing iteration to finish. This preserves the defining open-model property that the
arrival process is independent from system response time.

<a id="load-ramping-arrival-rate"></a>

## Ramping arrival rate

Use `RampingArrivalRateLoadProfile` for a piecewise-linear virtual arrival curve:

```csharp
var loadResult = await context.CreateLoadRunner().RunAsync(
    new RampingArrivalRateLoadProfile
    {
        StartRate = 0,
        TimeUnit = TimeSpan.FromSeconds(1),
        MaxConcurrentIterations = 20_000,
        Stages = new List<ArrivalRateStage>
        {
            new() { Duration = TimeSpan.FromMinutes(5), TargetRate = 500 },
            new() { Duration = TimeSpan.FromMinutes(10), TargetRate = 10_000 },
            new() { Duration = TimeSpan.FromMinutes(5), TargetRate = 10_000 },
            new() { Duration = TimeSpan.FromMinutes(5), TargetRate = 250 }
        }
    },
    iteration => ExecuteCheckoutAsync(iteration.CancellationToken),
    context.CancellationToken);
```

Causalia integrates each linear rate stage and schedules iterations at deterministic fractional arrival points. No real timer or wall-clock pacing
is used.

<a id="load-burst-traffic"></a>

## Burst traffic

A burst schedules all requested iterations at one virtual instant. The concurrency cap controls how many can actually start.

```csharp
var result = await context.CreateLoadRunner().RunAsync(
    new BurstLoadProfile
    {
        Iterations = 10_000,
        MaxConcurrentIterations = 2_500
    },
    iteration => HandleFlashSaleAsync(iteration.CancellationToken),
    context.CancellationToken);
```

This is useful for correctness scenarios around thundering herds, lock contention, duplicate handling and queue saturation.

<a id="load-metrics"></a>

## Metrics

`LoadRunResult` contains deterministic virtual metrics:

```text
ScheduledIterations
StartedIterations
CompletedIterations
FailedIterations
DroppedIterations
PeakConcurrency
VirtualElapsed
FailureRate
DroppedRate
CompletedIterationsPerVirtualSecond
Latency.Minimum
Latency.Mean
Latency.Median
Latency.Percentile95
Latency.Percentile99
Latency.Maximum
```

Latency statistics use virtual iteration durations. Percentiles are calculated from the exact durations recorded by the load run rather than a
wall-clock sampling thread.

<a id="load-failure-handling-and-thresholds"></a>

## Failure handling and thresholds

The default failure mode propagates an iteration exception into the simulation. New open-model arrivals stop once the failure becomes visible to
the deterministic scheduler.

For a load-style error budget, record iteration failures and evaluate thresholds after the profile completes:

```csharp
await context.CreateLoadRunner().RunAsync(
    profile,
    ExecuteCheckoutAsync,
    new LoadRunOptions
    {
        FailureMode = LoadIterationFailureMode.RecordAndContinue,
        MaximumFailureSamples = 100,
        Thresholds = new LoadThresholds
        {
            MaximumFailureRate = 0.01,
            MaximumDroppedRate = 0.001,
            MaximumPercentile99 = TimeSpan.FromSeconds(2)
        }
    },
    context.CancellationToken);
```

A threshold violation throws `SimulationLoadThresholdException`. Like other scenario exceptions it is wrapped by `SimulationFailedException`, so
the failure retains its seed, schedule and replay token. The exception also contains the completed `LoadRunResult` and individual threshold
violations.

<a id="load-exploration-and-replay"></a>

## Exploration and replay

Load iterations are normal deterministic asynchronous work. They therefore participate in schedule exploration:

```csharp
await Simulation.ExploreAsync(
    new ExplorationOptions
    {
        Strategy = ExplorationStrategy.CoverageGuided,
        Simulation = new SimulationOptions { Seed = 729381 },
        MaxSchedules = 10_000,
        MaxDecisionDepth = 250
    },
    async context =>
    {
        await context.CreateLoadRunner().RunAsync(
            new BurstLoadProfile
            {
                Iterations = 8,
                MaxConcurrentIterations = 8
            },
            iteration => ExecuteConcurrentOperationAsync(iteration.CancellationToken),
            context.CancellationToken);
    },
    cancellationToken);
```

If one load interleaving violates an invariant or throws, the resulting schedule can be replayed with `Simulation.ReplayAsync` and minimized with
`Simulation.MinimizeAsync` exactly like any other Causalia failure.

<a id="load-tracing"></a>

## Tracing

Every run writes compact `load:run:*` summary events. Set `LoadRunOptions.TraceIterations = true` when individual load iterations should appear
in the deterministic trace. `Causalia.Visualization` classifies those events as load events and correlates iteration events by logical iteration id.

Per-iteration tracing is intentionally off by default because large virtual workloads can otherwise produce very large trace artifacts.

For very large load runs, scheduler trace messages can also be disabled without changing scheduling, schedule capture or replay:

```csharp
var simulation = new SimulationOptions
{
    Seed = 729381,
    TraceSchedulerEvents = false
};
```

`TraceSchedulerEvents` defaults to `true` for compatibility with Causalia 1.0. Set it to `false` when the exact scheduler log is not needed and the
load run itself may contain tens or hundreds of thousands of virtual operations. Replay tokens and scheduler decisions are still captured internally.


<a id="modelbasedtesting"></a>

<a id="modelbasedtesting-model-based-testing"></a>

# Model-based testing

Causalia 1.6 adds executable state-machine model testing for deterministic systems. It explores which domain commands are valid from each model
state and can independently explore scheduler timelines inside every chosen command sequence.

<a id="modelbasedtesting-core-idea"></a>

## Core idea

A model-based test defines:

- an initial pure model state;
- a fresh system-under-test factory;
- the commands available from the current model state;
- a pure expected-state transition for every command;
- deterministic async execution against the real system under test;
- a typed verifier that compares the observation with the expected model state.

The model state is the oracle. It should remain small and deterministic rather than mirroring the implementation.

<a id="modelbasedtesting-define-a-model"></a>

## Define a model

```csharp
using Causalia.ModelBased;

var specification = new ModelBasedSpecification<CounterState, Counter>(
    "counter",
    () => new CounterState(0),
    static (_, cancellationToken) =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new Counter());
    },
    state =>
    [
        ModelCommand.Create<CounterState, Counter, int>(
            "increment",
            current => current with { Value = current.Value + 1 },
            static (counter, _, cancellationToken) => counter.IncrementAsync(cancellationToken),
            static (_, expected, observed) => ModelCommandVerification.Equal(expected.Value, observed),
            current => current.Value < 3)
    ]);
```

Command names are part of the replay contract and therefore must be stable and unique among the commands enabled from one state. Parameterized
commands can be represented by returning multiple command instances from the command provider; encode the stable argument identity in each command
name so an `mb1:` token identifies the same operation when replayed.

<a id="modelbasedtesting-explore-command-sequences"></a>

## Explore command sequences

```csharp
var result = await Simulation.CheckModelAsync(
    new ModelBasedOptions
    {
        Simulation = new SimulationOptions { Seed = 729381 },
        MaxSequences = 10_000,
        MaxCommandDepth = 20
    },
    specification,
    cancellationToken);
```

`SequencesExplored` counts executed non-empty command sequences. `CommandsExecuted` counts command executions across all sequence/schedule runs.
`ExhaustedWithinBounds` is false when `MaxSequences` stopped the frontier before it was empty. `DepthLimitReached` reports that at least one explored
state still had enabled commands at `MaxCommandDepth`.

Each sequence is executed from a new model state and a fresh system returned by the system factory. Mutable SUT state must therefore not be
captured outside that factory.

<a id="modelbasedtesting-explore-concurrency-inside-every-model-sequence"></a>

## Explore concurrency inside every model sequence

Model-state exploration and scheduler exploration are separate dimensions:

```csharp
var options = new ModelBasedOptions
{
    Simulation = new SimulationOptions { Seed = 729381 },
    MaxSequences = 5_000,
    MaxCommandDepth = 12,
    ScheduleExploration = new ModelBasedScheduleExplorationOptions
    {
        Strategy = ExplorationStrategy.DynamicPartialOrderReduction,
        MaxSchedules = 1_000,
        MaxDecisionDepth = 100,
        MaxPreemptions = 2
    }
};
```

The model determines which business operation sequence is legal. The scheduler then explores async interleavings generated while executing that
exact sequence. DPOR resource declarations inside commands keep their normal conservative semantics.

<a id="modelbasedtesting-failure-metadata-and-replay"></a>

## Failure metadata and replay

A verifier mismatch throws `SimulationModelViolationException` inside the deterministic simulation. Model exploration wraps the first failing
timeline in `SimulationModelExplorationFailedException`, which retains:

- `Sequence` with an `mb1:` replay token;
- the normal exact scheduler `Schedule` with a `v1:` replay token;
- the underlying `SimulationFailedException`;
- `ModelFailure` when a typed model verifier produced the failure.

Replay the model sequence with the normal seeded schedule:

```csharp
var sequence = ModelSequence.Parse(modelReplayToken);

await Simulation.ReplayModelAsync(
    new ModelBasedReplayOptions
    {
        Simulation = new SimulationOptions { Seed = 729381 }
    },
    sequence,
    specification,
    cancellationToken);
```

Or combine it with the exact scheduler timeline:

```csharp
await Simulation.ReplayModelAsync(
    new ModelBasedReplayOptions
    {
        Simulation = new SimulationOptions { Seed = 729381 },
        Schedule = SimulationSchedule.Parse(scheduleReplayToken)
    },
    sequence,
    specification,
    cancellationToken);
```

Replay is strict about model shape. If a persisted command is no longer enabled at that point, Causalia throws `SimulationModelReplayException`
instead of silently selecting another command.

<a id="modelbasedtesting-minimize-a-failing-model-sequence"></a>

## Minimize a failing model sequence

```csharp
var minimized = await Simulation.MinimizeModelAsync(
    options,
    new ModelBasedMinimizationOptions
    {
        MaxAttempts = 500
    },
    failingSequence,
    specification,
    cancellationToken);

Console.WriteLine(minimized.MinimizedSequence.ReplayToken);
```

The minimizer removes contiguous command ranges using bounded deterministic delta debugging. It keeps only candidates that remain legal model
sequences and reproduce the same failure class. A model mismatch must still fail on the same model and command name.

<a id="modelbasedtesting-reqnroll-and-other-test-runners"></a>

## Reqnroll and other test runners

`SimulationProvider` exposes `CheckModelAsync`, `ReplayModelAsync` and `MinimizeModelAsync`. It retains `LastModelBasedResult`,
`LastModelBasedFailure`, `LastModelBasedMinimizationResult` and `LastModelFailure`, so a Reqnroll `Then` step can inspect a failure discovered in a
previous `When` step.

<a id="modelbasedtesting-determinism-analyzer"></a>

## Determinism analyzer

The analyzer treats the complete executable model definition as deterministic code. `ModelBasedSpecification` initial-state factories, system
factories and command providers are covered, as are `ModelCommand.Create` transitions, execution delegates, verifiers and preconditions. Wall-clock
access, `Random.Shared`, `Task.Run`, blocking waits and the other existing determinism diagnostics therefore apply to both the model and SUT execution
without requiring a separate annotation.

<a id="modelbasedtesting-scope"></a>

## Scope

Causalia 1.6 is state-machine model-based testing, not automatic domain-model inference. The user supplies the model state, valid commands,
transitions and verifier. Causalia supplies deterministic sequence exploration, optional timeline exploration, replay, failure metadata and bounded
sequence shrinking.


<a id="networking"></a>

<a id="networking-deterministic-http-networking"></a>

# Deterministic HTTP networking

DS-13 extends `Causalia.AspNetCore` from one in-memory web application to a deterministic multi-service HTTP topology.

<a id="networking-register-services"></a>

## Register services

```csharp
var network = context.CreateHttpNetwork();
network.Register("orders", ordersHost);
network.Register("payments", paymentsHost);
```

A host can only be registered with a network created from the same `SimulationContext`. Logical service names are unique within one network.

<a id="networking-configure-a-directed-link"></a>

## Configure a directed link

```csharp
var link = network.Between("orders", "payments")
    .Latency(TimeSpan.FromMilliseconds(25))
    .Drop(0.01)
    .Duplicate(0.02)
    .Delay(0.05, TimeSpan.FromSeconds(2));
```

The link is directed. Configure the reverse direction separately when both directions need independent behavior.

`Latency` is persistent topology state. `Drop`, `Duplicate`, and `Delay` are deterministic probability policies backed by the normal Causalia fault engine.
Fault randomness is isolated from scheduler randomness and therefore does not perturb execution ordering merely because a policy is added.

Fault policies are frozen when a link processes its first request. This prevents a run from changing the meaning of earlier fault occurrences. Persistent
link state such as fixed latency and partition/heal status can still change during the scenario.

<a id="networking-partition-and-heal"></a>

## Partition and heal

```csharp
link.Partition();

// Calls from orders to payments now fail with SimulationNetworkPartitionException.

link.Heal();
```

A partition is explicit topology state rather than a probability decision. It remains active until healed and is visible in the deterministic trace.

<a id="networking-send-traffic"></a>

## Send traffic

```csharp
using var payments = network.CreateClient("orders", "payments");
using var response = await payments.GetAsync("/authorize/42", context.CancellationToken);
```

The client is a normal `HttpClient`, but the request never leaves the simulation. The destination URI is validated against the registered target host so a
client cannot silently escape its named route.

`HttpClient.Timeout` is set to `Timeout.InfiniteTimeSpan`. Use Causalia virtual time plus cancellation when a simulated timeout is required.

<a id="networking-duplicate-semantics"></a>

## Duplicate semantics

When a duplicate fault triggers, Causalia snapshots the outgoing request and performs real additional ASP.NET Core deliveries. Each delivery receives an
independent `HttpRequestMessage` and independent request body stream. The caller receives the primary response; extra deliveries are tracked simulation
operations and must finish before a successful simulation can complete.

The trace retains one logical request id and separate delivery attempts:

```text
http-network:request:accepted:12:orders->payments:POST:/authorize
http-network:request:duplicated:12:additional:1
http-network:request:delivered:12:attempt:1:payments
http-network:request:delivered:12:attempt:2:payments
```

<a id="networking-node-lifecycle"></a>

## Node lifecycle

- a source that is already crashed cannot originate a request;
- a source crash during virtual link latency cancels the outbound operation through generation-scoped cancellation;
- a destination that is already crashed rejects the delivery;
- a destination crash during endpoint execution cancels `HttpContext.RequestAborted` through the normal ASP.NET Core host integration;
- after restart, later HTTP requests can be delivered to the new node generation.

<a id="networking-replay-and-minimization"></a>

## Replay and minimization

Network drop, duplicate, and probabilistic delay effects are ordinary Causalia fault occurrences. They therefore participate in seeded replay and DS-9
minimized reproductions automatically. Partitions and fixed latency are deterministic scenario state and are reproduced by executing the same scenario.

<a id="networking-current-boundaries"></a>

## Current boundaries

DS-13 models logical request delivery rather than TCP. It does not simulate connection pools, DNS caches, TLS, HTTP/2 frames, congestion control,
packet fragmentation, kernel buffers, streaming backpressure, or half-open TCP connections. Those concerns can be layered on later without changing the
service-level API.


<a id="processlifecycle"></a>

<a id="processlifecycle-process-lifecycle"></a>

# Process lifecycle

Causalia 1.2 adds restartable process generations on top of the lower-level `SimulationNode` primitive.

A node models stable logical identity and generation-scoped cancellation. A `SimulationProcess<TGeneration>` additionally models volatile process state:
its generation factory is executed again after every restart, the previous generation is disposed, background work is cancelled, and durable state that
lives outside the process remains available.

<a id="processlifecycle-generic-process"></a>

## Generic process

A process generation implements `ISimulationProcessGeneration`:

```csharp
using Causalia.Processes;

internal sealed class WorkerGeneration : ISimulationProcessGeneration
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
```

Start it from a normal simulation:

```csharp
await using var process = await context.StartProcessAsync(
    new SimulationProcessOptions
    {
        Name = "orders"
    },
    (generation, cancellationToken) =>
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Create volatile generation state here. This factory runs again after restart.
        return Task.FromResult(new WorkerGeneration());
    },
    context.CancellationToken);
```

The stable process exposes:

```csharp
process.Name;
process.Node;
process.Generation;
process.State;
process.IsRunning;
process.Current;
```

<a id="processlifecycle-stop-crash-and-restart"></a>

## Stop, crash and restart

Graceful stop and crash deliberately have different semantics:

```csharp
await process.StopAsync(context.CancellationToken);
await process.RestartAsync(context.CancellationToken);

await process.CrashAsync(context.CancellationToken);
await process.RestartAsync(context.CancellationToken);
```

A graceful stop:

1. marks the process as stopping;
2. cancels generation-scoped background work;
3. invokes the generation `StopAsync` hook;
4. waits for tracked generation background work to finish;
5. disposes the volatile generation;
6. marks the stable node as stopped.

A crash:

1. immediately makes the process unavailable;
2. cancels generation-scoped work and the stable node generation;
3. does **not** invoke the generation `StopAsync` hook;
4. waits for cooperative in-flight generation work to unwind;
5. disposes volatile generation state;
6. leaves durable external state untouched.

Restart creates a new node generation and reruns the generation factory before the process becomes available again.

<a id="processlifecycle-generation-scoped-background-work"></a>

## Generation-scoped background work

Use `SimulationProcessGenerationContext.RunBackgroundAsync` for long-lived work that must be cancelled and recreated with the process generation:

```csharp
_ = generation.RunBackgroundAsync(
    async cancellationToken =>
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await PollAsync(cancellationToken);

            await Task.Delay(
                TimeSpan.FromSeconds(5),
                generation.Simulation.TimeProvider,
                cancellationToken);
        }
    });
```

Tracked background operations participate in normal deterministic scheduling, replay, exploration and failure propagation.

<a id="processlifecycle-aspnet-core-process-reconstruction"></a>

## ASP.NET Core process reconstruction

`StartAspNetCoreAsync` still provides the original lightweight host abstraction. Its `SimulationNode` can be crashed and restarted, but the same
`WebApplication` and service provider remain in memory.

Use `StartAspNetCoreProcessAsync` when the test requires full process-generation reconstruction:

```csharp
await using var process = await context.StartAspNetCoreProcessAsync(
    new AspNetCoreSimulationHostOptions
    {
        NodeName = "orders",
        BaseAddress = new Uri("http://orders.causalia.local/")
    },
    builder =>
    {
        builder.Services.AddSingleton<VolatileCache>();
    },
    app =>
    {
        app.MapGet(
            "/generation",
            (SimulationProcessGenerationContext generation) => Results.Ok(generation.Generation));
    },
    context.CancellationToken);
```

After:

```csharp
await process.CrashAsync(context.CancellationToken);
await process.RestartAsync(context.CancellationToken);
```

Causalia creates a fresh `WebApplication`, service provider and singleton graph. `process.Services` therefore points to a different DI container after
restart.

`SimulationProcessGenerationContext` is registered in ASP.NET Core DI for process hosts, alongside `SimulationContext`, `TimeProvider`, and the stable
`SimulationNode`.

<a id="processlifecycle-deterministic-hosted-background-services"></a>

## Deterministic hosted background services

For long-running hosted work under an ASP.NET Core process, derive from `SimulationBackgroundService` instead of `BackgroundService`:

```csharp
internal sealed class OutboxWorker : SimulationBackgroundService
{
    private readonly TimeProvider _timeProvider;

    public OutboxWorker(
        SimulationProcessGenerationContext generation,
        TimeProvider timeProvider)
        : base(generation)
    {
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await DispatchAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken);
        }
    }
}
```

Register it with the normal hosting API:

```csharp
builder.Services.AddHostedService<OutboxWorker>();
```

The worker is tracked by Causalia, receives generation cancellation on stop/crash, and is constructed again for the next process generation.

<a id="processlifecycle-durable-versus-volatile-state"></a>

## Durable versus volatile state

Keep durable resources outside the process factory:

```csharp
var database = context.CreateStorageDatabase("orders-db");
```

Then create a generation-scoped client inside DI:

```csharp
builder.Services.AddSingleton(
    services => database.CreateClient(
        services.GetRequiredService<SimulationProcessGenerationContext>().Node));
```

The client and service provider are volatile. The `SimulationStorageDatabase` is durable and survives process restart.

<a id="processlifecycle-network-registration-across-restarts"></a>

## Network registration across restarts

A restartable process can be registered directly with `SimulationHttpNetwork`:

```csharp
network.Register("orders", ordersProcess);
network.Register("payments", paymentsProcess);

using var client = network.CreateClient("orders", "payments");
```

The registration follows the logical process, not one concrete host generation. A client created before a restart therefore targets the replacement
ASP.NET Core generation automatically after the destination becomes running again.

<a id="processlifecycle-analyzer-boundaries"></a>

## Analyzer boundaries

`Causalia.Analyzers` treats inline `SimulationProcessGenerationContext` factory delegates and `SimulationBackgroundService.ExecuteAsync` overrides as
deterministic code. The normal CAU1001-CAU1004 diagnostics therefore apply to process startup factories and deterministic hosted workers.

<a id="processlifecycle-boundaries"></a>

## Boundaries

Causalia models process lifecycle inside one .NET test process. It does not fork operating-system processes, reproduce kernel scheduling, recreate a
real TCP stack, or serialize arbitrary CLR heap state. Volatile state is reconstructed because the generation factory and ASP.NET Core DI container are
recreated; durable state must be represented by resources that deliberately live outside the process generation.


<a id="productionreality"></a>

<a id="productionreality-production-reality-bridge"></a>

# Production Reality Bridge

Causalia 1.9 adds DS-25: a dependency-free bridge from production telemetry to deterministic simulation.

The bridge deliberately does not connect a simulation directly to a live production system. Production evidence is normalized outside the deterministic
execution boundary and then consumed as immutable observations. This keeps replay stable while still letting real latency, outcomes and correlated timing
shape the simulated world.

<a id="productionreality-normalize-production-evidence"></a>

## Normalize production evidence

A production observation contains a stable id, logical operation, start instant, duration, outcome, optional correlation id and string attributes:

```csharp
var dataset = ProductionRealityDataset.Create(
    "production-eu-west",
    observations);
```

For .NET applications, stopped `System.Diagnostics.Activity` instances can be imported without adding an OpenTelemetry package dependency:

```csharp
var dataset = ProductionRealityDataset.FromActivities(
    "checkout-service",
    activities);
```

Causalia maps an `ActivityStatusCode.Error` activity to `Failure`; other activity statuses are treated as successful observations. Trace ids become
correlation ids and activity tags become normalized string attributes.

<a id="productionreality-portable-pr2-evidence"></a>

## Portable pr2 evidence

Production evidence can be exported to a stable JSON representation:

```csharp
var json = dataset.ToJson();
var restored = ProductionRealityDataset.ParseJson(json);

Console.WriteLine(restored.Fingerprint); // pr2:...
```

`pr2:` is a content fingerprint over normalized source metadata and observations. It is not a scheduler replay token. Changing an operation, duration,
outcome, timestamp, correlation id or attribute changes the fingerprint.

Causalia 2.0.1 replaces the delimiter-based `pr1:` fingerprint with an unambiguous length-prefixed `pr2:` encoding. `ParseJson` continues to accept
legacy `pr1` documents and normalizes them to `pr2` when loaded; newly serialized documents always use `pr2`.

<a id="productionreality-reality-derived-profiles"></a>

## Reality-derived profiles

A dataset can be summarized per logical operation:

```csharp
var payments = dataset
    .CreateProfile()
    .GetOperation("payments.authorize");

Console.WriteLine(payments.SampleCount);
Console.WriteLine(payments.FailureRate);
Console.WriteLine(payments.Percentile95);
Console.WriteLine(payments.Percentile99);
```

Percentiles use the deterministic nearest-rank definition over exact observed durations. Profiles also expose minimum/maximum duration and failure,
timeout and cancellation rates.

<a id="productionreality-sample-production-behavior-inside-a-simulation"></a>

## Sample production behavior inside a simulation

Attach evidence once and ask Causalia to select an exact observed sample:

```csharp
var result = await Simulation.RunAsync(
    new SimulationOptions { Seed = 729381 },
    async context =>
    {
        context.ProductionReality.Use(dataset);

        var sample = await context.ProductionReality.ApplyAsync(
            "payments.authorize",
            context.CancellationToken);

        if (sample.Outcome == ProductionRealityOutcome.Failure)
        {
            // Route the real observed outcome through the application-specific test double.
        }
    },
    cancellationToken);
```

`ApplyAsync` advances virtual time by the selected production duration. Sampling uses an isolated deterministic random stream: it does not consume
scheduler, fault or `context.Random` values. Replaying the same deterministic execution therefore selects the same observations without perturbing
existing random behavior.

Causalia never guesses how a generic production `Failure` should map to an application exception, HTTP status, broker acknowledgement or provider error.
The test owns that semantic mapping.

<a id="productionreality-replay-one-correlated-incident"></a>

## Replay one correlated incident

A correlation id can be lifted into an incident:

```csharp
var incident = dataset.GetIncident("checkout-42");

await context.ProductionReality.ReplayIncidentAsync(
    incident,
    async (observation, operationCancellationToken) =>
    {
        await ExecuteObservedOperationAsync(
            observation,
            operationCancellationToken);
    },
    context.CancellationToken);
```

Every handler starts at the same relative offset at which the corresponding production operation started. Overlapping production observations therefore
become overlapping deterministic operations. The bridge does not force handler completion time because the handler may execute a real simulated SUT. If
a semantic adapter needs the exact observed duration, call `DelayObservedDurationAsync`.

<a id="productionreality-evidence-retained-on-results-and-failures"></a>

## Evidence retained on results and failures

Successful and failed simulations expose structured evidence:

```csharp
result.ProductionReality.Applications
failure.ProductionReality.Applications
```

Each application records the `pr2:` fingerprint, exact observation id, operation, observed outcome/duration, correlation id, application mode and virtual
instant at which it was applied. `SimulationProvider.LastProductionReality` exposes the latest evidence to Reqnroll and other scenario-scoped adapters.

Production-reality applications are also written to the causal trace under the `ProductionReality` visualization category.

<a id="productionreality-determinism-boundary"></a>

## Determinism boundary

Import `Activity` objects, telemetry exports or provider data before the deterministic scenario whenever possible. A simulation should not query a live
telemetry backend, production database or tracing API. Persist normalized `pr2` JSON as a test/incident artifact and replay that immutable evidence.

The determinism analyzer treats the handler passed to `ReplayIncidentAsync` as simulation code and reports the same uncontrolled time, randomness,
threading and blocking APIs it reports in normal Causalia scenarios.


<a id="reqnroll"></a>

<a id="reqnroll-causalia-with-reqnroll"></a>

# Causalia with Reqnroll

`Causalia.Reqnroll` provides a scenario-scoped `ReqnrollSimulationProvider` that is designed to work with Reqnroll's normal constructor context injection.
It deliberately does not reference Reqnroll runtime types, which keeps Reqnroll versioning out of the Causalia dependency graph.

<a id="reqnroll-install"></a>

## Install

Add the normal Reqnroll test-runner package for your project and add `Causalia.Reqnroll`.

For MSTest-based Reqnroll projects that means the project normally already references `Reqnroll.MSTest`, `MSTest.TestFramework`,
`MSTest.TestAdapter` and `Microsoft.NET.Test.Sdk`.

<a id="reqnroll-inject-the-provider"></a>

## Inject the provider

```csharp
using Causalia;
using Causalia.Reqnroll;
using Causalia.Exceptions;
using Causalia.Scheduling;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

[Binding]
public sealed class CheckoutSteps
{
    private readonly ReqnrollSimulationProvider _causalia;
    private readonly TestContext _testContext;

    public CheckoutSteps(ReqnrollSimulationProvider causalia, TestContext testContext)
    {
        _causalia = causalia;
        _testContext = testContext;
    }

    [Given("Causalia uses seed {long}")]
    public void GivenCausaliaUsesSeed(long seed)
    {
        _causalia.Configure(new SimulationOptions { Seed = checked((ulong)seed) });
    }

    [When("the deterministic scenario runs")]
    public async Task WhenTheDeterministicScenarioRuns()
    {
        await _causalia.RunAsync(
            async context =>
            {
                await Task.Yield();
                context.CancellationToken.ThrowIfCancellationRequested();
            },
            _testContext.CancellationToken);
    }

    [Then("the replay seed is {long}")]
    public void ThenTheReplaySeedIs(long seed)
    {
        Assert.AreEqual(checked((ulong)seed), _causalia.LastResult!.Seed);
    }
}
```

The provider exposes the full Causalia runtime, including simulated nodes. No Reqnroll-specific node API is required:

```csharp
await _causalia.RunAsync(
    async context =>
    {
        var worker = context.CreateNode("worker");
        var bus = context.CreateMessageBus();

        bus.RegisterNodeHandler<MyMessage>(
            worker,
            async (message, delivery, handlerCancellationToken) =>
            {
                await HandleAsync(message, delivery, handlerCancellationToken);
            });

        // Crash/restart can now be driven by the BDD scenario logic.
    },
    _testContext.CancellationToken);
```

Reqnroll keeps injected context objects for the scenario lifetime, so the same provider can be injected into multiple binding classes.
Its run, exploration and replay state therefore remain available to other binding classes during the same scenario.

<a id="reqnroll-explore-and-replay-from-bdd-steps"></a>

## Explore and replay from BDD steps

The same provider exposes DS-7 schedule exploration without introducing Reqnroll runtime types into Causalia:

```csharp
[When("all bounded checkout schedules are explored")]
public async Task WhenAllBoundedCheckoutSchedulesAreExplored()
{
    try
    {
        await _causalia.ExploreAsync(
            new ExplorationOptions
            {
                Simulation = _causalia.Options,
                MaxSchedules = 1_000,
                MaxDecisionDepth = 100
            },
            RunCheckoutScenarioAsync,
            _testContext.CancellationToken);
    }
    catch (SimulationExplorationFailedException)
    {
        // A Then step can inspect LastExplorationFailure and its replay token.
        throw;
    }
}
```

A later step can replay the exact schedule:

```csharp
await _causalia.ReplayAsync(
    _causalia.LastExplorationFailure!.Schedule,
    RunCheckoutScenarioAsync,
    _testContext.CancellationToken);
```



<a id="reqnroll-coverage-guided-bdd-exploration"></a>

## Coverage-guided BDD exploration

DS-15 is available through the existing scenario-scoped provider; no extra Reqnroll adapter is required:

```csharp
await _causalia.ExploreAsync(
    new ExplorationOptions
    {
        Strategy = ExplorationStrategy.CoverageGuided,
        Simulation = _causalia.Options,
        MaxSchedules = 10_000,
        MaxDecisionDepth = 100
    },
    async context =>
    {
        await ExecuteCheckoutAsync(context);
        context.Coverage.Observe("checkout-state", checkout.State.ToString());
        context.Coverage.Observe("retry-count", checkout.RetryCount);
    },
    _testContext.CancellationToken);
```

`LastExplorationResult` exposes the aggregate coverage metrics after a successful run. A failure still populates `LastExplorationFailure` with the
strict replay schedule, so a BDD flow can use coverage guidance for discovery and the existing replay/minimization APIs for diagnosis.

<a id="reqnroll-minimize-and-reproduce-from-bdd-steps"></a>

## Minimize and reproduce from BDD steps

The scenario-scoped provider also exposes DS-9 directly. A step can minimize a failure discovered by exploration or a normal seeded run:

```csharp
var failure = _causalia.LastExplorationFailure!.Failure;

var minimized = await _causalia.MinimizeAsync(
    new MinimizationOptions
    {
        MaxAttempts = 1_000
    },
    failure,
    RunCheckoutScenarioAsync,
    _testContext.CancellationToken);

ScenarioContext["causalia-reproduction"] = minimized.Reproduction.ReplayToken;
```

A later step can parse and reproduce the compact token:

```csharp
var reproduction = SimulationReproduction.Parse(
    (string)ScenarioContext["causalia-reproduction"]);

await _causalia.ReproduceAsync(
    reproduction,
    RunCheckoutScenarioAsync,
    _testContext.CancellationToken);
```

`LastMinimizationResult`, `LastFailure`, `LastFailureAnalysis`, `LastInvariantFailure`, `LastLinearizabilityFailure` and `LastConsistencyFailure` remain
available on the same provider instance for later `Then` steps.
The scenario delegate passed to minimization is executed multiple times, so run-specific mutable state should be created inside that delegate.


<a id="reqnroll-failure-intelligence-in-bdd-scenarios"></a>

## Failure intelligence in BDD scenarios

Causalia 1.7 automatically stores a descriptive `LastFailureAnalysis` whenever a provider run, replay or exploration captures a deterministic failure.
A later step can run causal reduction against the same scenario without reconstructing another adapter:

```csharp
using Causalia.FailureIntelligence;

var analysis = await _causalia.AnalyzeFailureAsync(
    new FailureAnalysisOptions
    {
        Minimization = new MinimizationOptions { MaxAttempts = 1_000 }
    },
    _causalia.LastFailure!,
    RunCheckoutScenarioAsync,
    _testContext.CancellationToken);

ScenarioContext["failure-signature"] = analysis.Signature.Token;
ScenarioContext["failure-trigger"] = analysis.Trigger.ToString();
```

The minimized analysis remains in `LastFailureAnalysis`, while `LastMinimizationResult` exposes the underlying DS-9 reduction. This lets BDD assertions
refer to a stable `fi2:` semantic defect identity instead of coupling a feature file to one seed or one concrete `v1:` scheduler timeline.

<a id="reqnroll-linearizability-in-bdd-scenarios"></a>

## Linearizability in BDD scenarios

DS-10 also uses the normal simulation context, so a Reqnroll scenario can record a concurrent history and require a legal sequential explanation
without a Reqnroll-specific wrapper:

```csharp
await _causalia.ExploreAsync(
    new ExplorationOptions
    {
        Simulation = _causalia.Options,
        MaxSchedules = 1_000,
        MaxDecisionDepth = 100
    },
    async context =>
    {
        var history = context.Linearizability.CreateHistory<CounterCommand, int>("counter");
        await ExecuteConcurrentCounterOperationsAsync(context, history);
        history.RequireLinearizable(CreateCounterSpecification());
    },
    _testContext.CancellationToken);
```

When a history is not linearizable, `_causalia.LastLinearizabilityFailure` remains available to later `Then` steps. If systematic exploration
discovered the violation, `_causalia.LastExplorationFailure` contains the exact schedule and replay token; DS-9 can minimize the same failure.

<a id="reqnroll-consistency-verification-in-bdd-scenarios"></a>

## Consistency verification in BDD scenarios

Causalia 1.5 consistency histories use the same normal `SimulationContext`, so BDD scenarios do not need a Reqnroll-specific consistency wrapper:

```csharp
using Causalia.Consistency;

await _causalia.RunAsync(
    context =>
    {
        var history = context.Consistency.CreateHistory<string>("orders")
            .Require(ConsistencyGuarantees.ReadYourWrites);
        history.Write("checkout", "primary", "order:42");
        history.Read("checkout", "secondary", "order:42", null);
        return Task.CompletedTask;
    },
    _testContext.CancellationToken);
```

A consistency violation is available through `_causalia.LastConsistencyFailure`. Exploration, replay and minimization keep using the normal provider
methods and exact schedule tokens.

<a id="reqnroll-model-based-testing-in-bdd-scenarios"></a>

## Model-based testing in BDD scenarios

Causalia 1.6 exposes model-based testing through the same scenario-scoped provider. A binding can build an executable
`ModelBasedSpecification<TState, TSystem>` and call `_causalia.CheckModelAsync(...)` with the scenario cancellation token.

When exploration fails, `_causalia.LastModelBasedFailure` retains both the `mb1:` model-sequence token and exact scheduler schedule, while
`_causalia.LastModelFailure` exposes a structured verifier mismatch when the failure came from a model command. Successful runs remain available
through `_causalia.LastModelBasedResult`, and command-sequence shrinking through `_causalia.MinimizeModelAsync(...)` stores
`LastModelBasedMinimizationResult`.

<a id="reqnroll-invariants-in-bdd-scenarios"></a>

## Invariants in BDD scenarios

The Reqnroll provider exposes the normal Causalia `SimulationContext`, so DS-8 invariants need no Reqnroll-specific wrapper:

```csharp
await _causalia.ExploreAsync(
    new ExplorationOptions
    {
        Simulation = _causalia.Options,
        MaxSchedules = 1_000,
        MaxDecisionDepth = 100
    },
    async context =>
    {
        context.Invariants.Never(
            "checkout is committed twice",
            () => checkout.CommitCount > 1);

        context.Invariants.Eventually(
            "checkout reaches a terminal state",
            TimeSpan.FromMinutes(5),
            () => checkout.IsTerminal);

        await ExecuteCheckoutAsync(context);
    },
    _testContext.CancellationToken);
```

If an invariant fails, a later `Then` step can inspect `_causalia.LastInvariantFailure`. When exploration found the violation,
`_causalia.LastExplorationFailure` also retains the exact schedule and replay token.

`LastExplorationResult`, `LastExplorationFailure`, `LastMinimizationResult`, `LastResult`, `LastFailure`, `LastInvariantFailure` and
`LastLinearizabilityFailure`, `LastConsistencyFailure` and `LastModelFailure` are
scenario-scoped because the provider itself is scenario-scoped.

<a id="reqnroll-current-semantic-boundary"></a>

## Current semantic boundary

Every `RunAsync` call starts one deterministic Causalia simulation. The provider itself survives across Reqnroll steps, but the virtual runtime does not yet stay alive
between separate calls. This keeps the adapter thin and predictable while leaving room for a later `ISimulationSessionProvider` that can drive one continuously
running virtual world across multiple BDD steps.

<a id="reqnroll-why-no-hard-reqnroll-dependency"></a>

## Why no hard Reqnroll dependency?

The adapter only needs the context-injection contract: a public POCO with a public constructor can be created and shared for one Reqnroll scenario.
Avoiding a compile-time Reqnroll dependency prevents Causalia from pinning a particular Reqnroll version and also makes the provider pattern reusable by other BDD runners.
<a id="reqnroll-determinism-analyzer"></a>

## Determinism analyzer

`Causalia.Analyzers` can be installed directly in the Reqnroll test project. The analyzer recognizes scenario delegates passed to
`ReqnrollSimulationProvider.RunAsync`, `ExploreAsync`, `ReplayAsync`, `ReproduceAsync` and `MinimizeAsync` through their
`Func<SimulationContext, Task>` parameter type, so no Reqnroll-specific analyzer plugin is required.

Use `[DeterministicSimulation]` on helper methods that live outside the scenario delegate when those helpers should be checked as well.

<a id="reqnroll-aspnet-core-scenarios"></a>

## ASP.NET Core scenarios

`Causalia.AspNetCore` composes directly with `ReqnrollSimulationProvider`; no additional Reqnroll plugin is required.

```csharp
await _causalia.RunAsync(
    async context =>
    {
        await using var host = await context.StartAspNetCoreAsync(
            app => app.MapGet("/health", () => Results.Ok()),
            context.CancellationToken);

        using var client = host.CreateClient();
        using var response = await client.GetAsync("/health", context.CancellationToken);
        Assert.IsTrue(response.IsSuccessStatusCode);
    },
    _testContext.CancellationToken);
```

The host, virtual time, simulated node and HTTP request trace live inside the same scenario run as the rest of the Causalia workload.


<a id="reqnroll-service-to-service-http-scenarios"></a>

## Service-to-service HTTP scenarios

DS-13 uses the same `SimulationContext`, so Reqnroll bindings can compose multiple ASP.NET Core services without another adapter:

```csharp
await _causalia.ExploreAsync(
    options,
    async context =>
    {
        var network = context.CreateHttpNetwork();
        await using var orders = await StartOrdersAsync(context, network);
        await using var payments = await StartPaymentsAsync(context);
        network.Register("orders", orders);
        network.Register("payments", payments);
        network.Between("orders", "payments").Latency(TimeSpan.FromMilliseconds(25)).Drop(0.01);

        using var client = orders.CreateClient();
        using var response = await client.PostAsync("/orders", content: null, context.CancellationToken);
    },
    _testContext.CancellationToken);
```

The network shares the scenario seed, trace, schedule exploration, fault replay and minimization infrastructure.

<a id="reqnroll-durable-storage-scenarios"></a>

## Durable storage scenarios

DS-14 composes with the same scenario-scoped provider; no Reqnroll-specific storage plugin is required:

```csharp
await _causalia.ExploreAsync(
    options,
    async context =>
    {
        var node = context.CreateNode("orders");
        var database = context.CreateStorageDatabase("orders-db");
        var client = database.CreateClient(
            node,
            new SimulationStorageClientOptions
            {
                Faults = new StorageFaultPlan().FailAfterCommit(0.01)
            });

        await ExecuteOrderScenarioAsync(context, client);
    },
    _testContext.CancellationToken);
```

The same seed, trace, fault occurrences, exploration schedules and DS-9 minimization apply to storage failures. EF Core scenarios can construct a
`SimulationStorageBoundary` inside the delegate and register it with `AddCausaliaStorageSimulation` on their `DbContextOptionsBuilder`.

<a id="reqnroll-exporting-a-failing-scenario-trace"></a>

## Exporting a failing scenario trace

`Causalia.Visualization` can consume the scenario-scoped provider result or failure without adding Reqnroll-specific runtime hooks:

```csharp
using Causalia.Visualization;

var html = _causalia.LastExplorationFailure?.ToTraceHtml()
    ?? _causalia.LastFailure?.ToTraceHtml()
    ?? _causalia.LastResult!.ToTraceHtml();

await File.WriteAllTextAsync(
    "scenario-trace.html",
    html,
    _testContext.CancellationToken);
```

The exported file is self-contained and retains the exact replay token, so it can be attached directly to BDD/CI test output.

<a id="reqnroll-deterministic-load-inside-a-reqnroll-scenario"></a>

## Deterministic load inside a Reqnroll scenario

`Causalia.Load` composes with the existing scenario-scoped provider. No additional Reqnroll adapter is required because the load runner is created
from the `SimulationContext` passed to the normal provider callback.

```csharp
await _causalia.ExploreAsync(
    new ExplorationOptions
    {
        Strategy = ExplorationStrategy.CoverageGuided,
        Simulation = _causalia.Options,
        MaxSchedules = 10_000,
        MaxDecisionDepth = 250
    },
    async context =>
    {
        var result = await context.CreateLoadRunner().RunAsync(
            new BurstLoadProfile
            {
                Iterations = 100,
                MaxConcurrentIterations = 100
            },
            iteration => ExecuteWorkflowOperationAsync(iteration.CancellationToken),
            context.CancellationToken);

        context.Coverage.Observe("load-peak-concurrency", result.PeakConcurrency);
    },
    _testContext.CancellationToken);
```

If exploration finds a failing load interleaving, `LastExplorationFailure` retains the exact schedule and can be replayed or minimized through the
same provider APIs used for non-load scenarios.


<a id="reqnroll-process-lifecycle"></a>

## Process lifecycle

A Reqnroll scenario can start `SimulationProcess<TGeneration>` or `AspNetCoreSimulationProcess` inside the provider's normal `SimulationContext`. Crash,
graceful stop, restart, generation-scoped background work and reconstructed ASP.NET Core DI therefore participate in the same exploration, replay and
minimization run without a separate Reqnroll integration.

<a id="reqnroll-time-travel-state"></a>

## Time-travel state

Causalia 1.8 exposes `LastTimeTravel` on the scenario-scoped provider. It points at the retained timeline from the latest successful execution or
deterministic failure, so later Reqnroll steps can inspect `tt1:` checkpoints and watched state without rerunning the scenario. Configure
`SimulationOptions.TimeTravel.CaptureMode = TimeTravelCaptureMode.SchedulerSteps` when automatic scheduler-boundary capture is required.


<a id="reqnroll-production-evidence"></a>

## Production evidence

Causalia 1.9 exposes `LastProductionReality` on the scenario-scoped provider. It returns the evidence consumed by the latest successful or failed run,
which allows later Reqnroll steps to assert the exact `pr2:` source and observation ids that reproduced a production incident.


<a id="storage"></a>

<a id="storage-deterministic-storage"></a>

# Deterministic storage

`Causalia.Storage` models durable versioned state without coupling `Causalia` core to a database framework.
The package targets `net10.0` and depends only on `Causalia`.

<a id="storage-durable-database-and-node-bound-client"></a>

## Durable database and node-bound client

```csharp
var node = context.CreateNode("orders");
var database = context.CreateStorageDatabase("orders-db");
var client = database.CreateClient(
    node,
    new SimulationStorageClientOptions
    {
        OperationLatency = TimeSpan.FromMilliseconds(5),
        CommitLatency = TimeSpan.FromMilliseconds(20)
    });

await client.WriteAsync(
    "orders/42",
    payload,
    context.CancellationToken);
```

The database owns durable state. A client can be bound to a `SimulationNode`; crashing that node cancels work associated with the current generation,
while already committed database state survives restart.

<a id="storage-transactions-and-commit-ambiguity"></a>

## Transactions and commit ambiguity

```csharp
var faults = new StorageFaultPlan()
    .FailAfterCommit(0.01, "commit-ack-lost");

var client = database.CreateClient(
    node,
    new SimulationStorageClientOptions
    {
        Faults = faults
    });

var transaction = client.BeginTransaction();
transaction.Write("orders/42", payload);
await transaction.CommitAsync(context.CancellationToken);
```

A `BeforeCommit` failure throws `SimulationStorageTransientException` before the staged changes become durable.
An `AfterCommit` failure throws `SimulationStorageAmbiguousCommitException` after the transaction is durable. This deliberately models lost commit
acknowledgements, connection loss after commit and similar retry hazards.

While a transaction commit is in progress, further writes, deletes, rollback attempts and overlapping commits on that transaction are rejected.
An attempt that fails before committing can be retried with the same staged changes.

<a id="storage-version-checked-writes"></a>

## Version-checked writes

Reads return a monotonically increasing per-key version:

```csharp
var current = await client.ReadAsync(
    "counter",
    context.CancellationToken);

await client.WriteAsync(
    "counter",
    nextValue,
    current.Version,
    context.CancellationToken);
```

A stale expected version throws `SimulationStorageConcurrencyException`. Transactions can also attach expected versions to staged writes and deletes;
all expected versions are validated before any staged change is committed.
Version history is retained across deletion and recreation of a key, so an old version cannot authorize a change to a newly created value.
Missing keys still read as version `0`; use that expected version to require that a key does not exist when creating it.

<a id="storage-faults-and-latency"></a>

## Faults and latency

```csharp
var faults = new StorageFaultPlan()
    .Delay(
        StorageOperationPhase.BeforeRead,
        0.10,
        TimeSpan.FromSeconds(2))
    .FailBeforeCommit(0.02, "database-unavailable")
    .FailAfterCommit(0.01, "acknowledgement-lost");
```

Storage faults use Causalia's isolated deterministic fault streams. Triggered occurrences therefore participate in exact trace capture and DS-9
minimization without perturbing scheduler randomness.

Base operation and commit latency use `SimulationContext.TimeProvider`, so hours or days of database delay advance virtual time rather than wall-clock time.

<a id="storage-provider-boundary"></a>

## Provider boundary

`SimulationContext.CreateStorageBoundary(...)` exposes the same deterministic operation boundary without using the built-in key/value database.
Runtime adapters such as `Causalia.EntityFrameworkCore` use this boundary around a real persistence framework.


<a id="storage-process-lifecycle"></a>

## Process lifecycle

For Causalia 1.2 process tests, create `SimulationStorageDatabase` outside the process generation factory and create clients inside each generation.
The database then represents durable state while the client, DI container and other generation objects remain volatile and are reconstructed after
restart.


<a id="timetravel"></a>

<a id="timetravel-time-travel-debugger"></a>

# Time-travel debugger

Causalia 1.8 adds deterministic execution checkpoints and watched-state snapshots as DS-24.

The debugger is intentionally replay-based. It does not attempt to clone and later mutate arbitrary live .NET object graphs. Instead, a scenario
registers deterministic textual state probes, Causalia captures immutable checkpoints at controlled execution boundaries, and deterministic replay
remains the source of truth when the execution must be run again.

<a id="timetravel-enable-scheduler-step-capture"></a>

## Enable scheduler-step capture

Manual checkpoints work without additional configuration. To capture both sides of every scheduler step, configure `TimeTravel`:

```csharp
using Causalia;
using Causalia.TimeTravel;

var result = await Simulation.RunAsync(
    new SimulationOptions
    {
        Seed = 729381,
        TimeTravel = new TimeTravelOptions
        {
            CaptureMode = TimeTravelCaptureMode.SchedulerSteps,
            MaxRetainedCheckpoints = 10_000
        }
    },
    async context =>
    {
        var state = new OrderState();

        context.TimeTravel.Watch(
            "order",
            () => $"status={state.Status};version={state.Version}");

        await ExecuteOrderAsync(state, context.CancellationToken);
    },
    cancellationToken);
```

`SchedulerSteps` captures these checkpoint kinds:

- `Start` after the scenario's synchronous setup prefix has registered probes;
- `BeforeSchedulerStep` immediately before one runnable deterministic work item executes;
- `AfterSchedulerStep` after the work item completed and invariants were observed;
- `Completed` for a successful execution;
- `Failure` immediately before a deterministic `SimulationFailedException` is created.

The default `ManualOnly` mode adds no automatic checkpoints.

<a id="timetravel-manual-checkpoints"></a>

## Manual checkpoints

Explicit checkpoints are always available:

```csharp
context.TimeTravel.Watch("order", () => order.State.ToString());

var token = context.TimeTravel.Checkpoint("before-payment");
await ChargeAsync(context.CancellationToken);
context.TimeTravel.Checkpoint("after-payment");
```

Checkpoint tokens use the opaque versioned `tt1:` format. The index is monotonic inside one deterministic execution, so the same supported replay
shape produces the same checkpoint token sequence.

<a id="timetravel-typed-probes"></a>

## Typed probes

When state is not already a string, provide an explicit deterministic formatter:

```csharp
context.TimeTravel.Watch(
    "inventory",
    () => inventory,
    value => $"available={value.Available};reserved={value.Reserved}");
```

Causalia deliberately requires an explicit representation instead of guessing how arbitrary object graphs should be cloned. The analyzer treats both
the capture delegate and formatter as deterministic simulation boundaries.

A probe exception is captured as snapshot data (`ErrorType` and `ErrorMessage`) and does not change the behavior of the simulation under test.

<a id="timetravel-navigate-backwards-and-forwards"></a>

## Navigate backwards and forwards

Successful results and deterministic failures expose a `TimeTravelTimeline`:

```csharp
var debugger = failure.TimeTravel.CreateDebugger();

while (debugger.MovePrevious())
{
    var checkpoint = debugger.Current!;
    Console.WriteLine($"{checkpoint.Token} step={checkpoint.Step} kind={checkpoint.Kind}");
}
```

The debugger starts at the latest retained checkpoint. It can:

- `MovePrevious()` and `MoveNext()`;
- `Seek("tt1:42")`;
- `SeekStep(step, TimeTravelSeekMode.AtOrBefore)`;
- `SeekTraceEvent(index, TimeTravelTraceSeekMode.BeforeEvent)`;
- jump to the previous or next change of one named state probe.

`TraceCount` on every checkpoint connects the state timeline to the causal trace without adding synthetic scheduler events to the trace itself.

<a id="timetravel-diff-state"></a>

## Diff state

```csharp
var before = failure.TimeTravel.Checkpoints[^2];
var after = failure.TimeTravel.Checkpoints[^1];
var diff = failure.TimeTravel.Diff(before, after);

foreach (var change in diff.Changes)
{
    Console.WriteLine($"{change.Name}: {change.Before?.Value} -> {change.After?.Value}");
}
```

Changes are classified as `Added`, `Removed` or `Changed`. Probe errors participate in equality, so a probe transitioning between a value and a
capture error is visible in the diff.

<a id="timetravel-retention"></a>

## Retention

`MaxRetainedCheckpoints` bounds debugger memory. When the limit is reached, Causalia discards the oldest checkpoints and keeps the newest state nearest
to the eventual completion or failure. Tokens are not renumbered. `TotalCheckpointCount`, `DroppedCheckpointCount` and `Truncated` make retention
explicit.

<a id="timetravel-replay-stability"></a>

## Replay stability

Time-travel capture does not create scheduler choices and does not consume scheduler, fault or user-random streams. Exact `v1:` replay therefore
remains unchanged. For deterministic probes and the same execution shape, replay produces the same `tt1:` checkpoint sequence and watched values.

Time-travel state probes should observe existing state only. They should not mutate the system under test or perform external I/O.

<a id="timetravel-testing-and-reqnroll-providers"></a>

## Testing and Reqnroll providers

`SimulationProvider` and `ReqnrollSimulationProvider` expose `LastTimeTravel`, pointing at the timeline from the latest successful run or deterministic
failure. Existing custom `ISimulationProvider` implementations remain source-compatible because the interface member has a default implementation.

<a id="timetravel-visualization"></a>

## Visualization

`Causalia.Visualization` includes retained checkpoints in `SimulationTraceDocument`. The standalone HTML viewer adds an interactive checkpoint
selector with previous/next navigation, `tt1:` token, scheduler step, checkpoint kind, label and watched state values.

This makes a CI trace artifact useful both as a causal event timeline and as a deterministic state debugger without any external JavaScript or CDN.


<a id="verificationplatform"></a>

<a id="verificationplatform-verification-platform"></a>

# Verification Platform

Causalia 2.0 turns the deterministic engines built in 1.0 through 1.9 into one ordered verification platform.
A `VerificationPlan` is a deterministic program of correctness checks: seeded simulation runs, bounded scheduler exploration and executable
model-based verification can be run together and reported as one CI artifact.

The platform layer does not introduce parallel orchestration. Plan steps execute in declaration order so the verification harness itself does not add
uncontrolled timing. Concurrency, load and interleaving exploration stay inside the Causalia engines that already control them.

<a id="verificationplatform-build-a-plan"></a>

## Build a plan

```csharp
using Causalia;
using Causalia.ModelBased;
using Causalia.Scheduling;
using Causalia.Verification;

var plan = VerificationPlan.Create(
    "checkout-release-gate",
    builder => builder
        .AddRun(
            "production-reality-smoke",
            new SimulationOptions { Seed = 729381 },
            RunProductionDerivedScenarioAsync)
        .AddExploration(
            "checkout-dpor",
            new ExplorationOptions
            {
                Strategy = ExplorationStrategy.DynamicPartialOrderReduction,
                Simulation = new SimulationOptions { Seed = 729382 },
                MaxSchedules = 100_000,
                MaxDecisionDepth = 250
            },
            RunConcurrentCheckoutAsync)
        .AddModel(
            "checkout-state-machine",
            modelOptions,
            checkoutSpecification));

var verification = await Simulation.VerifyAsync(
    plan,
    new VerificationRunOptions(),
    cancellationToken);
```

A scenario step can use the normal Causalia surface, including virtual time, invariants, consistency histories, linearizability, time-travel probes,
production evidence, deterministic load and ecosystem adapters. An exploration step uses the existing DFS, coverage-guided or DPOR scheduler engines.
A model step uses the executable state-machine engine introduced in 1.6.

<a id="verificationplatform-unified-results"></a>

## Unified results

`VerificationRunResult.Steps` preserves plan order and exposes typed evidence for every engine:

- `Simulation` for successful seeded runs;
- `Exploration` for successful scheduler exploration;
- `ModelBased` for successful model-state exploration;
- `Failure` and `FailureAnalysis` for failed steps;
- `TimeTravel` and `ProductionReality` when that evidence belongs to the step.

Equivalent failures from different steps or seeds are grouped through the existing semantic `fi2:` identity:

```csharp
foreach (var cluster in verification.FailureIntelligence.Clusters)
{
    Console.WriteLine($"{cluster.TriageRank}: {cluster.Signature.Token}");
}
```

By default the platform describes failures without rerunning them. Set `VerificationRunOptions.MinimizeFailures = true` when seeded scenario and
scheduler-exploration failures should be reduced to compact `m1:` reproductions before the final report is produced. Model failures keep their exact
`mb1:`/`v1:` evidence and receive descriptive failure intelligence.

<a id="verificationplatform-ci-gate-semantics"></a>

## CI gate semantics

Verification normally keeps running after a failure so one CI invocation can collect multiple defects. Use fail-fast only when that is more useful:

```csharp
var result = await Simulation.VerifyAsync(
    plan,
    new VerificationRunOptions
    {
        StopOnFirstFailure = true
    },
    cancellationToken);
```

Remaining steps are represented as `Skipped`; they are not silently omitted. `result.EnsurePassed()` throws `VerificationRunFailedException`
carrying the complete result and `vp1:` report fingerprint.

<a id="verificationplatform-portable-vp1-reports"></a>

## Portable `vp1:` reports

Every run creates `VerificationRunResult.Report`, a portable report with:

- ordered step status and seed;
- compact deterministic summaries;
- exact `v1:` schedule tokens where one exists;
- stable `fi2:` failure signatures;
- retained time-travel checkpoint counts;
- referenced `pr2:` production-evidence fingerprints;
- deterministic failure-cluster triage data.

The report fingerprint starts with `vp1:` and is content-addressed from the deterministic plan outcome. JSON roundtrip validates the fingerprint:

```csharp
var json = result.Report.ToJson();
await File.WriteAllTextAsync("causalia-verification.json", json, cancellationToken);

var restored = VerificationReport.ParseJson(json);
Console.WriteLine(restored.Fingerprint);
```

A modified report fails parsing rather than silently keeping an invalid fingerprint. This is an integrity check, not proof of authenticity: publish
reports with signed CI provenance when protection against a malicious rewrite is required.

<a id="verificationplatform-html-verification-dashboard"></a>

## HTML verification dashboard

`Causalia.Visualization` can export the whole platform result as one standalone HTML artifact:

```csharp
using Causalia.Visualization;

var html = result.ToVerificationHtml();
await File.WriteAllTextAsync("causalia-verification.html", html, cancellationToken);
```

The dashboard shows the `vp1:` identity, plan status, ordered verification engines and unified Failure Intelligence triage without CDNs or external assets.

<a id="verificationplatform-reqnroll-and-framework-neutral-providers"></a>

## Reqnroll and framework-neutral providers

`SimulationProvider` and `ReqnrollSimulationProvider` expose `VerifyAsync(...)` and retain `LastVerificationResult`. This allows a BDD scenario to
execute a complete verification gate in one step and inspect the resulting report or failure clusters in later steps.

<a id="verificationplatform-boundaries"></a>

## Boundaries

The Verification Platform orchestrates deterministic correctness engines; it does not replace normal unit tests, deployment-level integration tests
or real performance benchmarking. It also does not execute plan steps concurrently. If a verification step must exercise concurrency, distributed
load or overlapping production timing, model that inside the step so Causalia can control and replay it.


<a id="versioning"></a>

<a id="versioning-versioning-policy"></a>

# Versioning policy

Causalia follows Semantic Versioning from `1.0.0` onward.

<a id="versioning-package-train"></a>

## Package train

All first-party Causalia packages use the same version number and are released as one package train:

- `Causalia`
- `Causalia.Analyzers`
- `Causalia.Testing`
- `Causalia.Reqnroll`
- `Causalia.AspNetCore`
- `Causalia.Storage`
- `Causalia.EntityFrameworkCore`
- `Causalia.Visualization`
- `Causalia.Load`
- `Causalia.Dapr`
- `Causalia.Kafka`
- `Causalia.RabbitMQ`
- `Causalia.Grpc`
- `Causalia.Tool`

A repository build therefore has one authoritative version in `Directory.Build.props`.

<a id="versioning-compatibility"></a>

## Compatibility

For the stable 1.x line and the 2.0 platform release:

- patch releases fix defects without intentionally changing the public API;
- minor releases may add APIs, diagnostics, adapters and simulation capabilities while keeping existing supported APIs source-compatible;
- intentional breaking public API changes require a new major version;
- Causalia 2.0 keeps executable deterministic token families readable and adds orchestration/reporting instead of redefining execution semantics;
- replay and reproduction token formats remain versioned independently inside the token so future readers can reject unsupported formats
  explicitly rather than silently interpreting them differently;
- model command sequences use their own `mb1:` token and remain separate from scheduler `v1:` and minimized `m1:` tokens;
- semantic failure identities use their own `fi2:` token and deliberately exclude seed, concrete schedule and minimized reproduction;
- time-travel checkpoints use their own `tt1:` token and remain diagnostic positions rather than executable schedule tokens;
- normalized production evidence uses content-addressed `pr2:` fingerprints and remains immutable input rather than executable schedule state;
- Verification Platform reports use content-addressed `vp1:` fingerprints and reference, rather than reinterpret, underlying `v1:`, `m1:`, `mb1:`,
  `fi2:`, `tt1:` and `pr2:` evidence.

<a id="versioning-201-evidence-migration"></a>

### 2.0.1 evidence migration

Version 2.0.1 intentionally replaces delimiter-based `fi1:` and `pr1:` hashes with unambiguous length-prefixed `fi2:` and `pr2:` hashes. This is a
corrective identity-version change: distinct semantic inputs could collide in the older encoding. Legacy `pr1` dataset JSON remains readable and is
normalized to `pr2`; newly created failure signatures and production datasets always use the new prefixes. Executable replay formats are unchanged.

<a id="versioning-determinism-contract"></a>

## Determinism contract

A compatible release must not deliberately weaken these guarantees for supported deterministic code:

- the same supported seed and exact replay schedule reproduce the same scheduler decisions for the same program and Causalia version;
- exact replay detects execution-shape drift rather than silently accepting a different schedule;
- simulation time is provided through `SimulationContext.TimeProvider`;
- simulation randomness is provided through `SimulationContext.Random` and does not consume scheduler or fault randomness;
- fault streams remain isolated from scheduler randomness;
- production-reality sampling remains isolated from scheduler, fault and user randomness;
- inconclusive bounded checks are reported as inconclusive rather than successful.

Deterministic behavior can still be invalidated by user code that escapes the simulation boundary through uncontrolled wall-clock time,
randomness, threads or external systems. `Causalia.Analyzers` exists to detect common escapes.

<a id="versioning-target-frameworks"></a>

## Target frameworks

Causalia 2.0 targets `net10.0` for runtime packages. `Causalia.Analyzers` targets `netstandard2.0` for normal packaging so it
can run in supported Roslyn compiler hosts.


<a id="visualization"></a>

<a id="visualization-trace-visualization"></a>

# Trace visualization

`Causalia.Visualization` turns a deterministic `SimulationResult`, `SimulationFailedException` or
`SimulationExplorationFailedException` into a normalized causal trace document and can export that document as a fully self-contained HTML file.
The visualizer does not change scheduling, fault evaluation or replay semantics; it only consumes already-recorded deterministic trace data.

```csharp
using Causalia.Visualization;

var result = await Simulation.RunAsync(
    new SimulationOptions { Seed = 729381 },
    scenario,
    cancellationToken);

var document = result.ToTraceDocument();
var html = result.ToTraceHtml(
    new TraceHtmlOptions
    {
        Title = "Checkout race",
        ShowSchedulerEvents = false
    });

await File.WriteAllTextAsync(
    "checkout-race.html",
    html,
    cancellationToken);
```

The HTML viewer contains no external scripts, stylesheets or web assets. It can therefore be attached to a CI test result or opened offline. It shows
seed, replay token, scheduler step, virtual time, faults and failure metadata, and supports filtering by category, severity, logical lane and causal
correlation identifier. Causalia 1.7 failure analyses additionally surface the `fi2:` signature, semantic failure kind, trigger attribution, analysis
confidence and minimized `m1:` reproduction token.

Known Causalia trace protocols are normalized into categories such as scheduler, node, messaging, ASP.NET Core, service-to-service network,
storage, faults, invariants, consistency, model-based testing and linearizability. Message IDs, HTTP-network request IDs, storage operation IDs and
linearizability operation IDs are
extracted as correlation identifiers where possible. Unknown or user-defined `context.TraceEvent(...)` messages remain visible under the `User`
category instead of being discarded.

The normalized `SimulationTraceDocument` is public API. Future JSON, Mermaid, OpenTelemetry or desktop visualizers can therefore consume the same
model without adding dependencies to `Causalia.Core`.


<a id="visualization-process-lifecycle-events"></a>

## Process lifecycle events

Causalia 1.2 classifies `process:*` events as `TraceEventCategory.Process` and assigns them to `process/{name}` lanes. This makes generation creation,
running, stopping, crashes and restarts visible separately from the underlying low-level node events.

<a id="visualization-model-based-events"></a>

## Model-based events

Causalia 1.6 classifies `model:{name}:command:{index}:*` events as `TraceEventCategory.ModelBased`, places them on `model/{name}` lanes and
correlates start, verification and failure events by model/command index. This keeps one model command visually grouped even when the command
itself schedules concurrent deterministic work.

<a id="visualization-failure-intelligence"></a>

## Failure intelligence

Causalia 1.7 can visualize a `FailureAnalysis` directly:

```csharp
var html = analysis.ToTraceHtml(
    new TraceHtmlOptions
    {
        Title = "Failure intelligence"
    });
```

The representative minimized failure supplies the timeline. Summary cards add the stable semantic signature, `FailureKind`, proven `FailureTrigger`,
analysis confidence and compact reproduction token while retaining the exact scheduler replay token for the representative trace.


<a id="visualization-time-travel-debugger"></a>

## Time-travel debugger

Causalia 1.8 exports retained DS-24 checkpoints with every trace document. The standalone viewer shows checkpoint count in the summary and adds an
interactive previous/next selector containing the `tt1:` token, scheduler step, checkpoint kind, optional label and watched state values. Older
checkpoints discarded by `MaxRetainedCheckpoints` are reported explicitly in the summary.

Time-travel state is supplemental to the causal event trace: automatic checkpoints are not emitted as synthetic trace events, so enabling the debugger
does not change trace-event ordering or scheduler replay shape.


<a id="visualization-production-reality-bridge"></a>

## Production Reality Bridge

Causalia 1.9 classifies `reality:` trace entries as `ProductionReality` events and shows the consumed evidence count in the trace summary. This makes it
possible to see where production-derived timing/outcomes entered the deterministic timeline alongside scheduler, faults and time-travel checkpoints.


<a id="contributing"></a>

<a id="contributing-contributing-to-causalia"></a>

# Contributing to Causalia

<a id="contributing-prerequisites"></a>

## Prerequisites

Install the .NET 10 SDK selected by `global.json` and the .NET 10 targeting packs. In Visual Studio, these components are available through
the Visual Studio Installer. The repository test projects target .NET 10.

<a id="contributing-validate-a-change"></a>

## Validate a change

```bash
dotnet restore Causalia.slnx --locked-mode
dotnet build Causalia.slnx --configuration Release --no-restore
dotnet test Causalia.slnx --configuration Release --no-build
dotnet pack Causalia.slnx --configuration Release --no-build
```

If Visual Studio reports `NETSDK1127` after a failed restore, restore again after resolving the preceding NuGet error. If `NETSDK1127` remains,
install the missing .NET 10 targeting pack and reload the solution.

Builds treat warnings and low-or-higher NuGet vulnerability findings as errors. Commit an updated `packages.lock.json` whenever a dependency graph
changes intentionally.

<a id="contributing-deterministic-code"></a>

## Deterministic code

Use `SimulationContext.TimeProvider`, `SimulationContext.Random` and the supplied cancellation tokens within deterministic boundaries. Avoid wall-clock
time, process-global randomness, raw threads, blocking waits and `ConfigureAwait(false)`. Mark method-group scenario helpers with
`[DeterministicSimulation]`; use `[AllowNondeterminism("reason")]` only for a documented external boundary.

Add regression tests for behavior changes. Pass `TestContext.CancellationToken` through asynchronous tests and prefer exact exception assertions.


<a id="security"></a>

<a id="security-security-policy"></a>

# Security policy

<a id="security-supported-versions"></a>

## Supported versions

Security fixes are made on the current 2.0.x package train. Upgrade to the latest patch release before reporting a reproducible issue.

<a id="security-reporting-a-vulnerability"></a>

## Reporting a vulnerability

Use private vulnerability reporting on the canonical source repository. Do not open a public issue for a suspected vulnerability. Include the affected
package and version, impact, reproduction steps and any proposed mitigation. Avoid including credentials, customer data or other sensitive material.

Verification-report fingerprints and evidence hashes detect accidental content changes; they are not signatures and do not establish authenticity.
Use signed release and CI provenance when artifacts may cross an untrusted boundary.


<a id="changelog"></a>

<a id="changelog-changelog"></a>

# Changelog

All notable changes to Causalia are documented here.

The project follows Semantic Versioning from version 1.0.0 onward.

<a id="changelog-211---2026-09-20"></a>

## 2.1.1 - 2026-09-20

Adoption and documentation release.

### Added

- `Causalia.Tool` with `dotnet causalia inspect` for solution/project discovery, technology detection, ranked simulation boundaries, determinism hints,
  adoption-readiness signals, package recommendations, and stable JSON output;
- `dotnet causalia init` for generating one safe, compiling first Causalia test project from the same inspection model;
- concise `Simulation.RunAsync(scenario, cancellationToken)` and `context.Invariant(...)` golden-path APIs;
- `CAU1007` for `CancellationToken.None` inside deterministic simulation code, while keeping ordinary production code outside deterministic boundaries
  free from simulation-only diagnostics;
- beginner-facing `CAUSALIA FAILURE` presentation with invariant/outcome, relevant sequence, replay token, and reproduction guidance.

### Documentation

- reduced `README.md` to the installation/adoption/how-to path;
- added `SCENARIOS.md` as the problem-oriented cookbook and complete advanced reference;
- retained source-backed examples and extended release verification to validate and package both documents;
- added recipes for ambiguous commits, duplicate delivery, concurrent updates, inbox/outbox replay, broker redelivery, restart recovery, HTTP ambiguity,
  eventual consistency, lease races, exploration, replay, minimization, and failure intelligence.

### Compatibility

The adoption layer is additive. Existing deterministic execution, replay, minimization, model, consistency, linearizability, Production Reality, time-travel,
and Verification Platform semantics remain unchanged. Runtime projects remain .NET 10 only; the analyzer remains `netstandard2.0`.

<a id="changelog-201---2026-09-14"></a>

## 2.0.1 - 2026-09-14

Production-hardening release.

<a id="changelog-fixed"></a>

### Fixed

- dispatched public simulation, replay, reproduction, minimization and exploration work on the default task scheduler so calls no longer block the
  caller before returning a task, while preserving pre-start cancellation;
- disposed scheduler contexts, nodes, cancellation sources, request clones and response pipes at their ownership boundaries;
- preserved stopped-node waiters across crashes and treated generation cancellation during graceful stop as normal termination;
- corrected zero-preemption DPOR exploration so the active operation continues when it remains runnable and preemptive alternatives are pruned;
- replaced ambiguous delimiter-based Failure Intelligence and Production Reality hashes with collision-safe `fi2:` and `pr2:` encodings;
- retained legacy `pr1` JSON ingestion while normalizing newly loaded or emitted datasets to `pr2`;
- bounded schedule, reproduction, model-sequence, production-evidence, verification-report and deterministic-load inputs;
- propagated replacement ASP.NET Core request-abort tokens from node/request cancellation and retained the caller's original HTTP request on responses;
- made storage terminal transitions one-shot, recorded provider cancellation, and cleaned message-delivery attempt state after final delivery;
- classified trace severity from protocol status fields instead of arbitrary message substrings.

<a id="changelog-tooling-and-release-validation"></a>

### Tooling and release validation

- added `CAU1006` for `ConfigureAwait(false)` inside deterministic boundaries and declared analyzer support for both C# and Visual Basic;
- enabled locked restores, vulnerability auditing, package validation, runtime symbol packages and complete packaged documentation;
- mapped public dependencies to `nuget.org` so Central Package Management remains valid when additional machine-level feeds are configured;
- added Windows and Linux CI across .NET 10 with warnings-as-errors builds, tests and coverage collection;
- expanded regression coverage for lifecycle cancellation, DPOR bounds, hash collisions, storage state, HTTP ownership, input limits and trace severity.

<a id="changelog-compatibility"></a>

### Compatibility

Public runtime APIs remain source-compatible. Executable `v1:`, `m1:` and `mb1:` formats are unchanged. Existing `fi1:` values remain historical
evidence; new signatures use `fi2:`. Legacy `pr1` dataset JSON remains readable, while new fingerprints and serialized documents use `pr2:`.

<a id="changelog-200---2026-09-13"></a>

## 2.0.0 - 2026-09-13

Verification Platform release.

<a id="changelog-added"></a>

### Added

- DS-26 `VerificationPlan` and `VerificationPlanBuilder` for ordered composition of seeded simulation, schedule exploration and executable model verification;
- `Simulation.VerifyAsync` and `VerificationRunOptions` with collect-all or explicit fail-fast semantics;
- typed `VerificationStepResult` evidence retaining successful engine results, deterministic failures, Failure Intelligence, time-travel timelines and production-reality evidence;
- unified cross-step `FailureIntelligenceReport` clustering equivalent `fi1:` failures across engines, seeds and plan steps;
- portable `VerificationReport` JSON with content-addressed `vp1:` fingerprints and content-integrity validation on parse;
- CI-friendly `VerificationRunResult.EnsurePassed()` and structured `VerificationRunFailedException`;
- framework-neutral and Reqnroll provider support through `VerifyAsync` and `LastVerificationResult`;
- standalone verification-plan HTML export through `Causalia.Visualization`;
- [VerificationPlatform](#verificationplatform) covering plans, evidence composition, failure triage, CI gates and report boundaries.

<a id="changelog-platform-semantics"></a>

### Platform semantics

- verification plan steps execute sequentially in declaration order so orchestration itself does not introduce uncontrolled timing;
- concurrency, simulated load and production overlap remain inside deterministic Causalia scenarios where they can be replayed;
- scenario steps can compose invariants, consistency, linearizability, time travel, Production Reality, load and ecosystem adapters;
- scheduler steps can use depth-first, coverage-guided or DPOR exploration;
- model steps use the executable state-machine engine and preserve model/scheduler failure evidence;
- `StopOnFirstFailure` records later steps as `Skipped` rather than silently omitting them.

<a id="changelog-compatibility-1"></a>

### Compatibility

Causalia 2.0 keeps the established 1.x deterministic execution and evidence formats intact. Existing `v1:`, `m1:`, `mb1:`, `fi1:`, `tt1:` and
`pr1:` tokens retain their meanings; `vp1:` is a new content-addressed verification-report identity that references those artifacts.

<a id="changelog-190---2026-09-13"></a>

## 1.9.0 - 2026-09-13

<a id="changelog-added-1"></a>

### Added

- Added DS-25 Production Reality Bridge through `SimulationContext.ProductionReality`.
- Added normalized `ProductionRealityDataset` evidence with portable JSON roundtrip and stable content-addressed `pr1:` fingerprints.
- Added dependency-free import from stopped `System.Diagnostics.Activity` spans.
- Added production-derived per-operation latency percentiles and outcome rates.
- Added isolated deterministic sampling with `Sample` / `ApplyAsync`; sampled durations advance virtual time without consuming user, scheduler or fault randomness.
- Added correlated incident replay that preserves production relative start offsets and permits overlapping deterministic operations.
- Added structured production evidence to `SimulationResult`, `SimulationFailedException`, `ISimulationProvider` and `SimulationProvider`.
- Added `ProductionReality` trace visualization classification and summary evidence counts.
- Extended the determinism analyzer to treat `ReplayIncidentAsync` handlers as deterministic simulation code.

<a id="changelog-compatibility-2"></a>

### Compatibility

Causalia 1.9 is additive over the 1.0 compatibility baseline. Existing `v1:`, `m1:`, `mb1:`, `fi1:` and `tt1:` formats are unchanged. `pr1:`
identifies normalized immutable production evidence and is not an executable schedule token. Production sampling uses its own deterministic random
stream so enabling the bridge does not perturb existing user, scheduler or fault random sequences.

<a id="changelog-180---2026-09-13"></a>

## 1.8.0 - 2026-09-13

<a id="changelog-added-2"></a>

### Added

- Added DS-24 deterministic time-travel debugging through `SimulationContext.TimeTravel`.
- Added named deterministic state probes with explicit typed formatting and non-invasive probe-error capture.
- Added manual checkpoints and optional automatic start/before-step/after-step/completed/failure checkpoint capture.
- Added stable opaque `tt1:` checkpoint tokens, bounded newest-first retention metadata and deterministic replay stability.
- Added `TimeTravelTimeline`, cursor navigation by token/step/trace event, previous/next state-change navigation and checkpoint diffing.
- Added `SimulationResult.TimeTravel`, `SimulationFailedException.TimeTravel` and provider `LastTimeTravel` integration.
- Added interactive time-travel state inspection to `Causalia.Visualization` standalone HTML traces.
- Extended `Causalia.Analyzers` so time-travel capture and formatter delegates are deterministic simulation boundaries.
- Added [TimeTravel](#timetravel) and time-travel regression coverage across core, provider, analyzer and visualization projects.

<a id="changelog-compatibility-3"></a>

### Compatibility

Causalia 1.8 is additive over the 1.0 compatibility baseline. Existing `v1:`, `m1:`, `mb1:` and `fi1:` formats are unchanged; `tt1:` is a new opaque
checkpoint-navigation token. Time-travel capture does not create scheduler decisions or consume deterministic random streams. The new
`ISimulationProvider.LastTimeTravel` member uses a default interface implementation so existing custom providers remain source-compatible.

<a id="changelog-170---2026-09-13"></a>

## 1.7.0 - 2026-09-13

Deterministic failure intelligence release.

<a id="changelog-added-3"></a>

### Added

- stable versioned `fi1:` semantic signatures on every `SimulationFailedException`, independent from seed and concrete scheduler schedule;
- `FailureKind` classification for unhandled exceptions, invariant violation/evaluation, linearizability violation/inconclusive results, consistency
  violations, model violations, deadlocks and step-limit failures;
- `FailureAnalyzer.Describe` for immediate no-rerun classification, summaries and bounded trace context;
- `Simulation.AnalyzeFailureAsync` for deterministic causal reduction using the existing bounded minimizer;
- `FailureTrigger` attribution for deterministic, scheduler-ordering, fault-injection and mixed scheduler/fault reproductions;
- `FailureAnalysis` evidence including essential scheduler choices, essential fault occurrences, human-readable explanation and compact `m1:` token;
- `FailureAnalysisConfidence` to distinguish descriptive, bounded-budget and completed minimization results;
- `FailureAnalyzer.CreateReport`, `FailureCluster` and deterministic triage ordering for repeated semantic failure occurrences across seeds and schedules;
- automatic descriptive `LastFailureAnalysis` plus provider-level causal analysis in `SimulationProvider`, `ISimulationProvider` and Reqnroll;
- failure-intelligence metadata in `Causalia.Visualization`, including direct `FailureAnalysis` trace/HTML export;
- [FailureIntelligence](#failureintelligence) with semantic identity, causal attribution, clustering, provider and visualization guidance.

<a id="changelog-changed"></a>

### Changed

- DS-9 minimization now compares the same semantic failure identity used by public `fi1:` signatures, improving consistency/model-aware reproduction matching;
- known verification failures no longer depend on incidental virtual timestamps, search-state counts, seeds or scheduler replay tokens for failure identity.

<a id="changelog-validation"></a>

### Validation

- runtime smoke validation proves all four minimized trigger classes: deterministic, fault-only, schedule-only and schedule-plus-fault;
- runtime validation covers invariant, consistency, linearizability, model, deadlock, step-limit and unhandled-exception classification;
- runtime validation proves one `fi1:` identity remains stable across seeds and that minimized `m1:` reproduction retains the same semantic signature;
- clustering validation groups repeated signatures and deterministically prioritizes the repeated cluster;
- provider and visualization coverage verifies retained analyses and signature/trigger/reproduction metadata.

<a id="changelog-compatibility-4"></a>

### Compatibility

Causalia 1.7 is additive over the 1.0 compatibility baseline. Existing `v1:`, `m1:` and `mb1:` token formats are unchanged; `fi1:` is a new opaque
semantic-identity token. New `ISimulationProvider` members use default interface implementations so custom providers remain source-compatible.

<a id="changelog-160---2026-09-13"></a>

## 1.6.0 - 2026-09-13

Executable model-based testing release.

<a id="changelog-added-4"></a>

### Added

- `ModelBasedSpecification<TState, TSystem>` executable state-machine specifications with a fresh system under test for every deterministic run;
- strongly typed `ModelCommand.Create` commands with state preconditions, pure model transitions, async SUT execution and typed observation verification;
- bounded `Simulation.CheckModelAsync` command-sequence exploration with `MaxSequences` and `MaxCommandDepth`;
- optional per-sequence scheduler exploration using depth-first, coverage-guided or DPOR strategies;
- stable `mb1:` model-sequence replay tokens that compose with existing exact `v1:` scheduler replay schedules;
- `Simulation.ReplayModelAsync` for strict command-sequence replay with optional exact scheduler replay;
- `Simulation.MinimizeModelAsync` command-sequence delta debugging that preserves the deterministic failure class;
- structured `SimulationModelViolationException`, model replay diagnostics and model exploration failure metadata;
- model result/failure/minimization retention in `SimulationProvider` plus a backward-compatible `ISimulationProvider.LastModelFailure` convenience member;
- analyzer recognition of model system factories and command execution delegates as deterministic boundaries;
- model-based trace classification, dedicated visualization lanes and command correlation;
- [ModelBasedTesting](#modelbasedtesting) with state-machine, replay, DPOR composition and shrinking guidance.

<a id="changelog-validation-1"></a>

### Validation

- tests cover deterministic command-sequence traversal, preconditions, command-depth bounds and empty state spaces;
- tests cover structured verifier failures, strict `mb1:` parsing/replay and replay rejection after model-shape drift;
- tests cover two-dimensional replay using the exact model sequence and scheduler schedule;
- tests cover DPOR scheduler exploration nested inside model-sequence exploration;
- tests cover model command-sequence minimization and provider/visualization/analyzer integration.

<a id="changelog-compatibility-5"></a>

### Compatibility

Causalia 1.6 is additive over the 1.0 compatibility baseline. Existing simulation, exploration, consistency and linearizability APIs are unchanged;
model-based testing is opt-in and uses its own versioned command-sequence token.

<a id="changelog-150---2026-09-13"></a>

## 1.5.0 - 2026-09-13

Distributed consistency verification release.

<a id="changelog-added-5"></a>

### Added

- `SimulationContext.Consistency` and typed `ConsistencyHistory<TKey>` logical read/write histories;
- logical `ConsistencyVersion<TKey>` tokens with complete session causal predecessor closure;
- `ReadYourWrites` and `MonotonicReads` checks directly from session observations;
- replica-scoped `MonotonicWrites`, `WritesFollowReads` and `CausalVisibility` verification;
- optional `ReplicaReadAgreement` against explicitly observed replica snapshots;
- bounded virtual-time replica convergence requirements through `RequireConvergence`;
- `SimulationConsistencyViolationException`, violation kinds and successful `ConsistencyOutcome` result metadata;
- `SimulationFailedException.ConsistencyFailure` and a backward-compatible `ISimulationProvider.LastConsistencyFailure` convenience member;
- consistency trace classification, dedicated visualization lanes and version correlation;
- [Consistency](#consistency) with session, causal, replica and convergence guidance.

<a id="changelog-validation-2"></a>

### Validation

- tests cover stale read-your-writes observations and causally newer descendants;
- tests cover monotonic-read regression, monotonic-write gaps and writes-follow-reads gaps;
- tests cover causal predecessor visibility and replica/read disagreement;
- tests cover successful convergence before a virtual deadline and deterministic convergence failure at the deadline;
- DPOR exploration tests cover a replication/read race; replay tests cover exact consistency-failure reproduction; provider and visualization tests
  cover failure retention and trace classification.

<a id="changelog-compatibility-6"></a>

### Compatibility

Causalia 1.5 is additive over the 1.0 compatibility baseline. Existing simulations do not create consistency histories unless they opt into the new
API, and linearizability remains available for APIs that require a single legal sequential history.

<a id="changelog-140---2026-09-13"></a>

## 1.4.0 - 2026-09-13

Advanced deterministic exploration release.

<a id="changelog-added-6"></a>

### Added

- `ExplorationStrategy.DynamicPartialOrderReduction` with conservative equivalent-schedule elimination;
- explicit logical exploration operations with complete `Read`, `Write` and `Synchronize` resource-access declarations;
- unmodeled user operations remain conservatively dependent so DPOR never guesses independence;
- causal operation/work-item lineage across continuations and virtual-time wakeups for happens-before analysis;
- virtual timer scheduling captures the originating exploration operation so equal-time wakeups retain dependency metadata;
- optional `ExplorationOptions.MaxPreemptions` preemption bounding;
- `EquivalentSchedulesPruned`, `PreemptionBoundPruned` and `MaximumPreemptionsObserved` exploration metrics;
- analyzer recognition of exploration-operation lambdas as deterministic boundaries;
- [AdvancedExploration](#advancedexploration) with DPOR usage and soundness guidance.

<a id="changelog-validation-3"></a>

### Validation

- six independent logical operations reduce from 720 DFS orders to one DPOR schedule;
- three conflicting writes remain six schedules;
- read/read accesses are reduced while read/write conflicts are preserved;
- independent operations retain DPOR lineage across equal virtual-time delays;
- unmodeled operations remain conservative;
- races after equal virtual timers remain discoverable through causal lineage;
- DPOR-discovered failures retain exact `v1:` replay compatibility.

<a id="changelog-compatibility-7"></a>

### Compatibility

Causalia 1.4 is additive over the 1.0 compatibility baseline. Existing depth-first and coverage-guided strategies remain unchanged; applications opt into DPOR explicitly.

<a id="changelog-130---2026-09-13"></a>

## 1.3.0 - 2026-09-13

Deterministic ecosystem adapters release.

<a id="changelog-added-7"></a>

### Added

- new `Causalia.Dapr` package with deterministic state stores, ETags, conditional writes/deletes, state transactions, pub/sub redelivery,
  dead-lettering and service invocation over the simulated HTTP network;
- Dapr pub/sub delivery faults including virtual delay and lost acknowledgements that force at-least-once redelivery;
- new `Causalia.Kafka` package with partition logs, stable key partitioning, consumer groups, deterministic rebalances, local offset storage and durable committed offsets;
- Kafka ambiguous commit faults that distinguish rejection before commit from lost acknowledgement after a durable commit;
- new `Causalia.RabbitMQ` package with direct/fanout exchanges, queues, prefetch, manual ack/nack, requeue and redelivery after consumer closure;
- RabbitMQ publisher faults that distinguish broker rejection from a lost publisher confirm after routing;
- new `Causalia.Grpc` package with typed unary methods, canonical status codes, virtual deadlines, directed partitions, latency and explicit retry policies;
- gRPC deterministic `Unavailable` and delay fault policies;
- analyzer recognition of Dapr subscription handlers and gRPC unary handlers as deterministic boundaries;
- Dapr, Kafka, RabbitMQ and gRPC trace categories and correlation in `Causalia.Visualization`;
- [EcosystemAdapters](#ecosystemadapters) with production-boundary and usage guidance.

<a id="changelog-compatibility-8"></a>

### Compatibility

Causalia 1.3 is additive over the 1.0 compatibility baseline. The ecosystem packages are optional semantic test adapters and do not add production
SDK dependencies to `Causalia.Core`.

<a id="changelog-120---2026-09-13"></a>

## 1.2.0 - 2026-09-13

Full simulated process lifecycle release.

<a id="changelog-added-8"></a>

### Added

- generic `SimulationProcess<TGeneration>` with a stable logical node and replaceable volatile process generations;
- explicit `Starting`, `Running`, `Stopping`, `Stopped`, `Crashed` and `Restarting` process states;
- distinct graceful-stop and abrupt-crash semantics, where crashes deliberately skip generation `StopAsync`;
- generation factories that are rerun after restart so volatile object graphs are reconstructed;
- generation-scoped deterministic background work through `SimulationProcessGenerationContext.RunBackgroundAsync`;
- additive `SimulationNode.Stop()` and `SimulationNodeState.Stopped`;
- restartable `AspNetCoreSimulationProcess` and `StartAspNetCoreProcessAsync`;
- fresh ASP.NET Core `WebApplication`, DI service provider and singleton graph after every process restart;
- `SimulationProcessGenerationContext` in ASP.NET Core DI;
- deterministic `SimulationBackgroundService` for long-lived generation-scoped hosted work;
- process-aware `SimulationHttpNetwork` registrations that follow replacement ASP.NET Core generations;
- existing process `HttpClient` instances continue to target the replacement generation after restart;
- analyzer recognition of process generation factories and `SimulationBackgroundService.ExecuteAsync`;
- `TraceEventCategory.Process` and dedicated process lanes in visualization;
- [ProcessLifecycle](#processlifecycle) with generic process, ASP.NET Core, background worker, storage and network guidance.

<a id="changelog-compatibility-9"></a>

### Compatibility

Causalia 1.2 is additive over the 1.0 compatibility baseline. The original `SimulationNode` and `StartAspNetCoreAsync` APIs remain available with their
existing lightweight semantics; applications opt into reconstructed volatile process state through the new process APIs.

<a id="changelog-110---2026-09-13"></a>

## 1.1.0 - 2026-09-13

Deterministic simulated load release.

<a id="changelog-added-9"></a>

### Added

- new `Causalia.Load` package;
- fixed-concurrency closed-model workloads with stable logical actors and virtual think time;
- constant open-model arrival-rate workloads with fractional virtual pacing and deterministic dropped-arrival accounting;
- piecewise-linear ramping arrival-rate stages;
- same-instant burst workloads for saturation and thundering-herd scenarios;
- deterministic load metrics including scheduled, started, completed, failed and dropped iterations, peak concurrency and virtual throughput;
- exact virtual latency minimum, mean, median, p95, p99 and maximum statistics;
- record-and-continue iteration failure mode with bounded representative failure samples;
- deterministic acceptance thresholds for failure rate, dropped rate, p95, p99 and peak concurrency;
- replayable `SimulationLoadThresholdException` failures containing the completed load result;
- load coverage signals that participate in coverage-guided exploration;
- analyzer recognition of `LoadIterationContext` deterministic boundaries, including existing CAU1001-CAU1005 protections;
- optional per-iteration load tracing and load classification/correlation in `Causalia.Visualization`;
- additive `SimulationOptions.TraceSchedulerEvents` control for large virtual workloads without disabling schedule capture or replay;
- [Load](#load) with closed/open model, ramping, burst, threshold, exploration and replay examples.

<a id="changelog-compatibility-10"></a>

### Compatibility

Causalia 1.1 is additive over the 1.0 stable API baseline. Existing 1.0 public APIs remain source-compatible. All first-party packages continue
to ship as one versioned package train.

<a id="changelog-100---2026-09-12"></a>

## 1.0.0 - 2026-09-12

First stable release of Causalia.

<a id="changelog-stable-feature-baseline"></a>

### Stable feature baseline

- deterministic virtual time and isolated user randomness;
- deterministic async scheduling and seeded replay;
- deterministic messaging with latency, ordering and delivery attempts;
- generic deterministic fault injection;
- simulated node crash/restart with generation-scoped cancellation;
- bounded systematic schedule exploration;
- `Always`, `Never` and `Eventually` invariants;
- failing reproduction minimization;
- bounded linearizability checking;
- Roslyn determinism diagnostics;
- deterministic ASP.NET Core in-memory hosting;
- service-to-service HTTP network simulation with latency, drops, duplicates and partitions;
- provider-independent durable storage simulation and EF Core commit-boundary integration;
- coverage-guided exploration;
- normalized causal trace documents and standalone HTML visualization;
- framework-neutral testing provider and Reqnroll scenario provider.

<a id="changelog-compatibility-promise"></a>

### Compatibility promise

The public APIs shipped in the 1.0 packages form the compatibility baseline for the 1.x release line. Additive functionality may be introduced
in minor versions. Bug fixes and behavioral corrections that preserve the public contract may be introduced in patch releases. Intentional
breaking API changes require a new major version.
