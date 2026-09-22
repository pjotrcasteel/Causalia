const scenarios = {
  ack: {
    invariant: "The order is applied at most once",
    description: "The durable write succeeds, its acknowledgement is lost, and retry must not repeat the business effect.",
    schedules: "128",
    token: "CAUSALIA:ACK-7F2A-91C",
    hero: ["Order received", "Storage commit succeeds", "Acknowledgement disappears", "Retry duplicates the effect"],
    heroNote: "injected ambiguity",
    events: [
      ["Order handler starts", "attempt 1 enters the controlled boundary"],
      ["Storage commit succeeds", "durable state is now visible"],
      ["Commit acknowledgement is lost", "caller observes an ambiguous failure"],
      ["Retry repeats business effect", "invariant violated: observed count = 2"]
    ]
  },
  duplicate: {
    invariant: "A message changes business state only once",
    description: "The broker redelivers the same logical message. The handler must recognize the duplicate without losing legitimate work.",
    schedules: "64",
    token: "CAUSALIA:DUP-22B4-E10",
    hero: ["Message delivered", "Handler completes", "Broker redelivers", "Duplicate effect detected"],
    heroNote: "at-least-once delivery",
    events: [
      ["Message delivery #1", "handler begins processing"],
      ["Business effect committed", "first delivery completes"],
      ["Same message is redelivered", "broker retries after missing acknowledgement"],
      ["Second effect attempted", "invariant violated: duplicate business change"]
    ]
  },
  race: {
    invariant: "Only one writer wins the state transition",
    description: "Two workers load the same version and race to save competing updates. The loser must not silently overwrite the winner.",
    schedules: "312",
    token: "CAUSALIA:RACE-4D18-B72",
    hero: ["Two workers load v7", "Worker A saves v8", "Worker B still holds v7", "Lost update exposed"],
    heroNote: "competing schedule",
    events: [
      ["Workers A and B load version 7", "both observe the same starting state"],
      ["Worker A saves version 8", "first compare-and-swap wins"],
      ["Worker B resumes with version 7", "stale write reaches persistence"],
      ["Worker B overwrites state", "invariant violated: lost update"]
    ]
  },
  crash: {
    invariant: "Recovery resumes without repeating completed work",
    description: "The process crashes after durable progress but before the workflow checkpoint. Restart must continue from evidence, not from hope.",
    schedules: "96",
    token: "CAUSALIA:CRASH-8A04-C31",
    hero: ["Operation starts", "External effect completes", "Process crashes", "Restart repeats effect"],
    heroNote: "lifecycle fault",
    events: [
      ["Operation begins", "workflow enters a controlled lifecycle"],
      ["External effect completes", "downstream side effect is durable"],
      ["Process crashes before checkpoint", "in-memory progress disappears"],
      ["Restart repeats external effect", "invariant violated after recovery"]
    ]
  }
};

const fitContent = {
  unit: {
    kicker: "UNIT TESTS",
    title: "Make local logic fast, focused and easy to reason about.",
    body: "Best for pure behavior and small components. Usually one execution path at a time, with dependencies replaced or isolated.",
    speed: 100, realism: 28, control: 78, replay: 98
  },
  causalia: {
    kicker: "DETERMINISTIC SIMULATION",
    title: "Explore difficult orderings cheaply and replay them exactly.",
    body: "Model the semantics that matter, control time and scheduling, inject ambiguity, then assert domain invariants across many executions.",
    speed: 88, realism: 64, control: 100, replay: 100
  },
  integration: {
    kicker: "INTEGRATION TESTS",
    title: "Verify your code against real infrastructure behavior.",
    body: "Best for provider compatibility, serialization, network contracts and actual databases or brokers. More realistic, but less control over rare timing.",
    speed: 55, realism: 88, control: 38, replay: 58
  },
  e2e: {
    kicker: "END-TO-END TESTS",
    title: "Prove the assembled system works across real boundaries.",
    body: "Highest environment realism for critical journeys. Expensive to explore many schedules and usually hardest to reproduce timing-sensitive failures.",
    speed: 24, realism: 100, control: 18, replay: 34
  }
};

