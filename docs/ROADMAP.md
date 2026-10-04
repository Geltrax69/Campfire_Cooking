# Roadmap

The orchestrator keeps this file current. Status values: `todo`, `doing`, `review`, `done`, `blocked`.

**Rule:** finish and prove one phase before starting the next. Do not build Phase 4+ content early.

---

## Order of work

1. **Design Phase:** world, places, characters, money, items, skills, animals, rules. Documents in `docs/design/`, data in `Content/`. Prompts in `docs/DESIGN_PROMPTS.md`.
2. **Phase 0:** minimal setup for code (.NET only, no Unity).
3. **Phases 1–8:** the simulation, text-only, proven by tests and logs.
4. **Unity Phase (last):** visuals, controls, iPad/iPhone/Mac builds.

**Unity work does not start until the human says so.**

**Checkpoint — 2026-10-04:** Design Phase and simulation Phases 0–8 have
completed task evidence. The current full simulation suite passed **875/875** on this Mac
using .NET 8.0.425 on 2026-10-04.
The owner authorized the Unity Phase and selected an Apple shop and village
slice. Unity project/package setup and the first bridge are implemented; the
U-02 scene slice and U-07 shop UI slice passed 16 EditMode tests and one
runtime interaction test, with iPad/small landscape captures visually checked. These slices do not complete the full village tasks. iOS Build Support is not installed. Known simulation limitations
remain tracked in [OPEN_QUESTIONS.md](OPEN_QUESTIONS.md).
See `docs/tasks/completed/` for finished task evidence and
[DEVELOPMENT.md](DEVELOPMENT.md) for local commands.

---

## Design Phase (human + design agents)

The human approves every area before it's marked `done`.

| ID | Area | Prompt | Depends on | Produces | Status |
|---|---|---|---|---|---|
| D-01 | World, setting and tone | 1 | none | `docs/design/WORLD.md` | done |
| D-02 | Village map and locations | 2 | D-01 | `LOCATIONS.md`, `Content/world/locations.json` | done |
| D-03 | Characters (~20 villagers) | 3 | D-01, D-02 | `CHARACTERS.md`, `Content/npcs/npcs.json` | done |
| D-04 | Money and economy | 4 | D-01–D-03 | `ECONOMY.md`, `Content/economy/economy.json` | done |
| D-05 | Items and resources | 5 | D-02, D-04 | `ITEMS.md`, `Content/items/items.json` | done |
| D-06 | Skills | 6 | D-01, D-04, D-05 | `SKILLS.md`, `Content/skills/skills.json` | done |
| D-07 | Recipes and crafting | 7 | D-05, D-06 | `RECIPES.md`, `Content/recipes/recipes.json` | done |
| D-08 | Animals and taming | 8 | D-01, D-02, D-05 | `ANIMALS.md`, `Content/animals/species.json` | done |
| D-09 | Knowledge, rumors, reputation | 9 | D-03 | `KNOWLEDGE.md`, `Content/social/social.json` | done |
| D-10 | Town state and emergent events | 10 | D-02–D-04 | `TOWN.md`, `Content/world/town.json` | done |
| D-11 | Player start | 11 | D-01–D-06 | `PLAYER_START.md`, `Content/player/start.json` | done |
| D-12 | Consistency check and glossary | 12 | all above | fixes + `GLOSSARY.md` | done |

Parallel-safe once dependencies are approved: {D-05, D-09}, {D-06, D-08, D-10}.

**Design Phase exit:** D-12 passes and the human approves the whole set.

**Design Phase: DONE.** 2026-10-02 — the human reviewed the full summary and all D-06–D-12 autonomous decisions, and approved the Design Phase. All D-01–D-12 marked `done`, committed, and pushed to `claude/wizardly-clarke-tigogn`.

---

## Phase 0: Code setup (no Unity)

| ID | Task | Who | Status |
|---|---|---|---|
| P0-01 | Install the .NET 8 SDK so `dotnet test SimulationTests` works | Human | done (2026-10-03: .NET 8.0.425 on the development Mac) |
| P0-02 | Confirm the agent can read/write the repo and run `dotnet test` | Human | done (2026-10-02, with caveat — see note) |

