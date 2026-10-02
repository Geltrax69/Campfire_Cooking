# Living World — Animals and Taming (D-08)

## Summary

Millbrook's prototype simulates 5 animal species: chickens and pigs (domestic, at Alder Farm and other households), wild boar (the pigs' untamable cousins, folded into the same entry), deer and wolves (wild, in the worked forest), and the **brambleback** — an original small burrowing forager with mossy quills that eats ticks off livestock and hoards shiny things. Taming is a relationship, not a button: trust builds 0–100 through repeated calm interactions (one meaningful gain per day), takes 5 days for a brambleback and two full seasons for a wolf pup, and breaks through cruelty or neglect. Populations push on each other and on the village economy — a hard winter means deer near the farms, wolves following, livestock lost, prices rising, and hunters hired — with no scripted triggers.

---

## 1. The five species

### species_chicken — Chicken (domestic)

- **Habitat:** `loc_farm` (12 hens, the established flock), `loc_home_fenn`, a few other households — ~20 chickens village-wide.
- **Diet:** grain, insects, kitchen scraps, grit.
- **Predators:** foxes, hawks, dogs, the occasional hungry cat.
- **Population & breeding:** 20 birds, 2 roosters. A hen lays ~0.7 eggs/day in the warm months, ~0.2 in winter (reason: heritage birds, not modern layers — this is why Maren's ~5 eggs/day of sales is realistic and why eggs are worth 1 copper). Hens hatch 6–10 chicks each spring if allowed; villagers eat or trade most of them.
- **Daily behavior:** dawn chorus, scratch and peck all day within ~30 m of the coop, dust-bathe at midday, roost at dusk. Roosters crow at false dawns and fight hawks.
- **Temperament:** placid, curious, pecking order is real (a top hen rules the yard).
- **Fear/aggression:** scatter from sudden movement; a rooster will spur a dog or a small child to defend hens.
- **Produces:** `item_egg` (~0.7/day/hen warm months); stewing hens and capons become `item_pork`? No — chickens become meat via the kitchen (counted as food, not a separate item); feathers for pillows and fletching (flavor, not an item).
- **Danger:** none to people; a rooster can scratch a child.
- **Taming (individual bonding):** yes. +8/day hand-feeding corn, +5 gentle handling, −20 rough handling, −30 chased by dogs. Bonded at trust 60+ (~7 days). A bonded chicken comes when called (this is Lida's speckled-hen goal, expressed as mechanics), lays in a chosen nest box, and alarm-clucks at hawks. Neglect for 14 days → reverts to an ordinary flock bird.
- **Art:** `Assets/Models/Characters/Animals/chicken.glb` (adult), `Assets/Packs/Kenney/CubePets/animal-chick.glb` (chicks).

### species_pig — Pig, and its wild cousin the boar (domestic / wild)

One entry, two variants — the village pig and the forest boar are the same animal, split by upbringing (reason: WORLD.md names boars as a real danger, and the 5-species cap is better spent on the original creature).

- **Habitat:** domestic — `loc_farm` (2 sows, established); wild — `loc_forest_edge` and the deep forest fringe (~10 boars).
- **Diet:** grain, roots, windfall apples, kitchen waste; boars root for tubers and raid orchard fences in autumn.
- **Predators:** wolves take piglets; nothing takes an adult boar except a very bold wolf pack in deep winter.
- **Population & breeding:** the 2 sows farrow ~7 piglets each spring (reason: heritage sows average 6–8); the village keeps 2–4 and slaughters the rest in autumn (this is where ECONOMY.md's ~200 pork cuts come from). Boar sows farrow 4–6 in spring.
- **Daily behavior:** pigs — feed twice daily, wallow at midday, sleep in piles. Boars — nocturnal, solitary males, sounders of sows and young by day in thickets.
- **Temperament:** domestic pigs are placid, intelligent, and stubborn; boars are wary and short-tempered.
- **Fear/aggression:** pigs — a 100 kg sow shoves hard if startled; otherwise harmless. Boars — **the rule is distance**: sows with piglets charge within 15 m, lone boars bluff-charge; they never pursue far, but a tusk gash is a healer visit (reason: boars are WORLD.md's "dangerous if cornered" — common enough to respect, rare enough to surprise).
- **Produces:** `item_pork` (autumn slaughter), `item_honey`? No — pigs produce pork and, via rooting, turned garden soil (a farm chore they do for free).
- **Danger:** domestic — low. Wild boar — medium; the orchard fences exist because of them.
- **Taming:** domestic piglets, yes; **wild boars, no** (never — the design says so plainly). Piglet: +6/day regular feeding and calm presence, +10 scratching favorite spots, −25 if struck, −40 if starved. Bonded at 70+ (~12 days). A bonded pig follows its person, finds mushrooms in autumn (foraging, feeding the kitchen), and will stand between its person and a strange dog. If trust falls below 40 or the pig goes hungry, it roots up the garden — the risk is property, not people.
- **Art:** `Assets/Packs/Kenney/CubePets/animal-pig.glb` (domestic), `Assets/Packs/Kenney/CubePets/animal-hog.glb` (boar).

### species_deer — Deer (wild)

- **Habitat:** `loc_forest_edge` (~40 head in the worked forest), ranging to the farm's east fields in hard winters.
- **Diet:** saplings, bark in winter, herbs, orchard windfalls (a nuisance), garden greens (a bigger nuisance).
- **Predators:** wolves (~8 deer/year), hunters (~4/year, Ralf's quota), harsh winters.
- **Population & breeding:** ~40 (reason: the worked forest's browse supports about this many — more would strip saplings bare, which Tam would notice and report). Does drop 1 fawn each spring; twins are rare and celebrated.
- **Daily behavior:** graze at dawn and dusk, bed down midday in thickets, drink at the river at night. Yards up (herds cluster) in deep snow.
- **Temperament:** shy, alert, graceful; habituate slowly to familiar people.
- **Fear/aggression:** flight distance ~30 m from strangers, ~10 m from known villagers (reason: habituation is gradual and per-person — the simulation tracks it). **Stags in rut (30 autumn days) charge if approached within 10 m.** Does with fawns stomp small predators, not people.
- **Produces:** venison (meat, via hunting — counted through the kitchen, not a separate item), `item_pelt` (10 copper, feeding the bounty-adjacent trade), antler (craft flavor for later).
- **Danger:** low except rutting stags (medium, seasonal) and the winter yarding near farms (crop damage, not injury).
- **Taming:** fawns only — and only rescued or winter-habituated ones; **stags can never be tamed** (the design says so plainly). +4/day winter feeding at a fixed station, +2 calm presence, −30 sudden chase or loud noise. Bonded at 80+ (~20–30 days). A bonded deer carries light packs (10 kg — reason: a deer is not a mule), alarm-snorts at wolves (early warning the night watch values), and can lead a trusted person to water. Never safe near village dogs — the fear rule always wins, and a panicked deer in the square is its own small disaster.
- **Art:** `Assets/Models/Characters/Animals/Quaternius/stag.glb` (animated; also serves hinds at smaller scale), fallback `Assets/Packs/Kenney/CubePets/animal-deer.glb`.

### species_wolf — Wolf (wild)

- **Habitat:** the high forest and deep forest; dens a half-day's walk out. Hunts through `loc_forest_edge` in winter.
- **Diet:** deer (~8/year), rabbits, livestock when desperate, refuse in the hardest weeks.
- **Predators:** humans with bows (the 50-copper bounty); nothing else.
- **Population & breeding:** one pack of 5–7 — an alpha pair, yearlings, and 3–4 spring pups (reason: one pack is all the worked forest's game supports; two packs would mean constant livestock war). **2–4 livestock incidents per winter** (WORLD.md, load-bearing).
- **Daily behavior:** nocturnal in winter, crepuscular in summer; ranges widely, rests by day in dense cover; the pack howls on still winter nights (everyone hears it; children count the voices).
- **Temperament:** intelligent, cautious, loyal to the pack; avoids humans by preference.
- **Fear/aggression:** flight distance 50 m+ from humans; a cornered wolf fights badly and bravely. A starving pack in deep winter may test the palisade — this is the incident budget, not a nightly event.
- **Produces:** `item_pelt` (10 copper base; the crown's 50-copper winter bounty makes wolf work pay).
- **Danger:** to livestock — real. To people — very low, and the design keeps it that way (one remembered traveler incident in living memory, per WORLD.md's tone).
- **Taming:** **orphaned pups only**, and keeping one requires **the council's approval** (reason: a wolf in the village is a public-safety decision — Bram enforces it, Elswith witnesses it; this makes taming a wolf a social achievement, not just a grind). +5/day feeding meat, +5 play and training, +8 showing calm strength (standing ground without fear — wolves respect it), −50 if struck, **instant break if starved**. Bonded at 85+ (~60 days — two full seasons, reason: a wolf is a wild predator and trust must be total). A bonded wolf guards its person's home, hunts alongside them, tracks by scent, and stands in a fight. If trust breaks below 30 the wolf leaves for the wild; **if it breaks through cruelty, it may turn — and the village will demand it be put down.** The stakes are real and everyone knows them.
- **Art:** `Assets/Models/Characters/Animals/Quaternius/wolf.glb` (animated).

### species_brambleback — Brambleback (wild, original)

A small burrowing forager, rabbit-sized and round, with moss-green quills over a soft brown coat, a whiskered snout, and a habit of sitting up to watch you with bright black eyes. It eats ticks, grubs, and fallen grain; it hoards shiny things — buttons, bottle caps of glass, a lost copper — in its burrow and arranges them by size. Villagers call a tidy burrow "a brambleback's treasury" and leave porridge out for the ones near their barns. It is wholly original to Millbrook.

- **Habitat:** `loc_forest_edge` margins, field edges, hedgerows — ~25 active burrows.
- **Diet:** ticks, fleas, grubs, fallen grain, porridge (given).
- **Predators:** foxes (take the young), dogs, owls.
- **Population & breeding:** ~25 burrows; pairs raise 2–3 young each spring (reason: small litters, high predation — the population is stable, not booming).
- **Daily behavior:** forages at dusk and dawn along fixed rounds, naps midday in the burrow, grooms its quills meticulously. Each brambleback works a "round" of barns and coops like a tiny night watchman.
- **Temperament:** shy, methodical, vain about its treasury.
- **Fear/aggression:** freezes, then bolts for the burrow; if caught, rolls into a quill ball (unpleasant to hold, impossible to hurt without meaning it); never bites unless squeezed.
- **Produces:** nothing slaughtered — instead, **services**: fewer ticks on livestock (measurably healthier chickens and pigs), and the treasury (see taming).
- **Danger:** none. A quill ball in a child's hands is a lesson, not an injury.
- **Taming:** the easiest wild tame — this is the species that teaches the player how taming works. +12/day porridge or oats left at the burrow, +6 sitting quietly nearby (patience — it watches you for a long time before it approaches), −15 loud disturbance. Bonded at 50+ (~5 days). A bonded brambleback nests in your barn, keeps your livestock nearly tick-free (fewer illness events — a real, system-level benefit), and brings you things from its treasury: lost buttons, a dropped bead, once in a blue moon a copper someone dropped on the road. Children trade for its treasures, which makes it a small, strange part of the village economy.
- **Art:** MISSING ART: small rabbit-sized burrowing mammal with moss-green quills, whiskered snout, bright black eyes.

---

## 2. Taming: the relationship system

Taming is never a button. It is a per-animal **trust score (0–100)** built through repeated, calm interactions:

- **One meaningful trust gain per animal per day** (reason: relationships need time, and this bounds the simulation cost — 20 NPCs plus dozens of animals must stay cheap to tick).
- Trust **decays slowly** when neglected: −2/day for wild species, −1/day for domestic (reason: wild animals revert; domestic ones remember longer).
- **Hitting or starving a bonded animal zeroes trust and locks it for a season** — it will not bond again until spring (reason: betrayal has a cost the player can feel).
- Every species has a **bond threshold** and an expected **days to bond** (above). Reaching the threshold unlocks that species' abilities; falling below it suspends them (the animal stays friendly but stops working for you).
- **No skill is required to start** — anyone can leave porridge out. But the Taming skill (D-06, in parallel) multiplies trust gains and unlocks advanced abilities: a skilled tamer bonds a deer in two weeks instead of a month, and only a skilled tamer can keep a wolf past the first winter. The exact multipliers are D-06's to set; the hooks are here.

**What breaks trust, everywhere:** cruelty (−50 to zero), starvation (instant break for predators), neglect (slow decay), frightening (chase, loud noise near a shy species: −30), and for pack/herd animals, separating them from their kind.

---

## 3. Ecosystem: two chains

**Chain 1 — the hard winter (the wolf loop).** Deep snow → deer yard near the farms for browse → the wolf pack follows the deer → 2–4 livestock incidents (chickens, a piglet, once a lamb that isn't ours to lose — it's Corvin's) → egg and pork prices rise under the scarcity rule → the council pays Ralf's winter retainer → pelts come in → 50-copper bounties are paid → the pack thins → spring comes with fewer wolves → deer rebound → more deer at the orchard in autumn → bark stripped from young trees → Corvin's apple yield dips → Mira's wholesale price rises. Every link is a system rule; nothing is scripted. The player can intervene anywhere — feed the deer away from the farms, hunt with Ralf, guard the coops at night — and the loop bends.

**Chain 2 — the brambleback spring (the small loop).** A mild spring → brambleback boom → fewer ticks → healthier chickens → more eggs → egg price drops under the surplus rule → Maren's egg money falls → she gathers more herbs instead → remedy supply rises → Sella's prices soften → and the young bramblebacks dispersing draw foxes → foxes take chickens → villagers mend coops → Doran sells more nails. Small animals, small money, real consequences — the cozy half of the ecosystem.

**Two standing tensions the simulation must keep:** overhunting deer pushes wolves onto livestock (the pack must eat); too many pigs raise feed costs faster than pork prices (the farm's margin is thin by design).

---

## 4. Art summary

| Species | Art |
|---|---|
| Chicken | `Assets/Models/Characters/Animals/chicken.glb` (adult), `Assets/Packs/Kenney/CubePets/animal-chick.glb` (chicks) |
| Pig / boar | `Assets/Packs/Kenney/CubePets/animal-pig.glb`, `Assets/Packs/Kenney/CubePets/animal-hog.glb` (boar) |
| Deer | `Assets/Models/Characters/Animals/Quaternius/stag.glb` (animated), fallback `Assets/Packs/Kenney/CubePets/animal-deer.glb` |
| Wolf | `Assets/Models/Characters/Animals/Quaternius/wolf.glb` (animated) |
| Brambleback | MISSING ART: rabbit-sized burrowing mammal, moss-green quills, whiskered snout, bright black eyes |

---

## 5. Later (kept out of the prototype)

- Horses (travelers ride them; the village has none yet — a Phase 6 trade good).
- Sheep and a proper dairy chain (cut with the cow decision; revisit if the economy wants it).
- Cats (every tavern needs one; Bessa's mouser is background for now).
- Bees and honey expansion (Maren's hives are flavor until D-07 recipes want wax).
- The Grey — never statted, never encountered. It stays a rumor.
- Breeding programs (selective pig breeding, laying lines) — a Phase 4+ player project.

---

## 6. Decisions made without the human

1. **Pigs, not cows or sheep.** Alder Farm already keeps 2 pigs (CHARACTERS.md); cows would add a dairy chain the economy doesn't model, and sheep add wool the item list doesn't need. Pigs fit the approved facts.
2. **Wild boar folded into the pig entry** as the untamable wild variant, so WORLD.md's boars are honored inside the 5-species cap.
3. **The original creature is the brambleback** — a mossy-quilled burrowing forager that eats livestock ticks and hoards shiny things. Chosen because it touches real systems (animal health, lost-item barter, children's play) instead of being decoration, and because it's the gentle on-ramp that teaches taming.
4. **Wolf taming needs council approval.** A wolf in the village is a public-safety decision; Bram enforces it, Elswith witnesses it. This makes the wolf a social achievement, not just a long grind.
5. **Trust is 0–100 with one meaningful gain per animal per day,** decaying slowly when neglected; cruelty or starvation zeroes it and locks re-bonding for a season. Matches the shared conventions' scales and keeps the tick cost bounded.
6. **Foxes exist only as predators and mentions** (they take brambleback young and chickens), not as a sixth species — the cap holds.
7. **Lida's speckled hen is the prototype's bonding tutorial**, not a separate mechanic — her goal ("teach the hen to come when called") is now expressed in taming rules.
8. **Art:** used verified animated Quaternius models (wolf, stag) for the wild species players will watch most; the brambleback is MISSING ART for D-12.