const codeSamples = {
  inspect: {
    title: "terminal · adoption CLI",
    noteTitle: "Start without changing production code.",
    noteBody: "The CLI ranks likely simulation boundaries and points out useful seams such as TimeProvider and propagated cancellation.",
    link: "https://github.com/pjotrcasteel/Causalia#five-minute-start",
    code: `# Install the adoption CLI
dotnet tool install --global Causalia.Tool --version 2.1.1

# Inspect an existing solution
dotnet causalia inspect MyService.slnx

# Generate the first deterministic test project
dotnet causalia init MyService.slnx`
  },
  test: {
    title: "C# · first simulation",
    noteTitle: "Keep the invariant close to the behavior.",
    noteBody: "Use normal .NET seams where possible. Causalia belongs at the simulation boundary rather than leaking through your application.",
    link: "https://github.com/pjotrcasteel/Causalia#first-simulation",
    code: `[TestMethod]
public async Task OperationShouldHappenOnce()
{
    var observedEffects = 0;

    var result = await Simulation.RunAsync(
        async context =>
        {
            context.Invariant(
                "operation happens once",
                () => observedEffects <= 1);

            observedEffects++;

            await Task.Delay(
                TimeSpan.FromMilliseconds(10),
                context.TimeProvider,
                context.CancellationToken);
        },
        TestContext.CancellationToken);

    Assert.AreEqual(
        TimeSpan.FromMilliseconds(10),
        result.VirtualElapsed);
}`
  },
  reqnroll: {
    title: "Reqnroll · scenario integration",
    noteTitle: "Use deterministic context inside executable specifications.",
    noteBody: "Causalia.Reqnroll provides scenario-scoped simulation plumbing so behavior-level scenarios can exercise deterministic timing and failure semantics.",
    link: "https://github.com/pjotrcasteel/Causalia/blob/main/SCENARIOS.md",
    code: `dotnet add package Causalia --version 2.1.1
dotnet add package Causalia.Reqnroll --version 2.1.1

# Keep your feature focused on behavior.
# Use the scenario-scoped Causalia provider
# from bindings to drive virtual time,
# scheduling and controlled failure paths.`
  }
};

const observers = new WeakMap();
document.querySelectorAll(".reveal").forEach((element) => {
  const observer = new IntersectionObserver((entries, instance) => {
    entries.forEach((entry) => {
      if (!entry.isIntersecting) return;
      entry.target.classList.add("visible");
      instance.unobserve(entry.target);
    });
  }, { threshold: 0.1 });
  observers.set(element, observer);
  observer.observe(element);
});

async function copyText(value, button, idleText = "Copy") {
  try {
    await navigator.clipboard.writeText(value);
    const target = button.querySelector(".copy-label") || button;
    target.textContent = "Copied";
    window.setTimeout(() => { target.textContent = idleText; }, 1300);
  } catch {
    const target = button.querySelector(".copy-label") || button;
    target.textContent = "Select";
  }
}

document.querySelectorAll("[data-copy]").forEach((button) => {
  button.addEventListener("click", () => copyText(button.dataset.copy, button));
});

function activateButtons(selector, current) {
  document.querySelectorAll(selector).forEach((button) => {
    const isActive = button === current;
    button.classList.toggle("active", isActive);
    button.setAttribute("aria-selected", String(isActive));
  });
}

document.querySelectorAll("[data-hero-scenario]").forEach((button) => {
  button.addEventListener("click", () => {
    activateButtons("[data-hero-scenario]", button);
    const scenario = scenarios[button.dataset.heroScenario];
    ["hero-step-1", "hero-step-2", "hero-step-3", "hero-step-4"].forEach((id, index) => {
      document.getElementById(id).textContent = scenario.hero[index];
    });
    document.getElementById("hero-step-3-note").textContent = scenario.heroNote;
    document.getElementById("hero-schedules").textContent = scenario.schedules;
  });
});

let selectedScenario = "ack";
let revealedSteps = 0;
let runTimer = null;

function renderLabEvents(scenarioKey) {
  const scenario = scenarios[scenarioKey];
  const host = document.getElementById("lab-events");
  host.innerHTML = scenario.events.map((event, index) => `
    <div class="lab-event ${index === scenario.events.length - 1 ? "failure" : ""}" data-lab-step="${index + 1}">
      <span class="number">${String(index + 1).padStart(2, "0")}</span>
      <i></i>
      <div><strong>${event[0]}</strong><small>${event[1]}</small></div>
    </div>`).join("");
  updateScrubber(0);
}

