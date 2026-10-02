# Living World — Isekai Life RPG

A third-person, anime-inspired fantasy life RPG designed for iPad.

> **Status:** Design phase. No code yet. This document is the working design spec and will change as discussion continues.

---

## The core idea

**The world exists independently of the player.**

The player is not the center of the universe. NPCs have their own lives, animals have their own behavior, shops have real inventories, and resources exist physically in the world. People earn, spend, consume, and remember. Towns grow, decline, and change. The world keeps reacting after the player leaves.

```
PLAYER ACTION
  → WORLD STATE CHANGES
  → NPCS OBSERVE / EXPERIENCE THE CHANGE
  → NPCS MAKE THEIR OWN DECISIONS
  → WORLD STATE CHANGES AGAIN
  → NEW EVENTS EMERGE
```

The reward is the moment the player realizes: *"I did something earlier, and now something completely different is happening because of it."* The game does not always explain the connection; the player discovers it.

### Inspiration (feel only)

The tone draws on *Campfire Cooking in Another World with My Absurd Skill*, *Log Horizon*, *Sword Art Online*, *The Rising of the Shield Hero*, and *Free Guy*.

**Everything in this game is original.** No characters, names, stories, artwork, locations, or proprietary mechanics are taken from these works.

---

## Design pillars

1. **Systems, not scripts.** Build rules, characters, and mechanics that produce stories. Do not script every story.
2. **Game state is the source of truth.** The deterministic engine owns all facts. AI may only describe them.
3. **Knowledge is not truth.** What happened and what each NPC *knows* happened are tracked separately.
4. **No single correct playstyle.** Peaceful, adventurous, criminal, economic, social, or any mix.
5. **Consequences ripple.** Direct → indirect → secondary → long-term. The player rarely sees the whole chain at once.
6. **Prove the simulation first.** One village before any open world.

---

## The player

The player is transported or reincarnated into the world. They are **not** a chosen hero. They can become an adventurer, merchant, farmer, cook, tamer, blacksmith, healer, alchemist, fisher, hunter, explorer, builder, trader, mage, warrior, craftsman, or anything else the systems allow.

The player can help, ignore, steal, trade, fight, cook, tame, farm, explore, build, and become wealthy, famous, feared, or completely unknown.

---

## Skills

At the start, the player receives or selects an unusual ability. Skills are not limited to classic RPG classes.

| Category | Examples |
|---|---|
| Combat | Swordsmanship, Archery, Spearmanship, Shield Mastery, Martial Arts, Magic |
| Life | Cooking, Farming, Fishing, Hunting, Mining, Woodworking, Blacksmithing, Tailoring |
| Special | Animal/Monster Taming, Appraisal, Teleportation, Merchant, Cooking Magic, Beast Communication, Plant Growth, Item Creation, Tracking, Cartography |
| Absurd | Cleaning, Bread Making, Smelling, Talking to Chickens, Collecting, Rope Making, Shoe Repair, Gardening, Soup Making |

**No skill is useless.** Every skill must touch the world's systems. For example:

- **Cooking** affects food quality, NPC happiness and health, tavern popularity, food prices, tourism, trade, farm demand, monster attraction, and relationships.
- **Taming** affects animal and monster populations, transport, farming, security, hunting, breeding, trade, and town defense.

Two players entering the same village with different skills (Cooking, Taming, Blacksmithing, Farming, Merchant) should end up with noticeably different villages.

---

## NPCs

NPCs are simulated individuals, not quest dispensers.

| Aspect | Contents |
|---|---|
| Identity | Name, age, gender, appearance, occupation, home, family |
| Personality | Friendly, aggressive, shy, ambitious, greedy, honest, curious, cautious, adventurous, lazy, generous |
| Needs | Hunger, sleep, safety, money, social contact, entertainment, health |
| Goals | Buy a house, marry, protect family, become a knight, open a shop, travel, get rich, learn magic, retire |
| Relationships | Family, friends, enemies, coworkers, neighbors, merchants, the player |
| Memory | Important events involving the player, family, friends, enemies, the town |
| Knowledge | Only what they could reasonably have learned |

### Daily life

NPCs follow flexible schedules (wake, eat, work, lunch, work, home, dinner, socialize, sleep) and break them when events demand it. A monster attack sends a farmer home, a sick child sends them to the healer, heavy rain keeps them indoors, and a festival draws them to the town square.

### Memory

Memories are weighted by importance and fade accordingly:

