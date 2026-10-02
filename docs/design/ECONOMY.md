# Living World — Money and the Economy (D-04)

## Summary

Millbrook runs on copper — literally: all money is integer copper, with silver marks (100 copper) for larger sums and gold crowns (10,000 copper, which no villager has ever held) above that. One farm supplies the village's apples, one mill grinds its grain, one bakery feeds it, one tavern waters it, and one traveling-merchant route connects it to the outside world. Every one of the 20 villagers can afford their life — the weekly budget table proves it — though some only just. The apple stall starts with 20 apples, never resets, and lives or dies on two farm deliveries a week: steal 6 and the stall sells out within a day and a half. Money enters through merchants, travelers, and wolf bounties, and leaves through taxes, imports, and the community fund; the reeve's prosperity-scaled tax and the village's import appetite keep the total stable.

---

## 1. Currency

All money is stored as integer copper. Coins in circulation:

| ID | Name | Value (copper) | What it's for |
|---|---|---|---|
| coin_copper | Copper penny | 1 | Everything daily: bread, ale, apples |
| coin_silver | Silver mark | 100 | Larger sums: a tool, a month's rent-equivalent, tax payments |
| coin_gold | Gold crown | 10,000 | Royal and merchant scale. No one in Millbrook has ever held one (reason: keeps the village economy legibly small — the biggest local fortune is ~14 silver) |

**Why copper-only accounting:** the simulation never touches fractions, so money can never drift by rounding (AGENTS.md rule). Prices are integers; the smallest price step is 1 copper.

**Barter and credit** are normal alongside coin:
- **Farmgate barter:** eggs for mending, fish for bread, labor for meals. Common among households; the simulation treats it as paired transfers with no coin moving.
- **Tavern tabs (Bessa):** regulars run small weekly tabs, settled on Restday. Informal, capped by Bessa's judgment (~30 copper).
- **Store tabs (Tilda):** the secret ledger — real, mechanical debts (see section 6). No interest (reason: Tilda believes credit ruins people; she extends it anyway, quietly, and remembers).
- **Favors:** Sella takes coin, eggs, or favors interchangeably (reason: the healer takes what people can give — WORLD.md).

## 2. Cost of living

A normal adult needs about **10 copper a day** to live: ~7 for food (bread 5–6, ale or extras 1–2), ~2 for the household (fuel, candles, soap, mending), ~1 amortized for clothes and tools. Children cost ~5–6 (they eat often but little else). Farm households grow much of their own food, so their *cash* outgoings are lower — the table below is cash, with home-grown food noted.

**Weekly budget for all 20 villagers** (income vs. cash outgoings per week; every number has a reason in the Notes):

| NPC | Income/wk | Outgoings/wk | Net/wk | Note |
|---|---|---|---|---|
| Mira Holt | 170 | 84 | +86 | Sells ~13 apples/day at 3 copper, buys them at 1 from Corvin; saves toward the 2,000 stall refit |
| Ralf Hale | 85 | 70 | +15 | Hunter: meat sales + village winter retainer; solitary, spends little |
| Corvin Alder | 220 | 110 | +110 | Farm net; feeds 4, keeps Piotr, builds the tax reserve |
| Maren Alder | 35 | 28 | +7 | Egg money is her own; household food is home-grown |
| Piotr Alder | 0 | 14 | −14 | Covered by Corvin; his 80 copper stays saved toward the 500 road fund |
| Lida Alder | 0 | 0 | 0 | Family covers everything; 8 copper is treasure, not budget |
| Bessa Marlowe | 180 | 95 | +85 | Tavern net; eats her own food at cost |
| Doran Kettle | 140 | 84 | +56 | Lumpy trade; repays Tilda 10/wk of the 120 debt |
| Bram Stone | 140 | 77 | +63 | Guard's wage, 20/day from the village fund |
| Tilda Bray | 150 | 84 | +66 | Store margin on imports; carries 340 copper in tab receivables |
| Oda Fenn | 200 | 126 | +74 | Bakery net; household of 3 |
| Sima Fenn | 0 | 21 | −21 | Covered by Oda; her 150 is personal savings |
| Tom Fenn | 0 | 7 | −7 | Covered by Oda; errands are unpaid |
| Sella Wren | 80 | 63 | +17 | Remedies and visits; takes favors as well as coin |
| Garrick Alder | 130 | 98 | +32 | Miller's toll in kind + cash fees; household of 2; owes Tilda 60 |
| Tansy Alder | 0 | 0 | 0 | Family |
| Elswith Alder | 56 | 56 | 0 | Council stipend, 8/day; exactly covers her; 500 copper buffer for lean times |
| Tam Oakes | 100 | 77 | +23 | Logs, firewood, carving; owes Tilda 35 |
| Brynn Oakes | 40 | 56 | −16 | Weaving; household shares Tam's income — household net +7 |
| Jory Reed | 75 | 63 | +12 | Fish; owes Tilda 45, repays in smoked fish |

