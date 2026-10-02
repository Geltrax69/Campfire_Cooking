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
