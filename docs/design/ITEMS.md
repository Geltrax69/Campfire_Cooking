# Living World — Items and Resources (D-05)

## Summary

Millbrook's prototype needs 49 canonical item types: the 66 provisional `item_` ids scattered across the approved data collapse into one clean list — 9 true duplicates merged (flour variants, horseshoes, iron stock, cloth, dried herbs, poultice, ale barrel, plain bread), 8 workshop fixtures folded into `item_tool_basic`, and 1 service (`item_bed_night`, a tavern bed for the night) dropped as a non-item. Every surviving price matches `economy.json` exactly; effects are tuned to the need rates in CHARACTERS.md (an adult needs ~96 hunger/day, so a rye loaf restores 30 — a full meal). Five items are tracked individually (Tilda's ledger, the granary key, Lida's wooden chicken, Garrick's wife's shawl, Sima's recipe book); everything else is counted in bulk. Four new items the brief requires (stone, fishing rod, sword, pelt) are proposed with flagged prices.

---

## 1. How the list was canonicalized

The approved data used 66 distinct `item_` ids, many of them provisional (CHARACTERS.md warned they were). The canonicalization rules:

1. **True duplicates merged.** `item_flour_barley`/`item_flour_wheat`/`item_flour_sack` → `item_flour` (the bakery buys "flour" at 10/sack; the grain type doesn't change the sim). `item_barley`/`item_wheat`/`item_grain_sack` → `item_grain`. `item_horseshoe` → `item_horseshoes` (sold as a set of 4). `item_iron_stock_kg` → `item_iron_stock` (the unit is the kilogram). `item_cloth` → `item_cloth_imported` (Tilda's store sells imports; local cloth is `item_cloth_local`). `item_herb_dried` → `item_herb_bundle`. `item_poulitice` → `item_remedy`. `item_ale_barrel` → `item_ale` (1 barrel = 20 mugs; the orchestrator converts counts). `item_bread` → `item_bread_rye` (the standard loaf). `item_berry` → `item_berries`.
2. **Workshop fixtures folded into `item_tool_basic`.** The peel, scales, kettles, loom, logbook, nets, and fish traps in NPC possessions are fixtures of a trade, not trade goods. They map to `item_tool_basic` ("Basic tools (kit)", 40 copper) so every possession still references a real id. If the simulation later needs distinct looms or brew kettles, they can be split out.
3. **Services are not items.** `item_bed_night` (a tavern bed for the night, 10 copper) is dropped from the item list; the price stays in `economy.json` as a service.
4. **Nothing priced was cut.** Every good with a price in ECONOMY.md survives, which is why the list is 49 rather than 25–35: the economy already prices ~35 distinct goods, and the brief requires 14 more (stone, fishing rod, sword, pelt, horseshoes, story items). See open question 5.
5. **New items get flagged prices.** `item_stone` (2), `item_fishing_rod` (25), `item_sword` (120), `item_pelt` (10), `item_spear` (45), `item_sling` (5) are proposals, marked `*` below.

Scales (per shared conventions): traits/needs/quality are 0–100 integers, 50 = average. Money is integer copper. Weights are kilograms, deliberately rough (reason: the simulation needs encumbrance tiers, not physics — a log at 15 kg means "needs a cart or two people", an apple at 0.2 kg means "pocketable").

---

## 2. The canonical items

Format per item: id — name (category, base value, weight, stack, shelf life → what spoilage does, quality 0–100). Then origin, who uses it, effects, bulk/tracked, art.

### Food

