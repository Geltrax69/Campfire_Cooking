# U-07 slice: Apple shop interface

**Role:** Player & UI agent (6.9)
**Branch:** task/U-00-unity-foundation
**Depends on:** U-01 public API contract (provided below)

## Goal
Build a reviewable touch-first shop interface reading only immutable bridge snapshots.

## You may edit
- Assets/_Game/UI/ (including Tests)

## You must not edit
- Anything outside that folder.

## Context and API
Read AGENTS.md, docs/ARCHITECTURE.md, docs/ROADMAP.md, README.md, Assets/README.md, PRODUCT.md and DESIGN.md. Human chose Apple shop and village slice. Approved visual style is Kenney low-poly, iPad landscape, immersive minimal HUD. Use Unity UI Toolkit with runtime UIDocument/panel settings created from code or Unity editor, no browser web substitute. Bridge agent builds LivingWorld.Game.Bridge.WorldRunner: Snapshot (Minute, ShopApples, ApplePriceCopper, PlayerApples, PlayerCopper, Npcs immutable list), QueueBuyApples(int), QueueStealApples(int), AdvanceMinutes(int), SetPaused(bool), event SnapshotChanged. Coordinate exact event signature with bridge agent.

## Requirements
1. Build a status strip with time/location and a dismissible contextual shop panel with stock, price, coins, inventory, Buy one and Take six commands. Buttons at least 44 points, safe area margins.
2. Commands only through bridge; no simulation references in UI asmdef. Disable unavailable actions and show errors. Show pause state and a clearly marked prototype wait action if using AdvanceMinutes.
3. Shop opens from scene click/tap via a public ShowShop method (world bootstrap will wire).
4. UI subscribes/unsubscribes cleanly, handles missing runner, reflects queued actions only after tick.
5. Test UI state and button action dispatch in Tests with Unity test assembly. No invented weather or NPC omniscient facts.
6. Keep this initial implementation small. No player movement, radial menu or full dialogue yet; report limits honestly.
7. Do not run Unity or commit; orchestrator does both. Unity generates meta files.

## Done when
- [ ] Interface implemented
- [ ] Meaningful tests authored
- [ ] APIs and limits reported