function updateScrubber(step) {
  revealedSteps = Number(step);
  document.getElementById("trace-scrubber").value = String(revealedSteps);
  document.getElementById("scrubber-value").textContent = `step ${revealedSteps} / 4`;
  document.querySelectorAll("[data-lab-step]").forEach((row) => {
    row.classList.toggle("visible", Number(row.dataset.labStep) <= revealedSteps);
  });
}

function selectScenario(key, button) {
  selectedScenario = key;
  window.clearInterval(runTimer);
  activateButtons("[data-scenario]", button);
  const scenario = scenarios[key];
  document.getElementById("lab-invariant").textContent = scenario.invariant;
  document.getElementById("lab-description").textContent = scenario.description;
  document.getElementById("lab-result-title").textContent = "Ready to explore";
  const state = document.getElementById("lab-state");
  state.textContent = "idle";
  state.className = "state-pill idle";
  document.getElementById("lab-token").textContent = "—";
  renderLabEvents(key);
}

document.querySelectorAll("[data-scenario]").forEach((button) => {
  button.addEventListener("click", () => selectScenario(button.dataset.scenario, button));
});

document.getElementById("run-lab").addEventListener("click", () => {
  window.clearInterval(runTimer);
  const scenario = scenarios[selectedScenario];
  const state = document.getElementById("lab-state");
  state.textContent = "running";
  state.className = "state-pill running";
  document.getElementById("lab-result-title").textContent = `Exploring ${scenario.schedules} schedules…`;
  document.getElementById("lab-token").textContent = "—";
  updateScrubber(0);

  let step = 0;
  runTimer = window.setInterval(() => {
    step += 1;
    updateScrubber(step);
    if (step >= 4) {
      window.clearInterval(runTimer);
      state.textContent = "failure found";
      state.className = "state-pill failed";
      document.getElementById("lab-result-title").textContent = "Invariant violated on a replayable path";
      document.getElementById("lab-token").textContent = scenario.token;
    }
  }, 420);
});

document.getElementById("trace-scrubber").addEventListener("input", (event) => {
  window.clearInterval(runTimer);
  updateScrubber(event.target.value);
});

document.getElementById("copy-token").addEventListener("click", (event) => {
  const value = document.getElementById("lab-token").textContent;
  if (value !== "—") copyText(value, event.currentTarget);
});

document.querySelectorAll("[data-fit]").forEach((button) => {
  button.addEventListener("click", () => {
    activateButtons("[data-fit]", button);
    const data = fitContent[button.dataset.fit];
    document.getElementById("fit-kicker").textContent = data.kicker;
    document.getElementById("fit-title").textContent = data.title;
    document.getElementById("fit-body").textContent = data.body;
    document.getElementById("fit-speed").style.width = data.speed + "%";
    document.getElementById("fit-realism").style.width = data.realism + "%";
    document.getElementById("fit-control").style.width = data.control + "%";
    document.getElementById("fit-replay").style.width = data.replay + "%";
  });
});

function renderCode(key) {
  const sample = codeSamples[key];
  document.getElementById("code-title").textContent = sample.title;
  document.getElementById("code-example").textContent = sample.code;
  document.getElementById("code-note-title").textContent = sample.noteTitle;
  document.getElementById("code-note-body").textContent = sample.noteBody;
  document.getElementById("code-note-link").href = sample.link;
}

document.querySelectorAll("[data-code]").forEach((button) => {
  button.addEventListener("click", () => {
    activateButtons("[data-code]", button);
    renderCode(button.dataset.code);
  });
});

document.getElementById("copy-code").addEventListener("click", (event) => {
  copyText(document.getElementById("code-example").textContent, event.currentTarget);
});

function renderInstall() {
  const selected = ["Causalia", ...Array.from(document.querySelectorAll(".package-choice input:checked:not(:disabled)")).map((input) => input.value)];
  const command = selected.map((name) => `dotnet add package ${name} --version 2.1.1`).join("\n");
  document.getElementById("install-output").textContent = command;
  document.querySelectorAll(".package-choice").forEach((label) => {
    const input = label.querySelector("input");
    label.classList.toggle("selected", input.checked);
  });
}

document.querySelectorAll(".package-choice input:not(:disabled)").forEach((input) => {
  input.addEventListener("change", renderInstall);
});

document.getElementById("copy-install").addEventListener("click", (event) => {
  copyText(document.getElementById("install-output").textContent, event.currentTarget, "Copy all");
});

renderLabEvents(selectedScenario);
renderCode("inspect");
renderInstall();