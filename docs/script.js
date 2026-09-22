const scenarios={
ack:{invariant:"The order is applied at most once",description:"The durable write succeeds, its acknowledgement is lost, and retry must not repeat the business effect.",schedules:"128",token:"CAUSALIA:ACK-7F2A-91C",hero:["Order received","Storage commit succeeds","Acknowledgement disappears","Retry duplicates the effect"],heroNote:"injected ambiguity",events:[["Order handler starts","attempt 1 enters the controlled boundary"],["Storage commit succeeds","durable state is now visible"],["Commit acknowledgement is lost","caller observes an ambiguous failure"],["Retry repeats business effect","invariant violated: observed count = 2"]]},
duplicate:{invariant:"A message changes business state only once",description:"The broker redelivers the same logical message. The handler must recognize the duplicate without losing legitimate work.",schedules:"64",token:"CAUSALIA:DUP-22B4-E10",hero:["Message delivered","Handler completes","Broker redelivers","Duplicate effect detected"],heroNote:"at-least-once delivery",events:[["Message delivery #1","handler begins processing"],["Business effect committed","first delivery completes"],["Same message is redelivered","broker retries after missing acknowledgement"],["Second effect attempted","invariant violated: duplicate business change"]]},
race:{invariant:"Only one writer wins the state transition",description:"Two workers load the same version and race to save competing updates. The loser must not silently overwrite the winner.",schedules:"312",token:"CAUSALIA:RACE-4D18-B72",hero:["Two workers load v7","Worker A saves v8","Worker B still holds v7","Lost update exposed"],heroNote:"competing schedule",events:[["Workers A and B load version 7","both observe the same starting state"],["Worker A saves version 8","first compare-and-swap wins"],["Worker B resumes with version 7","stale write reaches persistence"],["Worker B overwrites state","invariant violated: lost update"]]},
crash:{invariant:"Recovery resumes without repeating completed work",description:"The process crashes after durable progress but before the workflow checkpoint. Restart must continue from evidence, not from hope.",schedules:"96",token:"CAUSALIA:CRASH-8A04-C31",hero:["Operation starts","External effect completes","Process crashes","Restart repeats effect"],heroNote:"lifecycle fault",events:[["Operation begins","workflow enters a controlled lifecycle"],["External effect completes","downstream side effect is durable"],["Process crashes before checkpoint","in-memory progress disappears"],["Restart repeats external effect","invariant violated after recovery"]]}
};

const audienceContent={
developer:{kicker:"FOR DEVELOPERS",title:"Turn “it only happens in production” into a test you can run again.",body:"Start from a real symptom, model one boundary, write one invariant and let deterministic exploration find the ordering that breaks it.",points:["Replay rare timing failures exactly","Advance time without waiting in real time","Keep the reproduction as a regression test"]},
agent:{kicker:"FOR AI CODING AGENTS",title:"Give the agent a stable failure before asking it to fix the code.",body:"Structured inspection output and exact replay make the agent loop less dependent on lucky timing. The agent can validate the same execution before and after its patch.",points:["Machine-readable inspect output","Replay the exact failure after an edit","Broaden exploration only after the reproduction passes"]},
lead:{kicker:"FOR ENGINEERING LEADS",title:"Reduce the engineering drag around failures that are expensive to reproduce.",body:"Adopt Causalia where retry, recovery or concurrency creates measurable debugging friction. Keep your real infrastructure tests and evaluate the new loop on one real failure class.",points:["Shorter reproduction loop","Executable regression evidence","More defensible AI-assisted changes"]}
};

const fitContent={
unit:{kicker:"UNIT TESTS",title:"Make local logic fast, focused and easy to reason about.",body:"Best for pure behavior and small components. Usually one execution path at a time, with dependencies replaced or isolated.",speed:100,realism:28,control:78,replay:98},
causalia:{kicker:"DETERMINISTIC SIMULATION",title:"Explore difficult orderings cheaply and replay them exactly.",body:"Model the semantics that matter, control time and scheduling, inject ambiguity, then assert domain invariants across many executions.",speed:88,realism:64,control:100,replay:100},
integration:{kicker:"INTEGRATION TESTS",title:"Verify your code against real infrastructure behavior.",body:"Best for provider compatibility, serialization, network contracts and actual databases or brokers. More realistic, but less control over rare timing.",speed:55,realism:88,control:38,replay:58},
e2e:{kicker:"END-TO-END TESTS",title:"Prove the assembled system works across real boundaries.",body:"Highest environment realism for critical journeys. Expensive to explore many schedules and usually hardest to reproduce timing-sensitive failures.",speed:24,realism:100,control:18,replay:34}
};

