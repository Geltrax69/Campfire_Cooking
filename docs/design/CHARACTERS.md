# Living World — Characters: The 20 Villagers (D-03)

## Summary

Millbrook's prototype simulates 20 people in 11 households: the Alder farming family (4), the miller's household (2), the baker's household (3), the woodcutter's household (2), and nine single or paired adults — the apple-stall owner, tavern keeper, blacksmith, guard, store owner, healer, elder, fisherman, and a hunter who lodges with the apple seller. Ages run from 8 to 71, with four children, working adults, and three elders. Everyone has their own routines, money, goals, secrets, and opinions; nobody exists to hand the player a quest. The Apple Test touches them all differently: Mira counts her stock at dusk, Bram needs evidence before he acts, and the tavern will be talking about it by nightfall.

---

## 1. Roster

| ID | Name | Age | Gender | Occupation | Home | Workplace |
|---|---|---|---|---|---|---|
| npc_mira_holt | Mira Holt | 34 | female | Apple-stall owner | loc_home_mira | loc_apple_stall |
| npc_corvin_alder | Corvin Alder | 45 | male | Farmer | loc_farm | loc_farm |
| npc_maren_alder | Maren Alder | 42 | female | Farmer's wife (dairy, poultry, garden) | loc_farm | loc_farm |
| npc_piotr_alder | Piotr Alder | 16 | male | Farmhand (son) | loc_farm | loc_farm |
| npc_lida_alder | Lida Alder | 8 | female | Child (daughter) | loc_farm | — |
| npc_bessa_marlowe | Bessa Marlowe | 52 | female | Tavern keeper, brews her own ale | loc_tavern | loc_tavern |
| npc_doran_kettle | Doran Kettle | 48 | male | Blacksmith (widower) | loc_home_smith | loc_blacksmith |
| npc_bram_stone | Bram Stone | 39 | male | Village guard | loc_home_guard | loc_guard_post |
| npc_tilda_bray | Tilda Bray | 41 | female | General-store owner (widow) | loc_home_bray | loc_general_store |
| npc_oda_fenn | Oda Fenn | 44 | male | Baker | loc_home_fenn | loc_bakery |
| npc_sima_fenn | Sima Fenn | 40 | female | Baker's wife (helps bake) | loc_home_fenn | loc_bakery |
| npc_tom_fenn | Tom Fenn | 12 | male | Baker's son, errand-runner | loc_home_fenn | loc_bakery |
| npc_sella_wren | Sella Wren | 60 | female | Healer | loc_healer_hut | loc_healer_hut |
| npc_garrick_alder | Garrick Alder | 38 | male | Miller (widower) | loc_home_miller | loc_mill |
| npc_tansy_alder | Tansy Alder | 10 | female | Child (miller's daughter) | loc_home_miller | — |
| npc_elswith_alder | Elswith Alder | 71 | female | Village elder, holds the granary key | loc_home_elder | loc_square |
| npc_tam_oakes | Tam Oakes | 36 | male | Woodcutter | loc_home_woodcutter | loc_forest_edge |
| npc_brynn_oakes | Brynn Oakes | 34 | female | Woodcutter's wife (weaving, herbs) | loc_home_woodcutter | loc_home_woodcutter |
| npc_jory_reed | Jory Reed | 50 | male | Fisherman | loc_river_alder | loc_river_alder |
| npc_ralf_hale | Ralf Hale | 55 | male | Hunter (widower), lodges with Mira | loc_home_mira | loc_forest_edge |

Scales used below: traits, needs, trust/affection are 0–100 integers (50 = average). Money is integer copper. Times are HH:MM; 1 tick = 1 game minute. Need rates carry a reason the first time each pattern appears.

Note: `item_` ids used in possessions (e.g. `item_knife`, `item_axe`) are provisional — the canonical item list is defined in D-05 (ITEMS.md). The 12 MiniCharacters models serve 20 NPCs, so some models are reused (children reuse adult models at smaller scale) — see open question 5.

---

## 2. The villagers

### The apple stall — Mira Holt and her lodger

**npc_mira_holt — Mira Holt, 34, female, apple-stall owner.** Widowed three winters ago in the fever winter; runs the stall alone and keeps her takings box at home (reason the stall's coin box stays under 200 copper). Sharp-eyed, counts her display every dusk — the habit the Apple Test depends on.
- Appearance: chestnut hair in a practical braid, flour-dusted apron over a green dress, quick hands. Model: `Assets/Packs/Kenney/MiniCharacters/character-female-b.glb`.
- Traits: friendly 65, honest 85, greedy 35, brave 45, curious 60, cautious 70, lazy 15, generous 55, ambitious 60, gossipy 55.
- Need rates: hunger 6/hr (reason: an active adult awake ~16h reaches ~96 hunger/day, so three meals of ~30 each), energy 4/hr, social 3/hr.
- Money: 850 copper. Possessions: takings box at home (~600 in coin), a good knife (item_knife), 20 apples at the stall at game start (item_apple; stock carries over day to day — no reset).
- Schedule (workday): 06:00–06:30 eat (loc_home_mira); 06:30–07:00 walk, fetch water (loc_well); 07:00–12:00 tend stall (loc_apple_stall); 12:00–12:45 eat at the stall; 12:45–19:00 tend stall; 19:00–19:30 count stock, close up (loc_apple_stall); 19:30–20:30 supper, coin box home (loc_home_mira); 20:30–21:30 tavern or neighbors (loc_tavern); 21:30–06:00 sleep.
- Restday: 06:00–08:00 slow morning (loc_home_mira); 08:00–09:00 shrine visit (loc_shrine); 09:00–12:00 market gossip, buys for the week (loc_square); 12:00–21:30 much like a workday evening, longer at the tavern.
- Goals: short-term — save 2,000 copper to replace the stall's worn canvas and display (type save_money, target 2000). Long-term — buy a share in a second orchard row so she isn't wholly dependent on Corvin's deliveries (type acquire_asset).
- Relationships: Ralf Hale (trust 60, affection 55 — lodger of two years, pays in meat, steady company); Corvin Alder (trust 70, affection 50 — supplier; haggles hard but always delivers); Bessa Marlowe (trust 65, affection 70 — evening confidante); Tom Fenn (trust 40, affection 60 — suspects the boy takes windfalls, can't prove it, half-fond anyway).
- Knowledge: knows her regulars' buying habits (true); knows her evening count was exact yesterday (true); believes the orchard boys take windfalls (true, but no proof); does NOT know who would steal from a display in daylight.
- If she learned of the theft: counts twice, goes cold and quiet, tells Bessa that evening, complains to Bram the next morning — and watches every customer harder for a week.
- Voice: "Apples are three copper. No, not two — the orchard doesn't run on wishes." / "You look hungry, love. Take the bruised one, it's sweeter anyway."

**npc_ralf_hale — Ralf Hale, 55, male, hunter.** Widower; lodges with Mira two years now, pays in meat and mends things. One of the two hunters the village pays in meat and respect. Has glimpsed "the Grey" twice in the deep forest — tells it at the tavern when the ale is good (WORLD.md decision 4: seed the Grey).
- Appearance: grey-streaked beard, scarred left hand, quiet tread. Model: `Assets/Packs/Kenney/MiniCharacters/character-male-c.glb`.
- Traits: friendly 45, honest 80, greedy 20, brave 70, curious 55, cautious 75, lazy 25, generous 60, ambitious 30, gossipy 40.
- Need rates: hunger 6/hr, energy 4/hr, social 2/hr (reason: a solitary man; low social need means he seeks company rarely but values it).
- Money: 250 copper. Possessions: hunting bow (item_bow), skinning knife (item_knife), winter cloak.
- Schedule (workday): 05:30–06:00 eat (loc_home_mira); 06:00–11:00 hunt/trap lines (loc_forest_edge); 11:00–12:00 dress game, deliver meat (loc_farm or loc_tavern); 12:00–13:00 eat (loc_tavern); 13:00–17:00 mend gear, chop wood (loc_home_mira); 17:00–18:00 supper; 18:00–21:00 tavern most evenings (loc_tavern); 21:30–05:30 sleep.
- Restday: sleeps later, mends Mira's stall, longer tavern evening; sometimes walks to the shrine for his wife.
- Goals: short-term — salt and smoke enough meat to see Mira's household through winter (type stockpile, target 60). Long-term — train Piotr to hunt responsibly so the village still has a hunter when he's gone (type teach).
- Relationships: Mira Holt (trust 70, affection 65 — landlady, friend, the closest thing to family left); Piotr Alder (trust 55, affection 60 — the boy he is half-training); Bessa (trust 60, affection 55 — she never charges him full price); Bram Stone (trust 45, affection 40 — respects the office, finds the man stiff).
- Knowledge: knows the forest's trap lines and game trails (true); believes he saw the Grey twice (unknown — he half-doubts himself); knows Tom Fenn takes orchard windfalls (true — won't tell Mira; "boys are boys, and Corvin's trees drop plenty").
- If he learned of the theft: shrugs, says thieves are like foxes — you don't catch them by shouting — and keeps an eye on the stall when passing.
- Voice: "Forest gives, forest takes. Mostly it just watches." / "I saw something past the second ridge, once. Twice. Don't ask me what."

### Alder Farm — the Corvin Alder household

**npc_corvin_alder — Corvin Alder, 45, male, farmer.** Works Alder Farm with his family; the village's only farm (D-02 decision: single supplier, so shortages are visible). Grows wheat, barley, keeps the famous orchard, chickens and pigs. Delivers apples to Mira's stall. Wants to clear more fields; Tam Oakes wants the treeline left alone.
- Appearance: broad shoulders, sun-lined face, straw hat. Model: `Assets/Packs/Kenney/MiniCharacters/character-male-a.glb`.
- Traits: friendly 55, honest 75, greedy 45, brave 55, curious 40, cautious 60, lazy 20, generous 50, ambitious 65, gossipy 30.
- Need rates: hunger 7/hr (reason: heavy labor burns more; four meals or bigger portions), energy 5/hr (reason: a 16-hour farm day costs ~80 energy), social 2/hr.
- Money: 1,200 copper (farm household savings). Possessions: hoe, axe (item_axe), scythe, two pigs, twelve chickens, cart.
- Schedule (workday): 05:30–06:00 eat (loc_farm); 06:00–12:00 field/orchard work (loc_farm); 12:00–12:45 eat (loc_farm); 12:45–18:00 field work, twice a week apple delivery to the stall (loc_apple_stall); 18:00–19:00 animals, tools (loc_farm); 19:00–20:00 supper (loc_farm); 20:00–21:00 tavern twice a week, otherwise home; 21:30–05:30 sleep.
- Restday: 06:30–08:00 slow morning; 08:00–10:00 light chores; 10:00–12:00 square, council talk (loc_square); afternoon family; evening tavern.
- Goals: short-term — bring in the barley before the autumn rains (type harvest, target date-driven). Long-term — clear the east field for more wheat (type expand, blocked by the treeline custom — needs the council).
- Relationships: Maren Alder (trust 90, affection 85 — wife, partner in everything); Mira Holt (trust 70, affection 50 — biggest customer, haggles); Tam Oakes (trust 50, affection 45 — respects him, wants his trees); Piotr Alder (trust 60, affection 75 — son; doesn't know the boy plans to leave).
- Knowledge: knows his orchard's yield to the bushel (true); knows the granary is half-full (true — council talk); believes clearing the east field is safe (disputed — Tam disagrees).
- If he learned of the theft: angry on Mira's behalf — she's his customer — and offers to check his own stores; tells Piotr that thieves steal from everyone in the end.
- Voice: "Rain before the weekend, mark me. Barley won't wait." / "The orchard's been in Alder hands eighty years. It'll be here when we're dust."

**npc_maren_alder — Maren Alder, 42, female, farmer's wife.** Runs the dairy, poultry, and kitchen garden; sells eggs at the farmgate. Hears everything — egg customers talk.
- Appearance: strong arms, hair in a kerchief, flour on her sleeves by 07:00. Model: `Assets/Packs/Kenney/MiniCharacters/character-female-a.glb`.
- Traits: friendly 75, honest 80, greedy 30, brave 40, curious 70, cautious 65, lazy 15, generous 70, ambitious 45, gossipy 65.
- Need rates: hunger 6/hr, energy 4/hr, social 4/hr (reason: thrives on company; the farmgate trade is her social life).
- Money: 200 copper of her own (egg money). Possessions: egg baskets, cheese press, garden tools.
- Schedule (workday): 05:30–06:00 eat; 06:00–09:00 dairy, chickens, garden (loc_farm); 09:00–11:00 farmgate sales, gossip (loc_farm); 11:00–12:00 cook; 12:00–12:45 eat; 12:45–17:00 garden, preserving, mending; 17:00–19:00 supper prep, animals; 19:00–21:00 family evening; 21:30–05:30 sleep.
- Restday: church of the Hearth at home (shrine shelf), visits Sima Fenn, rests.
- Goals: short-term — put up 40 jars of preserves before winter (type stockpile, target 40). Long-term — see Piotr settled, whether here or King's Rest (type family; she suspects he wants to leave and hasn't told Corvin).
- Relationships: Corvin (trust 90, affection 88); Sima Fenn (trust 75, affection 75 — preserves-swapping friend); Maren→Piotr (trust 70, affection 90 — knows he dreams of the city); Bessa (trust 60, affection 60).
- Knowledge: knows who buys eggs and what they complain about (true); suspects Piotr plans to leave (true, unconfirmed); knows Tom Fenn pockets windfalls (true — finds it funny).
- If she learned of the theft: clucks, feeds Mira extra eggs "for the trouble," and tells the whole farmgate by noon.
- Voice: "Eggs are fresh this morning, and so is the news." / "Eat, love. The winter won't feed itself, and neither will I."

**npc_piotr_alder — Piotr Alder, 16, male, farmhand.** Eldest child; strong, restless. Secretly plans to leave for King's Rest when spring comes — hasn't told his father. Learning to hunt from Ralf.
- Appearance: tall for sixteen, Corvin's jaw, Maren's eyes. Model: `Assets/Packs/Kenney/MiniCharacters/character-male-b.glb`.
- Traits: friendly 60, honest 65, greedy 30, brave 65, curious 80, cautious 40, lazy 30, generous 55, ambitious 80, gossipy 35.
- Need rates: hunger 7/hr (reason: growing boy doing farm labor — eats four times a day), energy 5/hr, social 3/hr.
- Money: 80 copper (saved wages from his father). Possessions: a sling, Ralf's old skinning knife (lent, not given).
- Schedule (workday): 05:30–06:00 eat; 06:00–12:00 farm work with Corvin (loc_farm); 12:00–12:45 eat; 12:45–17:00 farm work; 17:00–18:30 twice a week hunting lessons with Ralf (loc_forest_edge); 18:30–19:30 supper; 19:30–21:00 square with Tom Fenn or home; 22:00–05:30 sleep (reason: teenagers keep later hours — 8h sleep from 22:00).
- Restday: 07:00–09:00 sleep in; chores; afternoons at the river or square; evenings tavern doorway (too young to drink, old enough to listen).
- Goals: short-term — save 500 copper of his own for the road (type save_money, target 500; secret from Corvin). Long-term — leave for King's Rest in spring (type leave; secret from Corvin, half-known to Maren).
- Relationships: Maren (trust 80, affection 90 — she knows); Ralf Hale (trust 70, affection 70 — mentor); Tom Fenn (trust 65, affection 70 — friend); Corvin (trust 60, affection 75 — loves him, dreads telling him).
- Knowledge: knows the farm's routines blind (true); believes King's Rest is full of opportunity (unknown — city rumors); does NOT know his mother has guessed his plan.
- If he learned of the theft: outraged on principle, then quietly thrilled — the village finally has a mystery.
- Voice: "One day I'm seeing the capital. You'll see." / "Ralf says a hunter reads the forest like a letter. I'm learning the alphabet."

**npc_lida_alder — Lida Alder, 8, female, child.** Youngest; feeds the chickens, talks to them, and insists they answer (a nod to the absurd skill "Talking to Chickens"). Fearless with animals, shy with strangers.
- Appearance: two braids, missing front tooth, chicken feathers in her pockets. Model: `Assets/Packs/Kenney/MiniCharacters/character-female-e.glb` (child — reuse at smaller scale).
- Traits: friendly 70, honest 90, greedy 20, brave 50, curious 95, cautious 30, lazy 40, generous 70, ambitious 30, gossipy 50.
- Need rates: hunger 7/hr (reason: small, growing, always moving), energy 3/hr (reason: children recover fast — 8h play costs ~24), social 4/hr.
- Money: 8 copper. Possessions: a wooden chicken (carved by Tam), a ribbon.
- Schedule (workday): 06:30–07:00 eat; 07:00–09:00 chicken feeding, chatter (loc_farm); 09:00–12:00 lessons with Maren / play; 12:00–12:30 eat; 12:30–17:00 play, errands, garden help; 17:00–18:00 supper; 18:00–20:00 family; 20:00–06:30 sleep (reason: children sleep ~10.5h).
- Restday: much the same, more play, sometimes the square with Tansy.
- Goals: short-term — teach the speckled hen to come when called (type bond; she believes it's working). Long-term — have a goat of her very own (type acquire_asset).
- Relationships: Maren (trust 85, affection 95); Tansy Alder (trust 70, affection 80 — best friend); the speckled hen (trust 90, affection 100 — one-sided, she believes).
- Knowledge: knows every chicken by name (true, to her); believes the speckled hen understands her (unknown — the hen does come for corn); knows Tom took windfalls (true — saw him, told no one; children keep children's secrets).
- If she learned of the theft: wide-eyed, asks if the thief was hungry, offers to share her apple.
- Voice: "Speckle says the fox was back. She saw it, I asked her." / "Can I keep him? I'll feed him. I'll feed him every day forever."

### The Hearthside — Bessa Marlowe

**npc_bessa_marlowe — Bessa Marlowe, 52, female, tavern keeper.** Brews her own ale (D-02 decision: needs grain from Corvin and well water — no extra NPC). The tavern is the village's evening room and rumor engine; Bessa hears everything and repeats the harmless half.
- Appearance: formidable, flour-and-ale smell, sleeves always rolled. Model: `Assets/Packs/Kenney/MiniCharacters/character-female-c.glb`.
- Traits: friendly 80, honest 70, greedy 50, brave 55, curious 75, cautious 55, lazy 20, generous 65, ambitious 55, gossipy 85 (reason: the tavern keeper is the village's switchboard — high gossipy is load-bearing for D-09).
- Need rates: hunger 6/hr, energy 4/hr, social 5/hr (reason: thrives in crowds; the tavern is her element).
- Money: 1,100 copper. Possessions: the tavern's strongbox (kept in her room), brewing kettle, her mother's ladle.
- Schedule (workday): 06:00–07:00 eat, bake day-bread (loc_tavern); 07:00–10:00 brewhouse, cleaning; 10:00–14:00 serve midday trade (loc_tavern); 14:00–16:00 rest, accounts; 16:00–23:00 the evening room — food, ale, news (loc_tavern); 23:00–06:00 sleep (reason: 7h sleep; she naps in the afternoon).
- Restday: tavern open all day; busiest day; she thrives.
- Goals: short-term — brew a winter ale worth remembering (type craft, needs extra grain). Long-term — make the Hearthside the kind of place travelers write about (type reputation).
- Relationships: Mira Holt (trust 65, affection 70 — evening confidante); Ralf Hale (trust 60, affection 55 — never charges full price); Corvin Alder (trust 70, affection 55 — grain supplier); Bram Stone (trust 50, affection 45 — keeps the peace, mostly).
- Knowledge: knows everyone's drink and most of their business (true); knows Ralf's Grey story by heart (true); believes the stranger by the shrine will be trouble or a blessing — fifty-fifty (unknown).
- If she learned of the theft: tells the tavern within the hour — "poor Mira" — and the story grows a detail with every telling.
- Voice: "Ale's fresh, stew's hot, and the news is free." / "I don't spread gossip, love. I just... arrange it where people can hear."

### The smithy — Doran Kettle

**npc_doran_kettle — Doran Kettle, 48, male, blacksmith.** Widower five years; the forge is his company. Forges and repairs tools; iron itself is imported, so every nail is precious. Gruff, fair, unexpectedly gentle with children.
- Appearance: soot in the creases, forearms like oak roots, kind eyes. Model: `Assets/Packs/Kenney/MiniCharacters/character-male-c.glb`.
- Final model map (females): Mira female-b, Maren female-a, Lida female-e (child scale), Bessa female-c, Tilda female-d, Sima female-a, Sella female-f, Tansy female-e (child scale), Elswith female-f, Brynn female-d.
- Final model map (males): Corvin male-a, Piotr male-b, Doran male-c, Bram male-d, Oda male-e, Garrick male-f, Tam male-a, Jory male-b, Ralf male-c, Tom male-d (child scale).
- Traits: friendly 50, honest 85, greedy 30, brave 60, curious 45, cautious 60, lazy 20, generous 60, ambitious 40, gossipy 25.
- Need rates: hunger 7/hr (reason: forge work), energy 5/hr, social 2/hr.
- Money: 900 copper. Possessions: forge tools, iron stock (~20 kg — heavy, hard to steal), his late wife's ring (individual item, never sold).
- Schedule (workday): 06:00–06:30 eat (loc_home_smith); 07:00–08:00 fire the forge (loc_blacksmith); 08:00–12:00 forge work; 12:00–12:45 eat; 12:45–18:00 forge work, repairs; 18:00–19:00 supper (loc_home_smith); 19:00–21:00 tavern three nights a week, otherwise quiet evening; 21:30–06:00 sleep.
- Restday: sleeps in, tends his garden, visits the shrine for his wife, mends things for neighbors free.
- Goals: short-term — re-shoe the miller's cart horse and repair the granary hinges before winter (type craft). Long-term — take an apprentice before his hands give out (type teach; eyeing Tom Fenn).
- Relationships: Tom Fenn (trust 55, affection 60 — potential apprentice); Bram Stone (trust 60, affection 50 — repairs his gear); Corvin Alder (trust 65, affection 55 — tools for the farm); Sella Wren (trust 55, affection 50 — she eased his wife's last days).
- Knowledge: knows every tool in the village by its wear (true); knows iron prices from the last merchant (true); believes his best work is behind him (unknown — Bessa disagrees loudly).
- If he learned of the theft: snorts, says a thief who steals apples is either desperate or stupid, and offers Mira a better lock for the stall's coin box.
- Voice: "Iron doesn't lie. People do, but iron doesn't." / "Bring it back when it breaks, not when it's scrap. There's a difference."

### The guard — Bram Stone

**npc_bram_stone — Bram Stone, 39, male, village guard.** The village's one full-time guard. Takes the job seriously — the humor comes from the solemnity, not malice. Investigates theft only with evidence (WORLD.md law — load-bearing for the Apple Test). Keeps a logbook no one else reads.
- Appearance: neat uniform-ish tunic, meticulously kept spear, earnest expression. Model: `Assets/Packs/Kenney/MiniCharacters/character-male-d.glb`.
- Traits: friendly 55, honest 95, greedy 15, brave 75, curious 50, cautious 80, lazy 10, generous 50, ambitious 55, gossipy 30.
- Need rates: hunger 6/hr, energy 4/hr, social 3/hr.
- Money: 450 copper (guard's pay, from the council). Possessions: spear, logbook, the evidence shelf (mostly lost buttons and one suspicious spoon).
- Schedule (workday): 06:00–06:30 eat (loc_home_guard); 06:30–08:00 morning rounds (loc_square, loc_well); 08:00–12:00 post duty, logbook (loc_guard_post); 12:00–12:45 eat; 12:45–17:00 rounds, errands, repairs with Doran; 17:00–18:00 supper; 18:00–20:00 evening rounds; 20:00–21:00 tavern (one ale, on duty in spirit); 21:30–06:00 sleep. Winter: night watch rotation with volunteers.
- Restday: lighter rounds, longer tavern stay, writes up the week's log.
- Goals: short-term — finally organize the evidence shelf (type order; never quite happens). Long-term — be the guard Millbrook deserves when real trouble comes (type duty).
- Relationships: Elswith Alder (trust 80, affection 60 — reports to the council); Doran Kettle (trust 60, affection 50); Bessa Marlowe (trust 50, affection 45 — she feeds him information with his stew); Mira Holt (trust 55, affection 50 — wants to help her, needs proof).
- Knowledge: knows the law is evidence-first (true); knows every habitual shortcut and hiding spot in the village (true); believes the stranger should be watched "as a matter of routine" (his logbook says so).
- If he learned of the theft: takes Mira's complaint with full solemnity, writes it down, asks who saw what — and does nothing further without a witness or the goods. Exactly as designed.
- Voice: "Suspicion is not evidence, mistress Holt. But I have written it down." / "The palisade's for wolves. For people, we have the council. And me."

### The general store — Tilda Bray

**npc_tilda_bray — Tilda Bray, 41, female, general-store owner.** Widow; her late husband died owing a traveling merchant, and she paid every copper back — proud, precise, wary of credit, though she quietly extends tabs to struggling families (secret ledger).
- Appearance: ink-stained fingers, ledger always to hand, mourning brooch. Model: `Assets/Packs/Kenney/MiniCharacters/character-female-d.glb`.
- Traits: friendly 60, honest 90, greedy 40, brave 45, curious 60, cautious 85, lazy 15, generous 55, ambitious 60, gossipy 45.
- Need rates: hunger 6/hr, energy 4/hr, social 3/hr.
- Money: 1,400 copper (imports are expensive; takings sit longer between merchant visits — reason her coin box is bigger than Mira's). Possessions: the secret tab ledger, good scales, a bolt of blue cloth she's saving for something.
- Schedule (workday): 06:30–07:00 eat (loc_home_bray); 07:00–08:00 open, sweep (loc_general_store); 08:00–12:00 serve (loc_general_store); 12:00–12:45 eat; 12:45–18:00 serve, accounts; 18:00–19:00 close, supper (loc_home_bray); 19:00–21:00 mending, reading; 21:30–06:30 sleep.
- Restday: opens 09:00–13:00 only; afternoon visits Elswith; keeps the store's books.
- Goals: short-term — be ready for the autumn merchant with a full order list (type trade). Long-term — pay off the last of the store's debts and own it free and clear (type save_money, target 3000).
- Relationships: Elswith Alder (trust 75, affection 65 — the elder witnessed her husband's debts); Mira Holt (trust 60, affection 55 — fellow shopkeeper, friendly rivalry); Sella Wren (trust 65, affection 60 — buys remedies); the tab families (trust varies — secret).
- Knowledge: knows who owes what (true — the secret ledger); knows import prices to the copper (true); believes credit ruins people (belief, from experience).
- If she learned of the theft: sympathizes with Mira, tightens her own watch, and quietly wonders — then dismisses it — whether one of her tab families was desperate enough.
- Voice: "Salt's up a copper. Don't look at me — look at the road it traveled." / "I don't do credit. ...Well. This once. Don't tell."

### The bakery — the Fenn household

**npc_oda_fenn — Oda Fenn, 44, male, baker.** Up at 04:30; the bakery smells of bread by 05:00 and sells out by early afternoon most days (one oven, morning bake only). Proud, short-tempered when tired, soft with his son.
- Appearance: flour-white eyebrows, massive forearms, a burn scar on one wrist. Model: `Assets/Packs/Kenney/MiniCharacters/character-male-e.glb`.
- Traits: friendly 55, honest 75, greedy 35, brave 45, curious 40, cautious 55, lazy 10, generous 60, ambitious 50, gossipy 40.
- Need rates: hunger 6/hr, energy 5/hr (reason: up at 04:30, hauling flour and heat — needs a midday nap), social 2/hr.
- Money: 700 copper. Possessions: the oven (his kingdom), peels and pans, a sack of wheat flour.
- Schedule (workday): 04:30–05:00 fire the oven (loc_bakery); 05:00–06:00 bake; 06:00–12:00 sell, bake second batch; 12:00–13:00 eat, nap (loc_home_fenn); 13:00–14:00 sell out, clean; 14:00–17:00 rest, garden; 17:00–18:00 supper; 18:00–20:00 family; 20:30–04:30 sleep (reason: 8h sleep shifted early — the baker's day starts before dawn).
- Restday: bakes festival bread only; slower day; afternoon nap is sacred.
- Goals: short-term — get through harvest season without the oven cracking (type maintain). Long-term — build a second oven so the village never runs short (type build, target 5000).
- Relationships: Sima Fenn (trust 85, affection 85 — wife, baking partner); Garrick Alder (trust 70, affection 60 — flour supplier); Maren Alder (trust 75, affection 70 — preserves for bread, the eternal trade).
- Knowledge: knows the mill's flour by the handful (true); knows Tom sneaks bread to Tansy sometimes (true — pretends not to); believes a baker should never be woken after noon (strongly held).
- If he learned of the theft: "Apples! At least they didn't take bread," then sends Tom with a loaf for Mira.
- Voice: "Bread's done when it's done. Not before." / "You want it cheaper? The oven doesn't care what you want."

**npc_sima_fenn — Sima Fenn, 40, female, baker's wife.** Bakes alongside Oda; the gentle one customers actually talk to. Trades preserves with Maren.
- Appearance: flour-dusted dark hair, warm smile, quick with a bun for a child. Model: `Assets/Packs/Kenney/MiniCharacters/character-female-a.glb`.
- Traits: friendly 80, honest 80, greedy 25, brave 40, curious 60, cautious 60, lazy 20, generous 75, ambitious 40, gossipy 60.
- Need rates: hunger 6/hr, energy 4/hr, social 4/hr.
- Money: 150 copper (her own). Possessions: her mother's recipes (written — she can read a little), a copper kettle.
- Schedule (workday): 05:00–06:00 help bake (loc_bakery); 06:00–12:00 serve, shape loaves; 12:00–13:00 eat, rest (loc_home_fenn); 13:00–16:00 preserving, mending; 16:00–18:00 supper prep; 18:00–21:00 family, neighbors; 21:00–05:00 sleep.
- Restday: like Oda's, plus visits Maren.
- Goals: short-term — teach Tom to shape a proper loaf (type teach). Long-term — see Tom apprenticed well, whether to Oda or Doran (type family).
- Relationships: Oda (trust 85, affection 88); Maren Alder (trust 75, affection 75); Tom (trust 80, affection 95 — knows he takes windfalls, covers for him).
- Knowledge: knows Tom's windfall habit (true — secret); knows the bakery's accounts (true); believes Tom will make a fine baker if the wanderlust doesn't take him (hope).
- If she learned of the theft: worried for Mira, extra bread for the stall-holder, and a sharp look at Tom that says "not you, I hope."
- Voice: "Take the warm one, love. Oda pretends not to notice." / "A village is just people who feed each other. Remember that."

**npc_tom_fenn — Tom Fenn, 12, male, baker's son.** Errand-runner, windfall thief (low-stakes, per LOCATIONS.md), Piotr's friend. Doran has an eye on him as an apprentice. Good-hearted, fast, constitutionally unable to pass an orchard without looking up.
- Appearance: lanky, flour in his hair, pockets full of interesting stones. Model: `Assets/Packs/Kenney/MiniCharacters/character-male-d.glb` (child — reuse at smaller scale).
- Traits: friendly 70, honest 60, greedy 30, brave 60, curious 90, cautious 35, lazy 45, generous 60, ambitious 40, gossipy 55.
- Need rates: hunger 7/hr (reason: growing, always running), energy 3/hr, social 4/hr.
- Money: 30 copper. Possessions: a slingshot (never used on anything living — his mother's rule), interesting stones.
- Schedule (workday): 06:00–06:30 eat; 06:30–08:00 help at bakery (loc_bakery); 08:00–12:00 errands, lessons with Sima; 12:00–12:30 eat; 12:30–17:00 errands, play, orchard vicinity (loc_farm — windfalls); 17:00–18:00 supper (loc_home_fenn); 18:00–20:00 square with Piotr or Tansy; 20:30–06:00 sleep.
- Restday: errands done early, then freedom.
- Goals: short-term — be allowed to work the forge with Doran one full day (type learn). Long-term — decide: baker like father or smith like Doran (type choose; genuinely undecided).
- Relationships: Piotr Alder (trust 65, affection 70 — hero-worships him slightly); Tansy Alder (trust 60, affection 65 — friend); Sima (trust 80, affection 90 — she covers for him); Doran Kettle (trust 55, affection 60 — wants to impress him).
- Knowledge: knows he took windfalls twice (true — secret, though Sima, Lida, and Ralf know); knows the orchard's best climbing tree (true); believes the Grey is "hunters' talk" (skeptical, curious anyway).
- If he learned of the theft: thrilled and terrified in equal measure — a REAL thief — and very careful not to look guilty.
- Voice: "I didn't take anything! ...This time." / "Piotr says the capital has buildings taller than the mill. Taller!"

### The healer — Sella Wren

**npc_sella_wren — Sella Wren, 60, female, healer.** The village's one magic-worker (WORLD.md: rare, practical, costly — expressed as remedies and routines, never spells in the simulation). Learned from her grandmother. Fever-tea that actually works; wounds knit a little faster in her care. Couldn't save Garrick's wife three winters ago — he blames her quietly, she blames herself loudly (to herself).
- Appearance: silver hair in a knot, herb-stained fingers, eyes that notice everything. Model: `Assets/Packs/Kenney/MiniCharacters/character-female-f.glb`.
- Traits: friendly 70, honest 90, greedy 15, brave 60, curious 80, cautious 70, lazy 20, generous 85, ambitious 30, gossipy 45.
- Need rates: hunger 5/hr (reason: older, smaller appetite), energy 3/hr (reason: 60 years old — tires faster, naps), social 3/hr.
- Money: 400 copper (takes coin, eggs, or favors — reason: the healer takes what people can give). Possessions: dried herbs, prepared remedies, her grandmother's mortar.
- Schedule (workday): 06:30–07:00 eat (loc_healer_hut); 07:00–08:00 herb garden (loc_healer_hut); 08:00–12:00 patients (loc_healer_hut); 12:00–13:00 eat, nap; 13:00–17:00 patients, remedy-making, forest-edge gathering twice a week with Brynn (loc_forest_edge); 17:00–18:00 supper; 18:00–20:00 visits to the sick; 20:30–06:30 sleep. Emergencies: always.
- Restday: shrine visit, rest, remedies for the week.
- Goals: short-term — dry enough fever-tea for winter (type stockpile, target 30 bundles; needs Brynn's help). Long-term — pass the knowledge to someone before she forgets it herself (type teach; no apprentice yet — an open thread).
- Relationships: Brynn Oakes (trust 75, affection 70 — gathering partner, knows the hollow); Elswith Alder (trust 70, affection 65); Garrick Alder (trust 40, affection 55 — the wound between them); Doran Kettle (trust 55, affection 50).
- Knowledge: knows the fever-tea recipe and the hollow's location (true — shared only with Brynn); knows Garrick blames her (true); believes her grandmother's remedies work "a little better than they should" (true — the magic, background color).
- If she learned of the theft: brings Mira willowbark tea "for the nerves" and listens — healing by listening is half her craft.
- Voice: "Drink it all. Yes, it's bitter. Bitter is how you know it's working." / "The forest provides, child. We just have to ask properly."

### The mill — Garrick and Tansy Alder

**npc_garrick_alder — Garrick Alder, 38, male, miller.** Grandson of founder Tomas Alder; runs the watermill with its cracking wheel (WORLD.md's current problem — everyone knows the sound it makes). Widower three winters (fever winter); quietly blames Sella. His youngest daughter Tansy found the player on day 1.
- Appearance: dust-pale hair, strong back, tired eyes that smile anyway. Model: `Assets/Packs/Kenney/MiniCharacters/character-male-f.glb`.
- Traits: friendly 60, honest 80, greedy 30, brave 50, curious 45, cautious 65, lazy 25, generous 65, ambitious 50, gossipy 35.
- Need rates: hunger 6/hr, energy 5/hr (reason: mill work is heavy), social 3/hr.
- Money: 600 copper. Possessions: the mill (his inheritance and burden), miller's toll grain, his wife's shawl (kept, individual item).
- Schedule (workday): 05:30–06:00 eat (loc_home_miller); 06:00–12:00 mill work (loc_mill); 12:00–12:45 eat; 12:45–18:00 mill work; 18:00–19:00 supper with Tansy (loc_home_miller); 19:00–20:30 the wheel — listening, patching, worrying (loc_mill); 20:30–21:30 home; 21:30–05:30 sleep.
- Restday: morning with Tansy, afternoon the wheel, evening tavern (one ale, quiet corner).
- Goals: short-term — nurse the wheel through winter (type maintain; everyone knows it's failing). Long-term — save for a new wheel the village can't yet afford (type save_money, target 8000 — the village's shared hope).
- Relationships: Tansy Alder (trust 85, affection 95 — his world); Corvin Alder (trust 75, affection 70 — cousin, grain); Oda Fenn (trust 70, affection 60 — flour); Sella Wren (trust 40, affection 55 — the wound); Elswith Alder (trust 75, affection 70 — aunt, council).
- Knowledge: knows the wheel's every groan (true); knows the granary is half-full (true — council); believes Sella could have done more (belief — unfair, and half of him knows it).
- If he learned of the theft: shakes his head, tells Tansy that taking what isn't yours hollows you out, and offers Mira free grinding for a month.
- Voice: "The wheel's older than me and more stubborn. We'll see which of us outlasts the other." / "Your mother would've known what to say, Tansy. I just know about flour."

**npc_tansy_alder — Tansy Alder, 10, female, miller's daughter.** Finds the player on the riverbank on day 1 — the first face the player sees. Curious, brave for ten, still grieving her mother in the way children do (in bursts, between adventures).
- Appearance: Garrick's dust-pale hair in one thick plait, scraped knees, fearless grin. Model: `Assets/Packs/Kenney/MiniCharacters/character-female-e.glb` (child — reuse at smaller scale).
- Traits: friendly 75, honest 85, greedy 20, brave 65, curious 95, cautious 40, lazy 35, generous 70, ambitious 35, gossipy 50.
- Need rates: hunger 7/hr, energy 3/hr, social 4/hr (reasons: as Lida — children eat often, recover fast, need company).
- Money: 12 copper. Possessions: a river-stone collection, her mother's ribbon (worn on special days).
- Schedule (workday): 06:30–07:00 eat; 07:00–09:00 chores, water run with the miller's daughter's route (loc_well, loc_shrine, loc_river_alder); 09:00–12:00 lessons with Elswith twice a week, otherwise play; 12:00–12:30 eat; 12:30–17:00 play — river, square, Lida (loc_river_alder, loc_square); 17:00–18:00 supper (loc_home_miller); 18:00–20:00 with Garrick; 20:00–06:30 sleep.
- Restday: like a workday with more river and less lessons.
- Goals: short-term — find the perfect stone for her mother's shrine shelf (type sentimental). Long-term — learn to read properly from Elswith (type learn).
- Relationships: Garrick (trust 90, affection 95); Lida Alder (trust 70, affection 80 — best friend); Elswith Alder (trust 70, affection 75 — teacher); Tom Fenn (trust 60, affection 65).
- Knowledge: knows the riverbank paths blind (true); knows her father cries sometimes when he thinks she's asleep (true — secret, never told); believes the shrine keeps travelers safe (belief — she'll tell the player so on day 1).
- If she learned of the theft: asks Garrick a hundred questions about why, then decides the thief must have been very hungry and leaves an apple on the shrine "just in case."
- Voice: "Are you the stranger? I'm Tansy. Papa says you're to come for supper." / "The shrine keeps people safe. Mostly. I think. It kept you, didn't it?"

### The elder — Elswith Alder

**npc_elswith_alder — Elswith Alder, 71, female, village elder.** Holds the granary key; heads the village council with the miller and the guard. Remembers the village at half its size. The half-full granary keeps her up at night; she quietly rations without alarming anyone.
- Appearance: white hair in a severe bun, straight spine, hands that have done everything. Model: `Assets/Packs/Kenney/MiniCharacters/character-female-f.glb`.
- Traits: friendly 65, honest 95, greedy 10, brave 70, curious 60, cautious 85, lazy 15, generous 70, ambitious 30, gossipy 40.
- Need rates: hunger 5/hr, energy 2/hr (reason: 71 — short days, long rests; ~10h active), social 3/hr.
- Money: 500 copper. Possessions: the granary key (individual item — the village's trust made physical), letters from her son in King's Rest.
- Schedule (workday): 07:00–07:30 eat (loc_home_elder); 07:30–09:00 slow morning, shrine shelf (loc_home_elder); 09:00–11:00 council business, granary check twice a week (loc_square, loc_granary); 11:00–12:00 rest; 12:00–12:30 eat; 12:30–15:00 visits, teaches Tansy twice a week; 15:00–17:00 rest; 17:00–18:00 supper; 18:00–20:00 quiet evening, letters; 20:30–07:00 sleep.
- Restday: leads the Restday gathering; public apologies happen here (WORLD.md law).
- Goals: short-term — stretch the granary to spring without panic (type manage; secret — the village must not worry). Long-term — see the mill wheel replaced before she dies (type legacy).
- Relationships: Garrick Alder (trust 75, affection 70 — nephew, council); Bram Stone (trust 80, affection 60 — her guard); Tilda Bray (trust 75, affection 65); Sella Wren (trust 70, affection 65).
- Knowledge: knows the granary is half-full (true — secret from most); knows every family's standing (true); believes the village will survive winter "if we're careful and kind" (belief — she works to make it true).
- If she learned of the theft: sighs, hopes it's hunger not malice, and reminds Bram — gently, firmly — that suspicion is not evidence.
- Voice: "I've buried two husbands and a bad winter. The village is still here." / "Kindness is a strategy, child. The best one we have."

### The forest edge — the Oakes household

**npc_tam_oakes — Tam Oakes, 36, male, woodcutter.** Lives where the work is, at the forest edge. Cuts ~1 log/day sustainably (LOCATIONS.md: one woodcutter is sustainable, two are not — real tension). Opposes Corvin's east-field clearing. Carved Lida's wooden chicken.
- Appearance: tall, quiet, sawdust in his beard, gentle with trees and children. Model: `Assets/Packs/Kenney/MiniCharacters/character-male-a.glb`.
- Traits: friendly 55, honest 85, greedy 20, brave 60, curious 50, cautious 75, lazy 25, generous 65, ambitious 35, gossipy 30.
- Need rates: hunger 7/hr (reason: axe work), energy 5/hr, social 2/hr.
- Money: 350 copper. Possessions: felling axe (item_axe), wedges, the log pile by his cottage.
- Schedule (workday): 06:00–06:30 eat (loc_home_woodcutter); 06:30–07:00 walk to the cut (loc_forest_edge); 07:00–12:00 felling, limbing; 12:00–12:45 eat in the forest; 12:45–16:00 hauling, splitting; 16:00–17:00 stack, tools; 17:00–18:00 supper (loc_home_woodcutter); 18:00–20:00 carving, family; 21:00–06:00 sleep.
- Restday: no felling (his rule — the forest rests too); mends, carves, visits the square briefly.
- Goals: short-term — lay in his own winter wood before the first snow (type stockpile, target 180 bundles). Long-term — keep the treeline where it is (type protect; opposes Corvin's clearing).
- Relationships: Brynn Oakes (trust 90, affection 90 — wife); Corvin Alder (trust 50, affection 45 — the disagreement); Ralf Hale (trust 65, affection 60 — forest colleagues); Lida Alder (trust 60, affection 70 — carved her chicken).
- Knowledge: knows every workable tree in his cut (true); knows the herb hollow's vicinity but not its heart (true — Brynn's secret to keep); believes the forest notices how it's treated (belief — he acts like it's true).
- If he learned of the theft: "Apples aren't timber — they grow back by autumn," and offers Mira firewood at cost because "trouble's trouble."
- Voice: "Take one tree, plant your thanks. Take two, plant an apology." / "The forest and I have an arrangement. I keep it, mostly."

**npc_brynn_oakes — Brynn Oakes, 34, female, woodcutter's wife.** Weaves, keeps the cottage garden, and gathers herbs with Sella — she knows the healer's hollow locations (secret knowledge, shared only with Sella). Practical, observant, dry-humored.
- Appearance: auburn hair, deft hands, a basket never far away. Model: `Assets/Packs/Kenney/MiniCharacters/character-female-d.glb`.
- Traits: friendly 65, honest 80, greedy 25, brave 50, curious 70, cautious 70, lazy 20, generous 65, ambitious 45, gossipy 50.
- Need rates: hunger 6/hr, energy 4/hr, social 3/hr.
- Money: 120 copper. Possessions: loom, herb baskets, her grandmother's shawl.
- Schedule (workday): 06:00–06:30 eat; 06:30–09:00 garden, cottage (loc_home_woodcutter); 09:00–12:00 weaving, twice a week herb gathering with Sella (loc_forest_edge); 12:00–12:45 eat; 12:45–16:00 weaving, preserving; 16:00–18:00 supper prep; 18:00–20:30 family; 21:00–06:00 sleep.
- Restday: visits the square, trades cloth for goods.
- Goals: short-term — weave three lengths of winter cloth for trade (type craft, target 3). Long-term — learn all of Sella's herb lore properly (type learn).
- Relationships: Tam (trust 90, affection 92); Sella Wren (trust 75, affection 70 — the hollow secret); Maren Alder (trust 60, affection 60 — garden talk).
- Knowledge: knows the healer's hollow locations (true — secret, shared only with Sella); knows Tam worries about the east field (true); believes Sella's remedies work better than they should (true — she's seen it).
- If she learned of the theft: practical sympathy — takes Mira a basket of late berries "for the display, to fill the gap."
- Voice: "The hollow's where it is. That's all anyone needs to know — including you, love." / "Tam talks to trees. I talk to plants. Somebody has to listen in this family."

### The river — Jory Reed

**npc_jory_reed — Jory Reed, 50, male, fisherman.** Lives in the fisherman's hut by the river (loc_river_alder). Knows the Alder better than anyone; fishes ~3/day sustainably against a stock of ~60. Laconic, superstitious about the river, kind in a gruff way.
- Appearance: weathered face, tarred coat, hands like rope. Model: `Assets/Packs/Kenney/MiniCharacters/character-male-b.glb`.
- Traits: friendly 45, honest 80, greedy 25, brave 60, curious 45, cautious 70, lazy 35, generous 55, ambitious 30, gossipy 35.
- Need rates: hunger 6/hr, energy 4/hr, social 2/hr (reason: a solitary trade — the river is his company).
- Money: 300 copper. Possessions: nets, fish traps, a coracle, his father's knife.
- Schedule (workday): 05:30–06:00 eat (loc_river_alder); 06:00–10:00 check traps, fish (loc_river_alder); 10:00–11:00 sell/gut at the square twice a week (loc_square); 11:00–12:00 mend nets; 12:00–12:30 eat; 12:30–16:00 fish, traps; 16:00–17:30 smoke/dry the catch; 17:30–18:30 supper; 18:30–20:00 tavern twice a week, otherwise the riverbank; 21:00–05:30 sleep.
- Restday: mends everything, visits the tavern, feeds the ducks (he denies this).
- Goals: short-term — smoke 50 fish for winter (type stockpile, target 50). Long-term — build a proper smokehouse before his back gives out (type build, target 1500).
- Relationships: Bessa Marlowe (trust 60, affection 55 — buys his smoked fish); Corvin Alder (trust 55, affection 50); Ralf Hale (trust 55, affection 55 — fellow solitary); Tansy Alder (trust 50, affection 60 — she visits the river; he pretends to mind).
- Knowledge: knows the river's moods and the ford's depth by the stone (true); knows fish stocks are steady "if you don't get greedy" (true); believes the river takes a toll when disrespected (superstition — he leaves a crust at the shrine all the same).
- If he learned of the theft: "Hungry thieves I understand. It's the other kind you watch," and keeps his traps closer for a while.
- Voice: "River gives. River takes. Mostly it just flows." / "You want to know the ford? Ask the stones. They've been here longer than all of us."

---

## 3. Relationship web

Key bonds (trust/affection ≥ 70). Read `A → B` as A's bond toward B.

```
FARM HOUSEHOLD (loc_farm)
  Corvin ←→ Maren (90/85, 90/88) — the partnership the farm runs on
  Corvin → Piotr (60/75)   Maren → Piotr (70/90) — she has guessed his plan; he hasn't told
  Piotr → Maren (80/90) — tells her things he won't tell his father
  Maren → Sima (75/75) — preserves-for-bread friendship
  Corvin ⇄ Tam (50/45) — the east-field disagreement, respectful but real
  Corvin → Mira (70/50) — supplier and biggest customer, haggles hard

MILLER'S HOUSEHOLD (loc_home_miller) + ELDER
  Garrick → Tansy (85/95) — his world since the fever winter
  Garrick ⇄ Corvin (75/70) — cousins, grain
  Garrick → Sella (40/55) — the wound: blames her, half-knows it's unfair
  Elswith → Garrick (75/70) — nephew, fellow councillor
  Tansy → Elswith (70/75) — teacher; Tansy is learning to read
  Tansy ⇄ Lida (70/80) — best friends, river partners

BAKER'S HOUSEHOLD (loc_home_fenn)
  Oda ⇄ Sima (85/85) — baking partners, twenty years
  Sima → Tom (80/95) — covers for his windfalls
  Tom → Piotr (65/70) — hero-worship; Tom → Tansy (60/65) — friend
  Doran → Tom (55/60) — potential apprentice, watching him
  Oda → Garrick (70/60) — flour; Sima ⇄ Maren (75/75) — preserves for bread

MIRA'S HOUSEHOLD (loc_home_mira)
  Mira ⇄ Ralf (60/55, 70/65) — landlady and lodger, two years; closest thing to family
  Mira → Bessa (65/70) — evening confidante
  Ralf → Piotr (55/60) — half-training him to hunt
  Ralf → Bessa (60/55) — never charged full price

WOODCUTTER'S HOUSEHOLD (loc_home_woodcutter)
  Tam ⇄ Brynn (90/90, 90/92) — the steadiest marriage in the village
  Brynn → Sella (75/70) — the hollow secret
  Tam → Ralf (65/60) — forest colleagues
  Tam → Lida (60/70) — carved her wooden chicken

SINGLES & THEIR WEBS
  Bessa (loc_tavern) → Mira (65/70), Ralf (60/55), Corvin (70/55 — grain for ale), Bram (50/45)
  Bram (loc_guard_post) → Elswith (80/60 — reports to the council), Doran (60/50), Bessa (50/45), Mira (55/50)
  Tilda (loc_general_store) → Elswith (75/65 — witnessed her husband's debts), Mira (60/55 — friendly rivalry), Sella (65/60)
  Sella (loc_healer_hut) → Brynn (75/70), Elswith (70/65), Doran (55/50 — eased his wife's last days)
  Doran (loc_blacksmith) → Corvin (65/55 — farm tools), Bram (60/50 — gear repairs)
  Jory (loc_river_alder) → Bessa (60/55 — smoked fish), Ralf (55/55 — fellow solitary), Tansy (50/60 — pretends to mind her visits)
```

Tensions (the friction that keeps the village alive): Garrick ⇄ Sella (unspoken blame); Corvin ⇄ Tam (the east field); Piotr's secret plan vs. Corvin's need; Mira suspects Tom (windfalls) but is half-fond; Tilda's secret ledger; Elswith's secret rationing; Ralf's doubt about what he saw.

---

## 4. Who knows what

Truth vs. belief — what each person knows that others don't. (`true` = accurate, `unknown` = unverified, `false` = wrong.)

| Knower | Claim | Status | Who else knows |
|---|---|---|---|
| Elswith | The granary is half-full; she is quietly rationing | true | Garrick, Bram (council only) |
| Sella + Brynn | The healer's hollow locations; the fever-tea recipe | true | No one else |
| Ralf | He glimpsed "the Grey" twice past the second ridge | unknown | Tavern regulars (as a story) |
| Ralf | Tom Fenn takes orchard windfalls | true | Sima, Maren, Lida (children's grapevine) |
| Tom | He took windfalls twice | true | Sima, Maren, Lida, Ralf (see above) |
| Piotr | He plans to leave for King's Rest in spring; saving 500 copper | true | Maren (guessed, unconfirmed) |
| Maren | She has guessed Piotr's plan | true | No one (hasn't told Corvin) |
| Garrick | Believes Sella could have done more for his wife | belief (unfair) | Sella (knows he blames her) |
| Mira | Her evening counts; regulars' habits; the stall's coin box stays small | true | — |
| Mira | Suspects the orchard boys take windfalls | true (no proof) | — |
| Tilda | The secret tab ledger — who owes what | true | The debtors (each their own) |
| Tilda | Her husband died in debt; she paid it all back | true | Elswith |
| Bessa | Everyone's drink and most of their business | true (mostly) | — |
| Jory | The ford's depth by the stone; fish stocks steady "if you don't get greedy" | true | — |
| Corvin | His orchard's yield to the bushel | true | — |
| Doran | Every tool in the village by its wear; iron prices | true | — |
| Bram | The law is evidence-first; every hiding spot in the village | true | — |
| Lida | Believes the speckled hen understands her | unknown | The hen (unconfirmed) |
| Tansy | Her father cries when he thinks she's asleep | true | No one (never told) |
| Oda | Tom sneaks bread to Tansy sometimes | true | — (pretends not to see) |
| Elswith | Believes the village survives winter "if we're careful and kind" | belief | — (she works to make it true) |

Nobody knows: who will steal from the apple stall (the Apple Test's premise); whether the Grey is real; whether Piotr will actually leave; whether the wheel lasts the winter.

---

## 5. Later (kept out of the prototype)

- The other ~100 villagers exist as background only — names and faces for later phases, not simulated.
- Sella's missing apprentice: a thread for Phase 3+ (who learns the herb lore?).
- Piotr's departure (or decision to stay) — a Phase 7 generations beat, not scripted now.
- Doran's search for an apprentice; Tom's choice between oven and forge.
- Elswith's son in King's Rest — a letter, a visit, a connection to the wider world later.
- Other reincarnated people (WORLD.md) — none in the prototype.
- The deep forest and the truth about the Grey — rumor only, per WORLD.md decision 4.

---

## 6. Decisions made (2026-10-02, with the human)

1. **Ralf's Grey story is a starting belief**, not a scripted event — it spreads only through the normal rumor rules designed in D-09.
2. **Corvin can discover Piotr's savings**, but only through normal perception rules (the hiding place, attention, chance) — no special trigger.
3. **Tilda's tabs are real debts.** D-04 (economy) must model them: who owes what, and what happens when debts go unpaid.
4. **Brynn's apprenticeship stays open** — it should emerge from the simulation, not be decided now.
5. **12 models for 20 villagers is fine for the prototype.** Added to the art list: "villager visual variety (colors, hats, props)" for later.
6. **Name clash fixed:** Garrick's daughter is now **Tansy Alder** (`npc_tansy_alder`); the healer stays Sella Wren.
7. **Tom's windfall stealing stays.** It lets Mira suspect the wrong person — exactly the truth-vs-knowledge behavior D-09 must support.
