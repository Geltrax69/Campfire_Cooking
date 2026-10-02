# Design Prompts: World, Characters, Money and More

These prompts are for the **Design Phase**, which comes before any game code and long before Unity. You (the human) and the AI agents design the world together. Each design area produces two things:

1. **A design document** in `docs/design/` that explains the ideas in plain language (for you to read and approve).
2. **Data files** in `Content/` (JSON) that the simulation will load later. These turn the design into numbers and lists.

**Unity comes last.** Nothing in this phase needs Unity. When Unity starts, it will read the same `Content/` files.

---

## How to use these prompts

1. Paste **Prompt 0** into the main agent (Geltrax) once, at the start of the design phase.
2. Then go area by area (Prompts 1–11), roughly in order. Each prompt says what it depends on.
3. Geltrax can hand each area to a sub-agent. Areas without dependencies on each other can run in parallel.
4. **You approve each area** before the next one builds on it. Read the design document; ask for changes in plain words ("make Mira older", "apples should be cheaper").
5. After every few areas, run **Prompt 12** (consistency check).

Replace anything in `<angle brackets>` with your own ideas, or delete it and let the agent propose.

---

## Shared conventions (every design agent must follow these)

Paste this block into any prompt where an agent forgets the rules.

```
CONVENTIONS FOR ALL DESIGN WORK
- Originality: everything must be original. Do not copy names, characters, places, stories,
  or mechanics from existing anime, games or books (including Campfire Cooking in Another World,
  Log Horizon, Sword Art Online, Shield Hero, Free Guy). Inspiration for tone only.
- IDs: lowercase snake_case with a type prefix, unique across the project.
  npc_mira_holt, loc_apple_stall, item_apple, skill_cooking, species_wolf, recipe_apple_pie,
  group_guards, event_food_shortage.
- Money: integers only, in copper (the smallest coin). Never decimals.
- Time: game minutes or "HH:MM" 24-hour clock. 1 day = 1440 minutes.
- Scales: traits, needs, relationships, quality = 0–100 integers. 50 = average/neutral.
- Every number gets a short reason ("hunger rises 6/hour so adults eat ~3 times a day").
- Every design doc starts with a 5-line summary and ends with "Open questions for the human".
- Data files go in Content/<area>/ as JSON. Each file has "version": 1 at the top.
- Design for the simulation: anything a character does must be possible with the rules of the
  world (needs, money, items, places, time). No "magic" story triggers.
- Truth vs knowledge: when designing characters, note what each person knows and doesn't know.
- Keep the prototype small: one village, ~20 people, 3 shops, 1 farm, 1 tavern, 1 blacksmith,
  1 forest, 1 river, 10–20 item types, 5 animal species. Ideas for later go in a
  "Later" section, not in the data files.
- Only reference art that exists in Assets/ (see Assets/README.md) when suggesting how something
  looks, and note the file name. If nothing fits, write "MISSING ART: <description>".
```

---

## Prompt 0: Start the design phase (main agent)

```
You are the lead designer and orchestrator for "Living World", an original fantasy life-sim
RPG. We are in the DESIGN PHASE: no game code and no Unity yet. We are designing the world,
its people, its money and its rules, and writing them as documents and JSON data.

First read, in order: AGENTS.md, README.md (the game design vision), docs/DESIGN_PROMPTS.md
(this file, especially "Shared conventions"), docs/ROADMAP.md (Design Phase tasks),
and Assets/README.md (art we have).

Your job:
- Work through design areas D-01 to D-12 in docs/ROADMAP.md, in dependency order.
- For each area, brief a sub-agent with the matching prompt from docs/DESIGN_PROMPTS.md plus
  the shared conventions, and the approved documents it depends on.
- Review the result: does it follow the conventions, fit the vision in README.md, stay small
  enough for the prototype, and agree with earlier approved areas?
- Show me each finished area as a short summary (max 15 lines) with the key choices and the
  open questions. Wait for my approval or changes before marking it done.
- Commit approved documents to docs/design/ and data to Content/, update docs/ROADMAP.md,
  and push.
- Keep a running list of names and IDs in docs/design/GLOSSARY.md so nothing is duplicated.

Start by summarizing the plan in 10 lines and asking me the questions you need answered
before D-01 (world and tone).
```

