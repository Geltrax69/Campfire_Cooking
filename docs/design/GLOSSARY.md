# Glossary

Every name and ID used in the design documents and `Content/`, kept unique across the project. IDs are lowercase snake_case with a type prefix.

## Places
| ID | Name | Meaning |
|---|---|---|
| — | Millbrook | The frontier village (prototype location) |
| — | Wesmark | The kingdom |
| — | King's Rest | The capital |
| — | Eastmarch | The eastern borderland region |
| — | River Alder | The river the village sits on |
| — | Alder Road | The old trade road east, crossing the ford |

## People
| ID | Name | Meaning |
|---|---|---|
| — | Tomas Alder | Founder of Millbrook (80 years ago); his grandchildren run the mill |

## The 20 villagers (D-03)
| ID | Name | Role |
|---|---|---|
| npc_mira_holt | Mira Holt | Apple-stall owner |
| npc_ralf_hale | Ralf Hale | Hunter, lodges with Mira |
| npc_corvin_alder | Corvin Alder | Farmer |
| npc_maren_alder | Maren Alder | Farmer's wife (dairy, poultry, garden) |
| npc_piotr_alder | Piotr Alder | Farmhand, 16 |
| npc_lida_alder | Lida Alder | Child, 8 |
| npc_bessa_marlowe | Bessa Marlowe | Tavern keeper, brews her own ale |
| npc_doran_kettle | Doran Kettle | Blacksmith (widower) |
| npc_bram_stone | Bram Stone | Village guard |
| npc_tilda_bray | Tilda Bray | General-store owner (widow) |
| npc_oda_fenn | Oda Fenn | Baker |
| npc_sima_fenn | Sima Fenn | Baker's wife |
| npc_tom_fenn | Tom Fenn | Baker's son, 12 |
| npc_sella_wren | Sella Wren | Healer |
| npc_garrick_alder | Garrick Alder | Miller (widower) |
| npc_tansy_alder | Tansy Alder | Miller's daughter, 10 |
| npc_elswith_alder | Elswith Alder | Village elder, holds granary key |
| npc_tam_oakes | Tam Oakes | Woodcutter |
| npc_brynn_oakes | Brynn Oakes | Woodcutter's wife (weaving, herbs) |
| npc_jory_reed | Jory Reed | Fisherman |

## Beliefs, mysteries
| ID | Name | Meaning |
|---|---|---|
| — | the Hearth | Folk faith: household shrines, remembrance, small kindnesses; no intervening gods |
| — | the Grey | Something large and quiet glimpsed in the deep forest (rumor hook for later) |

## Calendar
| ID | Name | Meaning |
|---|---|---|
| — | Firstday … Sixthday, Restday | 7-day week; Restday is the rest day |
| — | Thawday | Festival: first Restday of spring |
| — | Harvest Feast | Festival: last Restday of autumn |
| — | Longnight | Festival: midwinter's longest night, bonfire in the square |

## Items (canonical, D-05)

Phase 1 = Apple Test + daily life; 2 = economy expansion; 3 = skills/crafting and later.

| ID | Name | Phase |
|---|---|---|
| item_ale | Ale (mug) | 1 |
| item_apple | Apple | 1 |
| item_axe | Axe | 1 |
| item_bread_barley | Barley loaf | 1 |
| item_bread_rye | Rye loaf | 1 |
| item_egg | Egg | 1 |
| item_firewood | Firewood (bundle) | 1 |
| item_fish | Fish (fresh) | 1 |
| item_flour | Flour (sack) | 1 |
| item_grain | Grain (sack) | 1 |
| item_herb_bundle | Herb bundle | 1 |
| item_knife | Knife | 1 |
| item_remedy | Remedy (vial) | 1 |
| item_stew | Stew (bowl) | 1 |
| item_berries | Berries (basket) | 2 |
| item_cheese | Cheese (portion) | 2 |
| item_cloth_imported | Cloth (length, imported) | 2 |
| item_cloth_local | Cloth (length, local) | 2 |
| item_dye | Dye (pot) | 2 |
| item_fever_tea | Fever-tea (packet) | 2 |
| item_fish_smoked | Smoked fish | 2 |
| item_granary_key | Granary key | 2 |
| item_honey | Honey (jar) | 2 |
| item_horseshoes | Horseshoes (set) | 2 |
| item_lamp_oil | Lamp oil (flask) | 2 |
| item_ledger | Tilda's tab ledger | 2 |
| item_log | Log | 2 |
| item_nails | Nails | 2 |
| item_pear | Pear | 2 |
| item_pork | Pork (cut) | 2 |
| item_ribbon | Ribbon | 2 |
| item_roll | Bread roll | 2 |
| item_rope | Rope (coil) | 2 |
| item_salt | Salt (pouch) | 2 |
| item_small_beer | Small beer | 2 |
| item_bow | Hunting bow | 3 |
| item_fishing_rod | Fishing rod | 3 |
| item_hammer | Hammer | 3 |
| item_hoe | Hoe | 3 |
| item_iron_stock | Iron stock (kg) | 3 |
| item_pelt | Pelt | 3 |
| item_recipe_book | Sima's recipe book | 3 |
| item_shawl | Garrick's wife's shawl | 3 |
| item_sling | Sling | 3 |
| item_spear | Spear | 3 |
| item_stone | Stone (block) | 3 |
| item_sword | Sword | 3 |
| item_tool_basic | Basic tools (kit) | 3 |
| item_wooden_toy | Carved wooden chicken | 3 |

