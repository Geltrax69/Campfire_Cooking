# Architecture

How Living World is built, and why. Agents: read `AGENTS.md` first. This document explains the design; `AGENTS.md` has the rules.

---

## 1. The big picture

```
┌──────────────────────────────────────────────────────────────┐
│  UNITY (Assets/_Game)                                        │
│                                                              │
│   Player input ──► Bridge ──► commands ──┐                   │
│   Visuals/UI   ◄── Bridge ◄── read-only ─┼───┐               │
└──────────────────────────────────────────┼───┼───────────────┘
                                           ▼   │
┌──────────────────────────────────────────────┴───────────────┐
│  SIMULATION (Packages/com.geltrax.livingworld.simulation)    │
│  Pure C#, deterministic, no Unity                            │
│                                                              │
│   WorldState  ◄──  Systems run every tick, in a fixed order  │
│   EventLog        (time → needs → decisions → actions →      │
│   Rng              perception → conversations/rumors →       │
│                    economy → memory decay)                   │
└──────────────────────────────────────────────────────────────┘
```

- **Simulation** decides everything that is true.
- **Unity** shows it and turns taps into commands.
- The **Bridge** is the only code that touches both.

**Why:** the simulation can be tested in seconds with `dotnet test`, without Unity, by any agent. It stays correct whether 2 or 2,000 objects are on screen, and later it can run faster than real time (catch-up when the player returns, or headless "run 30 days" experiments).

---

## 2. World state

`WorldState` holds everything that must be saved:

| Part | Contents |
|---|---|
| Clock | Current game minute (`long`). Day, hour and season are derived from it |
| Rng | The single seeded random generator and its current state |
| Locations | Places (shop, farm, tavern, homes, square, forest, river) with position, owner and type |
| Npcs | Identity, traits, needs, schedule, current activity, location, money |
| Inventories | Stock per owner per item type (aggregate counts) |
| TrackedItems | Individual records only for important items (unique tools, stolen goods if needed later) |
| Relationships | Pairwise values between NPCs and with the player (trust, affection, respect, fear) |
| Beliefs | Per NPC: what they believe, from which source, with what confidence |
| Memories | Per NPC: remembered events with importance and fading strength |
| Reputation | Per group (guards, farmers, merchants, ...) opinion of the player |
| EventLog | Recent world events (the truth). Old events are pruned once no memory or belief references them |
| Town | Derived stats (population, food supply, wealth, safety, crime, happiness) |

All IDs are strongly typed. All collections that are iterated during a tick are **ordered** (by ID) so the result is deterministic.

---

## 3. The tick (1 tick = 1 game minute)

Systems run in this fixed order every tick:

1. **Time:** advance the clock; fire day/hour boundaries.
2. **Needs:** hunger, sleep and other needs change over time.
3. **Decisions:** idle NPCs pick their next activity (section 5).
4. **Actions:** activities progress (walking, working, buying, eating). Actions that finish create **world events**.
5. **Perception:** for each new world event, decide which NPCs noticed it and create **observations** (section 6).
6. **Social:** NPCs who are together may talk; beliefs can spread as rumors (section 7).
7. **Economy:** production (farm), restocking, price updates. Some of these run hourly or daily rather than every tick.
8. **Memory:** memories fade; weak, unimportant ones are forgotten.

**Time scale:** to be decided by the human. Default for the prototype: **1 real second = 1 game minute** (one game day = 24 real minutes). The simulation doesn't care; the Bridge sets the rate.

**Fast-forward:** the simulation must be able to run thousands of ticks quickly with no visuals. This is how scenario tests work, and later how far-away areas catch up.

---

## 4. World events (truth)

Every meaningful thing that happens is a `WorldEvent`:

```
WorldEvent
  Id, Time, Location
  Type          (Purchase, Theft, StockCounted, Conversation, Attack, Gift, ...)
  Actor         (who did it)
  Targets       (who/what it affected)
  Details       (item type, quantity, price, ...)
  Visibility    (how noticeable: Hidden, Quiet, Normal, Loud)
```

Events are facts. NPCs never read the event log directly. They only learn about events through **perception** or **conversation**.

---

## 5. NPC decisions (utility AI)

Each NPC has:

- **Traits** (0–100): friendly, honest, greedy, brave, curious, lazy, generous, ambitious, ...
- **Needs** (0–100): hunger, energy, safety, money, social, fun, health.
- **Schedule:** default activities by hour (sleep, eat, work, socialize).
- **Goals:** long-term wishes (buy a house, open a shop) that bias choices. Later phases.

