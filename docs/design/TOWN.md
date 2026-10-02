# Living World — Town State and Emergent Events (D-10)

## Summary

Millbrook is designed as a living whole: 11 town stats (population, wealth, food supply, safety, housing, employment, trade, happiness, crime, infrastructure, outward reputation), each with a formula the simulation computes from world state and a starting value with a reason. Growth and decline run on conditions, never on upgrade buttons — the village can grow or shrink over months, and the thresholds that would turn it into a town are stated but unreachable in the prototype. Ten emergent events (food shortage, wolf attack, festival, fire, theft wave, merchant arrival, fever, drought, wheel failure, bridge project) fire only when world conditions are met, and each can chain into others. The player can move every stat, but never directly: through the three skill paths and ordinary actions whose consequences ripple.

---

## 1. Town stats

Every stat is computed by the simulation from world state — never set by hand, never scripted. Scales are 0–100 unless stated; 50 = average. Money is integer copper; time is game minutes.

| ID | Stat | Unit | How the simulation calculates it | Start | Reason |
|---|---|---|---|---|---|
| stat_population | Population | people (count) | living simulated NPCs + background villagers (100, abstracted) | 120 | WORLD.md: ~120 people, 30 households; 20 simulated in full |
| stat_wealth | Wealth | copper (count) | sum of simulated NPC money + village fund + granary stock at base price + shop stocks at base price | ~11,000 | 20 NPCs hold ~9,600 (CHARACTERS.md) + fund ~1,000 + granary 50 sacks × 8 = 400; the sim computes it exactly |
| stat_food_supply | Food supply | 0–100 | days of village food in storage (granary + household cellars) ÷ 180 × 100, capped at 100 | 35 | Granary holds ~50 of 100 needed sacks (ECONOMY.md) ≈ 23 days for 30 households + ~40 days in household stores ≈ 63 days → 35. Deliberately worried entering winter — the prototype's dramatic engine |
| stat_safety | Safety | 0–100 | 100 − (wolf incidents this winter × 15) − (unresolved crimes × 5) + (night watch active ? 10 : 0) + palisade bonus (0–10 by condition) | 65 | Wolves bolder the last two winters (WORLD.md); palisade sound; one guard. Danger at the edges, not in the square |
| stat_housing | Housing | 0–100 | sound roofs ÷ households needing them × 100 | 85 | Everyone housed, but no slack: young adults double up (Piotr at home). A new household would wait — growth pressure is visible |
| stat_employment | Employment | 0–100 | working-age NPCs with productive work ÷ working-age NPCs × 100 | 90 | Almost everyone works; children and elders excluded from the denominator. Piotr counts as employed-but-restless — the story, not the stat |
| stat_trade | Trade | 0–100 | this month's traveler + merchant copper ÷ 1,700 (peak warm month) × 100 | 75 | Game starts early autumn: ~900 traveler copper + a merchant visit ≈ 1,275 ÷ 1,700. Follows ECONOMY.md's seasonal curve |
| stat_happiness | Happiness | 0–100 | average over simulated NPCs of (social need met × 0.4 + food security × 0.3 + recent festival × 0.2 − fear events × 0.1) | 70 | Cozy baseline; Harvest Feast approaching; the granary worry drags it below 75 |
| stat_crime | Crime | incidents/month (count) | theft + burglary + vandalism events logged in the last 30 days | 2 | Tom's windfalls are the crime wave. Low baseline so a real theft wave is noticeable |
| stat_infrastructure | Infrastructure | 0–100 | wheel condition × 0.4 + palisade × 0.25 + well & roads × 0.2 + granary building × 0.15 | 57 | Wheel 30 (cracking) → 12; palisade 80 → 20; well/roads 70 → 14; granary 75 → 11. The wheel is the big drag — the village's shared hope made numeric |
| stat_reputation | Outward reputation | 0–100 | (apple fame 70 + road safety 60 + hospitality 65) ÷ 3, moved by traveler experiences | 65 | Apples famous for twenty miles; road safe-ish; Bessa's tavern well-liked. This is how *outsiders* see Millbrook — distinct from D-09's player-reputation-by-group |

**Stat interactions (all emergent):** low foodSupply drags happiness and raises crime; low safety drags trade (travelers avoid the road) and happiness; low infrastructure drags trade (bad ford crossing) and foodSupply (no mill → no flour); high crime drags trade and reputation; high happiness + employment + housing attract in-migration (section 2). The simulation recomputes these monthly; no stat is ever set directly.

