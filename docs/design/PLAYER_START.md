# Living World — The Player's Start (D-11)

## Summary

The player wakes on the riverbank below the Old River Shrine on a Thirdday morning in early autumn — 15 copper, strange clothes, an empty stomach, one skill at level 1, and a secret nobody in Millbrook would believe. Within the hour Tansy Alder finds them, by midday the whole village knows "there's a stranger by the shrine," and the village's default stance is polite wariness (reputation 45 with all six groups). Day 1 offers at least eight natural ways to eat and earn — harvest hands, firewood, the mill, the tavern kitchen, foraging, herbs, Jory's net, errands — with no quest markers, only people with needs. Three emergent first-week stories show how a cook, a tamer, and a swordsman each leave a different village behind them. The Apple Test at 14:00 on Day 1 is reachable through hunger, poverty, and opportunity — never railroaded.

---

## 1. How the player arrives

**Place:** the riverbank below the Old River Shrine (`loc_shrine`), position (−46, −30). **Time:** Thirdday morning, early autumn, **06:30** (reason: dawn enough to be found, early enough that the village's day is still forming — the player arrives *into* a morning, not on top of one). **What the player has:**

| Thing | Detail | Reason |
|---|---|---|
| Money | **15 copper** | ECONOMY.md: enough for five rye loaves, or one night's tavern bed (10) plus supper — the player can survive Day 1 but not Day 5 without working. |
| Clothes | Strange, foreign-cut, worn (not an inventory item) | Worth questions; Lida's witness description ("the stranger in the funny clothes") comes from these. |
| Stomach | **Hunger 70/100 at wake** | WORLD.md: "an empty stomach." Hunger 6/hr means ~11 hours to full — the player must eat on Day 1. This is the quiet engine of the Apple Test. |
| Skill | One of `skill_cooking` / `skill_taming` / `skill_swordsmanship`, **level 1, 0 points** | SKILLS.md: a head start, not destiny. |
| Secret | Full memories of our world | WORLD.md decision 1: reincarnation stays secret — claiming it gets the player laughed at or feared. |
| Reputation | **45** with all six `group_*` groups | KNOWLEDGE.md: neutral-to-cool, polite wariness. |
| Notoriety | **0** | KNOWLEDGE.md: nobody has noticed the stranger's tricks yet. |

The player wakes with no pack, no cart, no story — which is exactly what makes them strange (WORLD.md: travelers are normal; a traveler with *nothing* is not).

## 2. Skill selection

On arrival — before the world renders, in the quiet of character creation — the player is asked: ***"What were you good at, back then?"*** The framing is deniable by design (WORLD.md decision 1, SKILLS.md section 5): it can be read as a dream, a half-memory, or a question about the body they woke in. The player picks one of the three prototype skills and starts at level 1.

The choice is invisible to the village. No NPC knows which skill was chosen; what they see is what the player *does* (truth vs. knowledge — the skill is world truth, the reputation is what NPCs believe). The other two skills start at 0 and can be learned later (20 points to level 1).

## 3. Who notices the newcomer first

The news travels the way news travels in Millbrook — along the paths people actually walk. No one is scripted to find the player; the schedules do it.

1. **~07:00 — Tansy Alder** (`npc_tansy_alder`, 10). Her workday schedule has her fetching water at the well 07:00–09:00; the riverbank path passes the shrine, and she is curious 95. She finds the player awake (or wakes them) and runs for her father. Her first line is the player's first human contact: *"Are you the stranger? I'm Tansy. Papa says you're to come for supper."* (She decides this herself — Garrick hasn't said it yet. Children arrange things.)
2. **~07:30 — Garrick Alder** (`npc_garrick_alder`, miller). Tansy fetches him from the mill. He is honest 80, generous 65: he checks the stranger isn't hurt, offers water, and — Hearth hospitality, not a quest — tells them there's supper at the mill house tonight. He also does what a responsible miller does: he walks to the guard post and reports a stranger at the shrine.
3. **~08:30 — Bram Stone** (`npc_bram_stone`, guard). Bram logs the report ("stranger, young, odd clothes, no pack, by the shrine"), walks down to look the player over, asks three calm questions (name, where from, business here), and writes the answers down. He is not hostile — he is *thorough*. The player's answers become the village's first data about them (and the first thing that can later be compared against the truth).
4. **By midday — the village.** Tansy tells Lida (best friends); Lida tells Maren at the farmgate; Maren's egg customers hear it by 11:00; Bessa hears at the well and the tavern knows by evening. Travelers at the Hearthside ask about "the shrine stranger" over ale. The cautious (Tilda, Elswith) reserve judgment; the curious (Maren, Brynn, Sella) want to meet them; the children (Lida, Tansy, Tom) consider the player *their* discovery.

