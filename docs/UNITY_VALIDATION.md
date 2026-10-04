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
- Final Unity EditMode: **17/17 passed**, including JSON parsing/writing
  inside Unity and regression tests for clock boundaries and pause state.
- Runtime PlayMode: 2/2 passed; an actual Buy-button submit leaves stock
  unchanged until the tick, then moves one apple and charges three copper.
- Graphics captures: 1366×1024 and 844×390 actual Game View dimensions.
  A compact layout correction was required after first screenshot inspection.
- Local Mac development build: succeeded (Mono); iOS/IL2CPP not verified.

Final PlayMode confirmation: **2/2 passed** after the compact layout correction.
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

## Standalone Mac follow-up

Real app testing caught a gap in the initial validation: the original Mac build
used legacy-only input, so the Input System shop pointer was inactive. The
project now enables Both input backends. The scene/build setup checks that
Input System compilation is active and refuses a build needing an editor
restart. Runtime purchase tests alone were not proof of pointer interaction.

The rebuilt standalone app was operated using actual mouse clicks: pause,
click shop to open, queue Buy one while paused, Resume to process, Take six,
and close. Purchase changed stock20→19, copper15→12 and player apples0→1.
The theft changed stock19→13 and player apples1→7 without changing copper.
Closing the panel did not reopen it through the underlying scene collider.
No runtime exceptions were found in Player.log. Unity's text engine reports a
nonfatal ICU fallback for basic line breaking; non-Latin/emoji text is not
validated by this prototype.

Also fixed startup recovery: if HUD enables before the runner initializes,
the first valid snapshot now clears its stale “Village unavailable” message.
A regression test covers recovery and queuing while paused. Screenshots of
the real app are kept under ignored TestResults/mac-*.png.

Follow-up checks: 17/17 EditMode and 2/2 PlayMode passed; the extra PlayMode
check rejects legacy-only Active Input Handling.

## Town walkthrough follow-up

Added a compact central village exterior using uniformly scaled approved
location coordinates, licensed Kenney characters and generated buildings.
The player has collision-aware keyboard/touch directional controls, ground
click destinations, Cinemachine follow/orbit/zoom and camera obstruction
handling. Imported Mecanim idle/walk clips animate through playable graphs.
Named NPCs open read-only conversation panels with identity, smalltalk,
player knowledge, news, family, farewell and local apple-stall beliefs.
Sunlight and ambient lighting follow simulation minutes.

Validation: 877/877 simulation tests; 28/28 Unity EditMode tests; 3/3 PlayMode
integration tests. Tests cover queued purchasing, controller movement,
building/boundary blocking, dialogue modal movement blocking, actual topic
button submission, known identity/occupation, day/night intensity, and shop
plus dialogue captures at 1366×1024 and 844×390. Conversation tests prove
complete serialized state remains identical after talking and that Mira
does not identify an unseen thief, even after discovering missing stock.
All four final layout captures were inspected.

Visual review found and fixed a blocked starting camera, overlarge fountain
(height scaling had produced a 10.7-metre footprint), NPCs intersecting the
fountain, unreadable labels, noon overexposure, and player animation targeting
the wrong Unity animation system. All live UI documents guard world clicks,
including movement buttons, to prevent shop/NPC click-through.

The Mac development build succeeds and its process starts with the Input
System and Metal initialized; startup log has no runtime exceptions. Native
mouse walkthrough of this new town build is **pending**: Computer Use reports
the Mac locked and automatic unlock failed. The owner was asked to unlock.
The previous shop-only native click results above do not validate new town
pointer controls. Current captures are automated Unity Game View captures.

Remaining: NPCs stay at workplace preview positions with idle animation;
NavMesh/schedule movement, full village simulation assembly/rumor propagation,
authoritative player travel/save, joystick/pinch gestures, controller support,
radial menus, the farm/river exterior, and iPad device testing remain unfinished.
Other visible businesses are exterior landmarks; only the apple stall trades.
No weather or health facts were invented. This remains a prototype slice,
not a claim that the complete game works or that there are no remaining issues.

Final dialogue review corrected vowel-leading occupation articles (for example, “an apple-stall owner”). Two new regression cases first reproduced the mistake; the complete simulation suite then passed 877/877, and all three Unity PlayMode tests passed with the corrected phrasing visible in the captures.

## Stale-build follow-up — 2026-10-04 (late)

The previously launched Mac app predated the dialogue-article fix (bundle built
20:30, fix committed 21:34), so the running game still showed the old phrasing.
All suites were re-run from source on this machine: 877/877 simulation
(required `DOTNET_ROLL_FORWARD=LatestMajor` because the installed SDK is now
.NET 10 only; the net8.0 test host rolls forward), 28/28 EditMode, 3/3
PlayMode. The Mac development build was regenerated at 23:44 including the
corrected dialogue and relaunched; the fresh Player.log shows a normal startup
with zero exceptions.

The native mouse walkthrough remains pending for a tooling reason this time:
the ZCode Computer Use session had no permission broker wired up (no
`~/.zcode/cua-broker` socket), so screen control could not start at all, and
terminal `screencapture` lacks Screen Recording permission. The automated
Game View captures above are the current visual evidence; a human can click
through the rebuilt app directly.