When an NPC is free to choose, it scores each possible activity:

```
score = schedule fit + need urgency + trait bias + goal bias + situation modifiers
```

…and picks the highest (ties broken by the seeded RNG). Examples:

- Hungry + has money + believes the shop has apples → "buy apples" scores high.
- Shop had no apples last time (a belief!) → score for that shop drops; another shop or another food may win.
- Believes there's a thief around + cautious trait → stays home in the evening.

**Key rule:** scoring uses the NPC's **beliefs**, not world truth. An NPC goes to the shop because it *thinks* there are apples. Only when it arrives does it find out (and learn something new).

---

## 6. Perception and beliefs

When a world event happens, each NPC nearby gets a chance to notice it:

```
notice chance = visibility × distance factor × lighting (day/night) × attention (busy? asleep?) × trait (curious, cautious)
```

Rolled with the seeded RNG. Noticing creates an **observation**, which becomes a **belief**:

```
Belief
  Subject       (e.g. "apples missing from Mira's shop", "player was near the shop at 21:00")
  Claim         (structured, not free text)
  Source        (SawIt, ToldBy(NpcId), Inferred)
  Confidence    (0–100)
  Time learned
```

**Inference.** NPCs can draw simple conclusions:

- The shopkeeper counts stock (a `StockCounted` event) and finds 14 apples where the books say 20 → belief: "6 apples missing" (Inferred, high confidence). Not "the player stole them".
- The shopkeeper also believes "the player was in the shop yesterday" (from a witness rumor) → may form a suspicion: "the player might have taken them" (Inferred, low or medium confidence, rises with more evidence).

**Truth and knowledge stay separate.** The theft event says *the player stole 6 apples*. Only a witness who noticed it believes that, and maybe only partly ("someone took something from the apple stall").

---

## 7. Conversations and rumors

When NPCs are together (tavern, market, home, work), each tick there's a chance they talk. A conversation may pass beliefs:

- **What gets shared:** interesting, recent, important beliefs first (theft > weather).
- **Trust filter:** the listener's confidence = speaker's confidence × how much the listener trusts the speaker.
- **Distortion:** details may get vaguer or wrong ("6 apples" → "a lot of apples"; "the player" → "a stranger").
- **Source chain:** the listener's belief has `Source = ToldBy(speaker)`, so we can later explain "how did the guard know?"

Guards act on a suspicion only above a confidence threshold, so evidence must build up.

---

## 8. Memory

Memories are what an NPC remembers about events and people. They feed dialogue and relationships.

| Importance | Example | Fades in roughly |
|---|---|---|
| Minor | Player bought bread | days |
| Notable | Player was rude | weeks |
| Important | Player saved my daughter | years |
| Major | Player killed my brother | never (in practice) |

Memory strength decays each day based on importance; strong emotions and repeated events reinforce it. When strength reaches zero, the memory is forgotten (but relationship changes it caused remain).

---

## 9. Economy

- **Item types** have base value, perishability (apples spoil), category (food, material, tool).
- **Stock** is counted per owner and item type: `MiraShop: apple × 14`.
- **Buying:** a buyer wants N; if stock < N, they buy what's available or nothing (based on their decision), and a `FailedPurchase` or `PartialPurchase` event is recorded. That event is perceivable too ("the shop has run out").
- **Restocking:** shopkeepers order from producers (Farm) when stock is low or demand was missed. Delivery takes time.
- **Prices** move with recent demand vs. stock: missed sales push prices up; unsold stock pushes them down. Bounded per day so prices don't swing wildly.
- **Money conservation:** money moves between wallets. It only enters the world through defined sources (e.g. traveling buyers, wages from outside) and leaves through sinks (taxes, imports). Every transfer is an event. A test checks the total stays correct.

---

## 10. Saving

The whole `WorldState` (including RNG state and the event log) serializes to JSON with a `saveVersion`. Loading must restore an identical world: `run(1000)` must equal `save → load → run(1000)`.

---

## 11. Unity side