**P0-02 note (agent environment, 2026-10-02):** the agent's sandbox runs .NET 8 SDK 8.0.131 from `~/.dotnet` (persistent across VM restarts). `dotnet build` works via a local NuGet feed (`~/nuget-local`, 48 packages incl. NUnit 5 / NUnitLite / Microsoft.NET.Test.Sdk — the sandbox proxy does TLS interception that .NET's TLS stack can't complete, so nuget.org is unreachable from `dotnet` directly; `curl` works and was used to populate the feed). NUnit tests compile and pass (verified with the NUnitLite self-executing runner). Plain `dotnet test` can **not** run in the sandbox: `vstest.console` ↔ `testhost` communicate over TCP on `127.0.0.1`, and the sandbox transparently redirects all IPv4 TCP (including localhost) to the egress proxy, so the testhost never connects. GitHub Actions (P0-03) is therefore the authoritative `dotnet test SimulationTests` runner; the agent verifies locally with `dotnet build` + NUnitLite.

**Current environment (2026-10-03):** the development Mac runs .NET SDK 8.0.425.
Normal NuGet restore and `dotnet test SimulationTests` work without the old
sandbox workaround. P1-01 verifies discovery and execution: 31/31 tests pass in
both Debug and Release. See [DEVELOPMENT.md](DEVELOPMENT.md) for commands.

| P0-03 | GitHub Actions workflow that runs `dotnet test SimulationTests` on every push | Orchestrator | done (2026-10-02) |
| P0-04 | Decide the time scale: **1 real second = 1 game minute** (one game day = 24 real minutes) | Human | done (2026-10-02) |

---

## Phase 1: The Apple Test (simulation only, no graphics)

**Goal:** a text-only simulation of a tiny village where the Apple Test plays out on its own, proven by automated tests and a readable log.

Phase 1 uses the approved Design Phase data in `Content/`. If a needed value is missing, ask the human instead of inventing it.

### The Apple Test scenario

1. Small village (from `Content/`): the apple shop (20 apples) and its owner, a farmer, 3–5 villagers, a guard, a tavern where people meet in the evening.
2. Day 1, 14:00: the player steals 6 apples (player command). One villager is nearby and *may* notice, depending on perception.
3. The simulation runs for 3 game days with no further player input.
4. **Pass when all of these emerge (none scripted):**
   - Customers try to buy apples; at least one buys fewer than wanted or none.
   - The shop owner counts stock and **believes apples are missing**, without knowing who took them unless told.
   - If a witness saw the theft, a rumor about it spreads at least once (e.g. in the tavern), with a traceable source chain.
   - The shop owner restocks from the farmer and/or raises the apple price.
   - The guard does **not** treat the player as a thief unless the evidence reaches the confidence threshold.
   - Running the same seed twice gives the identical log.
   - Running a seed where nobody notices the theft gives a world where nobody suspects the player.
5. The log reads like a story a human can follow ("Day 2 08:15 — Tom wanted 5 apples; only 2 left; bought 2").

### Tasks

Run tasks in order unless marked parallel-safe. `Role` refers to `AGENTS.md` section 6.

