# Save format schema (formatVersion 6)

Written by `WorldSaver.Save(WorldState)` → indented JSON string.
Read by the loader. Top-level properties always appear in this order.

Version 2 (P2-12) adds every Phase 2 state section: relationships,
attributed memories, the eleven economy progress states, and per-lot
inventory ages. Version 3 (P3-04) adds every Phase 3 state section: skill
stores (player + NPCs), NPC happiness, tavern popularity and ingredient
demand. Version 4 (P4-04) adds every Phase 4 state section: the animal
store and the four ecosystem cursors (predation, breeding, winter pressure,
egg production). Version 5 (P5-04) adds every Phase 5 state section: town
stats, migration and emergent events. Version 6 (P6-04) adds every Phase 6
state section: the village registry, the trade-route ledger, and the
inter-village news store. Older documents still load (see Compatibility
below).

## Conventions

- **Definitions by ID only.** NPC definitions, item types, locations, schedules and
  the item catalog are referenced by their content ID strings (e.g.
  `"npc_mira_holt"`, `"item_apple"`, `"loc_apple_stall"`) and reloaded from
  approved `Content/` data at load time. They are never duplicated into the save.
- **Actors** are encoded as strings: `"player"` for the player,
  `"npc:<npcId>"` for an NPC (e.g. `"npc:npc_mira_holt"`).
- **Enums** (`WorldEventType`, `EventVisibility`, `BeliefClaimKind`,
  `BeliefSourceKind`, `ActivityKind`) are written as their C# names
  (`"Purchase"`, `"Normal"`, `"StockMissing"`, `"Inferred"`, `"Work"`).
- **Times** are integer game minutes (`GameTime.TotalMinutes`), written as JSON
  numbers. The clock (`"clock"`) is minutes since world start.
- **RNG** (`"rngState"`) is the `SimRng.State` ulong.
- **Nulls are explicit**: nullable fields are always present, with a JSON null
  when absent (never omitted).
- **Deterministic ordering**: every array is already in deterministic order
  (ordinal content-ID order, event ID order, FIFO command order, claim order for
  beliefs/memories). Property order inside each object is fixed.

## Sections

