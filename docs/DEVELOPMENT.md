# Simulation development

Unity is not required for this stage. The simulation is pure C# 9; the NUnit
test project compiles the actual source in the local simulation package.

## Prerequisite

Install the .NET 8 SDK. The development Mac was verified with SDK **8.0.425**
on 2026-10-03. If installed with Microsoft's user-local installer, expose it in
your current terminal (no global shell configuration change is needed):

```sh
export PATH="$HOME/.dotnet:$PATH"
dotnet --version
```

## Checks from the repository root

```sh
dotnet test SimulationTests
dotnet test SimulationTests --configuration Release
git diff --check
```

An empty test suite is not a passing simulation check: look for a nonzero
**Passed** total and zero failed/skipped tests. GitHub Actions also runs the
suite on pushes and pull requests. Check CI before merging a task branch.

## Tracking

- Current task statuses: [ROADMAP.md](ROADMAP.md).
- Active briefs: `docs/tasks/`; verified briefs: `docs/tasks/completed/`.
- Technical decisions: [ARCHITECTURE.md](ARCHITECTURE.md), decision log.
- No Unity visuals or device builds until explicitly authorised.

The repository's current default branch is `claude/wizardly-clarke-tigogn`,
not `main`; task PRs target that branch. Preserve that choice unless the
owner requests a rename.

## Current foundation checkpoint (2026-10-03)

P1-01–P1-07, P1-09–P1-11, P1-17 and P1-19 are implemented: the deterministic
core now also has atomic shop purchases, approved NPC schedules, configurable
utility decisions, per-NPC memory decay, and a readable world-truth report.
Full Debug and Release suites each contain 216 passing tests. No Unity editor or
device build was run.

Try the readable command → departure → travel → arrival foundation report:

```sh
dotnet test SimulationTests --filter FullyQualifiedName=LivingWorld.Simulation.Tests.Tools.SimulationLogWriterTests.FoundationTravelDemoPrintsWorldTruthReport --logger "console;verbosity=detailed"
```

For world setup, explicitly register `CommandSystem` and `TravelSystem` before
the first tick. Initialise travel with `state.InitializeTravel(map)`, register
NPC locations through `state.Travel.RegisterNpc`, and submit immutable
`IWorldCommand` instances with `state.EnqueueCommand`. Perception, belief-aware
decisions, theft and rumors are still upcoming; no playable village or Apple
Test is claimed yet. Production utility weights and initial need values remain
a human balancing decision and are intentionally caller-supplied for now.