Services (not items): `service_lodging` (tavern bed, 10 copper), `service_grinding` (miller's toll, in kind), `service_repair` (8/25 copper).
## Skills, species, groups, town (D-06, D-08, D-09, D-10)

| ID | Name |
|---|---|
| skill_cooking | Cooking |
| skill_taming | Taming |
| skill_swordsmanship | Swordsmanship |
| species_chicken | Chicken |
| species_pig | Pig (domestic) / Boar (wild) |
| species_deer | Deer |
| species_wolf | Wolf |
| species_brambleback | Brambleback |
| group_children | reputation group |
| group_council | reputation group |
| group_farmers | reputation group |
| group_guards | reputation group |
| group_merchants | reputation group |
| group_villagers | reputation group |
| event_bridge_project | town stat/event |
| event_drought | town stat/event |
| event_festival | town stat/event |
| event_fever | town stat/event |
| event_fire | town stat/event |
| event_food_shortage | town stat/event |
| event_merchant_arrival | town stat/event |
| event_theft_wave | town stat/event |
| event_wheel_failure | town stat/event |
| event_wolf_attack | town stat/event |
| stat_crime | town stat/event |
| stat_employment | town stat/event |
| stat_food_supply | town stat/event |
| stat_happiness | town stat/event |
| stat_housing | town stat/event |
| stat_infrastructure | town stat/event |
| stat_population | town stat/event |
| stat_reputation | town stat/event |
| stat_safety | town stat/event |
| stat_trade | town stat/event |
| stat_wealth | town stat/event |
| town_millbrook | town stat/event |

## Recipes and new items (D-07)

| ID | Name |
|---|---|
| recipe_campfire_stew | Campfire Stew |
| recipe_roast_fish | Roast Fish |
| recipe_porridge | Grain Porridge |
| recipe_broth | Thin Broth |
| recipe_bake_bread_rye | Rye Loaf (batch) |
| recipe_roast_pork | Roast Pork |
| recipe_brew_ale | Ale (batch) |
| recipe_apple_pie | Apple Pie |
| recipe_meat_pie | Meat Pie |
| recipe_seed_cake | Festival Seed-Cakes |
| recipe_feast_stew | Feast Stew |
| recipe_smoke_fish | Smoked Fish |
| recipe_master_stew | The Stew They Cross the Ford For |
| recipe_forge_nails | Nails (batch) |
| recipe_forge_horseshoes | Horseshoes (2 sets) |
| recipe_forge_knife | Knife |
| recipe_forge_hoe | Hoe |
| recipe_make_spear | Spear |
| recipe_carve_fishing_rod | Fishing Rod |
| recipe_patch_tool | Patch a Tool |
| recipe_sharpen_blade | Sharpen a Blade |
| item_roasted_fish | Roast Fish |
| item_porridge | Grain Porridge |
| item_broth | Thin Broth |
| item_apple_pie | Apple Pie |
| item_roast_pork | Roast Pork |
| item_seed_cake | Festival Seed-Cake |
| item_meat_pie | Meat Pie |

## Locations, coins, debts, sources, sinks (D-02, D-04)

| ID | Name |
|---|---|
| loc_apple_stall | Holt's Apple Stall |
| loc_general_store | Bray's General Store |
| loc_bakery | Crust & Crumb Bakery |
| loc_farm | Alder Farm |
| loc_tavern | The Hearthside |
| loc_blacksmith | Ember & Iron Smithy |
| loc_mill | Alder Watermill |
| loc_square | Market Square |
| loc_well | The Old Well |
| loc_granary | Village Granary |
| loc_guard_post | Guard Post |
| loc_healer_hut | Healer's Hut |
| loc_shrine | Old River Shrine |
| loc_forest_edge | Alder Forest Edge |
| loc_river_alder | River Alder & Ford |
| loc_home_miller | Miller's House |
| loc_home_mira | Mira's Cottage |
| loc_home_bray | Bray's House |
| loc_home_fenn | Fenn's House |
| loc_home_smith | Kettle's House |
| loc_home_guard | Stone's Cottage |
| loc_home_elder | Elder's Cottage |
| loc_home_woodcutter | Oakes' Cottage |
| coin_copper | Copper penny |
| coin_silver | Silver mark |
| coin_gold | Gold crown |
| debt_doran_iron | debt: npc_doran_kettle owes 120 |
| debt_bessa_staples | debt: npc_bessa_marlowe owes 80 |
| debt_garrick_nails | debt: npc_garrick_alder owes 60 |
| debt_jory_rope | debt: npc_jory_reed owes 45 |
| debt_tam_pork | debt: npc_tam_oakes owes 35 |
| source_merchants | source: Traveling merchants buying village goods |
| source_travelers | source: Traveler spending at tavern and stores |
| source_bounties | source: Wolf bounties from the crown |
| sink_taxes | sink: Reeve's tax collection |
| sink_imports | sink: Import purchases from merchants |
| sink_community_fund | sink: Community fund (wheel, palisade, granary) |
| sink_spoilage | sink: Spoilage, breakage, loss |
| sink_feast | sink: Harvest Feast reserve |