const codeSamples={
inspect:{title:"terminal · adoption CLI",noteTitle:"Start without changing production code.",noteBody:"The CLI ranks likely simulation boundaries and points out useful seams such as TimeProvider and propagated cancellation.",link:"https://github.com/pjotrcasteel/Causalia#five-minute-start",code:`# Install the adoption CLI
dotnet tool install --global Causalia.Tool --version 2.1.1

# Inspect without changing the solution
dotnet causalia inspect MyService.slnx

# Machine-readable output for automation / agents
dotnet causalia inspect MyService.slnx --format json

# Generate the first deterministic test project
dotnet causalia init MyService.slnx`},
test:{title:"C# · first simulation",noteTitle:"Keep the invariant close to the behavior.",noteBody:"Use normal .NET seams where possible. Causalia belongs at the simulation boundary rather than leaking through your application.",link:"https://github.com/pjotrcasteel/Causalia#first-simulation",code:`[TestMethod]
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
}`},
reqnroll:{title:"Reqnroll · scenario integration",noteTitle:"Use deterministic context inside executable specifications.",noteBody:"Causalia.Reqnroll provides scenario-scoped simulation plumbing so behavior-level scenarios can exercise deterministic timing and failure semantics.",link:"https://github.com/pjotrcasteel/Causalia/blob/main/SCENARIOS.md",code:`dotnet add package Causalia --version 2.1.1
dotnet add package Causalia.Reqnroll --version 2.1.1

# Keep the feature focused on behavior.
# Use the scenario-scoped provider from bindings
# to drive virtual time, scheduling and
# controlled failure paths.`}
};

const agentSteps={
inspect:{kicker:"STEP 01 · INSPECT",title:"Start with the solution, not with a guessed fix.",body:"Run the Causalia CLI in JSON mode so an agent can work from structured findings: likely boundaries, deterministic seams and risky APIs.",code:"dotnet causalia inspect MyService.slnx --format json",guardrail:"Inspection is a lead, not proof. The agent should understand the business invariant before editing production code."},
model:{kicker:"STEP 02 · MODEL",title:"Shrink the problem to one boundary and one guarantee.",body:"Pick a real symptom — retry, redelivery, restart, race — and express the invariant the system must preserve.",code:'context.Invariant("operation happens once", () => observedEffects <= 1);',guardrail:"Avoid simulating the entire application. A narrow boundary produces clearer evidence and lower adoption cost."},
explore:{kicker:"STEP 03 · EXPLORE",title:"Let deterministic scheduling find the inconvenient execution.",body:"Drive virtual time and modeled failures under a controlled scheduler instead of hoping the test runner hits the same race twice.",code:"var result = await Simulation.RunAsync(..., cancellationToken);",guardrail:"Modeled failure semantics must match the risk you are investigating. Exploration is only as meaningful as the boundary model."},
patch:{kicker:"STEP 04 · PATCH",title:"Change production behavior from evidence, not from speculation.",body:"Use the reduced failure sequence to make the smallest code change that restores the invariant.",code:"// apply the smallest evidence-backed fix",guardrail:"Do not optimize for a single green run. Preserve the failure reproduction before making the change."},
replay:{kicker:"STEP 05 · REPLAY",title:"Prove the same failure path no longer violates the invariant.",body:"Replay the exact execution first. Then run broader deterministic exploration and the repository's ordinary tests.",code:"await Simulation.ReplayAsync(..., replayToken, cancellationToken);",guardrail:"A passing replay proves that reproduction is fixed. It does not replace wider exploration or real infrastructure verification."}
};