```jsonc
{
  "formatVersion": 4,
  "clock": 1400,
  "rngState": 12345678901234567890,
  "eventLog": {
    "lastIssuedId": 42,          // high-water mark; survives pruning
    "lastIssuedTime": 1399,      // high-water mark; survives pruning
    "events": [ ... ]            // retained events in ID order; may be pruned
  },
  "pendingCommands": [
    { "eligibleMinute": 1401, "command": { "type": "TheftCommand", "payload": { ... } } }
  ],
  "npcs": [
    {
      "definitionId": "npc_mira_holt",   // reload definition from Content/
      "needs": { "hungerSixtieths": 3061, "energySixtieths": 4200, "socialSixtieths": 5100 },
      "isSleeping": false,               // agrees with intention (Sleep ⇒ sleeping)
      "happiness": 52,                   // v3: mood 0-100 (50 neutral); absent before v3
      "age": 34,                         // v7: years lived; the Content age before v7
      "isDeceased": false,               // v7: true once dead of old age; absent before v7
      // "born": { ... }                 // v7: only for NPCs born during the simulation
                                         // (no Content entry): name, gender, home,
                                         // needRates {hungerPerHour, energyPerHour, socialPerHour}
      "mother": "npc_sella_wren",        // v7: nullable NPC IDs; null/absent before v7
      "father": null,                    // v7
      "partner": "npc_bram_stone",        // v7
      "children": ["npc_tansy_alder"],   // v7: in link order
      "household": "household_loc_home_miller",  // v7: nullable; null/absent before v7
      "designatedHeir": null,            // v7: the will's named heir
      "intention": { "kind": "Work", "destination": "loc_apple_stall", "chosenAt": 1380 }
        // or null when the NPC has no current intention
    }
  ],
  "beliefs": {
    "npc_mira_holt": [                   // one array per registered belief store (may be empty)
      {
        "claim": {
          "kind": "StockMissing",
          "location": "loc_apple_stall",
          "itemType": "item_apple",      // null unless a stock claim
          "subject": null,               // actor string; null unless a presence/theft claim
          "quantity": 6                  // null unless a stock/theft claim
        },
        "source": {
          "kind": "Inferred",            // Seen | ToldBy | Inferred
          "speaker": null,               // actor string; present only for ToldBy
          "originEventId": 12,           // null when no truth reference
          "sourceChain": []              // rumor chain, NPC IDs in order; empty when none
        },
        "confidence": 70,                // 0–100
        "learnedAt": 1390
      }
    ]
  },
  "memories": {
    "npc_mira_holt": [                   // one array per registered memory store (may be empty)
      {
        "claim": { ... },                // same shape as a belief claim
        "importance": 80,                // 0–100
        "formedAt": 1380,
        "lastReinforcedAt": 1390,
        "strength": 72,                  // 0–importance; decays over time
        "originEventId": 12              // null when none
      }
    ]
  },
  "perceptionCursor": 42,                // last world event ID perception processed
  "shops": [
    {
      "location": "loc_apple_stall",
      "owner": "npc_mira_holt",
      "stock": { "item_apple": 9, "item_bread": 4 },  // item ID → count, ordinal order
      "lots": [                                      // per-lot ages, FIFO order; always present in v2
        { "item": "item_apple", "quantity": 5, "ageDays": 2 },
        { "item": "item_apple", "quantity": 4, "ageDays": 0 }
      ],
      // "lots" is absent in v1 documents: the loader builds age-0 lots from "stock".
      // Lot quantities must sum to the "stock" counts (contradiction → LoadException).
      "ownerCopper": 31,
      "ownerWalletShared": true,   // v2: true if the till is the owner's personal wallet
                                   // (one shared object); absent in v1 (defaults false).
      "prices": { "item_apple": 3 }                   // item ID → unit price, ordinal order
    }
  ],
  "belongings": [
    {
      "owner": "player",                 // actor string
      "inventory": { "item_apple": 6 },  // item ID → count, ordinal order
      "lots": [                          // same per-lot contract as shops
        { "item": "item_apple", "quantity": 6, "ageDays": 1 }
      ],
      "copper": 17
    }
  ],
  "production": { "completedIds": ["farm_apples"] },  // ordinal order
  "restock": {
    "triggeredIds": ["stall_restock"],                // ordinal order
    "pendingOrders": [
      { "configurationId": "stall_order", "requestedAt": 1300 }
    ]
  },
  "prices": {
    "progress": [
      {
        "configurationId": "stall_prices",
        "lastProcessedEventId": 40,
        "completedInterval": 2,
        "missedSalePending": true
      }
    ]
  },
  "reputation": {                        // null when reputation was never initialized
    "standings": [ { "group": "group_townsfolk", "value": 45 } ]  // group order
  },
  "travel": null,                        // null when travel was never initialized
  // otherwise:
  // "travel": {
  //   "npcs": [
  //     {
  //       "id": "npc_tom_fenn",
  //       "currentLocation": "loc_square",   // null while travelling
  //       "journey": { "origin": "loc_farm", "destination": "loc_square", "arrival": 1420 }
  //         // or null when not travelling
  //     }
  //   ]
  // }
  "relationships": {
    "pairs": [                           // from → to, ordinal by (from, to)
      {
        "from": "npc_mira_holt",
        "to": "npc_ralf_hale",
        "trust": 73,                     // 0–100
        "affection": 61,                 // 0–100
        "reason": "Steady trade"
      }
    ],
    "baselines": [                       // drift targets; same shape as pairs
      { "from": "npc_mira_holt", "to": "npc_ralf_hale", "trust": 50, "affection": 50, "reason": "Strangers" }
    ],
    "dynamicsCursor": 42,                // event ID the dynamics system has processed to
    "dynamicsDay": 3,                    // day the decay pass last ran
    "recallCursor": 41                   // event ID the recall pass has processed to
  },
  "attributedMemories": [                // claim order
    {
      "owner": "npc_mira_holt",
      "claim": { "kind": "WrongedBy", "location": "loc_apple_stall", "itemType": null,
                 "subject": "npc:npc_ralf_hale", "quantity": null },
      "originalTrustDelta": -4,
      "originalAffectionDelta": 0,
      "recalledTrustDelta": -2,          // |recalled| ≤ |original| enforced at load
      "recalledAffectionDelta": 0
    }
  ],
  "smithy": { "ironExhaustionOrdered": true },
  "merchantSchedule": { "initialized": true, "nextVisitDay": 25 },
  "travelerSpend": {
    "initialized": true, "lastPayoutDay": 10, "monthIndex": 2,
    "paidThisMonth": [100, 200]
  },
  "wolfBounty": { "initialized": true, "winterYear": 1, "bountyDays": [5, 10] },
  "villageFund": {
    "initialized": true, "fundsCopper": 600,
    "lastLevyDay": 7, "lastWageDay": 7, "lastRetainerDay": 1
  },
  "harvest": { "initialized": true, "lastWageDay": 8 },
  "tax": { "initialized": true, "lastCollectionDay": 15 },
  "communityFund": {
    "initialized": true, "communityCopper": 150, "feastCopper": 200,
    "lastMonthlyDay": 30, "lastFeastYear": 2
  },
  "economyBaseline": { "initialized": true, "baselineCopper": 9000 },
  "spoilage": { "initialized": true, "lastAgedDay": 7 },
  "debtLedger": {                        // null when the ledger was never initialized
    "initialized": true,
    "lastFeastYear": 1,
    "debts": [                           // open + closed, ordinal by (debtor, creditor)
      {
        "debtor": "npc_doran_kettle",
        "creditor": "npc_tilda_bray",
        "owedCopper": 100,
        "openedDay": 1,
        "lastPaymentDay": 8,
        "lastWeeklyDay": 8,
        "overdueDeclared": false,
        "terms": {
          "copperPerWeek": 10,
          "itemPerWeek": null,           // item ID string when an in-kind schedule
          "itemsPerWeek": 0,
          "itemCreditCopper": 0,
          "payChancePercent": 100,
          "overdueAfterDays": 60
        }
      }
    ]
  },
  "skills": {                              // v3: one array per actor WITH skills
    "player": [                            // "player" or "npc:<id>"; empty stores omitted
      {
        "skill": "skill_cooking",           // approved skill ID from Content/skills/
        "level": 1,                        // 0-5, never below what the points grant
        "practicePoints": 14,              // cumulative, never decreases
        "dailyPoints": 2,                  // 0-20, accrued on lastPracticeDay
        "lastPracticeDay": 6               // -1 before any practice
      }
    ],
    "npc:npc_bessa_marlowe": [
      { "skill": "skill_cooking", "level": 3, "practicePoints": 14, "dailyPoints": 2, "lastPracticeDay": 6 }
    ]
  },
  "tavernPopularity": {                    // v3: 0-100 renown + decay cursors
    "popularity": 64,
    "lastSkilledCookDay": 6,               // -1 when no skilled cook ever worked
    "lastDecayDay": 6                      // -1 before the decay system first ran
  },
  "ingredientDemand": {                     // v3: decay cursor + positive demand only
    "lastDecayDay": 6,
    "demand": { "item_firewood": 2, "item_fish": 2 }   // ordinal item order
  },
  "animals": [                            // v4: AnimalId order (AnimalStore.Capture)
    {
      "id": "animal_chicken_001",
      "species": "species_chicken",        // approved Content/animals/species.json
      "location": "loc_farm",
      "trust": 77,                        // 0-100
      "owner": "player",                  // actor string; null when wild/unbonded
      "age": "Adult",                     // "Young" or "Adult"
      "health": 100,                      // 0-100
      "lastInteractionDay": 52            // -1 before any taming interaction
    }
  ],
  "predation": {                           // v4: wolf-hunt cursor
    "initialized": true,
    "lastHuntDay": 90
  },
  "breeding": {                            // v4: spring-breeding cursors
    "initialized": true,
    "lastBreedingDay": 90,
    "lastBreedingYear": -1,                // -1 before the first spring breeding
    "nextBirthOrdinal": 1                  // birth IDs stay unique across save/load
  },
  "winterPressure": {                      // v4: winter livestock-loss state
    "initialized": true,
    "lastLossDay": 90,
    "lastWinterYear": -1,                  // -1 before the first winter roll
    "incidentDays": [104, 127],            // still outstanding, strictly increasing
    "deerAtFarms": false
  },
  "eggProduction": {                       // v4: laying cursor
    "initialized": true,
    "lastLayDay": 90
  },
  // NOTE: the coop inventories the eggs land in are caller-owned EggConfiguration
  // state, not world state — laid eggs are not in the save document. A load
  // resumes laying (the Produced truth events are in the event log) but the
  // basket starts empty.
  "townStats": {                           // v5: last monthly town-stats computation
    "computedMonth": 11,                   // absolute 30-day month index; -1 = never computed
    "values": {                            // null when never computed
      "population": 120,                   // simulated NPCs + 100 background
      "wealthCopper": 11000,
      "foodSupply": 35, "safety": 65, "housing": 85, "employment": 90,
      "trade": 75, "happiness": 70, "crime": 2,
      "infrastructure": 57, "reputation": 65
    }
  },
  "migration": {                           // v5: growth/decline state
    "additionalVillagers": 0,              // background villagers added by in-migration
    "additionalHouseholds": 0,
    "additionalSoundRoofs": 0,
    "lastInMigrationSeason": -1,           // -1 = never checked
    "lastOutMigrationYear": -1             // -1 = never checked
  },
  "emergentEvents": {                      // v5: active town events
    "activeEvents": [                      // ordinal event-ID order
      { "id": "event_merchant_arrival", "startedDay": 200 }
    ],
    "lastMerchantDay": 200                  // -1 = none yet
  },
  "villages": {                            // v6: village registry (LOD state)
    "lastDriftDay": 30,                    // 0 = never drifted
    "villages": [                          // ordinal VillageId order
      {
        "id": "village_kings_rest",
        "name": "King's Rest",
        "lod": "Abstract",                 // "Full" or "Abstract"
        "anchorLocation": "loc_square",
        "travelDaysFromMillbrook": 2,
        "population": 4997,
        "wealthCopper": 199810,
        "foodSupply": 90,                  // 0-100
        "mood": 50                         // 0-100
      }
    ]
  },
  "tradeLedger": {                         // v6: merchant journeys and cursors
    "lastProcessedDay": 30,                // 0 = never ticked
    "nextDepartureDay": 36,                // 1 = first departure pending
    "journeys": [                          // departure order
      {
        "routeId": "route_kings_rest_oakhollow",
        "cargo": [                         // non-empty
          { "item": "item_cloth_imported", "units": 7 }
        ],
        "departureDay": 1,
        "arrivalDay": 4,                   // > departureDay
        "boughtCopper": 175,
        "soldCopper": 280,
        "isComplete": true
      }
    ]
  },
  "news": {                                // v6: inter-village news store
    "nextId": 3,                           // >= 1; every news ID is below this
    "lastProcessedEventId": 42,            // 0 = never scanned
    "lastDeliveryDay": 12,                 // 0 = never delivered
    "inTransit": [                         // publish order
      {
        "news": {
          "id": 1,
          "origin": "village_millbrook",
          "about": "village_millbrook",
          "kind": "WolfAttack",            // NewsKind C# name
          "dayCreated": 10,                // >= 1
          "severity": 80                   // 0-100
        },
        "from": "village_millbrook",
        "to": "village_kings_rest",        // != from
        "arrivalDay": 12                   // >= dayCreated
      }
    ],
    "arrived": [                           // delivery order
      {
        "news": { "id": 2, "origin": "village_millbrook", "about": "village_millbrook",
                  "kind": "WolfAttack", "dayCreated": 10, "severity": 60 },
        "deliveredTo": "village_oakhollow"
      }
    ],
    "opinions": [                          // one per village pair, 0-100
      { "from": "village_kings_rest", "to": "village_millbrook", "opinion": 48 }
    ]
  },
  "aging": {                               // v7: aging cursor
    "initialized": true,
    "lastAgingDay": 720                    // >= 0
  },
  "family": {                              // v7: family cursor
    "initialized": true,
    "lastFamilyDay": 720,                  // >= 0
    "birthsSoFar": 3                       // keeps "npc_born_<n>" IDs unique; >= 0
  },
  "households": [                          // v7: ordinal HouseholdId order
    {
      "id": "household_loc_home_miller",
      "home": "loc_home_miller",
      "members": ["npc_garrick_alder", "npc_tansy_alder"]  // ordinal NpcId order
    }
  ],
  "inheritance": {                         // v7: inheritance cursor
    "initialized": true,
    "distributed": ["npc_bram_stone"]      // deceased NPCs already settled; ordinal order
  }
}
```