---

## Prompt 1: World, setting and tone (D-01)

**Depends on:** nothing. **Produces:** `docs/design/WORLD.md`

```
Design the setting for Living World. Follow the shared conventions.

The player is transported or reincarnated from our world into this one. They are NOT a chosen
hero. The world existed long before them and doesn't revolve around them.

My ideas (use, improve or replace): <e.g. cozy but with real danger; a frontier village at the
edge of a kingdom; magic is rare and practical; monsters exist in the wild>

Write docs/design/WORLD.md with:
1. Summary (5 lines).
2. Tone: how the game should feel (cozy, dangerous, funny, melancholic — in what balance).
3. The kingdom/region: name, who rules, how far the capital is, what the region is known for.
   Keep it light: only what affects the village.
4. The village (our prototype location): name, size, age, why it exists here (river? trade road?
   farmland?), what it produces and what it must import, its current problems and hopes.
5. Magic: how common it is, who can use it, what it costs, what it can't do.
6. Monsters and danger: what threatens the village, how often, how people protect themselves.
7. Calendar: days per week/season/year, seasons, festivals (1–3 for the prototype).
8. How the player arrives: what happens on day 1, what locals think of strangers.
9. Daily life: food, work, beliefs/religion, entertainment, law (who punishes theft, and how).
10. Later: ideas for the wider world, kept out of the prototype.
11. Open questions for the human.
```

---

## Prompt 2: Village map and locations (D-02)

**Depends on:** D-01. **Produces:** `docs/design/LOCATIONS.md`, `Content/world/locations.json`

```
Design the prototype village's places. Follow the shared conventions. Use docs/design/WORLD.md.

Required: 3 shops (one sells apples/fruit), 1 farm (with an apple orchard), 1 tavern,
1 blacksmith, homes for ~20 people, a village square, 1 forest, 1 river, a guard post or similar,
plus anything WORLD.md implies (shrine, well, mill...). Keep the total under ~25 locations.

For each location: id, name, type, purpose (what it produces, consumes or provides), owner (npc id
or "village"), opening hours if any, capacity (how many people fit), who usually goes there and why,
and what can be stolen, bought or found there.

Also design:
- A simple map: rough positions on a grid (x, y in metres) and a travel-time table in minutes
  between key locations (walking). Draw an ASCII map in the document.
- Resources in nature: what grows in the forest and river (wood, herbs, berries, fish), how much,
  how fast it regrows.
- Which art from Assets/ could represent each building (file names), or MISSING ART.

JSON shape for Content/world/locations.json:
{ "version": 1,
  "locations": [ { "id": "loc_apple_stall", "name": "...", "type": "shop",
    "owner": "npc_...", "position": { "x": 0, "y": 0 }, "capacity": 6,
    "openHours": { "open": "07:00", "close": "19:00" }, "tags": ["market","food"],
    "art": "Assets/..." } ],
  "travelMinutes": [ { "from": "loc_a", "to": "loc_b", "minutes": 4 } ] }
```

---

## Prompt 3: Characters, the 20 villagers (D-03)

**Depends on:** D-01, D-02. **Produces:** `docs/design/CHARACTERS.md`, `Content/npcs/npcs.json`

