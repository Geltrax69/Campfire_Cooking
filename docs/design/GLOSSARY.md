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