# Causalia 2.1.1

> Deterministic simulation testing for concurrent and distributed .NET software.
>
> **Website:** https://pjotrcasteel.github.io/Causalia/ · **NuGet:** https://www.nuget.org/packages/Causalia

Causalia makes failures caused by **timing, retries, duplicate delivery, partial persistence, restarts, and concurrency** reproducible.
Instead of hoping a race happens again, a test controls time and scheduling, injects the difficult production outcome, states the invariant that must
hold, and keeps an exact replay token when it fails.

Runtime packages target **.NET 10**. The analyzer package targets `netstandard2.0` so it can run in the compiler. Causalia complements unit and real
integration/end-to-end tests; it does not replace provider-specific verification or real performance testing.

<a id="why"></a>

## When Causalia is useful

Causalia is a strong fit when application correctness depends on one or more of these words:

- retry or idempotency;
- at-least-once delivery or redelivery;
- timeout after an external side effect;
- optimistic concurrency, ETag, CAS, lease, lock, or competing workers;
- inbox/outbox replay;
- process crash/restart;
- eventual consistency or distributed invariants;
- races that are difficult to reproduce in a normal test runner.

For simple CRUD with no meaningful concurrency or partial-failure behavior, ordinary unit/integration tests are usually the better tool.

<a id="five-minute-start"></a>

## Five-minute start

Install the adoption CLI:

```bash
dotnet tool install --global Causalia.Tool --version 2.1.1
```

Inspect an existing solution without changing it:

```bash
dotnet causalia inspect MyService.slnx
```

`inspect` ranks likely simulation boundaries, reports adoption-friendly seams such as `TimeProvider` and propagated cancellation, flags common
nondeterministic APIs, and recommends the Causalia packages that fit the selected project. For automation:

```bash
dotnet causalia inspect MyService.slnx --format json
```

Generate the first Causalia test project:

```bash
dotnet causalia init MyService.slnx
```

For a multi-project solution you can select the production project explicitly:

```bash
dotnet causalia init MyService.slnx --project src/Orders/Orders.csproj
```

`init` creates one compiling test project and a green deterministic simulation. It does not invent domain adapters or rewrite production code. Existing
generated output is protected; replacement requires `--force`.

Tests normally go in `tests/<Project>.Causalia.Tests`. If this would put them inside the production project's source tree,
`init` uses `.causalia/tests/<Project>.Causalia.Tests`, which .NET SDK default compile items exclude from the parent project.
The command prints the generated project path; production files are not modified. Projects with custom source globs must also exclude this directory.

<a id="first-simulation"></a>

## Your first simulation

Start with one business guarantee and one production boundary. Keep the application independent of Causalia where possible: inject normal .NET seams
such as `TimeProvider`, `CancellationToken`, and narrow storage/messaging/HTTP interfaces, then use simulated adapters from the test project.

```csharp
[TestMethod]
public async Task OperationShouldHappenOnce()
{
    var observedEffects = 0;

    var result = await Simulation.RunAsync(
        async context =>
        {
            context.Invariant("operation happens once", () => observedEffects <= 1);

            observedEffects++;
            await Task.Delay(
                TimeSpan.FromMilliseconds(10),
                context.TimeProvider,
                context.CancellationToken);
        },
        TestContext.CancellationToken);

    Assert.AreEqual(TimeSpan.FromMilliseconds(10), result.VirtualElapsed);
}
```

The useful next step is to replace `observedEffects++` with a real application entry point and one simulated dependency boundary. For example, model a
database commit whose acknowledgement is lost and verify that retry does not duplicate the business effect.

<a id="choose-scenario"></a>

## Pick the scenario by the production symptom

| Production symptom | Start with |
| --- | --- |
| Retry around a database write | commit succeeds, acknowledgement is lost |
| Handler can receive the same message again | duplicate/redelivered message |
| Two requests update the same state | concurrent update / ETag race |
| Inbox/outbox can be replayed | failure between durable record and external effect |
| Dapr/Kafka/RabbitMQ retries | acknowledgement/offset/confirmation ambiguity |
| Hosted work can restart | crash after partial durable progress |
| HTTP client retries | timeout after possible remote side effect |
| Replicas catch up later | eventual convergence under virtual time |
| Lease protects work | expiry/renewal race between workers |

The complete recipes and advanced APIs are in **[SCENARIOS.md](SCENARIOS.md)**. On NuGet, use the canonical repository copy:
https://github.com/pjotrcasteel/Causalia/blob/main/SCENARIOS.md

<a id="failure-output"></a>

## Understand and replay a failure

A first failure is presented around the information needed to act on it:

```text
CAUSALIA FAILURE
Invariant: order happens once
Observed: ...
Failure sequence:
  1. order received
  2. storage commit completed
  3. retry started
Replay: CAUSALIA:...
Reproduce: Simulation.ReplayAsync(...)
```