```
Design the ~20 people of the village. Follow the shared conventions. Use WORLD.md and LOCATIONS.md.

They must feel like real people with their own lives, not quest-givers. Include a mix of ages
(children to elders), families, jobs, personalities, and at least a few tensions and secrets.
Required roles: apple-shop owner, farmer (+ family), tavern keeper, blacksmith, at least one guard,
the owners of the other shops, a few villagers with ordinary jobs, children, an elder.

For each person:
- Identity: id, name, age, gender, appearance (short, matched to a character model in
  Assets/Packs/Kenney/MiniCharacters if possible), occupation, workplace, home, family members.
- Personality traits (0–100): friendly, honest, greedy, brave, curious, cautious, lazy, generous,
  ambitious, gossipy. Pick values that make each person distinct.
- Needs baseline: how fast they get hungry/tired, how much they care about money, social life, safety.
- Starting money (copper) and notable possessions.
- Daily schedule (weekday and rest day): hour-by-hour default activities with location ids.
- Goals: 1 short-term and 1 long-term goal that the simulation can pursue
  (e.g. "save 2,000 copper to repair the roof").
- Relationships: with at least 3 others (family, friend, rival, crush, debt...), with
  trust/affection values (0–100) and a one-line reason.
- Knowledge: what they know that others don't (a secret, a skill, a rumor). Truth vs belief.
- How they'd react if they learned someone stole from the apple shop.
- Voice: 2 sample lines of how they talk.

Also include a relationship web (ASCII or a table) and a "who knows what" table.

JSON shape for Content/npcs/npcs.json:
{ "version": 1,
  "npcs": [ { "id": "npc_mira_holt", "name": "Mira Holt", "age": 34, "gender": "female",
    "occupation": "shopkeeper", "home": "loc_...", "workplace": "loc_apple_stall",
    "family": [ { "id": "npc_...", "relation": "daughter" } ],
    "traits": { "friendly": 70, "honest": 85, "greedy": 30, "brave": 40, "curious": 55,
                "cautious": 65, "lazy": 20, "generous": 50, "ambitious": 60, "gossipy": 35 },
    "needRates": { "hungerPerHour": 6, "energyPerHour": 4, "socialPerHour": 3 },
    "money": 850, "possessions": [ { "item": "item_apple", "count": 20, "where": "loc_apple_stall" } ],
    "schedule": { "workday": [ { "from": "06:00", "to": "07:00", "activity": "eat", "location": "loc_..." } ],
                  "restday": [ ... ] },
    "goals": [ { "id": "goal_...", "description": "...", "type": "save_money", "target": 2000 } ],
    "relationships": [ { "with": "npc_...", "trust": 70, "affection": 60, "reason": "..." } ],
    "knowledge": [ { "claim": "...", "isTrue": true } ],
    "model": "Assets/Packs/Kenney/MiniCharacters/character-female-b.glb" } ] }
```

---

## Prompt 4: Money and the economy (D-04)

**Depends on:** D-01, D-02, D-03. **Produces:** `docs/design/ECONOMY.md`, `Content/economy/economy.json`

```
Design the money system and economy of the village. Follow the shared conventions.
Use WORLD.md, LOCATIONS.md and CHARACTERS.md.

1. Currency: coin names and values (all stored as copper; e.g. 1 silver = 100 copper), what coins
   look like, whether people use barter or credit (tabs at the tavern?).
2. Cost of living: what a normal adult earns per day and spends per day on food, rent/home,
   drink, tools. Make the numbers add up for every NPC in CHARACTERS.md over a week —
   show the table.
3. Production chains: who produces what, how much per day/season, what inputs they need
   (farm → apples → apple stall → villagers; ore? → blacksmith → tools → farmer...).
   Draw each chain.
4. Prices: base price for every item, how prices respond to scarcity and surplus
   (rules with limits, e.g. max ±10% per day), seasonal changes, quality effects.
5. Shops: stock levels, restocking rules (when, from whom, how long delivery takes),
   what a shopkeeper does when stock runs out or goes missing.
6. Money sources and sinks: where new money enters the village (selling to traveling merchants,
   tax refunds, adventurer bounties...) and where it leaves (taxes, imports, repairs). The total
   must stay stable over a simulated month — explain how.
7. Wages and jobs: who employs whom, how pay works, what happens if someone can't pay.
8. Theft and loss: how theft affects owners (loss, suspicion, prices, security), insurance? debts?
9. The Apple Test in numbers: starting stock 20 apples, price, daily demand by which NPCs,
   farm delivery schedule — so that stealing 6 creates a noticeable shortage within 1–2 days.
10. Player economy: how the player can earn money early (any skill), and what they can spend on.
11. Later and open questions.

JSON shape for Content/economy/economy.json:
{ "version": 1,
  "currency": [ { "id": "coin_copper", "name": "...", "valueCopper": 1 } ],
  "priceRules": { "maxDailyChangePercent": 10, "scarcityThreshold": 0.3, ... },
  "producers": [ { "location": "loc_farm", "item": "item_apple", "perDay": 30,
                   "seasons": ["summer","autumn"], "inputs": [] } ],
  "shops": [ { "location": "loc_apple_stall", "sells": [ { "item": "item_apple",
               "basePrice": 3, "startStock": 20, "restockBelow": 8, "restockFrom": "loc_farm",
               "deliveryMinutes": 240 } ] } ],
  "wages": [ { "employer": "npc_...", "employee": "npc_...", "perDay": 40 } ],
  "sources": [ ... ], "sinks": [ ... ] }
```

