# Living World — Recipes, Cooking and Crafting (D-07)

## Summary

Millbrook's prototype has 21 recipes: 13 cooking (campfire, tavern kitchen, bakery oven, brew kettle, smokehouse) and 8 crafting (forge, workbench). Cooking recipes map one-to-one onto the Cooking skill's five levels — a level-1 cook makes campfire stew, a level-5 cook makes the stew travelers cross the ford for. Crafting has no dedicated skill in Phase 1 (per SKILLS.md's "Later" list), so forge and workbench recipes are gated by place access and NPC permission instead: the player's lane is nails, repairs, and simple work — they cannot out-produce Doran, by design. Every recipe states inputs, time, quality rules, failure chance, and whether it is profitable to sell. Seven new output items (roasted fish, porridge, broth, apple pie, roast pork, seed cake, meat pie) are proposed with full item specs for the orchestrator to add to `items.json`.

---

## 1. How recipes work

**Quality.** Output quality = `clamp(average input quality + skill qualityBonus, 0, 100)`. The skill's `qualityBonus` is 0/+1/+2/+3/+4 (SKILLS.md). Shop goods sell at quality 50 unless the maker is skilled; Bessa's stew is quality 60, which is why the tavern's food has a reputation. `recipe_master_stew` has a floor: quality never below 80.

**Quality changes price.** Sale price = `baseValue + round((quality − 50) / 25)`, minimum 1 copper (reason: consistent with ECONOMY.md's ±1 quality note — a quality-75 stew sells for 5, a quality-100 stew for 6; modest, so skill matters without breaking the price rules).

**Failure.** `failChance = clamp((difficulty − skillLevel + 1) × 10, 0, 40)%`. A level-1 cook attempting a difficulty-1 recipe fails 10% of the time; attempting two levels above their skill fails 30%. On failure all inputs are lost (reason: the pigs eat well — failure must sting, but at 10% for at-level work it stays cozy, not punishing).

**Fuel.** Campfire recipes consume 1 `item_firewood` (fire is a need, not flavor — WORLD.md). Kitchen, oven, forge, and brew-kettle fuel is the owner's cost, folded into the permission to use the place.

**Permission.** Recipes at someone else's place need that NPC's willingness — from relationship and reputation, never a button (SKILLS.md teaching rule, applied to places). Oda's oven, Bessa's kitchen and brew kettle, Doran's forge, Jory's smokehouse, Tam's workbench are all borrowed, not owned.

**Level gates are hard.** A recipe's `minLevel` is required; the skill-level → recipe mapping in section 7 *is* the unlock model that the orchestrator wires into `skills.json`.

**Tool condition.** Tools and weapons have a 0–100 condition (new = 100), degrading with use — a Phase 3 simulation detail. `recipe_patch_tool` and `recipe_sharpen_blade` are the repair path; until condition is simulated, they are how the design says maintenance works.

---

## 2. Cooking recipes

### Level 1 — Campfire basics (`skill_cooking`, campfire at `loc_river_alder`)

**recipe_campfire_stew — Campfire Stew.** 45 min. Inputs: 1× `item_fish`, 1× `item_firewood`. Output: 2× `item_stew`. Value vs. ingredients: 5 in → 8 out — profitable, but 45 minutes and a 10% failure chance at level 1 keep it honest. This is the recipe every cook starts with, and the one the Apple Test's player most likely knows.

**recipe_roast_fish — Roast Fish.** 20 min. Inputs: 1× `item_fish`, 1× `item_firewood`. Output: 1× `item_roasted_fish` (new). Value: 5 in → 5 out — break-even; roasting is about eating well at the river, not commerce.

**recipe_porridge — Grain Porridge.** 25 min. Inputs: 1× `item_grain`, 1× `item_firewood`. Output: 2× `item_porridge` (new). Value: 10 in → 6 out — a loss in copper, because grain is worth more uncooked; porridge is what you eat when the bakery is closed and you're hungry now.

**recipe_broth — Thin Broth.** 30 min. Inputs: 1× `item_fish`, 1× `item_firewood`. Output: 3× `item_broth` (new). Value: 5 in → 9 out — the soup-kitchen staple; this is the recipe behind the winter soup kitchen in SKILLS.md's Soup Making chain.

### Level 2 — Kitchen hand

**recipe_bake_bread_rye — Rye Loaf (batch).** 90 min. Oven at `loc_bakery`, permission: `npc_oda_fenn`. Inputs: 1× `item_flour`. Output: 10× `item_bread_rye`. Value: 10 in → 30 out — very profitable, but the oven is the gate: one oven, Oda's morning bake comes first, and his willingness is the real price.

**recipe_roast_pork — Roast Pork.** 120 min. Kitchen at `loc_tavern`, permission: `npc_bessa_marlowe`. Inputs: 1× `item_pork`, 1× `item_herb_bundle`. Output: 1× `item_roast_pork` (new). Value: 15 in → 18 out — slightly profitable; a Sunday dish, not a business.

**recipe_brew_ale — Ale (batch).** 120 min active + 3 days fermenting. Brew kettle at `loc_tavern`, permission: `npc_bessa_marlowe`. Inputs: 1× `item_grain`. Output: 20× `item_ale` (available after 3 days). Value: 8 in → 40 out — the best margin in the village, which is exactly why Bessa does it herself and why the kettle is never free when you want it. Brewing is an investment over time, not a quick flip.

### Level 3 — Village cook

**recipe_apple_pie — Apple Pie.** 60 min. Kitchen at `loc_tavern`. Inputs: 4× `item_apple`, 1× `item_flour`. Output: 2× `item_apple_pie` (new). Value: 22 in → 16 out — a loss in copper and a gain in everything else: this is the recipe that visibly raises apple demand (see section 5).

**recipe_meat_pie — Meat Pie.** 75 min. Kitchen at `loc_tavern`. Inputs: 1× `item_pork`, 1× `item_flour`, 1× `item_egg`. Output: 2× `item_meat_pie` (new). Value: 23 in → 20 out — near break-even; solid tavern food.

**recipe_seed_cake — Festival Seed-Cakes.** 60 min. Oven at `loc_bakery`, permission: `npc_oda_fenn`. Inputs: 1× `item_flour`, 1× `item_honey`, 2× `item_egg`. Output: 4× `item_seed_cake` (new). Value: 20 in → 12 out — a loss, made for love and reputation: Sima's recipe book holds the secret ("more honey than you think"), and festival cakes are how a cook becomes *known*.

### Level 4 — The tavern's draw

**recipe_feast_stew — Feast Stew.** 120 min. Kitchen at `loc_tavern`. Inputs: 1× `item_pork`, 2× `item_fish`, 1× `item_herb_bundle`. Output: 4× `item_stew`. Value: 21 in → 16 out at base quality — but a level-4 cook's +3 bonus pushes quality to ~80, selling at 5–6 each (20–24 out). This is the first recipe where *skill itself* is the profit.

**recipe_smoke_fish — Smoked Fish.** 180 min. Smokehouse at `loc_river_alder`, permission: `npc_jory_reed`. Inputs: 3× `item_fish`, 1× `item_firewood`, 1× `item_salt`. Output: 3× `item_fish_smoked`. Value: 17 in → 12 out — a loss in copper, a gain in *time*: 2-day fish becomes 60-day fish. Preservation is the product.

### Level 5 — A reputation beyond the ford

**recipe_master_stew — The Stew They Cross the Ford For.** 180 min. Kitchen at `loc_tavern`. Inputs: 1× `item_pork`, 2× `item_fish`, 1× `item_herb_bundle`, 1× `item_honey`. Output: 3× `item_stew`, minimum quality 80. Value: 29 in → 18+ out — a loss in copper, always. Nobody makes this to get rich; they make it so travelers tell the story down the road. The value is reputation, which is exactly what SKILLS.md says a level-5 cook trades in.

---

## 3. Crafting recipes

No skill gates in Phase 1 — place access, NPC permission, and time are the gates (see decisions). Difficulty still drives failure chance, using an implicit craft level of 1 for the untrained (so difficulty-1 work fails 10%, difficulty-3 work fails 30% — the player learns by wasting iron, like everyone).

**recipe_forge_nails — Nails (batch).** 60 min. Forge at `loc_blacksmith`, permission: `npc_doran_kettle`. Inputs: 1× `item_iron_stock`. Output: 20× `item_nails`. Value: 15 in → 20 out — Doran's actual business, shared with someone he trusts.

**recipe_forge_horseshoes — Horseshoes (2 sets).** 90 min. Forge at `loc_blacksmith`, permission: `npc_doran_kettle`. Inputs: 2× `item_iron_stock`. Output: 2× `item_horseshoes`. Value: 30 in → 24 out — a loss, because the player's hammer-work is unskilled. Doran's margin is his craft; the recipe exists so the player *feels* the gap. (Reason: the player must not out-produce the smith — the village's economy depends on Doran being better at this.)

**recipe_forge_knife — Knife.** 120 min. Forge at `loc_blacksmith`, permission: `npc_doran_kettle`. Inputs: 1× `item_iron_stock`, 1× `item_log`. Output: 1× `item_knife`. Value: 23 in → 30 out — slightly profitable at decent quality; a knife is simple enough that care beats craft.

**recipe_forge_hoe — Hoe.** 150 min. Forge at `loc_blacksmith`, permission: `npc_doran_kettle`. Inputs: 2× `item_iron_stock`, 1× `item_log`. Output: 1× `item_hoe`. Value: 38 in → 35 out — near break-even; Corvin would rather buy from Doran, which tells you who this recipe is really for (the player, making their own).

**recipe_make_spear — Spear.** 120 min. Forge at `loc_blacksmith`, permission: `npc_doran_kettle`. Inputs: 1× `item_iron_stock`, 1× `item_log`. Output: 1× `item_spear`. Value: 23 in → 45 out — profitable, but demand is tiny: Bram, the winter watch, and nobody else. The village needs about two spears a year.

**recipe_carve_fishing_rod — Fishing Rod.** 120 min. Workbench at `loc_home_woodcutter`, taught by `npc_tam_oakes` (permission). Inputs: 1× `item_log`. Output: 1× `item_fishing_rod`. Value: 8 in → 25 out — Tam's craft, taught, not taken: the teaching rule (×2 practice, SKILLS.md) applies to learning it.

**recipe_patch_tool — Patch a Tool.** 30 min. Forge or workbench at `loc_blacksmith`, permission: `npc_doran_kettle`. Inputs: 1× `item_iron_stock`. Output: repair — restores 30 condition to one tool. Value: 15 in vs. the 8/25-copper `service_repair` in the economy — comparable, and available at midnight when Doran is asleep.

**recipe_sharpen_blade — Sharpen a Blade.** 20 min. Anywhere (whetstone is a fixture). Skill: `skill_swordsmanship`, min level 1. No inputs. Output: repair — restores 15 condition to one weapon. Value: free, but skill-gated — a swordsman maintains their own blade, which is part of what the 120-copper sword investment buys.

---

## 4. New output items (for the orchestrator to add to `items.json`)

Full specs, all phase 3 (cooking is a Phase 3 skill system), all bulk, quality range 0–100, two-stage spoilage per the human's D-05 decision:

- **item_roasted_fish — Roast Fish** (food, 5 copper, 0.5 kg, stack 20, fresh 2 days → stale: hunger −12 → spoiled: animal feed/compost). Effects: hunger −25, health +1. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/fish.glb`.
- **item_porridge — Grain Porridge** (food, 3 copper, 0.5 kg, stack 10, fresh 1 day → stale: hunger −14 → spoiled: animal feed/compost). Effects: hunger −28. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/bowl-cereal.glb`.
- **item_broth — Thin Broth** (food, 3 copper, 0.5 kg, stack 10, fresh 1 day → stale: hunger −10 → spoiled: animal feed/compost). Effects: hunger −20, health +2. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/bowl-broth.glb`.
- **item_apple_pie — Apple Pie** (food, 8 copper, 0.6 kg, stack 10, fresh 3 days → stale: hunger −15 → spoiled: animal feed/compost). Effects: hunger −30, health +1. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/pie.glb`.
- **item_roast_pork — Roast Pork** (food, 18 copper, 1.2 kg, stack 10, fresh 2 days → stale: hunger −20 → spoiled: animal feed/compost). Effects: hunger −40, health +2. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/meat-cooked.glb`.
- **item_seed_cake — Festival Seed-Cake** (food, 3 copper, 0.2 kg, stack 20, fresh 4 days → stale: hunger −8 → spoiled: animal feed/compost). Effects: hunger −15, social +2. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/cake.glb`.
- **item_meat_pie — Meat Pie** (food, 10 copper, 0.6 kg, stack 10, fresh 2 days → stale: hunger −18 → spoiled: animal feed/compost). Effects: hunger −35. Art: `Assets/Packs/Kenney/FoodKit/Models/GLB format/mincemeat-pie.glb`.

---

## 5. How recipes connect to the economy

1. **Apple pie raises apple demand.** A level-3 cook making pies for the square buys 4 apples per batch from Mira's stall — the stall's ~13/day demand visibly grows. The no-reset stock rule (D-02/D-04) means the shortage is *real*: Corvin's twice-weekly deliveries become the bottleneck, his negotiable wholesale price (D-04 decision 3) gets exercised for the first time, and the single-supplier tension the design built becomes something the player can feel. One recipe makes the whole apple chain load-bearing.
2. **Feast stew fills the tavern.** A level-4 cook's high-quality stew is why travelers stay an extra night (SKILLS.md's "tavern's good season" chain): bed + ale + supper revenue rises ~30%, Bessa orders more grain from Corvin, Corvin's income rises, and the reeve's prosperity-scaled tax takes its cut next collection. The player's cooking literally pays the village's taxes.
3. **Forge nails cost the village iron.** Every nail the player makes consumes 1 kg of the village's imported iron stock — and iron only arrives on the merchant's cart, weeks apart. Crafting has an opportunity cost the whole village feels: nails for the player's project are nails Doran can't use for horseshoes. The import economy (D-04) becomes tangible at the anvil.

---

## 6. Skill-level → recipe unlock mapping (for the orchestrator)

Wire these `recipe_` ids into the `unlocks` arrays in `Content/skills/skills.json`:

- `skill_cooking` L1: `recipe_campfire_stew`, `recipe_roast_fish`, `recipe_porridge`, `recipe_broth`
- `skill_cooking` L2: `recipe_bake_bread_rye`, `recipe_roast_pork`, `recipe_brew_ale`
- `skill_cooking` L3: `recipe_apple_pie`, `recipe_meat_pie`, `recipe_seed_cake`
- `skill_cooking` L4: `recipe_feast_stew`, `recipe_smoke_fish`
- `skill_cooking` L5: `recipe_master_stew`
- `skill_swordsmanship` L1: `recipe_sharpen_blade`
- `skill_taming`: no recipes (taming unlocks are animal interactions, D-08's domain)

---

## 7. Later (kept out of the prototype)

- A dedicated Crafting/Blacksmithing skill with the same 5-level system (SKILLS.md "Later") — when it exists, the forge recipes gain skill gates and Doran becomes a teacher with explicit levels.
- Distinct workshop fixtures (looms, brew kettles, ovens as items with condition) — folded into `item_tool_basic` for now.
- Recipe discovery and experimentation (burning dinner to learn) — currently recipes are known once unlocked; experimentation is a Phase 3+ system.
- Regional recipes from King's Rest and the neighboring villages (trade-route content).
- The mill wheel fund as a crafting goal (contributing nails and timber to the 8,000-copper wheel).

---

## 8. Decisions made without the human

1. **21 recipes: 13 cooking + 8 crafting** — the top of the brief's ranges, because the economy already prices the goods and each skill band needed at least one recipe per level.
2. **No Crafting/Blacksmithing skill in Phase 1** (it's on SKILLS.md's "Later" list). Forge and workbench recipes are gated by place access, NPC permission, and time instead of skill levels.
3. **`skill_swordsmanship` L1 unlocks `recipe_sharpen_blade`** — a swordsman maintains their own blade; it's part of what the 120-copper sword investment buys.
4. **Hard level gates**: a recipe's `minLevel` is required; the unlock mapping *is* the gate, matching `skills.json`'s unlock model.
5. **Failure wastes all inputs** (the pigs eat well); `failChance = clamp((difficulty − skillLevel + 1) × 10, 0, 40)%`.
6. **Output quality = clamp(avg input quality + skill qualityBonus, 0–100)**; `recipe_master_stew` has a floor of 80.
7. **Sale price adjusts with quality**: `baseValue + round((quality − 50) / 25)`, min 1 — consistent with ECONOMY.md's ±1 quality note.
8. **Seven new output items** proposed with full specs (section 4) for the orchestrator to add to `items.json` as phase 3.
9. **Fermentation**: `recipe_brew_ale`'s output is available 3 days after brewing — brewing is an investment over time, not a quick flip.
10. **Fuel**: campfire recipes consume 1 `item_firewood`; kitchen/oven/forge fuel is the owner's cost, folded into access permission.

---

## 9. New IDs for the glossary

`recipe_campfire_stew`, `recipe_roast_fish`, `recipe_porridge`, `recipe_broth`, `recipe_bake_bread_rye`, `recipe_roast_pork`, `recipe_brew_ale`, `recipe_apple_pie`, `recipe_meat_pie`, `recipe_seed_cake`, `recipe_feast_stew`, `recipe_smoke_fish`, `recipe_master_stew`, `recipe_forge_nails`, `recipe_forge_horseshoes`, `recipe_forge_knife`, `recipe_forge_hoe`, `recipe_make_spear`, `recipe_carve_fishing_rod`, `recipe_patch_tool`, `recipe_sharpen_blade`.