## Event entry

```jsonc
{
  "id": 12,
  "time": 1385,
  "location": "loc_apple_stall",
  "type": "Theft",
  "actor": "player",                    // actor string; null when no actor
  "targets": ["npc:npc_mira_holt"],     // actor strings; empty when none
  "visibility": "Normal",
  "itemType": "item_apple",            // null when not item-related
  "quantity": 6,                       // null when not applicable
  "copper": null,                      // null when no money moved
  "reputationGroup": null,             // set only for ReputationChanged events
  "reputationDelta": null              // set only for ReputationChanged events
}
```

## Pending command (TheftCommand — the only Runtime command type)

```jsonc
{
  "eligibleMinute": 1401,
  "command": {
    "type": "TheftCommand",
    "payload": {
      "location": "loc_apple_stall",
      "thief": "player",
      "sourceOwner": "npc:npc_mira_holt",
      "source": { "kind": "shopStock", "shop": "loc_apple_stall" },
      "destination": { "kind": "belongings", "owner": "player" },
      "item": "item_apple",
      "quantity": 6,
      "visibility": "Normal"
    }
  }
}
```

Inventory references resolve to the world-owned container holding the command's
`Inventory` object at save time:

- `"kind": "shopStock", "shop": "<locationId>"` — the shop's stock inventory.
- `"kind": "belongings", "owner": "<actor>"` — an actor's personal inventory.

