# U-02 slice: Unity preview scene

**Role:** World agent (6.8)
**Branch:** task/U-00-unity-foundation
**Depends on:** U-01 and U-07 slice API contracts

## Goal
Generate a reviewable Apple shop village slice scene through an editor menu, showing approved Kenney assets and connecting to bridge/UI.

## You may edit
- Assets/_Game/World/ (Editor and Tests included)

## Requirements
1. Read AGENTS.md and required docs plus PRODUCT.md and DESIGN.md.
2. Create editor menu Living World/Create Apple Shop Slice, generating scene in Assets/_Game/World/Scenes with WorldRunner and ShopHud components (coordinate names with agents). Camera, ground, shop, a few trees/props from licensed Kenney assets. Prefer FBX/OBJ assets available; glTFast package is installed by orchestrator if using GLB. Never edit vendor folders. No hand-edit scenes or meta GUIDs.
3. Minimal prototype world view honest about scope. Do not claim NavMesh/NPC animation done. A clickable shop opens UI. Coordinate UI public ShowShop method; no simulation references in World asmdef. Bridge allowed only setup and public snapshots.
4. Establish URP asset through editor generation and assign GraphicsSettings/QualitySettings; URP 17.6 package. Player landscape settings, active input system package; no invented weather.
5. Generate scene in batch via public static LivingWorld.Game.World.Editor.SliceSetup.Create method so orchestrator can execute it.
6. Provide a batch method that captures a camera screenshot and/or full UI runtime screenshot during PlayMode tests if feasible; orchestrator handles execution. Include meaningful setup tests.
7. Do not run Unity concurrently or commit. Report all limits.

## Done when
- [ ] Scene generator implemented
- [ ] Click shop shows UI
- [ ] Setup test authored and execution command reported
