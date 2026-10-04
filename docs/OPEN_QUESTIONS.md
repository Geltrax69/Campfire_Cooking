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

## Phase 7 (2026-10-04)

### Core event type additions
**Decision:** Added `WorldEventType.Death` (P7-01), `Birth` (P7-02), `Inheritance` (P7-03) — one enum line each, following the P3-03 MealEaten precedent.
**Question:** Ratify these additions.

### Birthday year
**Decision:** Birthdays are day 1–360 (the world calendar is 360 days, not 365).
**Question:** Confirm the 360-day year is the intended calendar.

### Deceased NPC handling
**Decision:** Deceased NPCs remain registered (for inheritance). How needs/schedule systems treat them is not yet specified.
**Question:** Should deceased NPCs be excluded from needs/schedules, or is the current behavior acceptable?

### Birth simplifications
**Decision:** One birth roll per household per year (10%); opposite-gender couples only; leavers settle at the same home location; babies copy the mother's need rates and get empty schedules/traits; Content `family` arrays must list both directions.
**Question:** Are these simplifications acceptable, or should any be revisited?

### Inheritance simplifications
**Decision:** A will names one heir for everything; a dead designee falls through to normal priority (doesn't disinherit); items go to the primary heir only (not split); orphan shares mingle in the guardian's wallet; no-heir items are absorbed by the household or left unclaimed (never destroyed/sold).
**Question:** Are these simplifications acceptable?

### Born NPC knowledge limits
**Decision:** Beliefs, memories, relationships, debts, and pending-command NPC references are still Content-strict in the loader. A born NPC can currently only appear in the event log, skills, and family sections.
**Question:** If a future phase lets newborns hold beliefs or debts, those sections need the same Content ∪ save-NPC treatment. Is this deferred correctly?

## Phase 8 (2026-10-04)

### News source for fact sheets
**Decision:** The fact sheet uses news arrived at the full-LOD village (Millbrook) as "what goes around town." All individual NPCs currently live in Millbrook; abstract villages have no residents.
**Question:** If NPCs ever live in abstract villages, this needs a per-NPC village mapping. Acceptable as-is?

### ShareNews phrasing
**Decision:** `ShareNews` phrases the most recent known news item and ignores `IsGoodNews`/`Severity` in wording (valence-neutral phrasing).
**Question:** Should phrasing reflect news severity/valence?

### Deceased NPC dialogue
**Decision:** `DialogueSession.Say` on a deceased NPC's sheet phrases what's there (no-throw); who may be talked to is the caller's decision.
**Question:** Should `Say` on a deceased NPC's sheet eventually return a special "they're gone" line instead?

### Template grammar
**Decision:** Pluralization is deliberately crude (`fish` → `fishes`); templates never need perfect grammar, only sheet-faithful nouns. A future AI adapter would handle this naturally.
**Question:** Acceptable for the simulation phase?

### Shared test runner
**Decision:** `~/workspace/run-sim-tests.sh` currently reports `Discovered 0 test cases` (FrameworkController.LoadTests returns a shallow suite node in this environment). Tests were verified green via a throwaway runner using the same FrameworkController pattern.
**Question:** Should fixing the shared test runner be its own task before further work?