| ID | Task | Role | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P1-01 | Core types: strongly typed IDs, `GameTime` (minutes → day/hour), seeded `SimRng` with saveable state | Core | P0-01 | Unit tests for time math and RNG reproducibility pass | done (2026-10-03) |
| P1-02 | `WorldState` container + `World.Tick()` running registered systems in a fixed order | Core | P1-01 | Test: empty world ticks 1440 times; order of systems is fixed | done (2026-10-03) |
| P1-03 | `WorldEvent` + `EventLog` (append, query by time/location/type, prune) | Core | P1-02 | Tests for append/query/prune | done (2026-10-03) |
| P1-04 | Command queue for player/NPC actions processed at tick start | Core | P1-02 | Test: commands apply in order at the next tick | done (2026-10-03) |
| P1-05 | Locations (shop, farm, tavern, homes, square) with simple travel time between them | Core | P1-02, P1-03/P1-04 for integration | Test: travel time lookup, NPC location changes after travel | done (2026-10-03; map and movement PRs) |
| P1-06 | Item types (apple, bread, ale, coin) + inventories with aggregate counts | Economy | P1-02 | Tests: add/remove, can't go negative | done (2026-10-03) |
| P1-07 | Shops: price list, buy (full/partial/fail) creating `Purchase` / `FailedPurchase` events; money transfer | Economy | P1-03, P1-06 | Tests incl. "wants 7, has 2"; money conserved | done (2026-10-03; wallet and shop PRs) |
| P1-08 | Theft command: moves items, creates `Theft` event with visibility | Economy | P1-04, P1-07 | Test: stock drops, event logged, no money moves | done (2026-10-03) |
| P1-09 | NPC data: identity, traits, needs, money, home, workplace; load from `Content/npcs/npcs.json` (Design Phase output) | Agents | P1-05 | Test: loads sample village file; needs change over time | done (2026-10-03; definitions and state PRs) |
| P1-10 | Schedules + utility decision-making (eat, sleep, work, shop, socialize) | Agents | P1-09 | Tests: hungry NPC with money goes shopping; night → sleep | done (2026-10-03; schedule and decision PRs; production tuning awaits human approval) |
| P1-11 | Beliefs store (claim, source, confidence, time) per NPC | Knowledge | P1-03 | Tests: add/update/query beliefs | done (2026-10-03) |
| P1-12 | Perception system: events → observations → beliefs with notice chance | Knowledge | P1-10, P1-11 | Tests: nearby awake NPC can notice; asleep/far NPC can't; seeded | done (2026-10-03) |
| P1-13 | Decisions use beliefs (e.g. NPC avoids a shop it believes is out of apples) | Agents | P1-10, P1-11 | Test: failed purchase changes next choice | done (2026-10-03) |
| P1-14 | Stock counting + inference: shopkeeper detects missing items | Knowledge | P1-07, P1-12 | Test: belief "6 apples missing", no thief identified | done (2026-10-03) |
| P1-15 | Conversations + rumor spreading with trust filter and distortion, source chain | Knowledge | P1-12 | Tests: rumor spreads in tavern; confidence drops with distrust | done (2026-10-03) |
| P1-16 | Restocking from farm + farm production + simple price adjustment | Economy | P1-07, P1-10 | Tests: low stock triggers order; missed sales raise price within bounds | done (2026-10-04; split event/production/restock/price PRs) |
| P1-17 | Memory with importance and decay | Knowledge | P1-11 | Tests: minor memory fades in days, major stays | done (2026-10-03) |
| P1-18 | Guard suspicion threshold + reputation by group | Knowledge | P1-15 | Test: guard acts only above threshold | done (2026-10-04; truth contracts and behavior PRs) |
| P1-19 | Readable simulation log / daily report tool | Test & Scenario | P1-03 | Log reads like a story; CLI or test output | done (2026-10-03; foundation truth report) |
| P1-20 | **Apple Test scenario tests** (witness and no-witness seeds, determinism) | Test & Scenario | P1-08 … P1-19 | All pass conditions above are automated | done (2026-10-04; harness + witness/no-witness acceptance and story report) |
| P1-21 | Save/load full world state to JSON; determinism across save/load | Persistence | P1-20 | Test: run(1000) == save→load→run(1000) | done (P1-21a–f; later long-run animal drift remains in OPEN_QUESTIONS.md) |

Parallel-safe groups once their dependencies are done: {P1-06, P1-09, P1-11}, {P1-16, P1-17, P1-19}.

**Phase 1 exit:** P1-20 and P1-21 pass, and the human has read one Apple Test log and agrees it feels like a living village.

---

## Phase 2: Relationships and economy (simulation only, no graphics)

Planned 2026-10-04. Focus: dynamic directed relationships (trust/affection) that
change through interaction and shape behavior; deeper attributed memories;
economy expansion (general-store goods, bakery chain, blacksmith, money sources
and sinks, debts). All new mutable state must be save/load-compatible (P2-12).