The trace is evidence, not automatic root-cause proof. If injected faults were active, Causalia reports that without claiming they caused the failure.
Use minimization and `FailureAnalyzer` when you need to prove whether the reduced reproduction depends on scheduler ordering, fault injection, both, or
neither.

<a id="analyzers"></a>

## Adoption-aware analyzers

`Causalia.Analyzers` only treats code as deterministic when it is inside a simulation boundary (for example `[DeterministicSimulation]` or a recognized
simulation scenario). Ordinary production code is not globally linted as if it were simulation code.

Important diagnostics include:

| Rule | Meaning |
| --- | --- |
| `CAU1001` | use virtual time instead of wall-clock/timer APIs |
| `CAU1002` | use deterministic simulation randomness |
| `CAU1003` | avoid uncontrolled concurrency such as `Task.Run` |
| `CAU1004` | avoid blocking waits |
| `CAU1005` | mark method-group simulation scenarios for deterministic analysis |
| `CAU1006` | keep awaits on the deterministic scheduler |
| `CAU1007` | propagate the simulation cancellation token instead of `CancellationToken.None` |

`dotnet causalia inspect` surfaces corresponding adoption hints before code has been moved into a deterministic boundary.

<a id="packages"></a>

## Packages

Start with `Causalia`, then add only the integrations your test actually needs.

| Package | Purpose |
| --- | --- |
| `Causalia` | core runtime, virtual time, scheduling, replay, invariants and verification primitives |
| `Causalia.Tool` | `dotnet causalia inspect` and `dotnet causalia init` |
| `Causalia.Analyzers` | deterministic-boundary diagnostics |
| `Causalia.Testing` | framework-neutral scenario-scoped provider |
| `Causalia.Reqnroll` | Reqnroll scenario provider |
| `Causalia.AspNetCore` | deterministic ASP.NET Core host and logical HTTP network |
| `Causalia.Storage` | provider-independent durable storage and ambiguous commit semantics |
| `Causalia.EntityFrameworkCore` | EF Core commit-boundary integration |
| `Causalia.Dapr` | Dapr state, ETag, pub/sub and service-invocation semantics |
| `Causalia.Kafka` | partitions, consumer groups, rebalances and committed-offset semantics |
| `Causalia.RabbitMQ` | exchanges, queues, acknowledgements and redelivery |
| `Causalia.Grpc` | unary calls, statuses, virtual deadlines, partitions and retries |
| `Causalia.Load` | deterministic simulated load for correctness under concurrency pressure |
| `Causalia.Visualization` | trace/failure/time-travel visualization and standalone HTML export |

Typical test-project start:

```bash
dotnet add package Causalia --version 2.1.1
dotnet add package Causalia.Analyzers --version 2.1.1
```

<a id="adopt-existing"></a>

## Add Causalia to an existing service

1. Run `dotnet causalia inspect` and choose one ranked boundary.
2. Add only the determinism seams required by that boundary — commonly `TimeProvider`, cancellation propagation, and a narrow external-effect interface.
3. Run `dotnet causalia init` to create the test project.
4. Replace the generated demonstration with the real application entry point and one simulated adapter.
5. Inject one realistic ambiguous failure or concurrency ordering and write one domain invariant.
6. Make the test reproduce the bug, fix production behavior, and keep the replay as a regression test.
7. Keep real infrastructure integration tests. Causalia verifies the semantics you model; it does not certify the provider itself.

If adoption requires pushing Causalia runtime types throughout the domain/application layer, the simulation boundary is probably too broad.

<a id="build"></a>

## Build and release verification

From the repository root:

```bash
dotnet restore Causalia.slnx --locked-mode
dotnet build Causalia.slnx -c Release --no-restore
dotnet test Causalia.slnx -c Release --no-build --no-restore
dotnet pack Causalia.slnx -c Release --no-build --no-restore -o artifacts/packages
python eng/verify.py --packages artifacts/packages --consumer
```

`eng/verify.py` validates the two authored Markdown documents, checks the source-backed examples in `SCENARIOS.md`, verifies package payloads, installs the
packaged CLI, executes `inspect` and `init`, builds/tests the generated project, and verifies the packaged determinism analyzer.

The supplied `publish-nuget.yml` is manually triggered on `main`. It validates on Linux and Windows, then publishes the verified packages using the
repository's `NUGET_API_KEY` secret. It does not publish automatically on a push.

<a id="docs"></a>

## Documentation

- **This README:** installation, adoption flow, first simulation, diagnostics and package selection.
- **[SCENARIOS.md](SCENARIOS.md):** real-world recipes, complete production-style example, exploration/DPOR, messaging, storage, process lifecycle,
  ecosystem adapters, consistency, linearizability, model-based testing, failure intelligence, time travel, Production Reality, Reqnroll, visualization,
  verification platform and versioning reference.

<a id="license"></a>

## License

MIT. See [LICENSE](LICENSE).
