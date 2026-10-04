# U-01-town: Town walkthrough and conversations

**Role:** Bridge agent (6.7)
**Branch:** task/U-00-unity-foundation
**Depends on:** existing Apple slice

## Goal
Make the approved Mac village slice walkable and enable truthful NPC conversations.

## You may edit
- Assets/_Game/Bridge/

## You must not edit
- Files outside owned folders, vendor assets or simulation package. Report shared changes needed.

## Requirements
Extend AppleSlice/WorldRunner for content-backed NPC identities and read-only conversations using FactSheetBuilder + DialogueSession + TemplatePhrasingEngine. Register NPC definitions/state using existing approved loader APIs. Add NpcDisplay.ModelPath and Occupation; preserve Id/Name/LocationId/Activity/Dialogue contracts. WorldRunner public string TalkToNpc(string npcId, ConversationTopic topic), enum ConversationTopic { Greeting, Smalltalk, AboutPlayer, News, Family, Farewell } in Bridge. Reject unknown IDs gracefully/clearly. No UI/simulation state mutation from dialogue. Keep current shop tests passing; test dialogue identity, ignorance before theft, talk does not change world, invalid ID. Do not pretend full village systems wired. Read-only snapshot locations may be workplace preview for now, label this honestly. No new packages. Report limits and run no Unity.

## Done when
- Requirements implemented and behavior tests authored
- Existing shop contracts preserved
- No invented world facts or unapproved design changes
- Report changes and limitations to orchestrator

## Validation record
Implemented within owned folders. Orchestrator ran 875 simulation, 28 EditMode and 3 PlayMode passing tests and inspected responsive captures. New native Mac pointer walkthrough remains pending a machine unlock. Full-phase limitations are recorded in docs/UNITY_VALIDATION.md and ROADMAP.md.
