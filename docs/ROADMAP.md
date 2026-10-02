# Roadmap

The orchestrator keeps this file current. Status values: `todo`, `doing`, `review`, `done`, `blocked`.

**Rule:** finish and prove one phase before starting the next. Do not build Phase 4+ content early.

---

## Phase 0: Setup (human + orchestrator)

| ID | Task | Who | Status |
|---|---|---|---|
| P0-01 | Install Unity Hub + Unity 6 (6000.x) with iOS and Mac Build Support | Human | todo |
| P0-02 | Open this repository folder in Unity Hub ("Add project from disk") so Unity creates `ProjectSettings/` and `Packages/manifest.json`; switch to URP | Human | todo |
| P0-03 | Install the .NET 8 SDK so `dotnet test SimulationTests` works | Human | todo |
| P0-04 | Give the agent access to this GitHub repository; confirm it can run `dotnet test` | Human | todo |
| P0-05 | Add Unity packages: glTFast, Input System, AI Navigation, Cinemachine, Test Framework (edit `Packages/manifest.json`) | Orchestrator | todo |
| P0-06 | Decide the time scale (default 1 real second = 1 game minute) | Human | todo |
| P0-07 | Set up Git LFS for future large files (needs care: existing files are already in normal git) | Orchestrator, with human approval | todo |
| P0-08 | GitHub Actions workflow that runs `dotnet test SimulationTests` on every push | Orchestrator | todo |

---

## Phase 1: The Apple Test (simulation only, no graphics)

**Goal:** a text-only simulation of a tiny village where the Apple Test plays out on its own, proven by automated tests and a readable log.

### The Apple Test scenario

1. Small village: shopkeeper Mira's apple shop (20 apples), a farmer, 3–5 villagers, a guard, a tavern where people meet in the evening.
2. Day 1, 14:00: the player steals 6 apples (player command). One villager is nearby and *may* notice, depending on perception.
3. The simulation runs for 3 game days with no further player input.
4. **Pass when all of these emerge (none scripted):**
   - Customers try to buy apples; at least one buys fewer than wanted or none.
   - Mira counts stock and **believes apples are missing**, without knowing who took them unless told.
   - If a witness saw the theft, a rumor about it spreads at least once (e.g. in the tavern), with a traceable source chain.
   - Mira restocks from the farmer and/or raises the apple price.
   - The guard does **not** treat the player as a thief unless the evidence reaches the confidence threshold.
   - Running the same seed twice gives the identical log.
   - Running a seed where nobody notices the theft gives a world where nobody suspects the player.
5. The log reads like a story a human can follow ("Day 2 08:15 — Tom wanted 5 apples; only 2 left; bought 2").

### Tasks

Run tasks in order unless marked parallel-safe. `Role` refers to `AGENTS.md` section 6.