---

## Prompt 5: Items and resources (D-05)

**Depends on:** D-02, D-04. **Produces:** `docs/design/ITEMS.md`, `Content/items/items.json`

```
Design all item types for the prototype (10–20 core items plus tools). Follow the shared
conventions. Use LOCATIONS.md and ECONOMY.md.

For each item: id, name, category (food, drink, material, tool, weapon, valuable, animal product),
base value (copper), weight, stack size, perishable? (shelf life in days and what spoilage does),
quality range, where it comes from (producer/location/nature), who uses it and for what,
effects when used (eating apple: hunger -15, health +1), whether it's tracked individually
(only important items — e.g. a family heirloom) or counted in bulk.

Include at least: apple, bread, flour, ale, meat, fish, herbs, wood, stone, iron, cloth, a few
tools (axe, hoe, fishing rod, knife), a sword, and 1–2 unique items with stories.
Match each to a 3D model in Assets/ (e.g. Assets/Packs/Kenney/FoodKit/...) or MISSING ART.

JSON: { "version": 1, "items": [ { "id": "item_apple", "name": "Apple", "category": "food",
  "baseValue": 3, "weight": 0.2, "stack": 50, "perishable": { "shelfLifeDays": 10 },
  "effects": { "hunger": -15 }, "tracked": false, "art": "Assets/..." } ] }
```

---

## Prompt 6: Skills (D-06)

**Depends on:** D-01, D-04, D-05. **Produces:** `docs/design/SKILLS.md`, `Content/skills/skills.json`

```
Design the skill system. Follow the shared conventions. The prototype has three player skills:
Cooking, Taming and Swordsmanship. Also outline (design only, no data) 5 "absurd" skills like
Cleaning, Talking to Chickens, Smelling, Rope Making, Soup Making.

Rule: no skill is useless. Every skill must change the world through its systems,
not through special story events.

For each skill:
- What the player can do with it at low, medium and high level.
- How it improves (practice? teaching? both?) — no XP-from-killing grind.
- Which world systems it touches and how (example: Cooking → food quality → NPC happiness,
  health, tavern popularity, food prices, demand for farm goods, relationships).
- Chain reactions it can cause in the village over days and weeks (give 2 examples each).
- How NPCs notice and react to the player's skill (reputation by group).
- What it costs (time, ingredients, risk).
- Starting skill choice: how the player gets their skill on arrival.

JSON: { "version": 1, "skills": [ { "id": "skill_cooking", "name": "Cooking",
  "levels": [ { "level": 1, "unlocks": ["recipe_..."], "qualityBonus": 0 } ],
  "practice": { "perUse": 1, "levelThresholds": [0, 100, 300, 700] },
  "affects": ["food_quality","npc_happiness","tavern_popularity"] } ] }
```

---

## Prompt 7: Recipes, cooking and crafting (D-07)

**Depends on:** D-05, D-06. **Produces:** `docs/design/RECIPES.md`, `Content/recipes/recipes.json`

```
Design recipes for cooking (10–15) and basic crafting (5–10, e.g. blacksmith and tools).
Follow the shared conventions. Use ITEMS.md and SKILLS.md.

For each: id, name, inputs (items + counts), tool/place needed (campfire, kitchen, forge),
time in minutes, skill and minimum level, output item and quality rules (ingredient quality
+ skill), effects of the result, value vs. ingredients (is it profitable to sell?).
Show how 2–3 recipes connect to the economy (apple pie raises apple demand...).
```

---

## Prompt 8: Animals and taming (D-08)

**Depends on:** D-01, D-02, D-05. **Produces:** `docs/design/ANIMALS.md`, `Content/animals/species.json`