**item_apple — Apple** (food, 3 copper, 0.2 kg, stack 50, 10 days → mushy apple: hunger −5)
- From: `loc_farm` orchard, sold at `loc_apple_stall`. Used by: everyone (snack, lunchboxes, children's pockets). Effects: hunger −15, health +1. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/apple.glb`.

**item_pear — Pear** (food, 4 copper, 0.2 kg, stack 50, 7 days → mushy pear: hunger −5)
- From: `loc_farm` orchard (summer–autumn), sold at `loc_apple_stall`. Used by: everyone (seasonal fruit). Effects: hunger −12. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/pear.glb`.

**item_berries — Berries (basket)** (food, 4 copper, 0.3 kg, stack 20, 3 days → moldy: health −2)
- From: `loc_forest_edge` (summer–autumn), gathered by children. Used by: everyone (seasonal treat); Brynn buys baskets to fill Mira's display gap. Effects: hunger −8. Bulk. Art: MISSING ART: basket of berries.

**item_bread_rye — Rye loaf** (food, 3 copper, 0.5 kg, stack 20, 4 days → stale: hunger −10)
- From: `loc_bakery` (from `loc_mill` flour). Used by: everyone — the daily bread; one loaf is a full meal (reason: hunger 6/hr × 16h ≈ 96/day; three meals of ~30). Effects: hunger −30. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/bread.glb`.

**item_bread_barley — Barley loaf** (food, 2 copper, 0.5 kg, stack 20, 4 days → stale: hunger −8)
- From: `loc_bakery`. Used by: everyone (the cheaper daily bread). Effects: hunger −25. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/bread.glb` (reuse; darker crust is a later art task).

**item_roll — Bread roll** (food, 1 copper, 0.15 kg, stack 30, 2 days → stale: hunger −4)
- From: `loc_bakery`. Used by: children, travelers (pocket food). Effects: hunger −12. Bulk. Art: MISSING ART: bread roll.

**item_stew — Stew (bowl)** (food, 4 copper, 0.6 kg, stack 5, 1 day → sour: health −3)
- From: `loc_tavern` (Bessa's kitchen). Used by: tavern customers, travelers — the best hot meal in the village. Effects: hunger −35, health +2. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/bowl-soup.glb`.

**item_flour — Flour (sack)** (material, 10 copper, 10 kg, stack 5, 90 days → musty: quality −20)
- From: `loc_mill` (from `loc_farm` grain; the miller's toll is 1/12 in kind). Used by: `loc_bakery` (Oda buys ~2.5 sacks/day); households. Effects: none (ingredient). Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/bag.glb` (reused as a flour sack).

**item_grain — Grain (sack)** (material, 8 copper, 10 kg, stack 5, 180 days → weevilly: quality −20)
- From: `loc_farm` (wheat and barley harvest, autumn). Used by: `loc_mill` (grinding); `loc_tavern` (Bessa buys at 8/sack for brewing); livestock feed. Effects: none. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/bag.glb` (reused as a grain sack).

### Drink

**item_ale — Ale (mug)** (drink, 2 copper, 0.5 kg, stack 10, 60 days → vinegary: hunger −2)
- From: `loc_tavern` (Bessa brews 2 batches/week from grain + well water). Used by: adults on tavern evenings — Bessa's best margin. Effects: hunger −5, social +5. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/mug.glb`.

**item_small_beer — Small beer** (drink, 1 copper, 0.5 kg, stack 10, 30 days → flat: hunger −1)
- From: `loc_tavern`. Used by: children and light drinkers. Effects: hunger −3, social +2. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/mug.glb` (reuse).

**item_fever_tea — Fever-tea (packet)** (drink, 5 copper, 0.1 kg, stack 20, 90 days dried → weak: health +3)
- From: `loc_healer_hut` (Sella brews from `item_herb_bundle`; the recipe is secret, shared only with Brynn). Used by: the sick — it works a little better than it should, which is the village's visible magic (WORLD.md: magic expressed as items, never spells). Effects: health +10, hunger −2. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/cup-tea.glb`.

### Animal products

**item_egg — Egg** (animal_product, 1 copper, 0.06 kg, stack 30, 14 days → rotten: health −3)
- From: `loc_farm` (Maren's hens), farmgate sales. Used by: everyone; Sella accepts eggs as payment. Effects: hunger −10. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/egg.glb`.

**item_cheese — Cheese (portion)** (animal_product, 6 copper, 0.4 kg, stack 20, 30 days → hard: hunger −10)
- From: `loc_farm` (Maren's dairy). Used by: everyone — keeps well, good travel food. Effects: hunger −20. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/cheese.glb`.

**item_pork — Pork (cut)** (animal_product, 12 copper, 1.0 kg, stack 20, 3 days → spoiled: health −5)
- From: `loc_farm` (autumn slaughter). Used by: everyone, cooked at home or the tavern (cooked: hunger −25; raw: hunger −15 — reason: cooking matters, per WORLD.md). Effects: hunger −25. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/meat-raw.glb`.

**item_fish — Fish (fresh)** (animal_product, 3 copper, 0.8 kg, stack 20, 2 days → spoiled: health −5)
- From: `loc_river_alder` (Jory's nets and traps; stock ~60, regrows 3/day). Used by: everyone (cooked). Effects: hunger −20. Bulk. Art: `Assets/Packs/Kenney/SurvivalKit/fish.glb`.

**item_fish_smoked — Smoked fish** (animal_product, 4 copper, 0.7 kg, stack 20, 60 days → tough: hunger −12)
- From: `loc_river_alder` (Jory's smoke). Used by: everyone — winter protein, travel food; Jory repays Tilda 2 fish/week. Effects: hunger −25. Bulk. Art: MISSING ART: smoked fish on a rack.

**item_honey — Honey (jar)** (animal_product, 8 copper, 0.5 kg, stack 10, 365 days → crystallized: hunger −8)
- From: `loc_farm` (hives, autumn). Used by: everyone (sweetener); traveling merchants buy it. Effects: hunger −10, health +1. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/honey.glb`.

**item_pelt — Pelt** (animal_product, 10 copper*, 1.5 kg, stack 10, does not spoil)
- From: `loc_forest_edge` (Ralf's hunting; wolf pelts earn the 50-copper crown bounty). Used by: traveling merchants (part of the ~800 copper/visit trade); winter clothing. Effects: none. Bulk. Art: MISSING ART: animal pelt.

### Health

**item_herb_bundle — Herb bundle** (material, 3 copper, 0.3 kg, stack 20, 60 days dried → faded: crafting only)
- From: `loc_forest_edge` (the healer's hollow; 12 patches, regrow in 3 days, dormant in winter; gathered by Sella and Brynn). Used by: `loc_healer_hut` (remedies, fever-tea); the player can forage and sell to Sella at 3 (D-04 player economy). Effects: none (crafting input). Bulk. Art: MISSING ART: bundle of dried herbs.

**item_remedy — Remedy (vial)** (valuable, 8 copper, 0.2 kg, stack 20, 180 days → weak: health +5)
- From: `loc_healer_hut` (Sella; herbs + time + a little magic). Used by: the sick and hurt (poultices, tinctures, wound salves). Effects: health +15. Bulk. Art: `Assets/Packs/Kenney/SurvivalKit/bottle.glb`. (Price 8 matches `economy.json`; ECONOMY.md gives the 5–10 range for quality and urgency.)

### Materials

**item_log — Log** (material, 8 copper, 15 kg, stack 10, does not spoil)
- From: `loc_forest_edge` (Tam cuts ~1/day — the sustainable rate). Used by: `loc_blacksmith` (handles), `loc_farm` (fences), builders. Effects: none. Bulk. Art: `Assets/Packs/Kenney/SurvivalKit/resource-wood.glb`.

**item_firewood — Firewood (bundle)** (material, 2 copper, 5 kg, stack 10, does not spoil)
- From: `loc_forest_edge` (Tam and Piotr; ~2 bundles/day per household in winter). Used by: every household in winter — a need, not flavor (WORLD.md). Effects: none. Bulk. Art: MISSING ART: bundle of firewood.

**item_iron_stock — Iron stock (kg)** (material, 15 copper, 1 kg, stack 50, does not spoil)
- From: imported by traveling merchants (~10 copper/kg) to `loc_blacksmith`; Doran keeps ~20 kg. Used by: the smithy (forges nails, tools, horseshoes). Effects: none. Bulk. Art: MISSING ART: iron bar.

**item_nails — Nails** (material, 1 copper, 0.02 kg, stack 200, does not spoil)
- From: `loc_blacksmith` (from `item_iron_stock`). Used by: everyone (building, repairs; Garrick owes Tilda 60 copper for nails and pitch). Effects: none. Bulk. Art: MISSING ART: nails.

**item_rope — Rope (coil)** (material, 5 copper, 1.5 kg, stack 10, does not spoil)
- From: imported to `loc_general_store`. Used by: farm, mill, river work; Jory owes Tilda for rope and tar. Effects: none. Bulk. Art: MISSING ART: coil of rope.

**item_salt — Salt (pouch)** (material, 6 copper, 0.5 kg, stack 20, does not spoil)
- From: imported to `loc_general_store`. Used by: everyone (preserving meat and fish for winter — the reason winter is survivable). Effects: none. Bulk. Art: MISSING ART: salt pouch.

**item_lamp_oil — Lamp oil (flask)** (material, 12 copper, 0.8 kg, stack 10, does not spoil)
- From: imported to `loc_general_store`. Used by: every household (winter light). Effects: none. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/bottle-oil.glb`.

**item_dye — Dye (pot)** (material, 20 copper, 1.0 kg, stack 10, does not spoil)
- From: imported to `loc_general_store`. Used by: Brynn and weavers; on Tilda's order list. Effects: none. Bulk. Art: MISSING ART: dye pot.

**item_cloth_local — Cloth (length, local)** (material, 25 copper, 2.0 kg, stack 10, does not spoil)
- From: `loc_home_woodcutter` (Brynn's loom) to `loc_general_store` (Tilda buys at 15, retails at 25). Used by: everyone — the village's one import substitution (ECONOMY.md). Effects: none. Bulk. Art: MISSING ART: folded cloth bolt.

**item_cloth_imported — Cloth (length, imported)** (material, 40 copper, 2.0 kg, stack 10, does not spoil)
- From: imported to `loc_general_store`. Used by: everyone (finer than local; Tilda's saved blue bolt). Effects: none. Bulk. Art: MISSING ART: folded cloth bolt (finer weave).

**item_ribbon — Ribbon** (valuable, 1 copper, 0.05 kg, stack 50, does not spoil)
- From: imported to `loc_general_store`. Used by: children (Lida's, Tansy's); left as shrine offerings. Effects: none. Bulk. Art: MISSING ART: ribbon.

**item_stone — Stone (block)** (material, 2 copper*, 5 kg, stack 20, does not spoil)
- From: gathered at `loc_river_alder` banks and `loc_forest_edge`. Used by: building and repairs (palisade, hearths, the well). Effects: none. Bulk. Art: `Assets/Packs/Kenney/SurvivalKit/resource-stone.glb`.

### Tools

**item_axe — Axe** (tool, 60 copper, 2.5 kg, stack 1, does not spoil)
- From: `loc_blacksmith` (from `item_iron_stock`). Used by: Tam (felling), Corvin (farm) — a real purchase at 60. Effects: none (tool quality affects work speed in the simulation). Bulk. Art: `Assets/Packs/Kenney/SurvivalKit/tool-axe.glb`.

**item_hoe — Hoe** (tool, 35 copper, 2.0 kg, stack 1, does not spoil)
- From: `loc_blacksmith`. Used by: `loc_farm` (Corvin's fields). Effects: none. Bulk. Art: `Assets/Packs/Kenney/SurvivalKit/tool-hoe.glb`.

**item_knife — Knife** (tool, 30 copper, 0.4 kg, stack 1, does not spoil)
- From: `loc_blacksmith`. Used by: everyone — the player's first real goal at 30 copper (D-04); Mira's good knife; Jory's father's knife. Effects: none. Bulk. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/cooking-knife.glb`.

**item_hammer — Hammer** (tool, 40 copper, 1.2 kg, stack 1, does not spoil)
- From: `loc_blacksmith`. Used by: builders, the smithy. Effects: none. Bulk. Art: `Assets/Packs/Kenney/SurvivalKit/tool-hammer.glb`.

**item_fishing_rod — Fishing rod** (tool, 25 copper*, 1.0 kg, stack 1, does not spoil)
- From: `loc_blacksmith` (Doran fits the hooks) / carved by Tam. Used by: Jory; the player (borrows Jory's net first, earns the rod — D-04 player economy). Effects: none. Bulk. Art: MISSING ART: fishing rod.

**item_tool_basic — Basic tools (kit)** (tool, 40 copper, 8 kg, stack 1, does not spoil)
- From: `loc_general_store` (imported); `loc_blacksmith`. Used by: households (mending, odd jobs). Also the canonical id for workshop fixtures folded in from the old data (peel, scales, kettles, loom, logbook, nets, traps — see rename mapping). Effects: none. Bulk. Art: `Assets/Packs/Kenney/SurvivalKit/box.glb` (reused as a tool chest).

**item_horseshoes — Horseshoes (set)** (tool, 12 copper, 1.0 kg, stack 5, does not spoil)
- From: `loc_blacksmith`. Used by: travelers (Doran's best traveler sale); farmers. Effects: none. Bulk. Art: MISSING ART: horseshoes (set of 4).

### Weapons

**item_bow — Hunting bow** (weapon, 80 copper, 1.5 kg, stack 1, does not spoil)
- From: crafted by hunters (Ralf); repaired at `loc_blacksmith`. Used by: Ralf (hunting, wolf work); Piotr's lessons. Effects: none. Bulk. Art: MISSING ART: hunting bow.

**item_sword — Sword** (weapon, 120 copper*, 3.0 kg, stack 1, does not spoil)
- From: imported from King's Rest by traveling merchants — no village smith forges swords (reason: the village's ironwork is tools and nails; a sword is an outsider's thing, which is exactly why the player's Swordsmanship path feels foreign). Used by: the player; travelers; the garrison road. Effects: none. Bulk. Art: MISSING ART: sword (Assets/README confirms none exist yet).

**item_spear — Spear** (weapon, 45 copper*, 2.5 kg, stack 1, does not spoil)
- From: `loc_blacksmith` (Bram's is village-made and meticulously kept). Used by: Bram (guard duty); the winter night watch. Effects: none. Bulk. Art: MISSING ART: spear.

**item_sling — Sling** (weapon, 5 copper*, 0.3 kg, stack 1, does not spoil)
- From: homemade — children make their own. Used by: Piotr, Tom (stones only, never anything living — Sima's rule). Effects: none. Bulk. Art: MISSING ART: sling.

### Unique items (tracked individually)

**item_ledger — Tilda's tab ledger** (unique, not for sale, 1.0 kg, stack 1, does not spoil)
- From: `loc_general_store` (kept at `loc_home_bray`); Tilda writes every tab in it. Used by: Tilda — the secret tab system (D-04: 340 copper across 5 debtors, no interest, Harvest Feast forgives under 20). Tracked individually. Art: MISSING ART: ledger book.
- Story: her late husband died owing a traveling merchant, and she paid every copper back. The ledger is her vow made paper: no debt unwritten, no debtor forgotten. It is the village's only bank.

**item_granary_key — Granary key** (unique, not for sale, 0.2 kg, stack 1, does not spoil)
- From: the village council; Elswith holds it. Used by: Elswith (granary checks twice a week). Tracked individually. Art: MISSING ART: large iron key.
- Story: a heavy iron key to the communal granary, currently guarding half the grain the village needs. Whoever holds it decides who eats in March. Elswith has held it for twenty years and never lost sleep over anything the way she loses sleep over this.

**item_wooden_toy — Carved wooden chicken** (unique, not for sale, 0.3 kg, stack 1, does not spoil)
- From: `loc_home_woodcutter` (Tam carved it for Lida). Used by: Lida — treasure, not a toy. Tracked individually. Art: MISSING ART: carved wooden chicken.
- Story: Tam spent three winter evenings carving it while Lida watched, asking questions he answered seriously. She named it after the speckled hen. It lives in her pocket, next to the feathers.

**item_shawl — Garrick's wife's shawl** (unique, not for sale, 0.5 kg, stack 1, does not spoil)
- From: `loc_home_miller` (kept, never worn now). Used by: Garrick (memory); Tansy (secretly). Tracked individually. Art: MISSING ART: folded shawl.
- Story: the fever winter took her three years ago. Garrick keeps her shawl folded at the foot of his bed and cannot throw it away. Tansy sometimes wears it on cold mornings when she thinks he isn't looking. He always notices, and never says so.

**item_recipe_book — Sima's recipe book** (unique, not for sale, 0.8 kg, stack 1, does not spoil)
- From: `loc_home_fenn` (her mother's handwriting; Sima can read a little). Used by: Sima (baking; teaching Tom). Tracked individually. Art: MISSING ART: recipe book.
- Story: her mother's recipes, written in a careful hand Sima is still learning to read properly. The festival seed-cake recipe is on the third page, with a note in the margin: "more honey than you think." Sima has never told Oda about the note.

---

## 3. Effects and the hunger math

Effects use the same 0–100 scales as CHARACTERS.md. Hunger rises 6/hr for an active adult (≈96/day over 16 waking hours), so three meals of ~30 cover a day: a rye loaf (−30) is a full meal, an apple (−15) is half a meal, a roll (−12) is a snack. This matches ECONOMY.md's cost of living (~7 copper/day on food: a 3-copper loaf plus extras). Laborers and children (7/hr) eat four smaller meals or bigger portions — bread and apples scale naturally.

Health effects are small on purpose (reason: food keeps you alive; only Sella's craft heals you — an apple gives +1, a stew +2, a remedy +15, fever-tea +10). Social effects on ale (+5) and small beer (+2) exist because the tavern is the village's social engine (D-09 will use them). Raw pork (−15) vs cooked (−25) is deliberate: cooking matters (WORLD.md), and it's the first hook for the Cooking skill in D-06.

Spoiled food never becomes poison that kills — it becomes sad: halved hunger, or a small health penalty (reason: the tone is cozy with real stakes, not survival-horror; spoilage is an economic loss, which is exactly what ECONOMY.md's ~150 copper/month spoilage sink needs).

---

## 4. Spoilage and the economy's sinks

ECONOMY.md assumes ~150 copper/month of value destroyed by spoilage, breakage, and loss. The shelf lives above make that plausible without any tuning: the bakery alone moves ~40 loaves/day (~120 copper/day in bread), the tavern serves ~40 mugs/day, and the stall moves ~13 apples/day. If ~5% of the village's ~3,000 copper/month food flow spoils — a stale loaf here, a turned stew there, fish that didn't sell by evening — that's ~150. The simulation doesn't need a spoilage *system* beyond the shelf-life rule: each dawn, perishables age one day; past their life, they become their spoiled form (or vanish, for the 1-day stew).

Tools and weapons don't spoil (reason: iron doesn't rot; Doran repairs what breaks — breakage is his business, another small sink). Unique items never spoil and are never sold.

---

## 5. Rename mapping for the orchestrator

Every `item_` id in the approved `npcs.json`, `locations.json`, and `economy.json` is covered: it either survives unchanged, renames per the table, or is intentionally dropped. **Do not edit those files in D-05** — apply these renames after the human approves.

| Old id | New id | Reason |
|---|---|---|
| `item_berry` | `item_berries` | plural naming |
| `item_bread` | `item_bread_rye` | the standard loaf is rye |
| `item_ale_barrel` | `item_ale` | bulk: 1 barrel = 20 mugs — multiply counts by 20 |
| `item_barley` | `item_grain` | merged grain |
| `item_wheat` | `item_grain` | merged grain |
| `item_grain_sack` | `item_grain` | the sack is the unit |
| `item_flour_barley` | `item_flour` | the bakery buys "flour" |
| `item_flour_wheat` | `item_flour` | the bakery buys "flour" |
| `item_flour_sack` | `item_flour` | the sack is the unit |
| `item_horseshoe` | `item_horseshoes` | sold as a set of 4 |
| `item_iron_stock_kg` | `item_iron_stock` | the kilogram is the unit |
| `item_cloth` | `item_cloth_imported` | Tilda's store sells imports |
| `item_herb_dried` | `item_herb_bundle` | one herb item |
| `item_poulitice` | `item_remedy` | one medicine item |
| `item_peel` | `item_tool_basic` | workshop fixture, folded in |
| `item_scales` | `item_tool_basic` | workshop fixture, folded in |
| `item_kettle` | `item_tool_basic` | workshop fixture, folded in |
| `item_brew_kettle` | `item_tool_basic` | workshop fixture, folded in |
| `item_loom` | `item_tool_basic` | workshop fixture, folded in |
| `item_logbook` | `item_tool_basic` | workshop fixture, folded in |
| `item_net` | `item_tool_basic` | workshop fixture, folded in |
| `item_fish_trap` | `item_tool_basic` | workshop fixture, folded in |
| `item_bed_night` | *(dropped)* | a service, not an item — the 10-copper tavern bed price stays in `economy.json` as a service |

New canonical ids with no old counterpart: `item_stone`, `item_fishing_rod`, `item_sword`, `item_pelt` (prices flagged `*` above).

---

## 6. How items connect to the economy

Three examples of items pulling their weight in the systems:

1. **Fever-tea → the healer's winter.** Sella's income (~80 copper/week in D-04) rests on remedies and fever-tea. Herb patches go dormant in winter, so stored bundles become valuable exactly when fever season hits — the +50% winter remedy price in ECONOMY.md emerges from the shelf-life and regrowth rules, not from a script.
2. **Pelts → the winter windfall.** Wolf bounties (50 copper/pelt) are the village's winter income. Pelts as a real item mean the bounty flows through inventory: Ralf must actually have the pelt, the reeve must actually take it, and a stolen pelt is a real theft with a real victim.
3. **Horseshoes → the traveler's purse.** A bed (10, service) plus supper, ale, and horseshoes (12) is the best single traveler sale in the village. When the road is busy, Doran's forge glows; when winter closes the ford, it goes cold — the seasonal traveler curve in D-04 reads directly off the anvil.

---

## 7. Later (kept out of the prototype)

- Distinct workshop fixtures (looms, brew kettles, ovens as items with condition) — folded into `item_tool_basic` for now.
- Regional goods from King's Rest and the neighboring villages (spices, paper, glass) — trade-route content for later phases.
- Crop seeds as distinct items (wheat seed vs. barley seed) — merged into `item_grain` for the prototype.
- Armor and shields — no art exists and no one in the village makes them; a merchant-phase addition.
- Coins as items (pennies, marks, crowns) — money is an integer balance in the simulation, not inventory, per AGENTS.md.
- Magic ingredients beyond herbs (the hollow's rarer plants) — Sella's secret, for later.

---

## 8. Open questions for the human

1. **New-item prices:** stone 2, fishing rod 25, sword 120, pelt 10, spear 45, sling 5 — all flagged `*` above. Approve, or adjust?
2. **List size:** 49 items vs. the 25–35 target. Every item is either priced in the approved economy or explicitly required (stone, fishing rod, sword, story items). Cut deeper (which ones?), or accept the 49?
3. **Fixtures folded into `item_tool_basic`:** the loom, brew kettle, logbook, nets, and traps become "basic tools." Acceptable for the prototype, or should any be distinct items now?
4. **`item_bed_night` dropped as an item** (it's a service — the tavern bed for 10 copper stays priced in the economy). Confirm the simulation should treat lodging as a service, not inventory.
5. **Spoiled food:** reduced effects (sad but edible) as designed, or should spoiled food become a distinct inedible state the simulation must clear?
