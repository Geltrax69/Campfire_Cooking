# P1-21e save format schema (formatVersion 1)

Written by `WorldSaver.Save(WorldState)` → indented JSON string.
Read by the P1-21f loader. Top-level properties always appear in this order.

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
  "formatVersion": 1,
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
      "ownerCopper": 31,
      "prices": { "item_apple": 3 }                   // item ID → unit price, ordinal order
    }
  ],
  "belongings": [
    {
      "owner": "player",                 // actor string
      "inventory": { "item_apple": 6 },  // item ID → count, ordinal order
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

1. Validate `formatVersion` first; reject anything but `1`.
2. Build order suggestion: fresh `WorldState(savedRngState, new GameTime(savedClock))`
   (per the P1-21f brief, `SimRng(ulong)` resumes the stream exactly), load
   approved definitions by ID, restore event log (retained events + both
   high-water marks), command queue, NPCs (`NpcState.Restore` checks the
   sleeping/intention agreement), beliefs, memories, perception cursor, shops,
   belongings, production/restock/price states, reputation (skip when null),
   travel (re-register NPCs and re-apply journeys when non-null).
3. `AppleScenarioState` is harness-owned and intentionally not persisted.
4. Wallet aliasing: a shop owner's wallet IS their personal wallet (one shared
   object — `Shop.Purchase` has a `ReferenceEquals` fast path for self-purchase).
   The save writes the copper value in both the shop and belongings sections;
   the loader must validate they agree (contradiction → `LoadException`) and
   restore a single shared `Wallet`. If a future phase ever wants them separate,
   the schema needs an explicit aliasing marker.
