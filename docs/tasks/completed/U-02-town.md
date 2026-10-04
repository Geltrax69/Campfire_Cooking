# U-02-town: Town walkthrough and conversations

**Role:** World agent (6.8)
**Branch:** task/U-00-unity-foundation
**Depends on:** existing Apple slice

## Goal
Make the approved Mac village slice walkable and enable truthful NPC conversations.

## You may edit
- Assets/_Game/World/

## You must not edit
- Files outside owned folders, vendor assets or simulation package. Report shared changes needed.

## Requirements
Expand SliceSetup.Create into a proper walkable village exterior using licensed Kenney buildings with paths, clear collision and bounds. Read approved Content/world/locations.json for names/positions, choose compact central-village subset, approved art substitutes Kenney only (no unknown-license loose models). Runtime NPC models from Content/npcs/npcs.json at corresponding preview workplaces via read-only bridge NpcDisplay.ModelPath (bridge agent adding), with name labels, collider and click interaction invoking ShopHud.ShowNpc(string npcId) (UI agent adding). Coordinate with UI agent: player TownWalker public Bind(Camera camera), spawn GameObject CharacterController + TownWalker under Player namespace; UI agent owns implementation. World references Player asmdef as required. Camera should follow walker (UI agent handles), generator binds. Existing shop interaction must not conflict with walking clicks or UI. Add day/night visuals from runner snapshot if safely scoped; no weather fiction. Runtime NPC positions explicitly workplace preview, no fabricated NPC AI. Meaningful PlayMode tests: walking changes position, blockers/bounds, NPC dialogue opens correct name, shop still buys, small landscape layout. Orchestrator runs Unity, do not run or commit. No edit scene YAML; regenerate through Create. Aim an inviting visible town, not empty clearing. Include signposts/landmark labels and available controls.

## Done when
- Requirements implemented and behavior tests authored
- Existing shop contracts preserved
- No invented world facts or unapproved design changes
- Report changes and limitations to orchestrator

## Validation record
Implemented within owned folders. Orchestrator ran 875 simulation, 28 EditMode and 3 PlayMode passing tests and inspected responsive captures. New native Mac pointer walkthrough remains pending a machine unlock. Full-phase limitations are recorded in docs/UNITY_VALIDATION.md and ROADMAP.md.