---

## 2. Growth and decline

**No upgrade buttons.** The village grows or shrinks because conditions change, and the player can only move the conditions.

**In-migration** (a new household arrives, +3–5 people): checked each season. Conditions: happiness ≥ 65 AND employment ≥ 80 AND foodSupply ≥ 50 AND housing ≥ 70. Then one household per season at most (reason: a frontier village absorbs newcomers slowly; word travels by merchant and traveler, and there must be a roof). Newcomers are background villagers first — the 20 simulated stay fixed in the prototype.

**Out-migration** (youth leaves for King's Rest — Piotr's plan, generalized): checked each spring. Conditions for each NPC aged 15–25: ambition ≥ 70 AND (employment prospects low OR a family tension). Resolved per-NPC through normal rules, never forced. Each departure: population −1, the household's happiness dips, and the village's "children stay" hope dims.

**Decline spiral:** foodSupply < 20 in winter → hunger events (WORLD.md decision 3: failure can be harsh) → happiness falls → out-migration rises → fewer hands for spring sowing → next autumn thinner. Wealth < 3,000 → palisade and wheel maintenance skipped → safety and infrastructure fall → trade falls. The spiral is real but slow: it takes seasons, and every step is visible in the stats.

**Village → town:** the thresholds, stated for later phases (Phase 5+): population ≥ 500 AND trade ≥ 70 sustained for 4 consecutive seasons AND infrastructure ≥ 70 AND foodSupply ≥ 60 AND housing ≥ 70. When all hold, the settlement *becomes* a town in the records — more travelers, a second farm, a real market charter. **Unreachable in the prototype** (reason: the sim runs months, not the years 4× population growth needs; and the design rule is "prove the village first"). The thresholds exist so growth has a direction, not a button.

---

## 3. Emergent events

Every event fires **only when its conditions are met** — checked by the simulation, never scripted, never on a timer except the calendar itself. Conditions read only facts the simulation owns: stats, stocks, calendar, season, weather. (NPC knowledge enters only through the rumor system, D-09 — an event never reads a belief.)

Fact vocabulary used below: `season`, `dayOfYear`, `stat_*` (above), `granarySacks`, `daysSinceRain`, `wheelCondition` (0–100), `communityFund` (copper), `crimePerMonth`, `livestockAtFarm` (head count), `lastMerchantDaysAgo`, `roadPassable` (bool).

### event_food_shortage — Food shortage
- **Conditions:** `stat_food_supply < 25` AND `season == winter`.
- **What happens:** Elswith's quiet rationing becomes open (the council announces shares — knowledge spreads through normal rumor rules); bread price hits the scarcity cap; NPCs skip meals (hunger need rises, health slowly falls); food-theft chance rises (granary, stalls, fish traps); happiness −10/month while it lasts.
- **Can follow:** event_fever (weakened villagers), event_theft_wave, event_out_migration (spring).
- **Reason:** the granary starts half-full — this event is one bad winter away, which is exactly the stakes WORLD.md ordered.