Any other command type, or a command whose inventories are not registered as a
shop stock or an actor's belongings, makes saving fail with `SaveException`
(never a partial document).

## Loader notes

1. Validate `formatVersion` first; reject anything but `1`, `2`, `3` or `4`.
2. Build order suggestion: fresh `WorldState(savedRngState, new GameTime(savedClock))`
   (per the P1-21f brief, `SimRng(ulong)` resumes the stream exactly), load
   approved definitions by ID, restore event log (retained events + both
   high-water marks), command queue, NPCs (`NpcState.Restore` checks the
   sleeping/intention agreement; v3 also restores `happiness` via
   `RestoreHappiness`), beliefs, memories, perception cursor, shops,
   belongings, production/restock/price states, reputation (skip when null),
   travel (re-register NPCs and re-apply journeys when non-null),
   relationships + baselines + cursors, attributed memories, and the eleven
   economy progress states via their `Restore*` methods (skip when null for v1);
   v3 then restores skill stores (`SkillStore.Restore` for the player and every
   listed NPC), tavern popularity and ingredient demand (skip when null for
   v1/v2 — the fresh defaults apply); v4 then restores the animal store
   (`AnimalStore.Restore`; species validated against Content/animals, trust and
   health ranges enforced by `AnimalState.Restore`) and the four ecosystem
   cursors via their `Restore*` methods (skip when null for v1/v2/v3 — the
   fresh defaults apply: an empty store and uninitialized cursors); v5 then
   restores town stats (skip when null for v1-v4 — uncomputed), migration
   (`MigrationState.Restore`; skip when null for v1-v4 — empty) and emergent
   events (`EmergentEventState.Restore` with known-ID validation; skip when
   null for v1-v4 — no active events).
