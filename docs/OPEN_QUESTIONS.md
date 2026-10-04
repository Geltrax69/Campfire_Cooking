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

## Phase 4 (2026-10-04)

### Wolf bond pacing
**Decision:** Mechanically, a wolf pup bonds in ~17 days of optimal interaction, but ANIMALS.md says "~60 days — two full seasons".
**Question:** Should the trust gains be tuned down to match the 60-day design, or is the faster mechanical pace acceptable?

### Egg coop inventories not persisted
**Decision:** Laid eggs go to caller-owned coop inventories (not world state), so they don't survive save/load. The Produced truth events do survive.
**Question:** Should coop inventories become world state, or should eggs route into an NPC/shop inventory?

### Bonded/BondBroken events not emitted
**Decision:** Added the event types but TamingSystem doesn't emit them yet (needs payload design: actor/location/visibility).
**Question:** What should the bond event payload contain?

### AnimalState.Owner type change
**Decision:** Changed from `NpcId?` to `ActorId?` so the player can bond animals.
**Question:** Ratify this change (P4-02's `SetOwner(NpcId?)` was updated to match).

### Core event type additions
**Decision:** Added `WorldEventType.Predation`, `AnimalBirth`, `AnimalCulled`, `CropDamage`, `Bonded`, `BondBroken` (one line each).
**Question:** Ratify these additions (P3-03 MealEaten precedent).

## Phase 5 (2026-10-04)

### Weather system
**Decision:** daysSinceSnow/daysSinceRain not tracked (no weather system). Wolf attack uses 10% daily chance in winter; fire 2% in summer; drought fires in late summer.
**Question:** Should a weather system be built, or are the seasonal proxies sufficient?

### Event effects minimal
**Decision:** Firing mainly marks the event active; consequences emerge from stat formulas. Direct effects (fire damaging buildings, merchant bringing goods) are future work.
**Question:** Which direct event effects are most important for the prototype?

### 270-day determinism split
**Decision:** Save/load determinism proven on 60-day run (byte-identical); 270-day run proves integration. Divergence at winter/spring boundary isolated to animal-system interaction.
**Question:** Should the animal-system divergence be investigated, or is the 60-day proof sufficient?

## Phase 6 (2026-10-04)

### Oakhollow name
**Decision:** Invented "Oakhollow" for the second neighboring hamlet (King's Rest is from WORLD.md).
**Question:** Is "Oakhollow" acceptable, or should it have a different name?

### Trade route persistence
**Decision:** Trade routes are static config (not persisted); only the mutable TradeRouteLedger (journeys, cursors) is saved.
**Question:** Should trade routes be content (JSON) so they can change, or is static config sufficient?

### News arrival truth
**Decision:** News arrival lives in NewsStore (arrived records); no NewsArrived event type added (abstract villages have no perceivers).
**Question:** Should news arrival be in the event log for consistency, or is the store sufficient?
