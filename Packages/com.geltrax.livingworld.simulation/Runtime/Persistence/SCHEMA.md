# Save format schema (formatVersion 3)

Written by `WorldSaver.Save(WorldState)` → indented JSON string.
Read by the loader. Top-level properties always appear in this order.

Version 2 (P2-12) adds every Phase 2 state section: relationships,
attributed memories, the eleven economy progress states, and per-lot
inventory ages. Version 3 (P3-04) adds every Phase 3 state section: skill
stores (player + NPCs), NPC happiness, tavern popularity and ingredient
demand. Older documents still load (see Compatibility below).

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
  "formatVersion": 3,
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

1. Validate `formatVersion` first; reject anything but `1`, `2` or `3`.
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
   v1/v2 — the fresh defaults apply).
3. `AppleScenarioState` is harness-owned and intentionally not persisted.
4. Wallet aliasing: a shop owner's wallet IS their personal wallet (one shared
   object — `Shop.Purchase` has a `ReferenceEquals` fast path for self-purchase).
   The save writes the copper value in both the shop and belongings sections;
   the loader must validate they agree (contradiction → `LoadException`) and
   restore a single shared `Wallet`. If a future phase ever wants them separate,
   the schema needs an explicit aliasing marker.

## Compatibility (v1 → v2 → v3)

Version 3 adds sections; it removes nothing. The loader accepts all three:

- **v3 → v3**: every section restores via its `Capture`/`Restore` pair.
- **v2 → v3**: skill stores restore empty, NPC happiness restores neutral (50),
  tavern popularity restores to its 50 baseline and ingredient demand to empty,
  all with cursors at -1 (never run). Rationale: every Phase 3 state has a
  safe uninitialized default — a v2 save predates the systems that mutate it,
  so "never started" is the truthful restore.
- **v1 → v3**: as v2 → v3, plus the v1 → v2 rules below.
- **v1 → v2**: the eleven economy states, relationships, and attributed memories
  keep their fresh-world defaults (uninitialized, empty, cursors at zero); shop
  and belongings lots are rebuilt as single age-0 lots from the saved counts.
  Rationale: every Phase 2 state has a safe uninitialized default — a v1 save
  predates the systems that mutate it, so "never started" is the truthful
  restore.
- **v4+**: rejected with `LoadException`. Versions below 1 are rejected too.

Corruption checks new in v2: lot quantities must sum to the saved counts;
attributed-memory recall deltas must satisfy |recalled| ≤ |original| per axis
(the runtime invariant, enforced at load). Either violation is a `LoadException`
and the world is discarded — the loader never returns a half-built world.
