# Living World — Village Map and Locations (D-02)

## Summary

Millbrook's prototype needs 23 places: 3 shops (apple stall, general store, bakery), Alder Farm with its orchard, the Hearthside tavern, a smithy, the watermill, the market square, the old well, a communal granary, the guard post, the river shrine, the healer's hut, the forest edge, the River Alder with its ford, and 8 household homes. Everything the 20 simulated villagers need fits inside ~130 metres of the square, with the farm and forest 2 minutes out — walking never dominates a day. Each place lists what it produces, who owns it, who goes there, and what can be bought or stolen there. The apple stall starts the game with 20 apples; stock carries over day to day and never resets — it only grows through real farm deliveries from Alder Farm, limited by what the orchard produces. That chain is what the Apple Test leans on.

---

## 1. How the village sits together

From WORLD.md's one-liner: the ford and mill at the river's bend, the market square and shops uphill from the water, farms spreading along the floodplain, the orchard on the south hill, forest hemming the east and north. The Alder Road runs east–west through the village; carts slow for the ford, so they stop at the square — which is why the shops and tavern face it. The guard post sits at the palisade gate where the road leaves east, the only direction trouble has ever come from (wolves, not armies).

Everything in the village core is within ~130 m of the square. Walking speed is 80 m/min (a normal unhurried pace — reason: 4.8 km/h matches real walking), so no trip inside the village takes more than a minute or two. The simulation should treat short trips as cheap but not free: a villager crossing the village 20 times a day spends real time doing it.

## 2. The map

Grid in metres, square at (0, 0). `~` = River Alder, `=` = Alder Road, `F` = forest.

```
        N (forest hems the north and east)
  y=90  . . . . . . . . . . . . . . . . . . F F F F F F F F F
  y=70  . . . . . . . . . . . . . . . . . . F F[wood]F F F F
  y=50  . . . . . . . . . . . . . . . . . . F F[heal]F F F F
  y=30  . . . . . . . . . .[eld]. . . . . . . . . . . . . .
  y=15  . . . . . . . .[mil]. . .[gry][grd][smy]. . . . . .
  y= 0  ~ ~ . . . . . .[gen]. . .[S/well]. . . .[smh]. . . .
  y=-15 ~ ~ . . .[mil]. . . . . .[app][bak]. . . . . . . . .
  y=-25 ~ ~ . . . . . .[grn][tav]. . .[mih]. . . . . .[FARM].
  y=-35 ~ ~ . . . . . .[shr]. . . .[fen]. . . . . . . . . .
  y=-50 ~ ~ . . . . . . . . . . . . . . . . . . . .[orchard]
        x -90 -70 -50 -30 -10   0  10  30  50  70  90  110 140
```

Legend: S/well = market square & old well · app = Holt's apple stall · gen = Bray's general store · bak = Crust & Crumb bakery · tav = The Hearthside tavern · grn = granary · smy = smithy · smh = smith's home · mil = mill · mih = Mira's home · fen = Fenn (baker) home · bray = Bray home · gry = guard's cottage · grd = guard post · eld = elder's cottage · heal = healer's hut · wood = woodcutter's home · mil(home) = miller's home · shr = old river shrine · FARM = Alder Farm · orchard = apple orchard (part of farm).

The deep forest past the second ridge is deliberately NOT on this map — it is a rumor, not a place (WORLD.md decision 4).

## 3. Locations

IDs are `loc_` prefixed snake_case. Owners are `npc_` ids that D-03 will define (names are proposals, not final); `"village"` means communal. Art paths are real files verified in `Assets/`; `MISSING ART` marks gaps. Capacity = people who fit comfortably (reason: affects crowding, noise, and how many can witness an event at once).

### Shops

**loc_apple_stall — Holt's Apple Stall** (shop)
- Purpose: sells apples and orchard fruit. The Apple Test's stage.
- Owner: npc_mira_holt (apple-shop owner). Position (14, -10). Open 07:00–19:00. Capacity 6.
- Who goes: everyone buying fruit; children sent with a coin; Mira's regulars gossip here mornings.
- Buy: apples (the stall starts the game with 20 and **stock carries over day to day — it never resets**; apples only arrive through real deliveries from Alder Farm), pears and berries in season (summer–autumn). Stealable: apples from the display, the small coin box (usually < 200 copper — reason: Mira banks takings at home each evening).
- Art: `Assets/Packs/Kenney/FantasyTownKit/stall-green.glb` + `Assets/Packs/Kenney/MiniMarket/display-fruit.glb` (display counter).

