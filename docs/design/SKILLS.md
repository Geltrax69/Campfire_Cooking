# Living World — Skills (D-06)

## Summary

Millbrook's prototype gives the player three skills — Cooking, Taming, Swordsmanship — each with 5 levels earned only through practice and teaching, never through XP-from-killing. Levels are slow (level 2 takes ~3 weeks of daily practice), capped per day so nobody grinds, and every level changes the world through its systems: a good cook shifts tavern trade and farm demand, a tamer reshapes the village's dogs and night safety, a swordsman changes who walks the road at night. The player picks one starting skill on arrival ("what were you good at, back then?") at level 1 and can learn the others from zero. Five absurd skills (Cleaning, Talking to Chickens, Smelling, Rope Making, Soup Making) are outlined as design only — each must touch real systems, because no skill is useless.

---

## 1. The skill system

One system for all skills, so the simulation implements it once.

**Levels.** Each skill has levels 1–5 (reason: low=1–2, medium=3, high=4–5 — exactly the bands Prompt 6 asks about, with two steps inside "low" and "high" so progress feels frequent early and meaningful late). The player is never shown numbers, only feel ("your stews are getting a reputation").

**Improvement.** Practice grants points; thresholds are cumulative:

| Level | Points needed | Meaningful uses (at +1/use) |
|---|---|---|
| 1 | 0 | Starting level (or dabbler's first step) |
| 2 | 100 | ~3–4 weeks of daily practice |
| 3 | 300 | ~2 months of steady practice |
| 4 | 700 | ~4–5 months of dedicated practice |
| 5 | 1500 | most of a year — a master |

- **perUse = 1** (reason: one meaningful action — a full cooking session, a real training bout, a genuine taming interaction — is the smallest unit the simulation can see; chopping one onion doesn't count).
- **Teaching doubles gains** (×2 per use) while being taught by someone better (reason: the apprentice model is the village's whole education system — Bessa teaching the player to cook is faster than burning stew alone). Named teachers: Bessa → Cooking, Ralf → Taming and the basics of Swordsmanship, Jory → Taming (animals), Bram → Swordsmanship (watch discipline). NPCs must be present and willing — willingness comes from relationship and reputation, not a button.
- **Daily cap: 20 points/day** (reason: prevents grinding one action 100 times in a day; practice needs rest, and it matches the cozy pacing — you cook today's meals, then you live the rest of the day).
- **No decay** (reason: the game is cozy, not punishing; a skill learned is kept. What fades is *reputation*, not competence — people forget your stew, they don't unlearn it).

**Learning a second skill.** The unchosen skills start at 0. Reaching **20 points** unlocks level 1 (reason: dabbling is slow — a week of evenings — but anyone can start; being taught gets you there in days).

**Costs are always real.** Every skill costs time (game minutes), ingredients or materials (consumed), and carries risk (failure, injury, or social cost). There are no free levels and no XP-from-killing — nothing in the skill system rewards violence as a shortcut (reason: AGENTS.md — no XP grind; the village's values are the game's values).

**Quality.** Skill levels grant `qualityBonus` 0/+1/+2/+3/+4, added to the item quality 0–100 scale (shared conventions: 50 = average). A level-5 cook's stew is meaningfully better than Bessa's good-day stew — and NPCs can taste the difference, which is what moves the systems.

---

## 2. Cooking — `skill_cooking`

The prototype's heart. Millbrook eats three times a day; the person who feeds it well becomes someone.

**What each level unlocks:**
- **Level 1 — Campfire basics.** Can cook simple meals from raw ingredients without ruining them (stew, roasted fish, porridge). Failure chance on anything fancy. `qualityBonus` 0. Uses the campfire at `loc_river_alder` or a hearth — no kitchen needed.
- **Level 2 — Kitchen hand.** Works a real kitchen (Bessa's, Oda's ovens for bread). Cooks reliably, wastes less (ingredient waste halves). Unlocks: tavern kitchen work (10 copper/day — D-04). `qualityBonus` +1.
- **Level 3 — Village cook.** Meals people remember. Stew that sells itself; food quality visibly better than average. Bessa offers assistant-cook work. Unlocks: selling own cooked dishes at the square; cooking for events (Thawday, Harvest Feast). `qualityBonus` +2.
- **Level 4 — The tavern's draw.** Travelers stay an extra night for the food. Unlocks: running the tavern kitchen on Bessa's brew days; festival head-cook. `qualityBonus` +3.
- **Level 5 — A reputation beyond the ford.** Traveling merchants carry word to King's Rest. Unlocks: teaching others (the player becomes a teacher — ×2 for students); dishes worth traveling for. `qualityBonus` +4.

**Systems touched:** food quality → NPC happiness (a good meal is a small daily happiness that compounds — WORLD.md) and health (better-fed villagers recover faster); tavern popularity (travelers stay longer → more ale/bed sales); food prices (demand shifts — good cooked food bids up raw ingredients); farm demand (more flour, eggs, pork ordered); relationships (feeding someone is a social act — Maren's egg-money logic, writ large); spoilage pressure (better cooks waste less).

**Chain reactions (2 examples):**
1. *The tavern's good season.* Player reaches level 3 and cooks for the Hearthside → travelers stay an extra night → Bessa's ale and bed revenue rises ~30% → she orders more grain from Corvin → Corvin's income rises → the reeve's prosperity-scaled tax takes a bigger cut next collection → Elswith quietly notes the player is "good for the village" (reputation, D-09) — and the winter is a little easier for everyone.
2. *The flour squeeze.* Player's festival cooking (level 3–4) doubles stew demand for a week → Oda can't keep up with bread for the crowds → flour price ticks up under the price rules → Garrick's mill runs longer hours → the cracking wheel's condition worsens (the village's current problem, made sharper) → the council starts talking about the 8,000-copper wheel in earnest. The player's success creates the village's next problem.

**How NPCs notice and react:** Bessa notices first (offers work at level 2 — reputation with tavern/merchant groups rises); travelers spread word (the road carries reputation outward); Maren and Sima compare notes (villager women, +affection); Tom asks to learn (a student). Nobody is threatened by a good cook — Cooking is the safest skill to be seen using (reason: feeding people is the Hearth's own virtue).

**Costs:** time (30–90 game minutes per real cooking session); ingredients consumed (a level-1 cook wastes ~30% — burned, over-salted, fed to the pigs); fuel (firewood — a need, not flavor); at level 1–2, real failure (a ruined batch is wasted copper and a bruised ego, witnessed by whoever's in the kitchen).

---

## 3. Taming — `skill_taming`

Taming is a relationship, not a button (README.md). The skill measures the player's ability to build trust with animals — read them, feed them right, be patient, show the right kind of strength. Species specifics (which animals, what each needs) are D-08's data; this skill defines how the *player's* ability changes what trust-building can achieve.

**What each level unlocks:**
- **Level 1 — Steady hands.** Farm animals don't shy from the player. Can help Jory with nets, calm livestock, approach dogs. Trust builds at the base rate.
- **Level 2 — Known to the dogs.** The village dogs know the player's voice. Can train a dog to simple commands (come, stay, watch). Unlocks: helping Ralf with trap lines; paid dog-minding. Trust gain ×1.5.
- **Level 3 — The night watch's friend.** Can train dogs to guard work — a trained dog on the night watch meaningfully raises burglary risk for thieves. Unlocks: guard-dog training for households (paid, 5 copper/dog/week); working with Bram's watch. Trust gain ×2.
- **Level 4 — Wild trust.** Can begin building trust with wild animals that will tolerate it (foxes, deer at the forest edge — D-08 defines which). Unlocks: finding lost livestock; the forest stops being quite so foreign.
- **Level 5 — The wolf's respect.** Can build trust with wolves — slowly, dangerously, through demonstrated strength and patience (never through food alone; a fed wolf is a dependent, not a partner). Unlocks: a tamed wolf companion. The whole village will have opinions about this.

**Systems touched:** animal trust and behavior (D-08 ecosystem); village safety (guard dogs change the burglary math — D-02's burglary rules); livestock losses (fewer wolf kills near trained dogs); hunting (Ralf's work changes when the player helps or competes); wolf bounties (50 copper/pelt — a level-4+ tamer can approach wolves, which is either a bounty or a friendship, the player's choice); NPC fear and affection (children adore the animal person; the cautious are wary); farm labor (a trained dog can help move livestock).

**Chain reactions (2 examples):**
1. *The quiet village.* Player trains guard dogs for three households (level 3) → nighttime petty theft and burglary attempts drop → Bram's watch gets bored → thieves (if any are watching the village) shift to travelers on the road or give up → the village *feels* safer → Elswith mentions it at council → but the dogs need feeding (meat/fish demand rises slightly) and one dog bites a drunk traveler → a small scandal, a compensation payment, a debate about the player's dogs. Safety has a price, paid in meat and gossip.
2. *The wolf question.* Player at level 5 befriends a wolf → livestock losses near the farm drop (the wolf's territory keeps other wolves wary) → Corvin is grateful and confused → Tilda refuses to serve "the wolf person" at first (fear, honest and open) → children follow the player at a distance, thrilled → Ralf, who has hunted wolves for thirty years, has the longest conversation of his life with the player → the winter bounty money dries up for whoever was counting on it. One relationship rearranges the village's whole predator economy.

**How NPCs notice and react:** Lida, Tansy, and Tom adore the animal person immediately (children, +affection fast); Ralf respects competence and is the natural teacher (hunters, +respect); Jory is grateful for help with nets; Bram is professionally interested at level 3+ (a guard-dog asset for the watch); Tilda and other cautious villagers are wary of level 4–5 (fear is honest — a wolf at the palisade is a story, and stories travel); Sella notes that animals know who's kind (the healer's quiet approval carries weight).

**Costs:** time (trust takes days to weeks — a level-5 wolf bond is a season's work, not a weekend); food (feeding animals costs real items — fish, meat, eggs); risk (level 1–2: nips and kicks; level 4–5: a wolf that decides you're prey — injury means Sella's remedies and lost days); social risk (see level 5 — the village's fear is a real cost, paid in reputation).

---

## 4. Swordsmanship — `skill_swordsmanship`

Not heroism — watchfulness. In Millbrook a sword is a tool for keeping the road safe and the wolves off, and the village treats it that way: useful, a little foreign, watched.

**What each level unlocks:**
- **Level 1 — A steady grip.** Can hold a spear on the winter night watch without being a liability. Deterrence by presence (a watch with a spear is a different calculation for a thief than a watch with a lantern). Unlocks: night watch shifts (8 copper/night in winter — D-04).
- **Level 2 — The road's companion.** Can escort a traveler or merchant cart along the Alder Road (15 copper/trip — D-04). Knows the road's bad spots. Unlocks: escort work; helping Ralf dress a boar safely.
- **Level 3 — The boar's answer.** Can face a boar or a bold wolf without freezing — drives it off, protects livestock in the moment. Unlocks: paid livestock-guarding in winter; training with Bram (the guard's spear forms).
- **Level 4 — The pack's problem.** Can stand against a wolf pack's probing with back to a wall and live — the village's best defense that isn't a palisade. Unlocks: leading the winter watch; travelers specifically ask for the player by name.
- **Level 5 — The quiet professional.** Fights are over before they start — positioning, timing, the thing Ralf calls "reading the forest like a letter" applied to people. Unlocks: teaching (Bram's watch, Piotr's admiration made formal). The player is now the most dangerous person in the village, and everyone knows it — which is its own cost.

**Systems touched:** village safety (a D-10 town stat — the night watch's effectiveness); wolf/livestock incidents (fewer losses in winter); traveler volume (a safe road is a busy road — the seasonal traveler curve in D-04 steepens); the guard's workload (Bram can rest, or be resented); NPC fear and respect (an armed stranger is *noticed*); the local economy of protection (escort fees, watch pay); injury and healing (Sella's remedies have a customer).

**Chain reactions (2 examples):**
1. *The safe road.* Player escorts merchants through autumn (level 2–3) → word spreads that the Alder Road is safe → traveler volume rises beyond the seasonal curve → Bessa's tavern fills → Tilda orders more imports → Doran's forge runs hot on horseshoes and repairs → the reeve's next assessment rises on visible prosperity → taxes bite a little harder, and Elswith gives the player a look that says *we see what you did, and the bill*. Safety is profitable, and profit is taxed.
2. *The winter that wasn't.* Player leads the winter watch at level 4 → wolf incidents drop to near zero → no livestock lost → meat prices stay stable → Ralf's bounty income collapses (he was counting on 2–4 pelts) → Ralf is quietly broke by spring → the player who saved the sheep has hurt the hunter → Ralf asks the player for help with the spring trap lines instead of money, and a different kind of debt begins. Every protection displaces someone's livelihood.

**How NPCs notice and react:** Bram respects competence immediately and offers watch work (guards, +trust — but watches *how* the player fights; cruelty would end it); Piotr hero-worships (the young, +affection, wants lessons at level 5); Ralf approves of practical defense, disapproves of showiness; Elswith and Tilda are wary of a visibly armed stranger (cautious, −trust until proven); travelers are grateful and talk (the road carries the reputation outward); Sella patches the player up and lectures about "all that strength and no sense" (affectionate, mostly).

**Costs:** time (training with Ralf or Bram, watch shifts are whole nights); money — a sword costs **120 copper, imported** (D-05: no village smith forges swords — the player's first great savings goal; until then, a spear at 45 or a quarterstaff); risk of injury (level 1–2 vs. a boar is genuinely dangerous — Sella's remedies cost 5–10 copper a dose, and a bad wound costs days); social cost (level 4–5: being the most dangerous person in a cozy village changes every conversation — the visibility rule in WORLD.md decision 5 applies double).

---

## 5. Starting skill choice

On arrival the player is asked — by the world, in the quiet of character creation — *"what were you good at, back then?"* The framing is deniable (WORLD.md decision 1: reincarnation stays secret): it can be read as a dream, a memory, or a question about the body they woke in. The player picks **one** of the three skills and starts at **level 1** (0 points). The other two start at 0 and can be learned later (20 points to reach level 1).

The choice is not destiny — it's a head start (reason: README.md — no chosen hero; a cook who learns the sword is a better story than a born swordsman). The village never knows which was chosen; what it sees is what the player *does* (reason: truth vs. knowledge — the skill is world truth, the reputation is what NPCs believe).

---

## 6. Absurd skills (design only — no data)

Five outlines. Each must touch world systems — no skill is useless, even the silly ones.

**Cleaning.** What it does: at low level, the player's own space and clothes stay clean; at high level, the player can clean *anything* — a tavern kitchen, a sickroom, the shrine steps — to a standard the village has never seen. Systems: sickness and health (a clean sickroom means Sella's remedies work better — fewer fever deaths in winter); tavern popularity (Bessa's kitchen gleaming → travelers trust the food); shrine and Hearth standing (cleaning the shrine is a visible piety — the faithful notice); Sella's income (fewer sick villagers is *less* business for the healer — she is grateful and slightly poorer). Chain: player cleans the tavern weekly → travelers remark on it down the road → the Hearthside's reputation grows → winter fever season hits → the clean sickrooms mean fewer deaths → Sella, grateful, teaches the player herb basics (a teaching bond). NPCs react: Bessa hires instantly; the tidy are trusted (Tilda, Elswith +); teenagers find it hilarious until they see the money in it.

**Talking to Chickens.** What it does: at low level, the player understands chicken moods (calm, alarmed, broody); at high level, the player can *ask* — and chickens notice everything in a farmyard. Systems: knowledge (chickens witness the farmyard — who came, who took eggs, what the fox did — a genuine, deniable information source for D-09's rumor system); egg production (calm, well-read hens lay better — Maren's egg money rises); farmyard safety (an alarmed chicken is an early warning for foxes and thieves). Chain: player befriends Maren's hens → learns Tom takes windfalls (confirming what half the village suspects) → tells no one, but Mira notices the player *knows* → a strange, careful friendship forms → Maren's hens lay 20% better → egg money funds Piotr's road fund faster → Corvin notices the money moving and asks questions. NPCs react: Lida is *thrilled* (a grown-up who hears them too); Maren is delighted and a little unnerved; Ralf finds it practical ("hens see everything"); Bram files it under "things to ask the stranger about, politely."

**Smelling.** What it does: at low level, the player notices spoiled food before eating it and smoke before seeing it; at high level, the player can track by scent — a person, a wolf, a lost child — and read a kitchen, a sickroom, or a tavern by its smells. Systems: spoilage (the stale-vs-fresh call is *perfect* — the player never wastes copper on turned food, and can warn others); fire prevention (smoke noticed early — the village's greatest fear, WORLD.md); tracking (hunting with Ralf, finding lost livestock, the guard's investigations gain a witness who *smelled* something); health (Sella values a nose that can smell fever coming). Chain: player smells smoke at 03:00 → wakes the household → a kitchen fire is caught before it takes the house → the village's fire-fear eases a notch → Bram asks the player to walk the night watch in winter (smoke, wolves, strangers) → the player smells a traveler's hidden fever → Sella is called early → a life saved, quietly, and nobody can explain quite how the player knew. NPCs react: Sella is fascinated (a medical nose); Bram is professionally interested; bakers are embarrassed (the player always knows when the bread is yesterday's); children dare each other to test it.

**Rope Making.** What it does: at low level, the player can twist serviceable cordage from fiber; at high level, the player makes rope as good as imported — and rope for *specific* jobs (trap lines, fishing nets, climbing, the mill's rigging). Systems: the import economy (rope is 5 copper imported — local rope undercuts Tilda's stock, the village's second import substitution after Brynn's cloth); Jory's and Ralf's work (good nets, good snares — their yields rise); the mill (Garrick's rigging is always fraying — a standing customer); construction (the palisade, the hoped-for bridge — D-10's town stats). Chain: player makes rope for Jory → his nets hold better → fish catch rises → smoked fish surplus → Tilda's debt repayments in fish clear faster → Jory, debt-free, buys the player ale and tells the tavern → two more customers → Tilda notices her imported rope isn't selling → she stops ordering it → the merchant's next visit carries one less thing Millbrook needs. NPCs react: Jory and Ralf are immediate customers and friends; Tilda is professionally annoyed then respectful (a competitor she can see); Tam supplies fiber and gains a trade partner.

**Soup Making.** What it does: not Cooking's rival — its humble sibling. At low level, the player can stretch a pot: feed four on ingredients for two. At high level, the player runs the festival cauldrons and the winter soup kitchen — the village's communal pot. Systems: hunger economics (soup is the cheapest hunger-per-copper in the village — the poor eat better); festivals (the Harvest Feast and Longnight pots are *events* — happiness, social bonds, D-09); winter survival (a communal soup kitchen in the lean months is the difference between hunger and hardship — WORLD.md decision 3); Bessa's kitchen (soup is her stew's rival and her ally — she both competes and collaborates). Chain: player starts a winter soup kitchen at the tavern → the poorest households eat well through February → spring comes with everyone stronger → the spring sowing goes faster → Corvin's harvest is better → the Harvest Feast is richer → the player is asked to run the cauldrons again, and "the soup person" becomes a village institution. NPCs react: Elswith weeps the first time (quietly, in the back); Bessa is torn between rivalry and relief; the poor love the player simply and completely; Maren contributes vegetables and gains a friend.

---

## 7. Skill visibility and notoriety

WORLD.md decision 5: conspicuous displays of old-world advantage draw thieves, kidnappers, or people who want to make the player work for them. Skills are the visible face of that advantage. The rule: **using a skill at level 3+ in public generates visibility** — NPCs notice, remember, and talk (D-09's rumor system carries it). Visibility is not good or bad; it is *attention*, and attention has a price:

- A level-4 cook whose festival stew feeds the square is beloved — and every traveler on the road has heard of them (kidnappers look for valuable people).
- A level-5 tamer with a wolf is feared as much as admired — fear is a kind of attention thieves respect and employers exploit.
- A level-5 swordswoman is the village's shield — and the first person anyone with a problem comes to, wanted or not.

The simulation tracks visibility per skill as a number (0–100); D-09 turns it into reputation effects. The player can stay quiet (use skills privately, at level 1–2, for friends) or become known. Both are valid playstyles (README.md: no single correct playstyle).

---

## 8. Later (kept out of the prototype)

- NPC skill levels modeled explicitly (Oda's baking, Doran's smithing as data the player can learn from and surpass).
- More life skills (Farming, Fishing, Hunting, Woodworking, Blacksmithing, Tailoring) as full skills with the same 5-level system.
- Special skills (Appraisal, Tracking, Cartography) when the systems they touch exist.
- Skill synergies (Cooking + Smelling = the perfect kitchen; Taming + Tracking = the complete hunter).
- The player's students: teaching NPCs skills that persist and spread (Phase 7 generations — knowledge as inheritance).
- Magic as a skill — only if the world's magic ever becomes more than background (WORLD.md says not in the prototype).

---

## 9. Decisions made without the human

1. **Five levels** per skill (low = 1–2, medium = 3, high = 4–5), thresholds [0, 100, 300, 700, 1500] cumulative points, +1 per meaningful use. (Reason: matches Prompt 6's bands with room to feel progress; ~3 weeks of daily practice to level 2.)
2. **Teaching doubles gains** (×2); named teachers per skill (Bessa→Cooking, Ralf→Taming and Swordsmanship basics, Jory→Taming, Bram→Swordsmanship). Willingness comes from relationship/reputation, not a button.
3. **Daily cap of 20 practice points** (reason: no grinding; cozy pacing).
4. **No skill decay** (reason: cozy, not punishing — reputation fades, competence doesn't).
5. **Starting skill at level 1** (0 points); others learnable from 0, 20 points to unlock level 1.
6. **qualityBonus 0/+1/+2/+3/+4** added to the item quality 0–100 scale.
7. **NPC skill levels are not modeled in Phase 1** — Oda, Doran, Bessa etc. are treated as high-implicit; the player learns from them via the teaching rule. (Explicit NPC skill data is a "Later" item.)
8. **Skill visibility (0–100 per skill)** feeds D-09's reputation system; level 3+ public use generates visibility. This implements WORLD.md decision 5 mechanically.
9. **Absurd skills stay design-only** (no JSON data), per the brief.
10. **No XP-from-killing anywhere** in the skill system (per AGENTS.md and the brief).
11. **Sword cost 120 copper (imported)** stands as the swordsman's first great goal; spear (45) is the practical starting weapon.
12. **Recipe unlocks are capability-based** (`action_cook_stew`, etc.) in this phase; D-07 will bind real `recipe_` ids to skill levels.

---

## 10. New IDs for the glossary

`skill_cooking`, `skill_taming`, `skill_swordsmanship`. (Absurd skills are design-only and get no ids yet.)