| Task | What | Area | Depends on | Pass condition | Status |
|---|---|---|---|---|---|
| P2-01 | Relationship state: directed trust/affection pairs loaded from `Content/npcs/npcs.json` into `WorldState`, with validated internal capture/restore | Knowledge | P1-21 | Tests: Mira's 4 relationships load exactly; bad data rejected | done (2026-10-04) |
| P2-02 | Relationship dynamics: trades, gifts, conversations, witnessed wrongs shift trust/affection by rule; slow decay toward baseline | Knowledge | P2-01 | Tests: honest trade raises trust; witnessed theft drops it; decay works | done (2026-10-04) |
| P2-03 | Relationships shape behavior: rumor trust uses relationship trust; friends get better prices; social choices prefer liked NPCs | Knowledge, Agents | P2-02 | Tests: friend discount; distrusted rumor loses confidence | done (2026-10-04) |
| P2-04 | Attributed interaction memories: who-did-what memories that reinforce relationship shifts on recall | Knowledge | P2-02 | Tests: betrayal memory keeps trust low; kindness remembered | done (2026-10-04) |
| P2-05 | Goods expansion: general store stocks salt, cloth, lamp oil, nails, rope, basic tools (stock + prices) | Economy | P1-21 | Tests: buying goods moves stock and conserves money | done (2026-10-04) |
| P2-06 | Bakery chain: farm grain → mill (Garrick's 1/12 toll in kind) → flour → Oda bakes ~40 loaves/day; oven constraint, sells out | Economy | P2-05 | Tests: daily bake; no flour = no bread; sellout by afternoon | done (2026-10-04) |
| P2-07 | Blacksmith: Doran's iron stock, tool production and repairs for copper | Economy | P2-05 | Tests: tools produced from iron; repair costs copper | done (2026-10-04) |
| P2-08 | Money sources: traveling merchants (~3 weeks, ~800 copper), seasonal travelers, winter wolf bounties (50/pelt) | Economy | P2-05 | Tests: merchant visit injects money; winter traveler drought | done (2026-10-04) |
| P2-09 | Money sinks: prosperity-scaled taxes, imports, community fund, two-stage spoilage (fresh→stale→spoiled) | Economy | P2-05 | Tests: tax scales with prosperity; spoilage destroys value | done (2026-10-04) |
| P2-10 | Debts: Tilda's tab ledger as mechanical debts with repayment schedules | Economy | P2-05 | Tests: Doran repays 10/week; tabs affect trade willingness | done (2026-10-04) |
| P2-11a | Daily-life drivers: village assembly, NPC eating/shopping, evening meetings, friend-pricing wiring | Agents + Knowledge | P2-01 … P2-10 | Tests: hungry NPC eats; low-food NPC buys; tavern rumors spread; 7-day run clean | done (2026-10-04) |
| P2-11b | Phase 2 acceptance: month-long village simulation | Test & Scenario | P2-11a | Total village copper stays within ±10% month-to-month; bakery sells out most days; relationships shift measurably | done (2026-10-04) |
| P2-12 | Persist Phase 2 state: extend saver/loader/schema; round-trip + determinism | Persistence | P2-01 … P2-11b | Tests: save→load→save byte-identical; P1-21f determinism still green; full-village 400/save/load/600 proof | done (2026-10-04) |

**Phase 2 DONE (2026-10-04).** All 12 tasks complete. 561 tests green.

## Phase 3: Skills, crafting and cooking (simulation only, no graphics)

Planned 2026-10-04. Focus: skill levels 1-5 earned through practice (never XP-from-killing);
cooking recipes from Content with quality, failure, fuel and permission; crafting at forge/workbench;
cooked-food quality affects NPC happiness/health, tavern popularity, ingredient demand and prices.
All new mutable state must be save/load-compatible.

| Task | What | Area | Depends on | Pass condition | Status |
|---|---|---|---|---|---|
| P3-01 | Skill state: SkillState with levels 1-5, practice points, thresholds (100/300/700/1500), daily cap 20, teaching x2, qualityBonus 0-4; NPC and player skill stores | Agents | P2-12 | Tests: practice grants points; level thresholds; daily cap enforced; teaching doubles; quality bonus by level | done (2026-10-04) |
| P3-02 | Recipe execution: RecipeDefinition from Content/recipes; cooking/crafting sessions consume inputs + fuel, roll failure by (difficulty - level), output items with quality = clamp(avg input + bonus); level gates hard; permission from relationships | Economy | P3-01 | Tests: at-level recipe succeeds ~90%; 2-above fails 30%; failure loses inputs; quality scales with skill | done (2026-10-04) |
| P3-03 | Cooking effects: meal quality -> NPC happiness/health; tavern popularity from cook skill; ingredient demand shifts prices; better cooks waste less | Agents, Economy | P3-02 | Tests: quality-80 stew raises happiness more than quality-50; tavern revenue rises with skilled cook | done (2026-10-04) |
| P3-04 | Phase 3 acceptance: NPC cooks daily meals; player practices cooking; skill improves; effects visible in week-long run | Test & Scenario | P3-01 … P3-03 | Tests: 7-day run: meals cooked, skill points accrue, no errors; readable log | done (2026-10-04) |

**Phase 3 DONE (2026-10-04).** All 4 tasks complete. 639 tests green. Skills (levels 1-5, practice, teaching, daily cap), recipe execution (21 recipes, quality, failure, fuel, permission), cooking effects (happiness, tavern popularity, ingredient demand, waste reduction), 7-day acceptance with save/load determinism (save format v3).

**Phase 4 DONE (2026-10-04).** All 4 tasks complete. 697 tests green. Animal state (5 species, trust 0-100), ecosystem dynamics (predation, breeding, winter pressure, 2-4 livestock losses/winter), taming through trust (bond thresholds, council approval for wolves), 90-day acceptance with save/load determinism (save format v4).

**Phase 5 DONE (2026-10-04).** All 4 tasks complete. 742 tests green. Town stats (11 computed monthly), growth/decline (in-migration, out-migration, decline spiral), 10 emergent events (conditions only, chaining emergent), 270-day acceptance with save/load determinism (save format v5).

**Phase 6 DONE (2026-10-04).** All 4 tasks complete. 787 tests green. Village LOD (Millbrook full, King's Rest/Oakhollow abstract), trade routes (goods flow, price differences, merchants), news travel (delayed, distorted, mood/opinion effects), 30-day multi-village acceptance with save/load determinism (save format v6).

## Later simulation phases (plan in detail when the previous phase is done)

| Phase | Focus | Status |
|---|---|---|
| 2 | Relationships and economy — see Phase 2 section above | DONE (2026-10-04) |
| 3 | Skills, crafting and cooking (cooking affects health, happiness, tavern popularity, prices) | next |
| 4 | Animal ecosystem (wolves, deer, livestock) and taming through trust | next |

## Phase 4: Animal ecosystem and taming (simulation only, no graphics)

Planned 2026-10-04. Focus: 5 species (chicken, pig/boar, deer, wolf, brambleback) with populations,
predation, breeding, and winter pressure; taming as trust 0-100 through repeated calm interactions
(one meaningful gain per day); bonded animals provide services; wolf taming needs council approval.
All new mutable state must be save/load-compatible.

| Task | What | Area | Depends on | Pass condition | Status |
|---|---|---|---|---|---|
| P4-01 | Animal state: species definitions from Content; population counts; individual animals with trust 0-100; AnimalStore per location | Agents | P3-04 | Tests: species load; populations track; trust 0-100; individual animals identifiable | done (2026-10-04) |
| P4-02 | Ecosystem dynamics: predation (wolves→deer/livestock), breeding (spring), winter pressure (deer→farms, wolves→livestock), livestock losses 2-4 per winter | Agents, Economy | P4-01 | Tests: winter increases livestock losses; wolf predation reduces deer; spring breeding increases populations | done (2026-10-04) |
| P4-03 | Taming: trust gains/losses per interaction type; one meaningful gain per day; bonded thresholds (chicken 60, pig 70, deer 80, wolf 85); wolf needs council approval; bonded services | Agents | P4-01 | Tests: chicken bonds in ~7 days; cruelty breaks trust; wolf pup takes ~60 days; council approval required | done (2026-10-04) |
| P4-04 | Phase 4 acceptance: season-long run; populations shift with winter; player tames a chicken; wolf incident occurs; readable log | Test & Scenario | P4-01 … P4-03 | Tests: winter→livestock losses; taming works; incident logged; save/load preserves animals | done (2026-10-04) |
| 5 | Town development emerging from population, food, housing, trade and safety; emergent events | next |

## Phase 5: Town development and emergent events (simulation only, no graphics)

Planned 2026-10-04. Focus: 11 town stats computed from world state (never set by hand);
growth/decline through conditions (in-migration, out-migration, decline spiral); 10 emergent
events that fire only when world conditions are met and can chain. All new mutable state
must be save/load-compatible.

| Task | What | Area | Depends on | Pass condition | Status |
|---|---|---|---|---|---|
| P5-01 | Town stats: 11 stats (population, wealth, food_supply, safety, housing, employment, trade, happiness, crime, infrastructure, reputation) computed from world state monthly | Agents, Economy | P4-04 | Tests: stats compute from world state; food_supply reflects granary; safety reflects wolf incidents | done (2026-10-04) |
| P5-02 | Growth/decline: in-migration (seasonal, conditions), out-migration (spring, per-NPC), decline spiral (low food → hunger → out-migration) | Agents | P5-01 | Tests: high stats → in-migration; low food in winter → happiness falls | done (2026-10-04) |
| P5-03 | Emergent events: 10 events (food_shortage, wolf_attack, festival, fire, theft_wave, merchant_arrival, fever, drought, wheel_failure, bridge_project) with conditions; chaining | Agents, Economy | P5-01 | Tests: food shortage fires when supply < 25 in winter; wolf attack fires in winter; events chain | done (2026-10-04) |
| P5-04 | Phase 5 acceptance: year-long run; stats move with seasons; events fire; readable log | Test & Scenario | P5-01 … P5-03 | Tests: year run completes; events fired; stats changed; save/load preserves town state | done (2026-10-04) |
| 6 | Expanded world: more villages, trade routes, level-of-detail simulation | next |

## Phase 6: Expanded world (simulation only, no graphics)

Planned 2026-10-04. Focus: neighboring villages (King's Rest, etc.) as LOD entities;
trade routes carrying goods and news between villages; level-of-detail simulation
(near = full, far = abstract). Millbrook remains the fully-simulated home village.

| Task | What | Area | Depends on | Pass condition | Status |
|---|---|---|---|---|---|
| P6-01 | Village LOD: neighboring villages as abstract entities (population, wealth, food, mood); LOD levels (full/abstract); village registry | Core, Agents | P5-04 | Tests: villages register; LOD levels; abstract villages tick without full NPC sim | done (2026-10-04) |
| P6-02 | Trade routes: routes between villages (goods, prices, travel time); merchants carry goods; price differences drive trade | Economy | P6-01 | Tests: goods flow along routes; prices differ by village; merchant profit | done (2026-10-04) |
| P6-03 | News travel: rumors and reputation spread between villages via travelers/merchants (delayed, distorted) | Knowledge | P6-01 | Tests: news from Millbrook reaches King's Rest after travel time; distortion | done (2026-10-04) |
| P6-04 | Phase 6 acceptance: multi-village run; trade flows; news spreads; readable log | Test & Scenario | P6-01 … P6-03 | Tests: 30-day multi-village run; trade happened; news arrived; save/load preserves villages | done (2026-10-04) |
| 7 | Reincarnation and generations (aging, families, inheritance) | next |

## Phase 7: Reincarnation and generations (simulation only, no graphics)

Planned 2026-10-04. Focus: NPC aging (birth → child → adult → elder → death);
families (parent/child links, households); inheritance (money/items pass to heirs);
reincarnation (player arrives as a stranger; the cycle continues). All new mutable
state must be save/load-compatible.

| Task | What | Area | Depends on | Pass condition | Status |
|---|---|---|---|---|---|
| P7-01 | Aging: NPC age advances yearly; life stages (child/adult/elder); death from old age (deterministic) | Agents | P6-04 | Tests: age advances; elders die; children become adults | done (2026-10-04) |
| P7-02 | Families: parent/child links; households; children inherit household | Agents | P7-01 | Tests: parent/child links; household membership; children stay | done (2026-10-04) |
| P7-03 | Inheritance: on death, money/items pass to heirs (spouse → children → village fund); wills (simple) | Economy, Agents | P7-02 | Tests: inheritance flows to heirs; money conserved; no orphans with wealth | done (2026-10-04) |
| P7-04 | Phase 7 acceptance: multi-year run; generations turn over; inheritance works; readable log | Test & Scenario | P7-01 … P7-03 | Tests: 5-year run; deaths and births; inheritance; save/load preserves families | done (2026-10-04) |
| 8 | AI dialogue from fact sheets (never changes game state) |

## Phase 8: Dialogue from fact sheets (simulation only, no graphics, no Unity)

Planned 2026-10-04. Focus: pure-simulation dialogue foundation. A **fact sheet**
is built from simulation truth (NPC identity, mood, beliefs/memories about the
player, known news); deterministic template phrasing turns it into speech.
Dialogue may phrase facts only — it cannot add facts, reveal unknown facts, or
change game state. No paid AI services; the phrasing engine is a deterministic
mock with a clean contract an AI model could later implement.

| Task | What | Area | Depends on | Pass condition | Status |
|---|---|---|---|---|---|
| P8-01 | FactSheet builder: identity, mood, beliefs/memories about player, known news; only NPC-known facts | Knowledge, Agents | P7-04 | Tests: fact sheet contains only known facts; excludes unknown; deterministic | done (2026-10-04) |
| P8-02 | Template phrasing: deterministic templates filled from fact sheet; seeded variation; mock AI contract | Knowledge | P8-01 | Tests: same input → same output; phrasing never adds facts | done (2026-10-04) |
| P8-03 | Dialogue contract: enforcement that dialogue never changes state; no secret leakage; read-only API | Knowledge | P8-02 | Tests: state unchanged after dialogue; secrets never appear | done (2026-10-04) |
| P8-04 | Phase 8 acceptance: dialogue sessions across NPCs; readable log; save/load of dialogue-relevant state | Test & Scenario | P8-03 | Tests: acceptance log; determinism proof | done (2026-10-04) |

---

## Unity Phase (last): see and play it

**Authorized by the owner on 2026-10-04.** First scope: Apple shop and village slice. Setup first:

| ID | Task | Who |
|---|---|---|
| U-00a | Install Unity Hub + Unity 6 with iOS and Mac Build Support | Human — partial: Hub, Unity 6000.6.4f1 and Mac support installed; iOS missing |
| U-00b | Add this folder as a project in Unity Hub; switch to URP | Orchestrator — done (Unity project initialized; URP scene generated) |
| U-00c | Add packages: glTFast, Input System, AI Navigation, Cinemachine, Test Framework | Orchestrator — done |
| U-00d | Git LFS for new large files (with human approval) | Orchestrator — deferred: no new large assets needed for slice |

| ID | Task | Role |
|---|---|---|
| U-01 | `WorldRunner` bridge: tick at chosen time scale, publish state changes | Bridge — implemented for Apple slice; tested clock/commands/snapshots |
| U-02 | Village scene from Kenney kits (shop, farm, tavern, homes, square, forest edge, river); NavMesh | World — doing: static shop slice; full village/NavMesh remain |
| U-03 | NPC prefabs (MiniCharacters) walking to their simulated locations with matching animations | World |
| U-04 | Day/night lighting from game time | World |
| U-05 | Player: virtual joystick + tap-to-move, third-person camera (rotate, pinch zoom) | Player & UI |
| U-06 | Tap NPC/object → contextual radial menu (Talk, Trade, Steal, Inspect…) sending commands | Player & UI |
| U-07 | Minimal HUD (time, weather, location) and shop/inventory panels | Player & UI — doing: time/location + apple shop/inventory slice; no weather simulation |
| U-08 | Template dialogue from NPC fact sheets (what they know, how they feel) | Player & UI |
| U-09 | First iPad build via Xcode; human play-test of the Apple Test | Human + Orchestrator |
| U-10 | iPad/iPhone/Mac optimization, polish, App Store release | All |

**Unity exit:** the human plays the Apple Test on an iPad and can discover the consequences in-game.