**loc_general_store — Bray's General Store** (shop)
- Purpose: sells imports the village can't make: salt, cloth, dyes, lamp oil, nails, rope, tools.
- Owner: npc_tilda_bray. Position (-14, 8). Open 08:00–18:00. Capacity 8.
- Who goes: anyone needing imported goods; farmers buying nails and tools; the miller buying lamp oil.
- Buy: salt, cloth, lamp oil, nails, rope, basic tools. Stealable: small goods, the coin box (bigger than Mira's — reason: imports are expensive, takings sit longer between merchant visits).
- Art: `Assets/Models/Buildings/Village/general-store.glb`.

**loc_bakery — Crust & Crumb Bakery** (shop)
- Purpose: bakes and sells bread from the mill's flour. Sold out by early afternoon most days (reason: one oven, morning bake only).
- Owner: npc_oda_fenn (baker). Position (20, -18). Open 06:00–14:00. Capacity 5.
- Who goes: everyone buying the day's bread; children sent at dawn.
- Buy: rye and barley loaves, rolls, festival seed-cakes. Stealable: loaves from the cooling rack, the till.
- Art: `Assets/Models/Buildings/Village/sunbeam-cottage.glb`.

### Production

**loc_farm — Alder Farm** (farm)
- Purpose: grows wheat, barley, apples (orchard on the south hill), keeps chickens and pigs; the apple stall's supplier.
- Owner: npc_corvin_alder (farmer) + family, who live in the farmhouse. Position (120, -24), orchard rows at (140, -60) are part of this location. Open: always (home). Capacity 40 (fields).
- Who goes: the farmer's family daily; hired hands at harvest; Mira collecting apple deliveries; children stealing windfalls (a known minor nuisance — reason: gives the theft system a low-stakes everyday case).
- Buy (farmgate): eggs, honey, pork in autumn. Delivers: apples to the apple stall, grain to the mill. Stealable: orchard apples (visible — hard to steal unseen in daylight), chickens, tools from the barn, stored grain (serious — the village notices).
- Art: `Assets/Models/Buildings/Village/farmhouse.glb`, `Assets/Models/Buildings/Farm/big-red-barn.glb`, `Assets/Models/Buildings/Farm/chicken-coop.glb`, `Assets/Models/Buildings/Farm/rail-fence.glb`, orchard rows of `Assets/Models/Nature/Trees/apple-tree.glb`.

**loc_mill — Alder Watermill** (mill)
- Purpose: grinds wheat and barley into flour for the bakery and households. The old wheel is cracking (WORLD.md's current problem) — it works, but everyone knows the sound it makes.
- Owner: npc_garrick_alder (miller, grandson of founder Tomas Alder). Position (-64, -2). Open 06:00–20:00. Capacity 8.
- Who goes: farmers bringing grain; the baker collecting flour; the miller's family (they live next door).
- Buy: grinding service (a share of the grain, not coin — reason: traditional miller's toll). Stealable: flour sacks, stored grain.
- Art: `Assets/Packs/Kenney/FantasyTownKit/watermill.glb`.

**loc_blacksmith — Ember & Iron Smithy** (smithy)
- Purpose: forges and repairs tools, horseshoes, nails; the only iron-work in the village (iron itself is imported).
- Owner: npc_doran_kettle (blacksmith). Position (42, 14). Open 08:00–18:00. Capacity 6.
- Who goes: farmers with broken tools; the guard for repairs; travelers needing horseshoes.
- Buy: tools, nails, horseshoes, repairs (paid in coin or kind). Stealable: hand tools (light), iron stock (heavy — reason: stealing 20 kg of iron unnoticed is a different crime than pocketing an apple).
- Art: `Assets/Models/Buildings/Village/goods-shed.glb` (smithy building) + `Assets/Packs/Kenney/SurvivalKit/workbench-anvil.glb` (forge and anvil).

### Food, drink, gathering

**loc_tavern — The Hearthside** (tavern)
- Purpose: ale, hot meals, rooms upstairs for travelers; the village's evening room and rumor engine. The keeper lives here. **Bessa brews the ale herself** from grain (bought from the mill) and well water — no separate brewer NPC needed.
- Owner: npc_bessa_marlowe (tavern keeper). Position (-2, -22). Open 10:00–23:00. Capacity 30.
- Who goes: everyone, evenings especially; travelers; hunters telling Grey stories (WORLD.md decision 4); the council meeting in the back room.
- Buy: ale, small beer, stew, bread, a bed for the night. Stealable: unattended mugs and purses (petty), the strongbox (kept in the keeper's room — serious).
- Art: `Assets/Models/Buildings/Village/great-hall.glb`.

**loc_square — Market Square** (square)
- Purpose: the village's centre. Daytime market spillover, the Longnight bonfire site, public apologies (WORLD.md law), festivals.
- Owner: village. Position (0, 0). Open: always. Capacity 60 (reason: the whole village minus the bedridden fits for festivals).
- Who goes: everyone, at some point most days.
- Buy: nothing fixed; traveling merchants set up here every few weeks (warm months). Stealable: nothing fixed — but unattended carts and purses in a crowd (the Apple Test's witness could easily be here).
- Art: `Assets/Packs/Kenney/FantasyTownKit/` road pieces + `Assets/Packs/Kenney/FantasyTownKit/fountain-round.glb` (used as a decorative centrepiece, NOT the well).

**loc_well — The Old Well** (well)
- Purpose: the village's main water source (reason: the river is 80 m downhill and floods dirty in spring). A few homes keep rain barrels as backup, but the well is what everyone relies on.
- Owner: village. Position (0, 4), at the square's edge. Open: always. Capacity 4 (around the rim).
- Who goes: everyone fetching water, mornings and evenings — which is why it is also a gossip spot (reason: D-09's rumor system needs natural meeting points).
- Buy: nothing. Stealable: nothing — but the well is where children dare each other, buckets go missing, and news travels.
- Art: MISSING ART: stone well with a small timber roof and windlass.

**loc_granary — Village Granary** (storage)
- Purpose: communal grain reserve for winter. Currently HALF what it should be (WORLD.md's current problem) — the village knows this and it shapes autumn behavior.
- Owner: village (council keeps the key; the elder holds it). Position (-16, -20). Open: by arrangement. Capacity 6.
- Who goes: the miller, the council, families drawing their stored share in winter.
- Buy: nothing — shares are by household right, not coin. Stealable: grain (a grave crime — reason: stealing from the granary threatens everyone's winter, and the whole village would turn on the thief if proof surfaced).
- Art: `Assets/Models/Buildings/Village/garden-shed.glb`.

### Order and care

**loc_guard_post — Guard Post** (guard)
- Purpose: one-room hut at the palisade gate on the east road; the guard's office, lockup (one cell), and the lost-property shelf.
- Owner: npc_bram_stone (guard). Position (26, 12). Open: always (someone is reachable). Capacity 4.
- Who goes: the guard; complainants; the night watch mustering in winter; children told to stay away (and therefore fascinated).
- Buy: nothing. Stealable: the evidence shelf (a terrible idea — reason: stealing evidence is how a suspicion becomes a manhunt).
- Art: `Assets/Models/Buildings/Defense/palisade-gate.glb` + `Assets/Models/Buildings/Village/garden-shed.glb` (hut).

**loc_healer_hut — Healer's Hut** (healer)
- Purpose: the healer's home, workroom and herb garden. Fever-tea that actually works; wounds knit a little faster here (WORLD.md magic, expressed as items and routines, never spells).
- Owner: npc_sella_wren (healer, one of the 20 — WORLD.md decision 2). Position (48, 48), at the forest edge. Open 08:00–18:00, and always in emergencies. Capacity 4.
- Who goes: the sick and hurt; mothers with feverish children; the curious; the skeptical.
- Buy: remedies, fever-tea, poultices, advice (paid in coin, eggs, or favors — reason: the healer takes what people can give). Stealable: dried herbs, prepared remedies.
- Art: `Assets/Models/Buildings/Village/bungalow-house.glb` + herb garden from `Assets/Packs/Kenney/NatureKit/`.

**loc_shrine — Old River Shrine** (shrine)
- Purpose: a weathered stone shrine of the Hearth faith on the riverbank; travelers leave a crust, locals leave flowers. The player wakes up below it on day 1.
- Owner: village (tended by whoever feels like it). Position (-46, -30). Open: always. Capacity 6.
- Who goes: the pious, the worried, children daring each other, the player on arrival.
- Buy: nothing. Offerings are sometimes left (bread, coins, ribbons). Stealable: offerings — the lowest, most shameful theft in the village (reason: D-09 needs crimes the whole village condemns without needing proof of who did it).
- Art: MISSING ART: small weathered stone shrine with an offering shelf, river-worn and old.

### The wilds

**loc_forest_edge — Alder Forest Edge** (forest)
- Purpose: the mapped, worked part of the forest: timber cutting, firewood, herbs, berries, mushrooms, hunting. Past the second ridge is rumor, not map.
- Owner: village (common wood — reason: no one owns the forest, which is why poaching disputes are about custom, not deeds). Position (90, 90). Open: daylight custom (the iron rule: not past the treeline alone after dusk). Capacity: open land.
- Who goes: the woodcutter daily; hunters; the healer for herbs; children for berries (never alone); boar hunters in autumn.
- Buy: nothing — you take, by custom, in moderation. Stealable: nothing to steal; but taking more than custom allows (clear-cutting, stripping herb patches) earns enemies (reason: the tragedy-of-the-commons is a system, not a script).
- Resources (amounts with reasons):
  - Standing timber: ~400 workable logs; sustainable take ~1/day per woodcutter (reason: regrows ~1/day, so one woodcutter working daily is sustainable and two are not — a real economic tension).
  - Firewood: effectively abundant; 2 bundles/day per household need (reason: winter is 90 days and firewood is a need, not flavor).
  - Herbs: 12 known patches (healer's hollow); each regrows in ~3 days in spring/summer/autumn, dormant in winter (reason: scarcity in winter makes stored remedies valuable).
  - Berries: 20 bushes, fruiting summer–autumn; picked clean in ~2 days by the village children (reason: a small seasonal windfall, gone fast).
  - Mushrooms: 8 known spots, autumn only, 1 day to regrow after rain (reason: a forager's secret worth keeping).
  - Game: deer, rabbits, boar — populations handled by the animal system (D-08), not counted here.
- Art: `Assets/Packs/Kenney/NatureKit/` (trees, rocks, plants).

**loc_river_alder — River Alder & Ford** (river)
- Purpose: water, fish, the gravel ford where the Alder Road crosses (the reason the village exists).
- Owner: village. Position (-80, 0). Open: always. Capacity: open water.
- Who goes: the fisherman; children (supervised, supposedly); travelers crossing at the ford; the miller's daughter fetching water (day 1).
- Buy: nothing — the river gives. Resources: fish stock ~60, regrows ~3/day (reason: the river never fully dries, so fishing is reliable but not infinite); reeds and clay at the banks (~30 clay loads, renewed by spring floods); fresh water.
- Stealable: nothing — but unattended fish traps are a classic petty theft (reason: another everyday low-stakes case for the theft system).
- Art: `Assets/Packs/Kenney/NatureKit/` river pieces + `Assets/Models/Buildings/Village/boat-house.glb` (fisherman's hut).

### Homes (8 households for the simulated 20)

Households, not houses-per-person — families share (reason: 120 villagers in 30 households, so ~4 per home; our 20 need ~5–7 roofs). The tavern keeper lives at the tavern, the farmer's family at the farm, the healer at her hut.

- **loc_home_miller — Miller's House** (-44, 22). npc_garrick_alder + family, including the youngest daughter (finds the player, day 1). Capacity 8. Art: `Assets/Models/Buildings/Village/bungalow-house.glb`.
- **loc_home_mira — Mira's Cottage** (24, -28). npc_mira_holt (apple stall). Her takings box lives here (reason: why the stall's coin box stays small). Capacity 6. Art: `Assets/Models/Buildings/Village/sunbeam-cottage.glb`.
- **loc_home_bray — Bray's House** (-22, 16). npc_tilda_bray (general store). Capacity 6. Art: `Assets/Models/Buildings/Village/mudbrick-house.glb`.
- **loc_home_fenn — Fenn's House** (28, -34). npc_oda_fenn (baker) + family; smells of bread at 05:00. Capacity 8. Art: `Assets/Models/Buildings/Village/front-porch.glb`.
- **loc_home_smith — Kettle's House** (50, 4). npc_doran_kettle (blacksmith) + family. Capacity 8. Art: `Assets/Models/Buildings/Village/bld-general-store-01.glb`.
- **loc_home_guard — Stone's Cottage** (34, 18). npc_bram_stone (guard). Small, tidy, one cell's worth of paperwork. Capacity 4. Art: `Assets/Models/Buildings/Village/garden-shed.glb` (second use — it reads as a small cottage).
- **loc_home_elder — Elder's Cottage** (8, 32). npc_elswith_alder (village elder); holds the granary key. Capacity 4. Art: `Assets/Models/Buildings/Village/mudbrick-house.glb` (second use).
- **loc_home_woodcutter — Oakes' Cottage** (62, 58). npc_tam_oakes (woodcutter) + family, at the forest edge (reason: the woodcutter lives where the work is). Capacity 8. Art: `Assets/Models/Buildings/Farm/log-pile.glb` beside a reused `bungalow-house.glb`.

Homes are not shops: nothing to buy; stealable are household goods and coin — and **home burglary is much harder and riskier than daytime petty theft**: doors are locked at night, owners are usually inside (often with dogs), and being caught means far bigger consequences (the stocks, a heavy fine, or banishment). It is the crime the guard investigates hardest after granary theft (reason: homes are where people feel safest, so violation matters most).

## 4. Travel times (walking, minutes)

Speed 80 m/min, rounded to the nearest minute, minimum 1 (reason: nothing in the village core is far enough to matter, but trips still cost ticks). Distances are straight-line between the coordinates above.

From the square to everywhere (the square is the village's hub):

| From | To | Min | | From | To | Min |
|---|---|---|---|---|---|---|
| loc_square | loc_well | 1 | | loc_square | loc_home_elder | 1 |
| loc_square | loc_apple_stall | 1 | | loc_square | loc_home_guard | 1 |
| loc_square | loc_general_store | 1 | | loc_square | loc_home_miller | 1 |
| loc_square | loc_bakery | 1 | | loc_square | loc_home_woodcutter | 1 |
| loc_square | loc_tavern | 1 | | loc_square | loc_mill | 1 |
| loc_square | loc_granary | 1 | | loc_square | loc_shrine | 1 |
| loc_square | loc_blacksmith | 1 | | loc_square | loc_river_alder | 1 |
| loc_square | loc_guard_post | 1 | | loc_square | loc_healer_hut | 1 |
| loc_square | loc_home_mira | 1 | | loc_square | loc_farm | 2 |
| loc_square | loc_home_bray | 1 | | loc_square | loc_forest_edge | 2 |
| loc_square | loc_home_fenn | 1 | | | | |
| loc_square | loc_home_smith | 1 | | | | |

Direct pairs that skip the square (shortcuts villagers actually use):

| From | To | Min | Why it matters |
|---|---|---|---|
| loc_mill | loc_shrine | 1 | the miller's daughter's water run (day 1) |
| loc_mill | loc_river_alder | 1 | the mill sits on the river |
| loc_mill | loc_home_miller | 1 | the miller lives by his mill |
| loc_shrine | loc_river_alder | 1 | the shrine is on the riverbank |
| loc_apple_stall | loc_farm | 1 | Mira's apple collection run |
| loc_apple_stall | loc_tavern | 1 | stall to tavern gossip hop |
| loc_farm | loc_mill | 2 | grain delivery (122 m + cart) |
| loc_farm | loc_forest_edge | 1 | farm's east fields meet the treeline |
| loc_farm | loc_healer_hut | 1 | the healer's shortcut through the fields |
| loc_healer_hut | loc_forest_edge | 1 | herb gathering |
| loc_home_woodcutter | loc_forest_edge | 1 | the woodcutter's commute |
| loc_blacksmith | loc_home_smith | 1 | the smith lives by his forge |
| loc_guard_post | loc_blacksmith | 1 | the guard's repair errands |
| loc_tavern | loc_granary | 1 | back-lane shortcut |

Travel through the square is never much slower than a direct path (reason: the village is compact — this keeps the simulation's pathing simple in Phase 1).

## 5. Art summary

Verified against `Assets/` (all files exist except where noted):

| Location | Art |
|---|---|
| loc_apple_stall | `Assets/Packs/Kenney/FantasyTownKit/stall-green.glb`, `Assets/Packs/Kenney/MiniMarket/display-fruit.glb` |
| loc_general_store | `Assets/Models/Buildings/Village/general-store.glb` |
| loc_bakery | `Assets/Models/Buildings/Village/sunbeam-cottage.glb` |
| loc_farm | `Assets/Models/Buildings/Village/farmhouse.glb`, `Assets/Models/Buildings/Farm/big-red-barn.glb`, `Assets/Models/Buildings/Farm/chicken-coop.glb`, `Assets/Models/Buildings/Farm/rail-fence.glb`, `Assets/Models/Nature/Trees/apple-tree.glb` (orchard rows) |
| loc_tavern | `Assets/Models/Buildings/Village/great-hall.glb` |
| loc_blacksmith | `Assets/Models/Buildings/Village/goods-shed.glb`, `Assets/Packs/Kenney/SurvivalKit/workbench-anvil.glb` |
| loc_mill | `Assets/Packs/Kenney/FantasyTownKit/watermill.glb` |
| loc_square | `Assets/Packs/Kenney/FantasyTownKit/` road pieces, `Assets/Packs/Kenney/FantasyTownKit/fountain-round.glb` (centrepiece) |
| loc_well | MISSING ART: stone well with timber roof and windlass |
| loc_granary | `Assets/Models/Buildings/Village/garden-shed.glb` |
| loc_guard_post | `Assets/Models/Buildings/Defense/palisade-gate.glb`, `Assets/Models/Buildings/Village/garden-shed.glb` (hut) |
| loc_healer_hut | `Assets/Models/Buildings/Village/bungalow-house.glb`, `Assets/Packs/Kenney/NatureKit/` (herb garden) |
| loc_shrine | MISSING ART: small weathered stone shrine with offering shelf |
| loc_forest_edge | `Assets/Packs/Kenney/NatureKit/` (trees, rocks, plants) |
| loc_river_alder | `Assets/Packs/Kenney/NatureKit/` (river pieces), `Assets/Models/Buildings/Village/boat-house.glb` |
| homes | `Assets/Models/Buildings/Village/bungalow-house.glb`, `sunbeam-cottage.glb`, `mudbrick-house.glb`, `front-porch.glb`, `bld-general-store-01.glb`, `garden-shed.glb`, `Assets/Models/Buildings/Farm/log-pile.glb` |

Some art is reused across homes (reason: a real village repeats its builders' patterns; the Unity phase can vary paint and props).

## 6. Later (kept out of the prototype)

- A real bridge over the ford (the village's quiet hope — a multi-season community project for later phases).
- A second watch post on the west road, if the village ever grows that worried.
- The deep forest past the second ridge (rumor only, per WORLD.md).
- The Alder Road east beyond the palisade — where travelers, merchants, and trouble come from.
- King's Rest and the two neighboring villages on the road.

## 7. Decisions made (2026-10-02, with the human)

1. **Apple stock never resets.** The stall starts the game with 20 apples; stock carries over day to day and only grows through real farm deliveries (limited by orchard production and delivery timing). **D-04 (economy) must be designed with this rule** — the Apple Test only works if the theft persists overnight.
2. **Bessa brews the ale herself** from grain and well water — no extra brewer NPC.
3. **One farm only.** Alder Farm is the single supplier; single supply makes shortages visible.
4. **The well is the main water source;** a few homes have rain barrels as backup.
5. **Home burglary is much harder and riskier than daytime petty theft** (locked doors at night, owners home, dogs, severe consequences if caught).

## 8. MISSING ART list (for D-12)

- `loc_well` — stone well with a small timber roof and windlass.
- `loc_shrine` — small weathered stone shrine with an offering shelf, river-worn and old.