### event_wolf_attack — Wolf attack
- **Conditions:** `season == winter` AND `livestockAtFarm > 0` AND (`daysSinceSnow > 20` OR game scarcity flag).
- **What happens:** wolves take livestock (1–3 head, usually chickens or a lamb — rarely more); Ralf and the night watch respond (Ralf's schedule shifts to the farm); a 50-copper bounty per pelt is claimable (ECONOMY.md); safety −15 for the month; the tavern talks about it for days.
- **Can follow:** event_theft_wave (no — wolves don't cause theft); instead: repeat attacks while conditions hold, and event_merchant_arrival dips (travelers hear).
- **Reason:** 2–4 incidents per winter is the budgeted danger (WORLD.md); the event is the budget made mechanical.

### event_festival — Festival
- **Conditions:** `dayOfYear` is the first Restday of spring (Thawday), last Restday of autumn (Harvest Feast), or midwinter (Longnight).
- **What happens:** work pauses; everyone gathers at `loc_square`; happiness +15 for the month; tavern revenue doubles that week; at Harvest Feast, debts under 20 copper are forgiven (ECONOMY.md); at Longnight, the bonfire must be fed till dawn (firewood consumption spikes — a real cost).
- **Can follow:** event_merchant_arrival (merchants linger for festivals), event_theft_wave (crowds mean pickpockets).
- **Reason:** festivals are the village's heartbeat — the one scheduled joy, and the sim treats them as calendar facts, not story beats.

### event_fire — Fire
- **Conditions:** `season == summer` AND `daysSinceRain >= 14` AND (forge left hot OR hearth accident — small daily chance, seeded).
- **What happens:** a building catches fire; every household's water barrel (required by law, WORLD.md) gives a containment roll — the fire is usually stopped at one building, sometimes two; infrastructure −10 per building damaged; community fund pays repairs; happiness −5; the village remembers who was careless (reputation, via rumors).
- **Can follow:** event_food_shortage (if the barn or granary burns — rare and devastating).
- **Reason:** fire is the ordinary danger that kills more villagers than monsters (WORLD.md) — it must be real, and the water-barrel law must matter.

### event_theft_wave — Theft wave
- **Conditions:** `crimePerMonth >= 6` OR (`stat_wealth` visibly up AND traveler influx — strangers + unattended goods).
- **What happens:** Bram doubles patrols (his schedule shifts); households lock doors earlier (evening schedules shift); shop wariness rises (prices +1, suspicion of strangers); travelers are warned at the tavern; the player flaunting wealth or modern gear can *be* the trigger (WORLD.md decision 5: visibility has a cost).
- **Can follow:** event_guard_action (evidence → stocks, fine, or banishment per WORLD.md law).
- **Reason:** the Apple Test's social aftermath, generalized — the village learns, slowly and systemically.

### event_merchant_arrival — Merchant arrival
- **Conditions:** `season != winter` AND `roadPassable` AND `lastMerchantDaysAgo >= 21`.
- **What happens:** merchants set up at `loc_square` for 2–3 days; Tilda restocks imports (salt, cloth, lamp oil, dyes); Doran buys iron (~150 copper); villagers sell apples, pelts, honey, smoked meat (~800 copper inflow); import prices dip 10% while they're in town; rumors and news from King's Rest arrive (rumor-system input).
- **Can follow:** event_festival (merchants time visits for festivals), event_theft_wave (crowds).
- **Reason:** the village's lifeline to the outside world — and the money source the whole economy balances on (ECONOMY.md).

### event_fever — Fever
- **Conditions:** late winter (`dayOfYear` in last 30 days of winter) AND `stat_food_supply < 40` AND recent crowding (festival or tavern-heavy weeks).
- **What happens:** illness spreads by contact (the sick are those the sim's contact rules pick — no targeting); Sella's remedies and fever-tea spike in demand (her stockpile goal matters); sick NPCs miss work (production dips); in harsh runs, the old or weak can die (WORLD.md decision 3 — never scripted, always from the numbers).
- **Can follow:** event_food_shortage (missed work → late spring sowing → thin autumn).
- **Reason:** the fever winter that widowed Mira, Garrick, and Doran is in living memory — the village fears this more than wolves.

### event_drought — Drought
- **Conditions:** `season == summer` AND `daysSinceRain >= 30`.
- **What happens:** the autumn harvest comes in thin (grain and apple yields × 0.6); the river runs low (ford easy to cross — trade up briefly — but fish stock down); fire risk up (feeds event_fire conditions); Corvin's east-field clearing argument gains urgency.
- **Can follow:** event_food_shortage (the following winter), event_fire.
- **Reason:** weather is the slow disaster — it never attacks, it just doesn't rain, and the village pays months later.

### event_wheel_failure — The wheel fails
- **Conditions:** `wheelCondition <= 0` (it starts at 30 and degrades ~2/month of heavy use; Garrick's patching slows it).
- **What happens:** the mill stops — no flour, no bread within days; Oda can't bake (bakery crisis); bread price hits scarcity cap; Garrick's long-term goal (8,000-copper wheel) becomes the village's open project: timber from Tam, iron from Doran, labor from anyone (the player can help); until replaced, grain is ground by hand querns (slow, poor quality).
- **Can follow:** event_food_shortage (no bread in winter is a shortage), event_community_project (the replacement).
- **Reason:** the cracking wheel is the village's most visible shared problem (WORLD.md) — its failure must be a when, not an if, driven by use and maintenance.

### event_bridge_project — Building the bridge
- **Conditions:** `communityFund >= 5000` AND `season != winter` AND council agreement (Elswith, Garrick, Bram each willing — read from their goals and relationships, not a flag).
- **What happens:** a multi-season community project: timber (Tam, ~40 logs), iron fittings (Doran), labor (villagers give Restday hours; the player can contribute labor, timber, or coin); while building, the ford stays usable; when done: winter trade no longer drops to zero (travelers cross year-round), trade +10 permanently, infrastructure +10, and the village's quiet hope is answered.
- **Can follow:** (nothing required — it enables winter event_merchant_arrival instead).
- **Reason:** "build a real bridge over the ford so winter trade doesn't stop" is the hope nobody says aloud (WORLD.md) — the design turns it into conditions and work, never a quest.

---

## 4. How the player moves the stats (without controlling them)

The player never sets a stat. Every influence below runs through the world's systems — needs, money, items, places, time, knowledge — and NPCs react through their own rules.

| Stat | What the player can do (examples) |
|---|---|
| Population | Make the village thrive (high happiness, full granary) and newcomers arrive over seasons; or drive people away with crime and fear. Never direct. |
| Wealth | Work for coin, sell goods, donate to the community fund — or steal it away. The total moves either way. |
| Food supply | Help with harvest (labor), donate grain, hunt and share meat, improve yields with old-world knowledge like crop rotation — but conspicuous cleverness draws attention (WORLD.md decision 5: thieves and coercers notice). |
| Safety | Join the night watch (Swordsmanship), hunt wolves for bounties (Taming → trained dogs), escort travelers. Or make it worse. |
| Housing | Haul timber, help raise a roof, repair storm damage — labor the village remembers. |
| Employment | Hire help, take an apprentice, create work (a second oven needs builders; the bridge needs hands). |
| Trade | Cooking: make the Hearthside famous — better food → travelers stay longer, spend more, and carry the village's name out. Escort travelers safely (Swordsmanship) so the road's reputation grows. |
| Happiness | Cook good food, help at festivals, be kind in small daily ways — the simulation rewards both, per WORLD.md. Or be the reason for fear. |
| Crime | Simply don't steal — or be the theft wave. Help Bram watch, and the stat falls. |
| Infrastructure | Contribute coin, timber, or labor to the wheel fund and the bridge; help rebuild after fire. |
| Outward reputation | Travelers carry stories out: a village that feeds strangers and keeps its road safe is spoken of well in King's Rest markets — which brings more travelers. A village of thieves is spoken of too. |

**Skill paths, summarized:** Cooking → food quality → NPC happiness and health → tavern popularity → trade and farm demand. Taming → dogs and livestock protection → safety → wolf bounties → wealth. Swordsmanship → night watch and escorts → safety → trade. Two players with different skills end up with noticeably different villages — the README's promise, delivered through the stats above.

---

## 5. Later (kept out of the prototype)

- A second farm, a market charter, a real inn — the town-threshold rewards, for Phase 5+.
- The neighboring villages on the Alder Road (trade partners or rivals, depending on reputation).
- The royal garrison's interest if the village grows rich enough to tax harder.
- A village council that outgrows three people — formal offices, written records.
- Plague, war, and other kingdom-scale events — Phase 6+ material, not the prototype's weather.

---

## 6. Decisions made without the human

1. **Outward reputation is a town stat; player reputation is D-09's.** `stat_reputation` measures how outsiders see Millbrook. The player's standing with guards, farmers, merchants etc. belongs to the rumor/reputation system (D-09) — no overlap by design.
2. **Food supply starts at 35, deliberately worried.** The half-full granary plus thin barley put the village one bad winter from a real shortage — that tension is the prototype's dramatic engine, and WORLD.md explicitly allows harsh failure.
3. **Growth is tracked but unreachable in the prototype.** The village→town thresholds (500 people etc.) are stated so growth has a direction, but the sim runs months, not the years 4× growth needs. No content for later phases is built early.
4. **The bridge "agreement" is a condition, not a script.** Council agreement is read from Elswith's, Garrick's, and Bram's goals and relationships each season — three NPCs must each be willing, for their own reasons. If one refuses, the project waits.
5. **Events never read NPC knowledge.** Conditions use only simulation-owned facts (stats, stocks, calendar, weather). What villagers *believe* about an event travels only through the rumor system — truth vs. knowledge stays clean.
6. **Crime is a count, not a 0–100 scale.** Incidents per month (starting at 2) is legible: 2 is Tom's windfalls, 8 is a wave. Small integers beat abstract scales here.
7. **Decline runs through youth out-migration, not death** — except in harsh winters, where WORLD.md allows real hunger and loss. Piotr's plan is the template, generalized per-NPC.
8. **Festivals are one event type with three dated variants,** not three events — the calendar date is the condition, and the variant changes the effects (debt forgiveness only at Harvest Feast, the bonfire's firewood cost only at Longnight).