```
Design 5 animal species for the prototype (e.g. chickens, cows or sheep, deer, wolves,
plus 1 original small creature). Follow the shared conventions.

For each species: id, name, wild/domestic, habitat (location ids), diet, what eats it,
population size and breeding rate, daily behavior, temperament, fear and aggression rules,
what it produces (eggs, milk, meat, hide), danger to people/livestock.

Taming: taming is a relationship, not a button. For each tameable species: how trust is built
(feeding, protecting, patience, showing strength, solving a problem), what breaks trust,
how long it takes, what a tamed animal can do (carry, guard, hunt, find things, help farm).

Ecosystem: how populations affect each other and the village (too many wolves → livestock losses
→ meat prices rise → hunters hired). Write 2 example chains.

Match each species to a model: Assets/Packs/Kenney/CubePets or
Assets/Models/Characters/Animals/Quaternius (animated wolf, horse, stag, fox), or MISSING ART.
```

---

## Prompt 9: Knowledge, rumors and reputation (D-09)

**Depends on:** D-03. **Produces:** `docs/design/KNOWLEDGE.md`, `Content/social/social.json`

```
Design how information and opinions work among the villagers. Follow the shared conventions.
Use CHARACTERS.md.

1. Perception: what makes someone notice an event (distance, light, attention, traits).
   Give rules with numbers, and 5 examples using real villagers and places.
2. Memory: importance levels with examples and how long each lasts.
3. Rumors: where and when people talk (tavern, well, market), what topics spread first,
   how stories get distorted, how trust changes belief.
4. Who are the gossips, the trusted sources, the skeptics (by name)?
5. Reputation groups for the prototype (guards, farmers, merchants, villagers, children...),
   which villagers belong to which, and what raises/lowers the player's standing with each.
6. Law: what the guard does with a suspicion vs. proof; what punishment looks like.
7. The Apple Test, socially: walk through day by day what each relevant villager could
   know, believe and say — once with a witness, once without.
```

---

## Prompt 10: Town state and emergent events (D-10)

**Depends on:** D-02, D-03, D-04. **Produces:** `docs/design/TOWN.md`, `Content/world/town.json`

```
Design the village as a living whole. Follow the shared conventions.

1. Town stats (population, wealth, food supply, safety, housing, employment, trade, happiness,
   crime, infrastructure, reputation): how each is calculated from the simulation, starting values.
2. How the village grows or declines over months — from conditions, never from an upgrade button.
   What makes a village become a town?
3. 10 emergent events (food shortage, monster attack, festival, fire, theft wave, merchant arrival,
   illness, drought...): what world conditions cause each, what happens, and what events can
   follow. No scripted triggers — only conditions.
4. How the player can influence each stat without directly controlling it.
```

---

## Prompt 11: The player's start (D-11)

**Depends on:** D-01 to D-06. **Produces:** `docs/design/PLAYER_START.md`, `Content/player/start.json`

```
Design the player's arrival and first days. Follow the shared conventions.

1. How the player arrives (place, time, what they have: clothes, items, money).
2. Skill selection or discovery at the start.
3. Who notices the newcomer first, what villagers think and say about a stranger.
4. The first day without quest markers: what natural opportunities exist (people with needs,
   jobs available, things to find), at least 8, based on CHARACTERS.md and ECONOMY.md.
5. Three very different "first week" stories that could emerge — one for a cook, a tamer and
   a swordsman — showing the village ending up different each time.
6. Where and how the player can sleep, eat and earn money early.
```

---

## Prompt 12: Consistency check (any time; D-12 at the end)

```
Review all approved design documents in docs/design/ and all files in Content/. Report:
1. Contradictions between documents (names, numbers, places, relationships, schedules).
2. Broken references (an id used somewhere but not defined, or defined twice).
3. Economy problems: does every NPC's budget work for a week? Do money sources and sinks balance
   over a month? Does the Apple Test create a shortage as designed?
4. Schedules that don't work (two places at once, shops open with no owner present,
   travel times longer than the gap).
5. Anything that copies existing anime/games too closely.
6. Things that are too big for the prototype.
7. MISSING ART list collected from all documents.
Fix only clear mistakes (typos, broken ids); list everything else for me with your recommendation.
Then update docs/design/GLOSSARY.md.
```

---

## Prompt 13: Changing something later (any time)

```
I want to change: <describe the change in plain words>.
Find every design document and Content file affected, show me the list and what would change in
each, and wait for my OK. Then make all the changes together, run the consistency check
(Prompt 12) on the affected areas, commit and push.
```