3. `AppleScenarioState` is harness-owned and intentionally not persisted.
4. Wallet aliasing: the save records per shop whether the till is the owner's
   personal wallet (one shared object) or a separate till
   (`"ownerWalletShared"`); the loader restores the same sharing because
   systems use `ReferenceEquals` to avoid double-counting. A shared till whose
   saved balances contradict is a `LoadException`. Version 1 documents have no
   marker and default to separate tills.

## Compatibility (v1 → v2 → v3 → v4 → v5 → v6 → v7)

Version 7 adds sections; it removes nothing. The loader accepts all seven:

- **v7 → v7**: every section restores via its `Capture`/`Restore` pair.
- **v6 → v7**: NPCs restore the Content age, alive, with no family links or
  households; aging, family and inheritance cursors restore uninitialized
  (systems stay quiet until a world-build step initializes them) and no
  estates are marked distributed. Rationale: a v6 save predates the systems
  that mutate generation state, so "never started" is the truthful restore —
  matching a world built before the generations phase.
- **v5 → v7**: as v6 → v7, plus the v5 → v6 rules below.
- **v4 → v7**: as v5 → v7, plus the v4 → v5 rules below.
- **v3 → v7**: as v4 → v7, plus the v3 → v4 rules below.
- **v2 → v7**: as v3 → v7, plus the v2 → v3 rules below.
- **v1 → v7**: as v2 → v7, plus the v1 → v2 rules below.

