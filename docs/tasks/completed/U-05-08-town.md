# U-05-08-town: Town walkthrough and conversations

**Role:** Player & UI agent (6.9)
**Branch:** task/U-00-unity-foundation
**Depends on:** existing Apple slice

## Goal
Make the approved Mac village slice walkable and enable truthful NPC conversations.

## You may edit
- Assets/_Game/Player/ and Assets/_Game/UI/

## You must not edit
- Files outside owned folders, vendor assets or simulation package. Report shared changes needed.

## Requirements
Implement walkable third-person player TownWalker (namespace LivingWorld.Game.Player, public Bind(Camera camera)); CharacterController collision, keyboard WASD/arrows, touch-friendly visible directional controls or joystick, camera follow and optional orbit/zoom using approved Input System/Cinemachine if possible. Ground tap walking optional if conflicts with shop/NPC; avoid UI click-through. Expose testable move input/movement route for meaningful tests. Provide public ShopHud.ShowNpc(string npcId), hide shop while dialogue active, name/occupation + read-only fact-based line via WorldRunner.TalkToNpc(id, ConversationTopic) (bridge agent adding). Conversation buttons Greeting/Smalltalk/AboutPlayer/News/Family/Farewell; unknown npc/readiness errors graceful, close restores walking, movement blocked while modal/dialogue open. Provide ShopHud.IsModalOpen property (world/player uses). Keep Pause and shop working and recoverable errors. Remove obsolete missing-movement text, state NPC schedules/navigation remain preview. Safe areas>=44 touch targets, no overlap compact landscape. Read PRODUCT.md/DESIGN.md and impeccable skill relevant guidance, extend existing style. New Player asmdef refs Bridge/UI/InputSystem (avoid cycles UI must not reference Player; Player locates HUD). Coordinate directly World agent for camera/spawn contract. Add meaningful presenter dialogue tests if practical; no Unity runs or commits.

## Done when
- Requirements implemented and behavior tests authored
- Existing shop contracts preserved
- No invented world facts or unapproved design changes
- Report changes and limitations to orchestrator

## Validation record
Implemented within owned folders. Orchestrator ran 875 simulation, 28 EditMode and 3 PlayMode passing tests and inspected responsive captures. New native Mac pointer walkthrough remains pending a machine unlock. Full-phase limitations are recorded in docs/UNITY_VALIDATION.md and ROADMAP.md.
