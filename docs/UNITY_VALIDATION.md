# Unity slice validation — 2026-10-04

The owner authorized an Apple shop and village slice. This is the first real
Unity project, not completion of the Unity Phase.

## Implemented

- Unity 6000.6.4f1 project with URP 17.6 and the fixed package choices.
- Content-backed Apple Test bridge with queued simulation purchase/theft,
  immutable snapshots, one minute per second, pause and bounded catch-up.
- Generated licensed Kenney shop preview scene and clickable shop collider.
- Touch-sized UI Toolkit shop, coins/inventory, time/location, pause and wait.
- Build-time authoritative Content staging with ownership-safe cleanup.
- JSON runtime assemblies with upstream MIT licenses; no serializer rewrite.
- Roadmap checkpoint corrected and P3–P8 completed briefs archived.

## Validation

- Simulation: 875/875 passed, Debug, .NET 8.0.425.
- Final Unity EditMode: **16/16 passed**, including JSON parsing/writing
  inside Unity and regression tests for clock boundaries and pause state.
- Runtime PlayMode: 1/1 passed; an actual Buy-button submit leaves stock
  unchanged until the tick, then moves one apple and charges three copper.
- Graphics captures: 1366×1024 and 844×390 actual Game View dimensions.
  A compact layout correction was required after first screenshot inspection.
- Local Mac development build: succeeded (Mono); iOS/IL2CPP not verified.

Final PlayMode confirmation: **1/1 passed** after the compact layout correction.
Both captures were visually inspected; shop actions are visible on both sizes.

## Remaining / known limitations

The slice reuses fixed scenario buyer/witness configuration; general NPC
schedules, movement, NavMesh, rumors, save/load, radial interaction, template
conversation UI and day/night are not wired here. Decorative scene apples are
not inventory entities. Preview lighting is constant. No weather facts are
invented. Later work should assemble the full approved simulation without
promoting scenario inputs into general NPC AI.

Unity Hub, Unity 6, Mac Build Support and Xcode are installed. iOS Build Support
is missing; install that module via Hub before U-09. Device signing and real
iPad touch/performance checks remain with the owner.

Initial glTFast import reported native job errors for Quaternius wolf, horse
and stag outside this slice. Those animated imports must be repaired before
animal visuals are used. Original vendor assets were not edited. Unity's
first import generated thousands of .meta files; these must be versioned so
asset/scene references remain stable. No new large art assets or LFS migration.

The simulation limitations in OPEN_QUESTIONS.md remain open; starting Unity
was not approval to change animal pacing, weather, health or persistence rules.