- **v6 → v6**: every section restores via its `Capture`/`Restore` pair.
- **v5 → v6**: the village registry restores empty (no villages), the trade
  ledger restores empty (no journeys, cursors at 0/1) and the news store
  restores empty (next ID 1, no transit/arrived/opinions, cursors at 0).
  Rationale: a v5 save predates the expanded-world systems, so "no villages
  yet" is the truthful restore — matching a world built before Phase 6.
- **v4 → v6**: as v5 → v6, plus the v4 → v5 rules below.
- **v3 → v6**: as v4 → v6, plus the v3 → v4 rules below.
- **v2 → v6**: as v3 → v6, plus the v2 → v3 rules below.
- **v1 → v6**: as v2 → v6, plus the v1 → v2 rules below.

- **v5 → v5**: every section restores via its `Capture`/`Restore` pair.
- **v4 → v5**: town stats restore uncomputed (month -1, no values), migration
  restores empty (no added villagers, cursors at -1) and no emergent events are
  active. Rationale: a v4 save predates the town systems, so "never computed"
  is the truthful restore — matching a world built before the town-development
  phase.
- **v3 → v5**: as v4 → v5, plus the v3 → v4 rules below.
- **v2 → v5**: as v3 → v5, plus the v2 → v3 rules below.
- **v1 → v5**: as v2 → v5, plus the v1 → v2 rules below.
- **v4 → v4**: every section restores via its `Capture`/`Restore` pair.
- **v3 → v4**: the animal store restores empty and the four ecosystem cursors
  restore uninitialized (systems stay quiet until a world-build step initializes
  them). Rationale: a v3 save predates the systems that mutate animal state, so
  "never started" is the truthful restore — matching a world built without the
  animal-population step.
- **v2 → v4**: as v3 → v4, plus the v2 → v3 rules below.
- **v1 → v4**: as v2 → v4, plus the v1 → v2 rules below.
- **v2 → v3**: skill stores restore empty, NPC happiness restores neutral (50),
  tavern popularity restores to its 50 baseline and ingredient demand to empty,
  all with cursors at -1 (never run). Rationale: every Phase 3 state has a
  safe uninitialized default — a v2 save predates the systems that mutate it,
  so "never started" is the truthful restore.
- **v1 → v2**: the eleven economy states, relationships, and attributed memories
  keep their fresh-world defaults (uninitialized, empty, cursors at zero); shop
  and belongings lots are rebuilt as single age-0 lots from the saved counts.
  Rationale: every Phase 2 state has a safe uninitialized default — a v1 save
  predates the systems that mutate it, so "never started" is the truthful
  restore.
- **v5+**: rejected with `LoadException`. Versions below 1 are rejected too.

Corruption checks new in v2: lot quantities must sum to the saved counts;
attributed-memory recall deltas must satisfy |recalled| ≤ |original| per axis
(the runtime invariant, enforced at load). Either violation is a `LoadException`
and the world is discarded — the loader never returns a half-built world.

Corruption checks new in v4: animal IDs must be unique; every animal species
must be an approved species ID; trust and health must be 0-100 (enforced by
`AnimalState.Restore`); winter incident days must be strictly increasing.