| Importance | Example | Retention |
|---|---|---|
| Minor | "Player bought bread." | Short |
| Important | "Player saved my daughter." | Long |
| Major | "Player killed my brother." | Very long |

### Knowledge vs. world truth

This distinction is critical.

- **World truth:** The player stole six apples.
- **Shopkeeper knows:** Six apples are missing.
- **Witness knows:** "I saw someone near the shop."
- **Guard later learns:** "A witness saw the player."

Information spreads only through believable channels. An NPC never magically knows what happened elsewhere, and never accuses the player without evidence.

#### Systems this needs (to be specified)

- **Perception:** Whether an NPC notices an event depends on line of sight, distance, lighting, and attention.
- **Information propagation (rumors):** Who tells whom, when, how accurately, and how much the listener believes it.

---

## Example: the apple chain

| Step | Event | Apples in stock |
|---|---|---|
| 0 | Shop starts the day | 20 |
| 1 | Player steals 6 | 14 |
| 2 | NPC A buys 5 | 9 |
| 3 | NPC B buys 7 | 2 |
| 4 | NPC C wants 5; only 2 left | 2 |

NPC C may buy something else, try another shop, come back later, complain, or pay more elsewhere. The shopkeeper notices the demand and may reorder, raise prices, contact the farmer, check the inventory, or suspect theft.

No artificial quest penalty is needed. The consequences come from the simulation.

---

## Consequence layers

| Layer | Example |
|---|---|
| Direct | Player steals an item; inventory changes immediately |
| Indirect | The shop loses stock |
| Secondary | A customer can't buy what they came for |
| Tertiary | The customer shops elsewhere |
| Long-term | The original shop loses revenue |
| Very long-term | The shopkeeper changes their business strategy |

---

## Objects and resources

Everything important has persistent state. For example:

```
APPLE_1842
  Origin:   Farm_07
  Owner:    Shopkeeper_Mira
  Location: Market Shop
  Quality:  82%
  Age:      2 days
  Status:   Fresh
```

When stolen, the owner becomes the Player. When sold, the owner becomes the buyer and the location becomes their house. When eaten, the status becomes Consumed.

**Not every object needs its own record.** Track important objects individually and large quantities as totals.

---

## Economy

| Production | Consumption |
|---|---|
| Farm → crops | NPCs → food |
| Mine → minerals | Blacksmith → iron |
| Forest → wood | Builder → wood + stone |
| Fishing area → fish | Tavern → food + drink |
| | Adventurers → equipment |

Core resources include food, wood, stone, iron, cloth, herbs, livestock, and magical materials. Prices respond to supply, demand, scarcity, distance, quality, season, and local events. Shop inventories are never static.

**To be specified:** where money enters the world and where it leaves. Without both, prices drift upward forever or the economy stalls.

---

## Towns

A town tracks population, wealth, food supply, safety, housing, employment, trade, happiness, crime, infrastructure, and reputation.

Towns grow from conditions, not upgrade buttons. When population, food, housing, trade, and safety all rise, a village eventually *becomes* a town. The player can contribute without directly controlling it.

**Emergent events** come from world conditions and can trigger further events: food shortages, monster attacks, festivals, fires, thefts, droughts, merchant arrivals, new mines, outbreaks of a fictional disease, wealthy travelers, a new adventurer guild, new farms, migration, political conflict, and new trade routes.

---

## Animals and taming

Animals are part of the ecosystem, with species, hunger, health, age, temperament, territory, reproduction, relationships, fear, aggression, and trust. Wolves hunt deer, deer eat plants, and farmers protect livestock.

**Taming is a relationship, not a button.** The animal encounters the player, gets fed, watches the player, is protected by them, and gradually builds trust until it accepts them. Different animals need different approaches: a wolf may respect strength, a horse may respond to patience, and a magical creature may need a problem solved.

Tamed animals can fight, carry items, guard farms, hunt, breed, travel with the player, help with farming, and find resources.

---

## Reputation

There is no single good/evil meter. Reputation depends on the group or person:

| Group / NPC | Example |
|---|---|
| Town guards | Trust: low |
| Farmers | Trust: high |
| Merchants | Trust: medium |
| Adventurers | Respect: high |
| A specific NPC | Friendship: high |
| Another NPC | Hatred: high |

The player can be a hero to farmers and a criminal to the guards at the same time.

---

## Quests and dialogue

There are no `!` markers over heads. Opportunities come from conversation:

> "I've been trying to find someone who can deliver this medicine to my sister."

The player may help, refuse, negotiate, steal the medicine, make their own, ask someone else, or ignore it.

