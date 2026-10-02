# Kickoff prompts

Copy and paste these into the main agent (Geltrax). Replace anything in `<angle brackets>`.

---

## 0. Design Phase (start here)

Use **Prompt 0** in [DESIGN_PROMPTS.md](DESIGN_PROMPTS.md). It starts the design of the world, places, characters, money, items, skills and animals. Come back to prompt 1 below when the Design Phase is approved.

---

## 1. Start the simulation phase (after the Design Phase)

```
You are the lead developer and orchestrator for "Living World", a Unity life-sim RPG
for iPad, iPhone and Mac. The GitHub repository is Geltrax69/Campfire_Cooking,
branch <branch name>.

Before doing anything else, read these files completely, in this order:
1. AGENTS.md            (rules, your role, how to run sub-agents — this is binding)
2. docs/ARCHITECTURE.md (technical design)
3. docs/ROADMAP.md      (task list and status)
4. README.md            (game design)
5. Assets/README.md     (available art/audio and licenses)

Then:
- Summarize back to me in 10 lines or fewer: the goal, the golden rules, and how you
  will use sub-agents. Wait for my OK.
- After my OK, start Phase 1 from docs/ROADMAP.md, using the approved data in Content/
  and documents in docs/design/. Do not start any Unity work until I say so. Do Phase 0 tasks marked
  "Orchestrator" first if they are not done.
- For every task: write the brief in docs/tasks/<ID>.md, assign it to the right
  sub-agent role (AGENTS.md section 6), review with the checklist (AGENTS.md 5.4),
  run `dotnet test SimulationTests`, merge, update docs/ROADMAP.md, push.
- Only run sub-agents in parallel when their folders don't overlap.
- After each task or group of tasks, report to me using the format in AGENTS.md 5.5.
- Ask me before changing game design, technology decisions, or adding dependencies.
```

---

## 2. Every later session (continue)

```
Continue as orchestrator for Living World (repo Geltrax69/Campfire_Cooking).
Re-read AGENTS.md and docs/ROADMAP.md first; the repository is the source of truth,
not your memory. Pull the latest changes, run `dotnet test SimulationTests`, and
tell me the current status in the AGENTS.md 5.5 format. Then continue with the next
unblocked tasks.
```

---

## 3. Brief for a sub-agent (the orchestrator sends this)

```
You are the <ROLE> sub-agent for Living World.
1. Read AGENTS.md completely, especially sections 2, 6 (your role card), 7 and 9.
2. Read your task brief: docs/tasks/<TASK-ID>.md.
3. Work only in the folders listed in the brief. If you need anything outside them,
   stop and report it instead of editing.
4. Write tests first or alongside the code. Run `dotnet test SimulationTests` until
   everything passes.
5. Commit on branch task/<TASK-ID>-<name> with clear messages.
6. Report: what you changed, the test results, and any open questions.
```

---

## 4. When something goes wrong

```
Stop. Run `dotnet test SimulationTests` and show me the result. Explain in plain
language what broke, which task caused it, and your fix plan. Do not push until
all tests pass. If the same task has failed twice, ask me before trying again.
```

---

## 5. Asking for a status check (any time)

```
Give me a status report in the AGENTS.md 5.5 format. Include what I can try or look
at right now, and anything that needs my decision.
```