**What the village thinks, in aggregate:** not fear, not welcome — *assessment*. Reputation 45 with everyone. The stranger is being watched a little, which is exactly why the Apple Test works socially (WORLD.md).

## 4. The first day: eight opportunities, no quest markers

Nothing points at the player. These are simply the village's needs on a Thirdday in early autumn, and a stranger with empty pockets will hear about them by asking, watching, or being told.

| # | Opportunity | Who / where | Terms | Why it's natural |
|---|---|---|---|---|
| 1 | **Harvest hands** | Corvin Alder, `loc_farm` | 8 copper/day | It's early autumn — the barley won't wait (CHARACTERS.md: Corvin's short-term goal). Corvin hires anyone with hands; Piotr (16) resents the competition and is curious about the stranger in equal measure. |
| 2 | **Firewood hauling** | Tam Oakes, `loc_home_woodcutter` / `loc_forest_edge` | 6 copper/day | Winter is 90 days and firewood is a need (LOCATIONS.md). Tam cuts; hauling is the bottleneck. Brynn offers water and gossip. |
| 3 | **Mill help** | Garrick Alder, `loc_mill` | 5 copper/day | The wheel needs constant patching (his goal: nurse it through winter). The player learns the wheel's groans — and that the village's biggest problem is 8,000 copper away. |
| 4 | **Tavern pot-wash** | Bessa Marlowe, `loc_tavern` | 6 copper/day + a meal | Evenings. Bessa sizes the player up while they scrub; a cook-skilled player is noticed by 19:00 (SKILLS.md: Bessa notices first). The meal is worth more than the coin to a hungry stranger. |
| 5 | **Berry baskets** | Mira Holt, `loc_apple_stall` | 4 copper/basket | Autumn berries at the forest edge; Mira buys baskets to fill her display. The player meets the stall, its prices, and Mira's sharp eyes *before* 14:00 — which matters. |
| 6 | **Herb bundles** | Sella Wren, `loc_healer_hut` | 3 copper/bundle | The 12 herb patches regrow in 3 days; Sella and Brynn can't gather them all. The healer's hut at the forest edge is also where the player first hears that remedies "work a little better than they should." |
| 7 | **Jory's net** | Jory Reed, `loc_river_alder` | Keep half the catch (~6 copper/day value, once trusted) | Jory is solitary, not unkind. He lends the net to anyone who asks properly and watches from the bank. Trust is the wage; fish are the bonus. |
| 8 | **Errands** | Oda Fenn / Tilda Bray / Maren Alder | 2–5 copper per errand | Tom's old job, now his sometimes. Flour to the bakery, a message to the farm, eggs to carry. Small coin, large gossip — errands are how a stranger learns the village's map of who owes whom. |

A ninth, unpriced: **the shrine offering** (1 copper, a crust of bread, a ribbon). Leaving something at the Hearth shrine is noticed by the pious and costs almost nothing — the cheapest reputation the player can buy (and the one crime — stealing offerings — the whole village condemns).

**The day's shape:** a player who works (opportunities 1–4, 6–8) earns 5–8 copper and eats; a player who forages (5–6) earns 3–4 per basket/bundle and eats what they gather. Either way, Day 1 ends with the player fed, slightly richer or slightly poorer, and known — a little — by the people they worked for. No one tells them to steal apples. But the stall is 1 minute from the square, Mira is sometimes at the back of it, and the player's stomach remembers the morning.

## 5. Three first weeks

Each story runs Day 1–7, early autumn. Every beat follows from the systems — needs, money, knowledge, reputation, skill practice — never from a script. The village is *noticeably different* at the end of each week, in a different way.

### The cook's week

- **Days 1–2:** the player takes Bessa's pot-wash (6/day + meal) and cooks the family meal at the mill house badly but earnestly — level-1 practice (+1/use), 30% waste, Tansy pretends it's good. Hunger solved; 12 copper earned.
- **Days 3–4:** Bessa, who notices first, offers kitchen help at 10/day (SKILLS.md: level-2 unlock — the player is practicing daily, ~20 points by week's end with the daily cap). The tavern's stew improves; two travelers stay an extra night for the food.
- **Days 5–7:** Bessa orders more grain from Corvin; Maren's egg sales tick up (more breakfasts). Elswith notes the stranger is "good for the village" (villagers +5, merchants +5). The player's visibility rises — Cooking is the safest skill to be seen using, but the tavern is *talking*.
- **The village, changed:** the Hearthside is busier; Corvin's income is up; the reeve's next assessment will notice the prosperity. The player has ~60 copper, a standing offer of kitchen work, and Maren as a friend. **Bessa's question, hanging:** will she make the stranger her assistant cook?

### The tamer's week

- **Days 1–2:** the player helps Jory with the nets (level-1 practice: farm animals don't shy; Jory's catch steadies) and is kind to the village dogs. Lida, Tansy, and Tom adopt the player instantly (children +8 — the animal person is *theirs*).
- **Days 3–4:** Ralf, who respects competence, starts teaching (×2 gains — the player is at ~30 points by week's end). The player trains the mill-house dog to "watch." Garrick sleeps better; Tansy brags at the well.
- **Days 5–7:** Bram, professionally interested, asks the player to walk the night watch with the trained dog. A petty theft attempt at the square (the normal theft rules, an opportunist, not a script) fails because the dog barks — the first time the village *sees* what the stranger is for.
- **The village, changed:** one household has a guard dog; Bram's watch has a volunteer; the children have a hero. The player has ~45 copper, Ralf's respect, and a reputation as "the animal person" — which Tilda finds *interesting* and slightly alarming. **Ralf's question, hanging:** will he take the stranger on the winter trap lines?

### The swordsman's week

- **Days 1–2:** the player takes farm work (8/day) and is seen moving like someone who has held a weapon — Bram notices (cautious 80, professional interest) and offers a watch shift: 8 copper/night. The player has no sword (120, imported — the first great goal); Bram lends a spear (45 value, the practical weapon).
- **Days 3–4:** the player escorts a merchant cart along the Alder Road (15 copper/trip — SKILLS.md level-2 unlock). The merchant arrives safely and *talks* — down the road, the Alder Road is "the safe one now." Traveler volume ticks up.
- **Days 5–7:** training with Bram (×2 gains, ~30 points by week's end). Piotr hero-worships openly and asks for lessons; Elswith watches the armed stranger with the council's careful eyes (council +2 for the service, −2 for the wariness — both true).
- **The village, changed:** the road feels safer; Bessa has two more travelers' worth of ale money; Bram's workload eases. The player has ~70 copper and a standing arrangement with the watch — and is, visibly, the most dangerous person in a cozy village. **Bram's question, hanging:** is the stranger the watch's answer, or its next problem?

## 6. Sleep, food, and money

**Sleep.** Three options, all real:
- **The Hearthside bed** — 10 copper/night (ECONOMY.md). The player arrives with 15: one night is affordable, two is not. Bessa is matter-of-fact about it.
- **Garrick's floor** — free, one night, maybe two. Hearth hospitality: the miller whose daughter found the stranger offers the floor by the hearth. This is the default first night if the player accepts supper — it puts the player in a household, gives Tansy a friend, and costs Garrick nothing but a blanket. It does *not* extend indefinitely (reason: hospitality is custom, not tenancy — after 2–3 nights the player is expected to have a plan, and Garrick will say so, kindly).
- **The riverbank** — free, cold, and visible. Sleeping rough costs nothing and buys the player a reputation as *that* stranger (villagers −2, children +2 — they find it romantic). Winter makes this impossible; autumn makes it merely uncomfortable.

**Food.** The player must eat ~96 hunger/day (6/hr × 16h). Options: Bessa's pot-wash meal, Maren's farmgate eggs (1 copper), Oda's barley loaf (2), Mira's bruised apple (free, "it's sweeter anyway"), Jory's half-catch, foraged berries. A hungry player with 15 copper can eat for ~5 days without working; a working player eats indefinitely.

**Money.** Day 1–7 income paths (all ECONOMY.md rates): farm 8/day, woodcutting 6, mill 5, tavern pot-wash 6 + meal, kitchen help 10 (cook level 2), errands 2–5, berries 4/basket, herbs 3/bundle, half-catch ~6, night watch 8/night, escort 15/trip. The 30-copper knife is the first real goal (ECONOMY.md); the 120-copper sword is the swordsman's horizon.

## 7. The Apple Test is reachable

No script points the player at the stall. The design makes the theft *possible and natural* through four facts:

1. **Hunger.** The player wakes at 70/100 and burns 6/hr. By 14:00 (~7.5 hours awake) they are at ~115 without food — genuinely hungry unless they've eaten. Hunger is a need, and needs override schedules (AGENTS.md 6.2).
2. **Poverty.** 15 copper buys five rye loaves — or one bed and supper. Every copper spent on food is a copper not saved toward the knife. Apples are 3 copper each; six apples are 18 copper the player doesn't have.
3. **Opportunity.** The stall is 1 minute from the square. Mira tends it 07:00–19:00 but is sometimes at the back, serving, or fetching — the standard Apple Test seeds her not noticing. The display is reachable; pocketing is quiet (loudness 1.0).
4. **Knowledge.** Opportunity 5 (berry baskets for Mira) puts the player at the stall *before* 14:00 in the normal course of a foraging day. The player has seen the apples, the prices, and Mira's habits. What they do with that is theirs.

The theft is one choice among many — work, forage, beg, buy, or steal. The simulation doesn't care which; it only records what happened. (If the player doesn't steal, the Apple Test's scenario runner issues the theft as a player command anyway — the design's job is to make it *plausible*, not mandatory.)

## 8. Later (kept out of the prototype)

- The player buying or building a home (Phase 5+ town development).
- The player taking an apprentice or teaching villagers (Phase 7 generations).
- Romance, marriage, children of the player's own (Phase 7+).
- The player leaving Millbrook for King's Rest or the neighboring villages (Phase 6 expanded world).
- Notoriety 80+ consequences playing out over months (kidnap/coercion attempts are possible in the prototype's rules, but the full arc is later).
- The reincarnation secret coming out — what the village does with the truth (Phase 7+ story fuel).

---

## Decisions made without the human

(Per the standing instruction: I decided these myself, following README.md's vision, the shared conventions, and the approved areas.)

1. **Wake time 06:30.** Dawn enough to be found, early enough that the village's day forms around the player rather than on top of them.
2. **Hunger 70/100 at wake.** "An empty stomach" made mechanical: ~11 hours to full at 6/hr, so the player must eat on Day 1. This is the quiet engine that makes the 14:00 Apple Test plausible.
3. **Garrick's floor is the default first night** (Hearth hospitality, 1–2 nights, then the player needs a plan). It grounds the player in a household without scripting a quest — and it puts Tansy's friendship and Garrick's trust on the table from night one.
4. **The skill-choice question is deniable by design** ("What were you good at, back then?") — the village never learns the answer; only the player's actions are visible.
5. **The eight opportunities are ordered by how a stranger actually hears about them** (work first, foraging second, errands through gossip) — no markers, just the village's needs on a Thirdday.
6. **The ninth, unpriced opportunity (shrine offering)** is deliberate: the cheapest reputation in the game, and it teaches the player that the Hearth notices — which is exactly the lesson shrine-theft later inverts.
7. **First-week stories end with open questions, not resolutions** (Bessa's offer, Ralf's trap lines, Bram's assessment) — the village keeps living; the player keeps choosing.
8. **The Apple Test needs no scripted motive.** Hunger + poverty + opportunity + prior knowledge of the stall is sufficient; the scenario runner issues the theft as a player command, and the design's job is plausibility, which the numbers provide.
9. **Sleeping rough has a small reputation cost** (villagers −2, children +2) — visible enough to matter, small enough to be a valid playstyle (README.md: no single correct playstyle).
10. **Clothes are worn, not inventoried.** The strange clothes exist as an appearance fact (feeding Lida's witness description), not an item — nothing in the prototype needs to trade or steal them.
