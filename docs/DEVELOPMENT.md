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

## Current checkpoint (2026-10-04)

Simulation Phases 1–8 task evidence is complete. The full Debug suite passed
875/875 tests on this Mac. The owner authorized Unity and selected a first
Apple shop and village slice. Known simulation gaps remain in OPEN_QUESTIONS.md.

## Unity prototype

Use Unity 6000.6.4f1 (installed under /Applications/Unity/Hub/Editor/).
The versioned Packages manifest pins URP, Input System, glTFast, Cinemachine,
AI Navigation and Test Framework to this editor's package versions.

Open this repository as a Unity project. From the menu choose
**Living World → Create Apple Shop Slice** (regenerates the prototype scene),
then open `Assets/_Game/World/Scenes/AppleShopSlice.unity` and press Play.
Tap/click the green shop to open its panel. Buy one or take six apples; the
simulation executes the command at the next game minute. The prototype wait
button advances one hour. Pause/Resume controls the simulation clock.

This slice has scenario customers and witness tuning, preview lighting and a
static camera. General NPC navigation, player movement, radial interaction,
full dialogue, day/night, saving, and iPad builds are separate remaining work.
It is not the full playable village. Scene assets are licensed Kenney models.

Unity runs:

```sh
/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath . -executeMethod \
  LivingWorld.Game.World.Editor.SliceSetup.Create -quit -logFile /tmp/livingworld-scene.log
/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
  -testResults TestResults/unity-editmode.xml -logFile /tmp/livingworld-editmode.log
/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath . -runTests -testPlatform PlayMode \
  -testResults TestResults/unity-playmode.xml -logFile /tmp/livingworld-playmode.log
```

Mac build support and Xcode are installed; iOS Build Support is missing.
Unity Hub must add that module before U-09. Signing and the real iPad play-test
remain human checks. New large files need LFS approval; no asset history
migration was performed. Unity-generated .meta files are versioned to keep
references stable; original vendor art files were not changed.

Final Unity verification: 16 EditMode tests and one PlayMode interaction test
passed. Captures are in ignored TestResults/. Mac development build is at
Builds/LivingWorld.app (generated, not versioned). See UNITY_VALIDATION.md.