function byId(id){return document.getElementById(id)}
function activateButtons(selector,current){document.querySelectorAll(selector).forEach(button=>{const active=button===current;button.classList.toggle("active",active);button.setAttribute("aria-selected",String(active))})}
async function copyText(value,button,idleText){try{await navigator.clipboard.writeText(value);const target=button.querySelector(".copy-label")||button;const fallback=idleText||target.textContent;target.textContent="Copied";window.setTimeout(()=>{target.textContent=fallback},1300)}catch{const target=button.querySelector(".copy-label")||button;target.textContent="Select"}}

document.querySelectorAll(".reveal").forEach(element=>{const observer=new IntersectionObserver((entries,instance)=>{entries.forEach(entry=>{if(!entry.isIntersecting)return;entry.target.classList.add("visible");instance.unobserve(entry.target)})},{threshold:.1});observer.observe(element)});
document.querySelectorAll("[data-copy]").forEach(button=>button.addEventListener("click",()=>copyText(button.dataset.copy,button,"Copy")));
document.querySelectorAll("[data-copy-target]").forEach(button=>button.addEventListener("click",()=>{const target=byId(button.dataset.copyTarget);if(target)copyText(target.textContent,button,"Copy prompt")}));

document.querySelectorAll("[data-hero-scenario]").forEach(button=>button.addEventListener("click",()=>{activateButtons("[data-hero-scenario]",button);const scenario=scenarios[button.dataset.heroScenario];["hero-step-1","hero-step-2","hero-step-3","hero-step-4"].forEach((id,index)=>{const el=byId(id);if(el)el.textContent=scenario.hero[index]});if(byId("hero-step-3-note"))byId("hero-step-3-note").textContent=scenario.heroNote;if(byId("hero-schedules"))byId("hero-schedules").textContent=scenario.schedules}));

document.querySelectorAll("[data-audience]").forEach(button=>button.addEventListener("click",()=>{activateButtons("[data-audience]",button);const data=audienceContent[button.dataset.audience];byId("audience-kicker").textContent=data.kicker;byId("audience-title").textContent=data.title;byId("audience-body").textContent=data.body;byId("audience-points").innerHTML=data.points.map(point=>`<li>${point}</li>`).join("")}));

let selectedScenario="ack";let runTimer=null;
function updateScrubber(step){if(!byId("trace-scrubber"))return;const value=Number(step);byId("trace-scrubber").value=String(value);byId("scrubber-value").textContent=`step ${value} / 4`;document.querySelectorAll("[data-lab-step]").forEach(row=>row.classList.toggle("visible",Number(row.dataset.labStep)<=value))}
function renderLabEvents(key){if(!byId("lab-events"))return;const scenario=scenarios[key];byId("lab-events").innerHTML=scenario.events.map((event,index)=>`<div class="lab-event ${index===scenario.events.length-1?"failure":""}" data-lab-step="${index+1}"><span class="number">${String(index+1).padStart(2,"0")}</span><i></i><div><strong>${event[0]}</strong><small>${event[1]}</small></div></div>`).join("");updateScrubber(0)}
document.querySelectorAll("[data-scenario]").forEach(button=>button.addEventListener("click",()=>{selectedScenario=button.dataset.scenario;window.clearInterval(runTimer);activateButtons("[data-scenario]",button);const scenario=scenarios[selectedScenario];byId("lab-invariant").textContent=scenario.invariant;byId("lab-description").textContent=scenario.description;byId("lab-result-title").textContent="Ready to explore";byId("lab-state").textContent="idle";byId("lab-state").className="state-pill idle";byId("lab-token").textContent="—";renderLabEvents(selectedScenario)}));
if(byId("run-lab"))byId("run-lab").addEventListener("click",()=>{window.clearInterval(runTimer);const scenario=scenarios[selectedScenario];const state=byId("lab-state");state.textContent="running";state.className="state-pill running";byId("lab-result-title").textContent=`Exploring ${scenario.schedules} schedules…`;byId("lab-token").textContent="—";updateScrubber(0);let step=0;runTimer=window.setInterval(()=>{step+=1;updateScrubber(step);if(step>=4){window.clearInterval(runTimer);state.textContent="failure found";state.className="state-pill failed";byId("lab-result-title").textContent="Invariant violated on a replayable path";byId("lab-token").textContent=scenario.token}},420)});
if(byId("trace-scrubber"))byId("trace-scrubber").addEventListener("input",event=>{window.clearInterval(runTimer);updateScrubber(event.target.value)});
if(byId("copy-token"))byId("copy-token").addEventListener("click",event=>{const value=byId("lab-token").textContent;if(value!=="—")copyText(value,event.currentTarget,"Copy")});