**The table adds up:** every NPC either earns their keep or is explicitly covered by a household head. The 20 together net about **+588 copper/week** (~2,350/month). That surplus is the village's savings engine: roughly 40% goes to the tax reserve, 30% to the community fund (the wheel, the palisade), the rest to household savings (section 6 explains the sinks that keep the total stable).

**What a normal adult earns and spends per day (the headline numbers):** earns ~20 (a guard's wage; skilled trades 20–30), spends ~10 on living. A loaf of rye (3) is about an hour's unskilled labor — food is affordable, savings are slow, and a 30-copper knife is a real purchase. That is the intended feel: nobody starves in a normal year, nobody gets rich quick.

## 3. Production chains

Every chain states who produces what, how much, and what it costs them. Quantities are per day unless noted; seasons are 90 days.

**Apples: orchard → stall.** Alder Farm's orchard yields ~40 pickable apples/day in autumn (reason: ~30 mature trees × ~120 apples per season, picked over 90 days). Corvin delivers **45 apples twice a week** (Secondday and Fifthday, 08:00, 1 minute's walk per the travel table) to Mira's stall at **1 copper each** wholesale (reason: farmgate price ≈ a third of retail — the stall's work is the other two thirds). Mira retails at 3. Windfalls (~10/day in autumn) go to cider, children, and Tom. In winter the orchard sleeps: deliveries drop to 20 twice a week from the root cellar (~1,000 stored), and the price rules (section 4) lift the retail price. **The stall never resets** — its stock is last delivery minus sales minus theft, full stop.

**Grain: farm → mill → bakery.** Autumn harvest: ~60 sacks wheat, ~40 sacks barley from Alder Farm (reason: one farm feeds ~120 people; a sack feeds a household ~2 weeks). Corvin carts grain to the mill; Garrick takes the **miller's toll of 1/12 in kind** (reason: the traditional toll — paid in grain, not coin, per LOCATIONS.md) and grinds the rest. Oda buys flour at **10 copper/sack**, ~2.5 sacks/day, and bakes ~40 loaves/day (reason: one oven, morning bake only — the bakery sells out by early afternoon). The communal granary holds the village reserve: currently ~50 sacks against ~100 needed (reason: last year's thin barley — WORLD.md's problem; Elswith rations quietly).

**Ale: grain + well water → tavern.** Bessa buys grain from Corvin at 8 copper/sack and brews **2 batches/week, 20 mugs per batch** (reason: one brew kettle, one brew day each — her schedule has brewhouse mornings). Sells at 2 copper/mug. Grain cost 16/week against ~80/week ale revenue — brewing is her best margin and why she does it herself.