| ID | Task | Role | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P1-01 | Core types: strongly typed IDs, `GameTime` (minutes → day/hour), seeded `SimRng` with saveable state | Core | P0-03 | Unit tests for time math and RNG reproducibility pass | todo |
| P1-02 | `WorldState` container + `World.Tick()` running registered systems in a fixed order | Core | P1-01 | Test: empty world ticks 1440 times; order of systems is fixed | todo |
| P1-03 | `WorldEvent` + `EventLog` (append, query by time/location/type, prune) | Core | P1-02 | Tests for append/query/prune | todo |
| P1-04 | Command queue for player/NPC actions processed at tick start | Core | P1-02 | Test: commands apply in order at the next tick | todo |
| P1-05 | Locations (shop, farm, tavern, homes, square) with simple travel time between them | Core | P1-02 | Test: travel time lookup, NPC location changes after travel | todo |
| P1-06 | Item types (apple, bread, ale, coin) + inventories with aggregate counts | Economy | P1-02 | Tests: add/remove, can't go negative | todo |
| P1-07 | Shops: price list, buy (full/partial/fail) creating `Purchase` / `FailedPurchase` events; money transfer | Economy | P1-03, P1-06 | Tests incl. "wants 7, has 2"; money conserved | todo |
| P1-08 | Theft command: moves items, creates `Theft` event with visibility | Economy | P1-04, P1-07 | Test: stock drops, event logged, no money moves | todo |
| P1-09 | NPC data: identity, traits, needs, money, home, workplace; load from JSON in `Assets/_Game/Content/` | Agents | P1-05 | Test: loads sample village file; needs change over time | todo |
| P1-10 | Schedules + utility decision-making (eat, sleep, work, shop, socialize) | Agents | P1-09 | Tests: hungry NPC with money goes shopping; night → sleep | todo |
| P1-11 | Beliefs store (claim, source, confidence, time) per NPC | Knowledge | P1-03 | Tests: add/update/query beliefs | todo |
| P1-12 | Perception system: events → observations → beliefs with notice chance | Knowledge | P1-10, P1-11 | Tests: nearby awake NPC can notice; asleep/far NPC can't; seeded | todo |
| P1-13 | Decisions use beliefs (e.g. NPC avoids a shop it believes is out of apples) | Agents | P1-10, P1-11 | Test: failed purchase changes next choice | todo |
| P1-14 | Stock counting + inference: shopkeeper detects missing items | Knowledge | P1-07, P1-12 | Test: belief "6 apples missing", no thief identified | todo |
| P1-15 | Conversations + rumor spreading with trust filter and distortion, source chain | Knowledge | P1-12 | Tests: rumor spreads in tavern; confidence drops with distrust | todo |
| P1-16 | Restocking from farm + farm production + simple price adjustment | Economy | P1-07, P1-10 | Tests: low stock triggers order; missed sales raise price within bounds | todo |
| P1-17 | Memory with importance and decay | Knowledge | P1-11 | Tests: minor memory fades in days, major stays | todo |
| P1-18 | Guard suspicion threshold + reputation by group | Knowledge | P1-15 | Test: guard acts only above threshold | todo |
| P1-19 | Readable simulation log / daily report tool | Test & Scenario | P1-03 | Log reads like a story; CLI or test output | todo |
| P1-20 | **Apple Test scenario tests** (witness and no-witness seeds, determinism) | Test & Scenario | P1-08 … P1-19 | All pass conditions above are automated | todo |
| P1-21 | Save/load full world state to JSON; determinism across save/load | Persistence | P1-20 | Test: run(1000) == save→load→run(1000) | todo |

Parallel-safe groups once their dependencies are done: {P1-06, P1-09, P1-11}, {P1-16, P1-17, P1-19}.

**Phase 1 exit:** P1-20 and P1-21 pass, and the human has read one Apple Test log and agrees it feels like a living village.

---

## Phase 2: See it in Unity (first playable village)

| ID | Task | Role |
|---|---|---|
| P2-01 | `WorldRunner` bridge: tick at chosen time scale, publish state changes | Bridge |
| P2-02 | Village scene from Kenney kits (shop, farm, tavern, homes, square, forest edge, river); NavMesh | World |
| P2-03 | NPC prefabs (MiniCharacters) walking to their simulated locations with matching animations | World |
| P2-04 | Day/night lighting from game time | World |
| P2-05 | Player: virtual joystick + tap-to-move, third-person camera (rotate, pinch zoom) | Player & UI |
| P2-06 | Tap NPC/object → contextual radial menu (Talk, Trade, Steal, Inspect…) sending commands | Player & UI |
| P2-07 | Minimal HUD (time, weather, location) and shop/inventory panels | Player & UI |
| P2-08 | Template dialogue from NPC fact sheets (what they know, how they feel) | Player & UI |
| P2-09 | First iPad build via Xcode; human play-test of the Apple Test | Human + Orchestrator |

**Exit:** the human plays the Apple Test on an iPad and can discover the consequences in-game.

---

## Later phases (plan in detail when the previous phase is done)

| Phase | Focus |
|---|---|
| 3 | Relationships and memory depth; economy expansion (more goods, bakery, blacksmith, money sources and sinks) |
| 4 | Skills, crafting and cooking (cooking affects health, happiness, tavern popularity, prices) |
| 5 | Animal ecosystem (wolves, deer, livestock) and taming through trust |
| 6 | Town development emerging from population, food, housing, trade and safety; emergent events |
| 7 | Expanded world: more villages, trade routes, level-of-detail simulation |
| 8 | Reincarnation and generations (aging, families, inheritance) |
| 9 | AI dialogue from fact sheets (never changes game state) |
| 10 | iPad/iPhone/Mac optimization, polish, App Store release |