Dialogue reflects who the player is, what the NPC remembers, what they need, what they know, their relationship with the player, and recent local events. If the player stole from an NPC yesterday, the conversation changes, but only if that NPC knows about it.

---

## Death, reincarnation, and generations

Death does not necessarily mean reloading a save. The player can reincarnate as a new character in the **same, already changed world**. The previous life's relationships, property, buildings, money, descendants, reputation, enemies, friends, achievements, crimes, and discoveries all remain.

NPCs age, marry, have children, and pass on businesses and property. Over generations the town changes, and time feels like it is really passing.

---

## AI architecture

**The deterministic game engine controls** inventory, money, relationships, world state, schedules, production, consumption, combat, health, ownership, time, and events.

**An LLM may assist with** natural dialogue, contextual conversation, personality expression, rumors, explanations, and story narration.

The AI must never contradict the game state. It receives a structured summary of what the NPC knows, not the whole world.

> **Game state is the source of truth.**

---

## Saving

A save preserves the **world**, not just the player's position: world time, NPC states, inventories, ownership, relationships, buildings, economy, player state, animal states, important events, and memories.

---

## iPad design

- Landscape orientation, touch first, large touch targets, minimal clutter
- Optional controller and Apple Pencil support

| Input | Action |
|---|---|
| Virtual joystick or tap-to-move | Movement |
| Tap NPC | Interaction menu |
| Tap object | Inspect / interact |
| Long press | Contextual info |
| Swipe | Rotate camera |
| Pinch | Zoom |

**Camera:** third person, cinematic but practical, with rotation, zoom, target focus, and an optional lock.

**UI:** immersive, not an MMO-style HUD.

- **Top:** time, weather, location
- **Bottom left:** movement
- **Bottom right:** contextual actions
- **On interaction:** a radial menu (e.g. Talk, Trade, Give, Ask, Follow, Inspect) that only shows options that make sense

Combat avoids requiring many tiny buttons.

---

## First playable prototype

Build **one village** only.

| Content | Amount |
|---|---|
| NPCs | 20 |
| Shops | 3 |
| Farm, tavern, blacksmith | 1 each |
| Forest, river | 1 each |
| Resources | 10–20 |
| Animal species | 5 |
| Player skills | Cooking, Taming, Swordsmanship |

Plus a day/night cycle, basic economy, NPC schedules, memory and relationships, persistent inventory, and basic town development.

> **Open question:** This list includes systems from later phases (skills, taming, town development). A smaller first step would include only what the apple experiment needs: schedules, shop inventories, farm supply, perception, memory, and rumors.

### The first experiment

1. Player enters the village and visits the apple shop (20 apples).
2. Player steals 6 apples and leaves.
3. Time passes. NPCs keep living their lives.
4. Customers try to buy apples, and the inventory changes.
5. The shopkeeper notices the shortage. NPCs react naturally.
6. Player returns and can discover that their action affected the town.

**If this feels convincing, the core concept works.**

---

## Success criteria

The prototype succeeds when the player can say: *"I did something yesterday, and the world changed because of it."*

Eventually the player should have moments like:

- "I didn't know that NPC saw me."
- "I didn't realize that shop depended on that farmer."
- "I stole those seeds months ago, and now food is expensive."
- "That child I helped grew up."
- "That wolf I tamed now has offspring."
- "That shop exists because of something I did."

The central experience is the player shifting from *"What quest does the game want me to do?"* to *"What can I do in this world?"*

---

## Development roadmap

| Phase | Focus |
|---|---|
| 1 | Living village prototype |
| 2 | NPC memory and relationships |
| 3 | Economy and persistent resources |
| 4 | Skills, crafting, cooking |
| 5 | Animal ecosystem and taming |
| 6 | Town development |
| 7 | Expanded world |
| 8 | Reincarnation and generations |
| 9 | Advanced AI dialogue |
| 10 | iPad optimization |

**Do not begin with a huge open world. Prove the simulation first.**

---

## Open decisions

- **Engine:** Unity, Godot, or native Swift with RealityKit
- **Prototype visuals:** full 3D, or a simple top-down/text view to test the simulation first
- **Dialogue AI:** on-device (offline, free per use) or cloud (higher quality, costs per request, needs a connection)
- **Time scale:** how many real minutes equal one in-game day
- **Off-screen simulation:** how much detail runs away from the player, and how the world catches up on return
- **Measurable pass/fail for the apple experiment**

---

## Repository layout

```
/
├── README.md     ← this design document
└── Assets/       ← art, models, audio, and other game assets (uploaded separately)
```