**Timber: forest → village.** Tam cuts **~1 log/day** (reason: LOCATIONS.md — the forest regrows ~1/day, so one woodcutter is sustainable and two are not). A log sells at 8 copper (to Doran for handles, Corvin for fences, villagers for building). Firewood: ~2 bundles/day per household in winter at 2 copper/bundle — Tam and Piotr supply it; it is a need, not flavor. Tam also carves (Lida's wooden chicken is the prototype).

**Fish: river → tables.** Jory catches ~3/day against a stock of ~60 regrowing 3/day (reason: LOCATIONS.md — reliable but not infinite). Sells fresh at 3, smoked at 4. Unattended traps are the classic petty theft.

**Iron: merchants → smithy → village.** Iron is imported — Doran cannot make it, only shape it. He keeps ~20 kg stock, buys from traveling merchants at ~10 copper/kg, sells nails at 1 copper each, tools at 40–80, horseshoes at 12/set, repairs at 8–25. Every nail is precious because the next merchant is weeks away.

**Eggs, pork, honey (farmgate):** Maren sells ~5 eggs/day at 1 copper; autumn slaughter gives ~200 pork cuts at 12; ~10 jars of honey at 8 in autumn. Small, steady, cash.

**Remedies: hollow → healer → village.** Sella and Brynn gather from the 12 herb patches (regrow in 3 days, dormant in winter — LOCATIONS.md). ~6 remedies/week at 5–10 copper, or eggs/favors. Winter scarcity makes stored remedies valuable — a real seasonal price story.

**Cloth: Brynn → Tilda → village.** Brynn weaves ~1 length per 2 weeks; Tilda buys at 15, retails at 25 (reason: imported cloth is dearer, so local weaving undercuts it — the village's one import substitution).

## 4. Prices

Base prices in copper (provisional `item_` ids — D-05 canonicalizes them):

| Item | Base | Item | Base | Item | Base |
|---|---|---|---|---|---|
| Apple | 3 | Rye loaf | 3 | Ale (mug) | 2 |
| Pear | 4 | Barley loaf | 2 | Small beer | 1 |
| Berries (basket) | 4 | Roll | 1 | Stew (bowl) | 4 |
| Egg | 1 | Flour (sack) | 10 | Bed (night) | 10 |
| Cheese (portion) | 6 | Salt (pouch) | 6 | Remedy | 5–10 |
| Pork (cut) | 12 | Cloth (length, local) | 25 | Herb bundle | 3 |
| Fish (fresh) | 3 | Cloth (length, imported) | 40 | Firewood (bundle) | 2 |
| Fish (smoked) | 4 | Lamp oil (flask) | 12 | Log | 8 |
| Honey (jar) | 8 | Nails (each) | 1 | Iron stock (kg) | 15 |
| Knife | 30 | Rope | 5 | Hoe | 35 |
| Axe | 60 | Hammer | 40 | Dye (pot) | 20 |
| Bow | 80 | Horseshoes (set) | 12 | Repair (simple/major) | 8 / 25 |

**Price rules** (the simulation's pricing system, not a script):
- **Max daily change:** ±10% of base price, minimum 1 copper (reason: prices move, but never jump — a 3-copper apple can go to 4 in a day, not to 6).
- **Scarcity:** stock below 30% of the shop's normal level → price drifts up, capped at +50% of base (reason: the shopkeeper covers losses and rations demand without gouging).
- **Surplus:** stock above 150% of normal → price drifts down, floor at −30% of base (reason: perishables must move; a glut is a sale).
- **Seasonal:** apples +50% in winter (stored), −20% in autumn glut; fish −20% summer; firewood +100% midwinter; remedies +50% winter (reason: each follows its real supply story).
- **Quality:** bruised/windfall −1, fine specimens +1 (reason: Mira's "take the bruised one, it's sweeter" — she discounts honestly).

## 5. Shops

**Holt's Apple Stall** (`loc_apple_stall`, Mira Holt). Hours 07:00–19:00. **Start stock: 20 apples. Stock carries over day to day and never resets** (`stockRule: carries_over_no_reset`). Restock: Corvin's deliveries, Secondday and Fifthday 08:00, 45 apples at 1 copper wholesale (delivery takes ~1 minute each way; Mira sometimes collects them herself). **Restock trigger:** Mira orders early if stock falls below 10 (reason: ~1 day of demand — she won't be caught empty twice). **When stock runs out:** she tells customers plainly, suggests pears/berries in season, and asks Corvin for an early delivery. **When stock goes missing:** she counts twice, goes cold and quiet, tells Bessa that evening (rumor), complains to Bram next morning (he logs it; no evidence, no action), and watches every customer harder for a week — and quietly suspects Tom.

**Bray's General Store** (`loc_general_store`, Tilda Bray). Hours 08:00–18:00. Sells imports: salt, cloth, lamp oil, nails, rope, basic tools. Restock: traveling merchants every ~3 weeks in warm months; Tilda keeps a full order list for the autumn visit. Her coin box runs bigger than Mira's (reason: imports are expensive and takings sit longer). When an item runs out, she rations regulars first and notes it in the ledger.

**Crust & Crumb Bakery** (`loc_bakery`, Oda Fenn). Hours 06:00–14:00. Bakes ~40 loaves each morning from the mill's flour; sells out by early afternoon most days (reason: one oven). No restock problem — the constraint is the oven, and Oda's long-term goal is a second one (5,000 copper).

**The Hearthside** (`loc_tavern`, Bessa Marlowe). Hours 10:00–23:00. Ale, small beer, stew, bread, beds. Bessa brews 2 batches/week; stew depends on the day's market. Travelers are the margin — a bed (10) plus supper and ale (~8) is the best single sale in the village.

## 6. Money sources and sinks

Money must be conserved: every copper is tracked, and coins only enter or leave through named gates.

**Sources (money enters the village):**
1. **Traveling merchants** buy village goods — apples, timber, honey, smoked meat, Ralf's pelts. Every ~3 weeks in warm months, ~800 copper per visit (reason: a merchant's cart holds only so much, and Millbrook is one stop of many).
2. **Travelers** spend at the tavern and stores — beds, ale, meals, horseshoes. ~750 copper/month in warm months, ~200 in winter (reason: the Alder Road quiets when the ford runs high and cold).
3. **Wolf bounties** — the kingdom pays 50 copper per wolf pelt in winter, 2–4 incidents per winter (reason: WORLD.md — the crown wants the roads safe; this is the village's winter windfall).

**Sinks (money leaves the village):**
1. **Taxes** — the reeve collects twice a year (spring and autumn). The 20 pay ~1,200 per collection (Corvin 180, Tilda 200, Bessa 150, Mira 120, Oda 100, Doran 100, Garrick 80, the rest smaller), i.e. ~400/month equivalent. **The assessment scales with visible prosperity** (reason: the reeve is mostly honest and the village is visibly richer in good years — this is the system's stabilizer: good years pay more).
2. **Imports** — iron, salt, cloth, lamp oil, dyes from the merchants: ~600/month in warm months (reason: Tilda's order list; Doran's iron alone is ~150/month).
3. **Community fund** — the wheel, the palisade, the granary: ~150/month set aside by the council (reason: the village's shared hope has a price — Garrick's 8,000-copper wheel).
4. **Spoilage, breakage, loss** — food spoils, tools break, coins go missing: ~150/month of value destroyed (reason: D-05 will set shelf lives; nothing keeps forever).

**A worked warm month:** inflow 1,550 (merchants 800 + travelers 750) vs. outflow 1,500 (taxes 400 + imports 600 + community 150 + spoilage 150 + feast reserve 200 for the Harvest Feast). **The total stays stable** because (a) the tax assessment rises in good years and falls in bad ones, (b) Tilda orders more imports when the village has coin and fewer when it doesn't, and (c) the council's fund absorbs the remainder. Winter reverses the flow — few travelers, no merchants, but taxes and imports were front-loaded — and the village draws on savings, which is exactly what savings are for. The simulation must reproduce this: total village copper stays within a ±10% band month to month, with the seasonal swing carried by storage (grain, cellared apples, the fund).

## 7. Wages and jobs

- **Bram Stone (guard):** 20 copper/day from the village fund. The fund is fed by a **household levy of 2 copper/week** — the 20 pay 40/week; the background ~100 villagers are abstracted as paying the rest (~800/month), so the fund balances at ~960/month against Bram (600) + Elswith's stipend (240) + Ralf's winter retainer (60/week in winter) + incidentals.
- **Elswith Alder (elder):** council stipend 8/day from the fund (reason: the village pays its elder in respect and coin).
- **Ralf Hale (hunter):** 60/week retainer in winter for the watch and wolf work, plus meat sales; in summer he lives off hunting and odd jobs.
- **Piotr, Tom, Sima:** family labor, no wage — kept by the household, with personal savings (Piotr's 80) or pocket money.
- **Day labor (the player's market):** farm help 8/day, woodcutting help 6/day, mill help 5/day, tavern pot-wash 6/day + a meal, errands 2–5 (reason: unskilled day rates sit just under the guard's 20 — the wage ladder is visible and fair).
- **If someone can't pay:** Doran extends short credit to people he trusts; Tilda's tabs (section 8); the council levy in arrears is noted by Elswith and forgiven in genuine hardship — but a household that won't pay finds trade drying up (reason: WORLD.md law — the reputation system enforces what the council won't).

## 8. Debts — Tilda's tab ledger

Tilda's secret ledger (`item_ledger`, kept at `loc_home_bray`) is a **mechanical system**, not color. Outstanding tabs:

| Debtor | Owes | For | Terms |
|---|---|---|---|
| Doran Kettle | 120 | Iron stock, bought on credit before the autumn merchant visit | Repays 10/week |
| Bessa Marlowe | 80 | Salt and lamp oil over the summer | Repays ~5/week, irregularly |
| Garrick Alder | 60 | Nails and pitch for wheel patching | Behind; Tilda is patient (Tansy's sake) |
| Jory Reed | 45 | Rope and tar | Repays in smoked fish, 2 fish/week (~8 value) |
| Tam Oakes | 35 | Salt pork last winter | Repays 3/week |

Total outstanding: **340 copper**. Terms: **no interest** (reason: Tilda believes credit ruins people — she charges none, but she remembers everything). Debts are expected within the season; at the **Harvest Feast, debts under 20 are forgiven** in the traditional symbolic amounts (reason: WORLD.md festivals — the feast wipes slates partly clean). **Consequences of non-payment:** after ~60 days overdue, Tilda tells Elswith; the council witnesses the debt; the debtor's standing drops — no one extends them credit, Tilda refuses further tabs, and Bessa's tavern hears about it (reason: WORLD.md law — a debtor who won't pay finds no one will trade with them; the simulation's reputation system does the enforcing). New tabs are then refused until the old one clears.

## 9. Theft and loss

- **Petty theft** (apples from the display, orchard windfalls, fish traps, unattended mugs): the owner absorbs the loss, wariness rises, prices may tick up 1 copper, and the story reaches Bessa's tavern within the hour (reason: the rumor system in D-09 feeds on exactly this).
- **Shop theft with evidence** (a witness, the goods found, a confession): Bram investigates — restitution plus a public apology at the Restday gathering for a first offense (reason: WORLD.md law — shame is the real penalty).
- **Burglary** (homes, at night): much harder and riskier — locked doors, occupants home, dogs. Caught burglars face the stocks, a heavy fine, or banishment; it is the crime Bram investigates hardest after granary theft (reason: D-02 decision — homes are where people feel safest).
- **Granary theft:** the grave crime. Stealing the village's winter threatens everyone; if proof surfaces, the whole village turns on the thief (reason: LOCATIONS.md — this is the one crime the village answers as one).
- **Shrine offerings:** the lowest theft. Universally condemned — even rumor without proof costs reputation (reason: D-09 needs crimes the village condemns without needing proof of who did it).
- **Tom's windfalls** are the everyday case the system is tuned on: low stakes, widely half-known, never proven — the perfect fog for Mira to suspect the wrong person when real theft happens.

## 10. The Apple Test in numbers

Setup: game starts on a **Thirdday morning in early autumn**. The stall holds **20 apples** at **3 copper** each. Corvin's deliveries come **Secondday and Fifthday at 08:00, 45 apples at 1 copper wholesale**. Daily demand is ~13 apples: Bessa 3 (tavern), Sima 2, Tilda 1, Elswith 1, Oda 1, Garrick 1 (Tansy), Maren 1, Sella 1, Doran 1, miscellaneous 1.

**The test, tick by tick:**
- Morning: Mira sells ~5 → 15 on display by 14:00.
- **14:00 — the player steals 6** → 9 left. (Theft event, visibility per perception rules.)
- Afternoon: buyers take ~5 → **4 left at closing**.
- **19:00 — Mira counts.** Her tally says 20 − 5 − 5 = 10; the display says 4. **Belief formed: "6 apples missing."** She does not know who took them. She suspects Tom (windfalls) — wrong person, right habit.
- Evening: Mira tells Bessa. The tavern talks. (If a witness saw the theft, the rumor carries a description; if not, it carries only "someone.")
- **Day 2 (Fourthday):** 4 apples, morning demand ~8 → **sold out by ~11:00**. Bessa wants 4, gets 2. Oda's errand finds none. `FailedPurchase` events fire — customers buy pears, come back later, or complain.
- Mira asks Corvin for an early delivery; his next run is Fifthday morning anyway — the shortage lasts ~1.5 days, exactly as designed.
- **Day 3 (Fifthday) 08:00:** 45 apples arrive. Scarcity pricing (stock hit 0) lifts the price to **4 copper** for a few days, then settles as supply normalizes.
- **Mira complains to Bram** on Fifthday. He takes the complaint with full solemnity, writes it down, asks who saw what — and does nothing further without a witness or the goods. Exactly as designed.
- **Nothing is scripted.** Every step above follows from the numbers: demand, deliveries, the count, the price rules, the evidence law, the rumor rules.

**Why the no-reset rule is load-bearing:** if the stall reset to 20 each morning, the theft would vanish overnight and none of this would happen. The design depends on stock being *memory*.

## 11. Player economy

The player starts with **15 copper**, strange clothes, and an empty stomach.

**Earning early (no skill needed):** farm help 8/day (Corvin, especially harvest), woodcutting help 6/day (Tam), mill help 5/day (Garrick), tavern pot-wash 6/day + a meal (Bessa), errands 2–5 (Tom's old job, now his), fishing with Jory's borrowed net — keep half the catch (~6/day value, once trusted), foraging: a basket of berries 4 (Mira), a herb bundle 3 (Sella), mushrooms 2 in autumn.

**Spending:** rye loaf 3, barley loaf 2, stew 4, ale 2, a tavern bed 10/night, a knife 30 (the first real goal), clothes 40, a shrine offering 1.

**Skill paths change the village:**
- **Cooking:** Bessa hires kitchen help (10/day) → assistant cook → the tavern's food improves → travelers stay longer, food prices shift, farm demand rises (reason: the skill touches the systems, per README — no skill is useless).
- **Taming:** help Jory and Ralf with animals → train the village's dogs → guard-dog work for the night watch → wolf bounties (50/pelt) become reachable.
- **Swordsmanship:** night watch 8/night in winter → escort travelers on the Alder Road 15/trip → the village feels safer, and safety is a town stat (D-10).

## 12. Later (kept out of the prototype)

- A village bank or moneylender (nobody fills this role yet — Tilda's tabs are personal, not a business).
- Regional price differences with King's Rest and the neighboring villages (trade routes, Phase 6).
- The mill wheel fund as a community investment mechanic (players contributing to the 8,000 goal).
- Counterfeit worries if silver starts circulating more (a merchant-phase problem).
- Crop futures and forward contracts (a merchant-playstyle toy for much later).

## 13. Open questions for the human

1. **Tax numbers:** the reeve takes ~1,200 per collection from the 20 (~2,400/year). Is that the right weight — felt but not crushing — or should taxes bite harder?
2. **Traveler volume:** ~750 copper/month from travelers in warm months. Too many strangers for a frontier village, or does the Alder Road justify it?
3. **Apple wholesale:** Corvin sells to Mira at 1 copper (a third of retail). Fair for kin and a steady buyer, or should he drive a harder bargain?
4. **Bounties:** 50 copper per wolf pelt from the crown. Enough to make winter wolf work tempting but not a gold rush?
5. **Feast forgiveness:** Harvest Feast forgives debts under 20 copper. Keep it purely symbolic, or should bigger debts ever be renegotiated there?