- **Bridge (`Assets/_Game/Bridge`):** `WorldRunner` MonoBehaviour owns the `WorldState`, ticks it at the chosen time scale, and publishes changes (NPC moved, activity changed, stock changed) for visuals and UI.
- **Commands:** player actions (`BuyCommand`, `StealCommand`, `TalkCommand`, `GiveCommand`) go into a queue processed at the start of the next tick. This keeps the simulation deterministic and makes the player follow the same rules as NPCs.
- **NPC visuals:** each NPC has a prefab (Kenney MiniCharacters) that walks via NavMesh to where the simulation says it is, and plays the animation matching its activity.
- **Level of detail (later):** NPCs near the player are simulated every tick with full detail; far-away areas run cheaper, coarser updates and catch up on demand.

---

## 12. Dialogue AI (Phase 8)

When the player talks to an NPC, the game builds a **fact sheet** from simulation data: who the NPC is, their mood, what they believe and remember about the player, recent local news they know. An AI model may turn that into natural speech. It may not add facts, change state, or reveal anything the NPC doesn't believe. Until then, dialogue uses templates filled from the same fact sheet.

---

## 13. Decision log

Add new entries at the bottom: date, decision, reason.

| Date | Decision | Reason |
|---|---|---|
| 2026-10-02 | Unity 6 + URP, targets iPad/iPhone/Mac | Best fit for a 3D, animation-heavy life sim on all three; strong AI-agent familiarity with C#/Unity |
| 2026-10-02 | Simulation as pure C# local package, tested with `dotnet test` | Fast, engine-independent tests agents can run without Unity |
| 2026-10-02 | 1 tick = 1 game minute; integer money; single seeded RNG | Determinism and reproducible tests |
| 2026-10-02 | Kenney low-poly as the main art style | Most complete free (CC0) coverage, including animated villagers |
| 2026-10-02 | Order: Design Phase → simulation → Unity last | Human wants to design the world, characters and money first; Unity only at the end |
| 2026-10-02 | Game data as JSON in `Content/` at the repository root | Designed before Unity exists; the simulation and later Unity both read it |
| 2026-10-02 | Built by AI agents (Muse "Geltrax" + sub-agents), human tests on device | Workflow defined in `AGENTS.md` |
| 2026-10-03 | P1-01: content IDs are readonly typed string values with ordinal comparison; default IDs are invalid | Preserve approved JSON keys, prevent mixed ID types, and keep ordering independent of locale |
| 2026-10-03 | P1-01: SplitMix64 with one unsigned 64-bit saved state and rejection-sampled bounded draws | Stable reference vectors, reproducible save/resume and unbiased choices without engine dependencies; not a security RNG |
| 2026-10-03 | P1-01: GameTime uses checked nonnegative long minutes, with day 1 beginning at minute 0 | Avoid silent overflow and make human-readable day/hour/minute reporting unambiguous |
| 2026-10-03 | P1-02: World captures system keys at registration, orders by phase then ordinal ID, and closes registration at the first tick | Same systems produce identical execution order regardless of setup order, without per-tick sorting |
| 2026-10-03 | P1-02: advance the clock before Commands, then Needs → Decisions → Actions → Perception → Social → Economy → Memory | Commands belong to the new minute, before NPC decisions and perception |
| 2026-10-03 | P1-02: a failed tick permanently faults its World runner; no rollback or automatic retry | Earlier systems may have changed state; replaying would silently duplicate actions. Reload a known-good save when persistence exists |
| 2026-10-03 | P1-03: per-world immutable event records, positive sequential IDs and nondecreasing timestamps survive pruning | Stable truth references and repeatable ordering; player and NPC actor IDs remain distinct |
| 2026-10-03 | P1-03: queries return immutable snapshots; pruning requires explicit retained event IDs | Future beliefs/memories must protect referenced truth; no automatic pruning or knowledge inference is introduced |
| 2026-10-03 | P1-04: command eligibility is captured as current minute + 1; FIFO processing uses an explicitly registered CommandSystem | Input submitted during any phase cannot execute in that same minute; dequeue before execution prevents accidental retry after a partial failure |
| 2026-10-03 | P1-05a: immutable location map precomputes shortest integer durations over approved undirected travel links | Use Content/world/locations.json values without rebalancing; same-location travel is zero, disconnected pairs have no route. JSON loading currently exists only in tests |
| 2026-10-03 | P1-05b: travel stores absolute arrival minutes and removes endpoint presence while in transit; explicitly register TravelSystem in Actions | In-tick departures cannot gain a free minute; future perception must not treat a travelling NPC as present at either endpoint |
| 2026-10-03 | P1-05b: append departure/arrival truth before changing travel state, process simultaneous arrivals by ordinal NPC ID | Failed event validation leaves that move unchanged; overdue arrivals are recorded at processing time, not backdated |
| 2026-10-03 | P1-06: bulk inventory counts are catalog-validated, unbounded by UI stack size, and transfers are atomic | Prevent negative or partially moved stock while preserving exact item totals for later trade and theft systems |
| 2026-10-03 | P1-09: NPC need state stores integer sixtieths; callers supply starting levels, and sleeping only pauses awake drain | Preserve exact per-hour content rates without fractional loss and avoid inventing unapproved starting or sleep-recovery tuning |
| 2026-10-03 | P1-11: belief identity is the structured claim; provenance, confidence and learned time are explicit replacement data | Keep one deterministic belief per claim without automatic confidence amplification, while retaining referenced truth events for safe pruning |
| 2026-10-03 | P1-19: human-readable reports live in simulation test tools and label output as world truth | Give scenario tests a deterministic narrative view without introducing presentation concerns or NPC omniscience into runtime code |
| 2026-10-03 | P1-07: wallets and shops are caller-owned state; purchases preflight every mutation and partial fulfillment must be explicitly requested | Conserve integer copper and item totals, and make failed transactions atomic without silently changing an NPC's buying policy |
| 2026-10-03 | P1-10: approved weekly schedules are defaults; utility tuning and external eligibility context are caller-supplied, and decisions record intentions only | Keep schedules interruptible and deterministic without inventing unapproved balancing values or letting decision logic mutate world truth directly |
| 2026-10-03 | P1-17: memory decay advances in whole elapsed days, major memories retain strength in the prototype, and event references are retained as a union with beliefs | Make decay independent of tick batching, avoid overflow at extreme times, and keep truth events reachable while any knowledge record cites them |
| 2026-10-03 | P1-08: theft is an immutable queued command that preflights an exact inventory transfer, records truth, and never creates knowledge or moves money | Keep the player under the same deterministic command rules as NPCs while leaving witness and suspicion decisions to later systems |
| 2026-10-03 | P1-12: perception processes each supported truth event once in event-ID order and observers in ordinal NPC order, using caller-owned presence/awake context and notice tuning | Prevent omniscience and hidden defaults while making seeded observation reproducible and traceable to its truth event |
| 2026-10-03 | P1-13: decision contexts may contain an immutable owner-labelled belief snapshot; exact-claim thresholds and utility penalties are caller configured | Ensure an NPC reacts only to its own knowledge without reading global truth or silently choosing production balancing values |
| 2026-10-04 | P1-14: a physical count replaces the owner's current missing-stock belief for that item/location and never identifies a culprit | Prevent contradictory deficit beliefs while keeping count-derived knowledge separate from theft truth |
| 2026-10-04 | P1-15: rumor exchange uses caller-configured trust, plausibility, salience and seeded safe-distortion policy with traceable nonrepeating source chains | Support approved social formulas without inventing an exact mutation transformation or allowing facts to teleport |
| 2026-10-04 | P1-16: production, pending restock orders and price progress live in caller-owned restorable state; prices use explicit integer bands/steps/bounds | Keep systems as logic, make all economic progress saveable, conserve wholesale goods/copper and avoid rounding-dependent hidden defaults |
| 2026-10-04 | P1-18: suspicion evaluates an explicit suspect using only one guard's beliefs and caller evidence policy; group reputation is bounded 0–100 with actual deltas logged as truth | Enforce evidence-first law generically and make every standing change deterministic, explainable and saveable |
| 2026-10-04 | P1-20: the Apple Test uses fixed scenario inputs with a seeded 50% witness branch, zero rumor mutation, caller-owned progress and a separate human story report | Prove both witnessed and unwitnessed emergent outcomes without putting scenario narration or hard-coded consequences into runtime systems |
| 2026-10-04 | Content fix: add item_cloth_local to general store sells in locations.json | Match approved economy.json and design (Tilda retails local cloth); human-approved |
| 2026-10-04 | Content fix: flip 7 cooked-food hunger values to negative; add NoFoodItemHasPositiveHungerEffect test | ITEMS.md convention: eating lowers hunger (negative restores); positive values restored nothing due to runtime clamp |
| 2026-10-04 | P3-01: skill levels 1-5, thresholds 20/100/300/700/1500, daily cap 20, teaching x2, qualityBonus 0-4, no decay | Per SKILLS.md: practice and teaching only, never XP-from-killing; cozy pacing |
| 2026-10-04 | P3-02: recipe failure = clamp((difficulty - level + 1) x 10, 0, 40)%; quality = clamp(50 + bonus); permission = trust >= 40 | Per RECIPES.md formulas; trust>=40 is a simplification, real persuasion is future work |
| 2026-10-04 | P3-02: waste is all-or-nothing (failure loses 100%, success wastes 0%) not partial 30% | Simplification; inventories don't track per-unit waste yet |
| 2026-10-04 | P3-03: meal happiness = round((Q-50)/25); tavern popularity 0-100 starts 50; ingredient demand +1 per input, halves daily | Per SKILLS.md "small daily happiness that compounds"; popularity decays toward 50 |
| 2026-10-04 | P3-03: added WorldEventType.MealEaten (one enum line in Core) | No existing event type fit; persistence unaffected |
| 2026-10-04 | P3-04: save format v3 (skills, happiness, tavern popularity, ingredient demand); v2/v1 load with defaults | All Phase 3 state must survive save/load |
| 2026-10-04 | P3-04: meal happiness recalibrated (51+ -> +1, 85+ -> +2) because ordinary cooking maxes at quality 54 | Old formula made happiness unreachable; WORLD.md promises "small daily happiness" |
| 2026-10-04 | P3-04: NpcState.Restore no longer requires sleeping-flag/intention agreement | NeedsSystem drives IsSleeping from schedule; intentions unwired; flag recomputed each tick |
| 2026-10-04 | P4-01: 5 species (chicken, pig/boar, deer, wolf, brambleback); trust 0-100; domestic start 20-30, wild start 0 | Per ANIMALS.md; 6 forest pigs treated as wild boars (never tameable) |
| 2026-10-04 | P4-02: wolves hunt deer daily (higher in winter); 2-4 livestock losses per winter; spring breeding with population caps | Per WORLD.md load-bearing 2-4 winter incidents; no scripted triggers |
| 2026-10-04 | P4-02: egg production 0.7/day warm months, 0.2/day winter | Heritage birds, not modern layers (ANIMALS.md) |
| 2026-10-04 | P4-03: taming trust gains per species; one meaningful gain per day; bond thresholds 60/70/80/85; wolf needs council approval | Per ANIMALS.md and SKILLS.md; wild boars and stags never tameable |
| 2026-10-04 | P4-03: AnimalState.Owner changed from NpcId? to ActorId? (player can bond animals) | Player is an actor, not an NPC |
| 2026-10-04 | P4-03: taming skill multiplies trust gains (L2 x1.5, L3 x2) | Per SKILLS.md |
| 2026-10-04 | P4-04: save format v4 (animals + ecosystem cursors); v1-v3 load with empty animal store | Matches world built without animal-population step |
| 2026-10-04 | P4-04: added WorldEventType.Bonded/BondBroken (not yet emitted by TamingSystem) | P3-03 MealEaten precedent; payload design deferred |
| 2026-10-04 | P5-01: 11 town stats computed monthly from world state (never set by hand) | Per TOWN.md; stat interactions emerge from formulas, not hardcoded rules |
| 2026-10-04 | P5-02: in-migration (happiness>=65, employment>=80, food>=50, housing>=70; 3-5/season); out-migration (spring, age 15-25, ambition>=70, 30% chance) | Per TOWN.md; decline spiral emerges from stat formulas |
| 2026-10-04 | P5-03: 10 emergent events fire only when conditions met; chaining is emergent (conditions, not hardcoded) | Per TOWN.md; weather not tracked so wolf attack/fire/drought use seasonal proxies |
| 2026-10-04 | P5-03: added WorldEventType.EmergentEventFired/Ended | P3-03 precedent; enables perception/memory/rumor of events |
| 2026-10-04 | P5-04: save format v5 (town stats, migration, emergent events); v1-v4 load with town defaults | All Phase 5 state must survive save/load |
| 2026-10-04 | P5-04: 270-day acceptance (not 365) due to runtime; 60-day save/load determinism split from full run | Winter/spring boundary divergence isolated to animal-system interaction (out of scope) |
| 2026-10-04 | P6-01: Village LOD (Full/Abstract); Millbrook full, King's Rest (2 days) and Oakhollow (1 day, invented) abstract | King's Rest from WORLD.md; Oakhollow is an invented hamlet name |
| 2026-10-04 | P6-01: abstract villages drift daily (population ±1, wealth ±10, seasonal food, mood toward 50) | Placeholder until P6-02 wires real trade |
| 2026-10-04 | P6-02: trade routes with goods, prices, travel time; merchants buy low/sell high | Price differences drive trade; abstract-to-abstract proven |
| 2026-10-04 | P6-03: news travels between villages (delayed by travel time, 20% distortion ±20 severity) | News from EmergentEventFired; mood ±(1+severity/25), opinion shifts |
| 2026-10-04 | P6-04: save format v6 (villages, trade ledger, news); v1-v5 load with village defaults | Trade routes are static config (not persisted); only mutable ledger is saved |
| 2026-10-04 | P7-01: LifeStage (Child 0-14, Adult 15-59, Elder 60+); mutable NPC age, IsDeceased; deterministic birthday from NPC ID (day 1-360, 360-day year); old-age death 5%/15%/40% | Deceased NPCs remain registered for inheritance |
| 2026-10-04 | P7-01: added WorldEventType.Death | P3-03 precedent; ratification needed |
| 2026-10-04 | P7-02: parent/child links (MotherId/FatherId set-once), PartnerId, ChildrenIds, HouseholdId; HouseholdRegistry; FamilyState (LastFamilyDay, BirthsSoFar); baby IDs npc_born_<n> | Children inherit parents' household; 15-year-olds may leave (50% seeded roll) |
| 2026-10-04 | P7-02: one birth roll per household per year (10% seeded); eligible couple = first childbearing-age woman (18-45) with eligible partner; opposite-gender couples only | Simplification; documented in code |
| 2026-10-04 | P7-02: added WorldEventType.Birth | P3-03 precedent; ratification needed |
| 2026-10-04 | P7-03: inheritance priority designated-heir → living spouse → living children (eldest first, split equally, remainder to eldest) → living parents (mother, father) → village fund | Items go to primary heir as one bundle; under-15 shares held by guardian |
| 2026-10-04 | P7-03: no-heir money to fund, items to household eldest or unclaimed (never destroyed/sold — selling would invent money) | Money conservation is the invariant |
| 2026-10-04 | P7-03: added WorldEventType.Inheritance; InheritanceSystem (agents.inheritance, Actions phase, after AgingSystem) | P3-03 precedent; ratification needed |
| 2026-10-04 | P7-04: save format v7 (all Phase 7 state: aging, family, households, inheritance, NPC age/deceased/family fields); v1-v6 load with generation defaults | Born NPCs get inline detail block in save (no Content entry) |
| 2026-10-04 | P7-04: event-log actor validation now Content ∪ save-NPCs (was Content-only) | Birth events list newborns as actors; required for save/load |
| 2026-10-04 | P7-04: 5-year acceptance (1800 days, seed 42): 1 birth (day 720), 1 death (day 771, Elswith at 74), 1 inheritance, money conserved (1800 copper); save-at-day-720 → load → continue byte-identical | Proves generations turn over deterministically |
| 2026-10-04 | P8-01: FactSheet (plain data: identity, MoodBand, beliefs with SourceKind, memories, news, household members); FactSheetBuilder (static, pure, reads only knowledge stores, never world truth) | Dialogue phrases beliefs, not truth; degrades gracefully |
| 2026-10-04 | P8-02: IPhrasingEngine interface (contract for future AI adapter); TemplatePhrasingEngine (seeded, deterministic); 6 DialogueIntents; templates vary by MoodBand and SourceKind (seen/told/inferred hedges) | Same (sheet, intent, seed) → same output; never invents facts |
| 2026-10-04 | P8-03: DialogueSession holds only sheet + engine (no WorldState reference) — enforcement by construction; reflection test locks the design | Dialogue physically cannot reach mutable state |
| 2026-10-04 | P8-04: Phase 8 acceptance (4 NPCs, distinct moods/beliefs/news); knowledge/truth split demonstrated; save/load doesn't change utterances | No runtime changes needed; P8-01/02/03 APIs sufficient |

| 2026-10-04 | Owner authorized Unity; first slice is Apple shop and village | Establish the real simulation-to-Unity boundary before the full playable village. |
| 2026-10-04 | Ship System.Text.Json 8.0.6 and its non-platform runtime dependencies with simulation package, retaining MIT licenses | Unity lacks the serializer used by existing simulation; preserve schema and behavior instead of rewriting persistence. Orchestrator approved; IL2CPP remains to verify. |
