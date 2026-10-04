# Open Questions (for human review)

Decisions the orchestrator made without the human that need review. The user asked to continue through all phases without stopping; these are recorded here for end-of-project review.

## Phase 3 (2026-10-04)

### Tavern hearth
**Decision:** Bessa cooks campfire stew at `loc_tavern`. SKILLS.md §1 allows campfire recipes "at the river campfire or any hearth" — the recipe's `loc_river_alder` names the public campfire.
**Question:** Is the tavern hearth an acceptable cooking location for campfire recipes, or should the location gate be strict?

### Teaching rate
**Decision:** Player taught by Bessa → 2 practice points per successful cooking (teaching ×2).
**Question:** Confirm the teaching bonus applies to cooking practice as implemented.

### Fish supply premise
**Decision:** The 7-day acceptance scenario assumes a daily morning fish delivery (Jory's round). A week's fish rots by day 5 under P2-09 spoilage, so stockpiling would cheat the world's own rules.
**Question:** Is the daily delivery premise acceptable, or should the scenario use preserved/smoked fish?

### RecipeExecution actor type
**Decision:** `RecipeExecution.Cook` takes `NpcId` (unused); the player has no `NpcId`, so the acceptance scenario replays Cook's exact steps for the player.
**Question:** Should a future phase add an `ActorId` overload to `Cook`?

### Meal → health not implemented
**Decision:** The health system doesn't exist yet, so meal quality → health is future work (documented in code).
**Question:** When the health system is built, should high-quality meals (Q≥70) grant a small recovery bonus?

### Permission simplification
**Decision:** Recipe permission = trust ≥ 40 with the permitting NPC (or actor IS the permitting NPC).
**Question:** Is this acceptable as a placeholder until real persuasion/rental systems exist?

## Content fixes (2026-10-04, human-approved)

These were approved by the human before implementation:
1. ✅ Added `item_cloth_local` to general store `sells` in `locations.json` (matches economy.json and design).
2. ✅ Flipped 7 cooked-food hunger values to negative (ITEMS.md convention: eating lowers hunger). Added `NoFoodItemHasPositiveHungerEffect` test.