document.querySelectorAll("[data-agent-step]").forEach(button=>button.addEventListener("click",()=>{document.querySelectorAll("[data-agent-step]").forEach(item=>item.classList.toggle("active",item===button));const data=agentSteps[button.dataset.agentStep];byId("agent-detail-kicker").textContent=data.kicker;byId("agent-detail-title").textContent=data.title;byId("agent-detail-body").textContent=data.body;byId("agent-detail-code").textContent=data.code;byId("agent-detail-guardrail").textContent=data.guardrail}));
if(byId("copy-agent-prompt"))byId("copy-agent-prompt").addEventListener("click",event=>copyText(byId("agent-prompt").textContent,event.currentTarget,"Copy agent prompt"));

document.querySelectorAll("[data-fit]").forEach(button=>button.addEventListener("click",()=>{activateButtons("[data-fit]",button);const data=fitContent[button.dataset.fit];byId("fit-kicker").textContent=data.kicker;byId("fit-title").textContent=data.title;byId("fit-body").textContent=data.body;byId("fit-speed").style.width=data.speed+"%";byId("fit-realism").style.width=data.realism+"%";byId("fit-control").style.width=data.control+"%";byId("fit-replay").style.width=data.replay+"%"}));

function renderCode(key){if(!byId("code-example"))return;const sample=codeSamples[key];byId("code-title").textContent=sample.title;byId("code-example").textContent=sample.code;byId("code-note-title").textContent=sample.noteTitle;byId("code-note-body").textContent=sample.noteBody;byId("code-note-link").href=sample.link}
document.querySelectorAll("[data-code]").forEach(button=>button.addEventListener("click",()=>{activateButtons("[data-code]",button);renderCode(button.dataset.code)}));
if(byId("copy-code"))byId("copy-code").addEventListener("click",event=>copyText(byId("code-example").textContent,event.currentTarget,"Copy"));

function renderInstall(){if(!byId("install-output"))return;const selected=["Causalia",...Array.from(document.querySelectorAll(".package-choice input:checked:not(:disabled)")).map(input=>input.value)];byId("install-output").textContent=selected.map(name=>`dotnet add package ${name} --version 2.1.1`).join("\n");document.querySelectorAll(".package-choice").forEach(label=>{const input=label.querySelector("input");label.classList.toggle("selected",input.checked)})}
document.querySelectorAll(".package-choice input:not(:disabled)").forEach(input=>input.addEventListener("change",renderInstall));
if(byId("copy-install"))byId("copy-install").addEventListener("click",event=>copyText(byId("install-output").textContent,event.currentTarget,"Copy all"));

function updateImpact(){if(!byId("incidents"))return;const incidents=Number(byId("incidents").value),engineers=Number(byId("engineers").value),hours=Number(byId("hours").value),cost=Number(byId("cost").value);const totalHours=incidents*engineers*hours,monthly=totalHours*cost,yearly=monthly*12;byId("incidents-value").textContent=incidents;byId("engineers-value").textContent=engineers;byId("hours-value").textContent=hours;byId("cost-value").textContent="€"+cost.toLocaleString("en-US");byId("impact-hours").textContent=totalHours.toLocaleString("en-US")+" h / month";byId("impact-monthly").textContent="€"+monthly.toLocaleString("en-US")+" / month";byId("impact-yearly").textContent="€"+yearly.toLocaleString("en-US")+" / year"}
["incidents","engineers","hours","cost"].forEach(id=>{const el=byId(id);if(el)el.addEventListener("input",updateImpact)});

renderLabEvents(selectedScenario);renderCode("inspect");renderInstall();updateImpact();