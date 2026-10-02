# Living World — World, Setting and Tone (D-01)

## Summary

Living World is set in **Millbrook**, a frontier farming village of about 120 people in the **Eastmarch**, the eastern borderland of the small kingdom of **Wesmark**. The tone is a cozy life-sim first — bread, festivals, gossip, small kindnesses — with real danger at the edges: wolves in winter, a bad harvest, the deep forest nobody maps. Magic is rare, practical and costly; monsters are scarce but real. The player arrives reincarnated from our world with their memories intact — not a chosen hero, just a stranger the village must decide what to do with. The simulation needs nothing more than this village: every system in the prototype can be expressed through needs, money, items, places and time.

---

## 1. Tone

**Cozy first, dangerous at the edges.** The default feeling is warmth: the smell of the bakery at 06:00, children shouting by the river, the tavern full on Restday evening, neighbors who notice when you're missing. Danger is never the point of a day, but it is always nearby and everyone knows it: a wolf takes a lamb in winter, the mill wheel breaks at the worst moment, a storm flattens the barley two weeks before harvest.

The balance, roughly: 80% ordinary life, 20% trouble. Trouble should feel like weather — sometimes fair, sometimes cruel, never personal, never scripted to punish the player. Humor comes from people, not jokes: gossips, eccentrics, a guard who takes his job too seriously. Melancholy is allowed in small doses (an empty chair at the tavern, a farmhouse gone quiet), but the world is fundamentally hopeful — things can get better through work and kindness, because the simulation rewards both.

**What this means for design:** no grimdark, no cosmic horror, no chosen-one epic. The Apple Test should feel like a village incident, not a quest — a shopkeeper counting stock at dusk and frowning, not a dramatic cutscene.

## 2. The kingdom and the region

**The kingdom of Wesmark** is small — three days' ride east to west — ruled from the capital **King's Rest** by an aging king and his council of landholders. It is a quiet kingdom: no current wars, a standing army mostly for show, taxes collected twice a year by traveling reeves who are universally disliked and mostly honest.

**The Eastmarch** is the kingdom's eastern borderland, a day's travel of farms and forest between the last royal garrison and the wild lands beyond. It is known for three things: good apples, bad roads, and people who mind their own business. The capital is a week's travel west by cart (about 6 days at 8 hours of travel a day — far enough that news arrives stale and help arrives late, which is exactly why the village must handle its own problems).

What reaches Millbrook from the wider kingdom: tax collectors (twice a year), traveling merchants (every few weeks in the warm months), the occasional royal decree nailed to the tavern door, and rumors — always rumors — of whatever the capital is arguing about. Nothing else. The prototype never needs the capital on screen; it exists as a source of merchants, taxes, and gossip.

## 3. The village: Millbrook

- **Name:** Millbrook. **Age:** about 80 years, founded when a miller named Tomas Alder dammed the river and built the first watermill. His grandchildren still run it.
- **Size:** roughly 120 people in about 30 households. The prototype simulates ~20 of them in full detail — the people the Apple Test touches (reason: 20 is the most the first simulation can handle, and the apple chain only needs the shopkeeper, the farmer, a few customers, a guard and the tavern crowd).
- **Why it exists here:** the **River Alder** bends around a gravel ford, and the **Alder Road** — the old trade road east — crosses it. Carts must slow for the ford, so they stop; where carts stop, people sell things. The floodplain soil is rich, the river never fully dries, and the forest gives timber and game.
- **What it produces:** apples (the orchard on the hill is famous for twenty miles), wheat and barley flour, ale, timber, honey, eggs, pork. **What it must import:** iron tools and nails, salt, cloth and dyes, lamp oil, anything "fine" — all bought from traveling merchants at a markup.
- **Current problems:** the old mill wheel is cracking and a new one costs more than the village has saved; wolves have been bolder the last two winters; the young keep leaving for King's Rest; last year's barley was thin and the granary is half what it should be.
- **Hopes:** repair the mill before it fails, build a real bridge over the ford so winter trade doesn't stop, and — the quiet hope nobody says aloud — that the village grows enough that the children stay.

**Layout in one line (D-02 will map it):** the ford and mill at the river's bend, the market square and shops uphill from the water, farms spreading along the floodplain, the orchard on the south hill, forest hemming the east and north.

## 4. Magic

Magic is **rare, practical, and costly** — a craft, not a superpower.

- **How common:** perhaps one person in a few hundred can work real magic, and most of them do small, useful things. Millbrook has exactly one: the healer, who can knit a wound a little faster and brew fever-tea that actually works. Everyone knows someone who knows someone who saw something stranger, once.
- **Who can use it:** no academies, no chosen bloodlines — it shows up unpredictably, and most practitioners learn from one master in the old apprentice way. The village healer learned from her grandmother.
- **What it costs:** always something real — deep fatigue (a day's work of tiredness for an hour's working), rare materials (herbs that only grow in one hollow), and time (most workings take hours of preparation). Nobody throws magic around casually because nobody can afford to.
- **What it can't do:** create food or coin from nothing, raise the dead, compel a person's will, see the future clearly, or reach across distance. It nudges the world; it never rewrites it.

**For the prototype:** magic is background color. The healer exists, her remedies work slightly better than they should, and villagers half-believe it. No spells in the simulation yet — anything "magical" must be expressible as items, needs and knowledge, like everything else.

## 5. Monsters and danger

The wilds press against the village on two sides, and the village respects that.

- **Wolves:** the real, yearly threat. Packs come down from the high forest in winter when game is scarce — typically 2–4 incidents per winter (reason: enough to matter to the sheep farmers, few enough that the village isn't under siege). They take lambs, chickens, and — rarely, and everyone remembers when — a careless traveler. The village answers with a palisade along the east fields, a night watch rotation in deep winter, and two hunters who are paid in meat and respect.
- **Boars:** dangerous if cornered, mostly a problem for the unwary and the orchard fences.
- **The deep forest:** nobody maps past the second ridge. Hunters speak of "the Grey" — something large, quiet, glimpsed twice in living memory. It is deliberately left undefined: a rumor for later phases, never encountered in the prototype (reason: the prototype's danger budget is spent on wolves and weather; one mystery on the horizon is enough).
- **The other dangers are ordinary:** fire (every house has a water barrel by law), a bad harvest, fever in late winter, the river in flood. These kill more villagers than monsters ever have, and the simulation should treat them as the serious threats they are.

**Protection:** one full-time guard plus a night watch of volunteers in winter, the palisade, dogs everywhere, and the iron rule every child learns: *don't go past the treeline alone after dusk.*

## 6. Calendar

Built for clean simulation math (reason for every number below):

- **Day:** 1,440 game minutes, clock in HH:MM. Work starts around 06:00, sleep around 21:30 — a 15.5-hour active day, because pre-industrial people rise and sleep with the light.
- **Week:** 7 days, named **Firstday** through **Sixthday**, then **Restday** (reason: a named rest day gives every NPC a weekly schedule variation the simulation can implement, and "Restday" is exactly what tired villagers would call it).
- **Season:** 90 days. **Year:** 360 days, four seasons (reason: 90 divides evenly into crop-growth math and the farm delivery schedules D-04 will define).
- **Seasons:** Spring (sowing), Summer (growing), Autumn (harvest), Winter (the lean months). Winter is 90 days of cold because the survival pressure is the point — firewood, stored food and warm clothes are not flavor, they are needs.
- **Festivals (3):**
  1. **Thawday** — the first Restday of spring. The first furrow is plowed, seed-cakes are shared, and the year's luck is wished for. Everyone attends; the tavern keeper's busiest day.
  2. **Harvest Feast** — the last Restday of autumn. Tables in the square, the whole harvest's best on display, debts forgiven in small symbolic amounts, young people showing off.
  3. **Longnight** — midwinter's longest night. A bonfire in the square that must not go out until dawn; families take turns feeding it. Quiet, a little solemn, and the year's best storytelling.

## 7. How the player arrives

The player **wakes up on the riverbank below the old shrine, on a Thirdday morning in early autumn, in the body of a young traveler — with all their memories of our world intact.** No prophecy, no voice from the sky, no one expecting them. They have the clothes they woke in (strange, foreign-cut, worth questions), an empty stomach, and whatever skill from their old life came through clearest.

**Day 1:** the miller's youngest daughter finds them while fetching water and runs for her father — the first face the player sees is a frightened, curious girl of about ten. Within the hour, the guard has been told, and by midday most of the village knows "there's a stranger by the shrine." Reactions split the way real villages split: the tavern keeper sees a paying customer, the guard sees a problem to log, the children see an adventure, the cautious see a risk.

**What locals think of strangers:** Millbrook gets travelers on the Alder Road, so a stranger is not shocking — but a stranger with no cart, no pack, no story, and odd clothes is. The default stance is *polite wariness*: food and a roof can be earned, trust cannot. The player is an outsider until the simulation says otherwise — reputation starts at neutral-to-cool with every group, and every kindness or theft moves it from there. (This is why the Apple Test works socially: the stranger is already being watched a little.)

**Why reincarnation, not a portal:** it explains the skill choice without destiny. On arrival, the player is asked — by the world, in the quiet of character creation — *what were you good at, back then?* The answer becomes their starting skill (Cooking, Taming, or Swordsmanship in the prototype). No one in the village needs to know or believe the truth; whether the player tells anyone is itself a social decision with consequences.

## 8. Daily life

- **Food:** bread (rye and barley; wheat bread is for festivals), pottage, apples in autumn, cheese, eggs, pork and chicken, river fish, ale (small beer for children, proper ale for adults). A normal adult eats about three meals a day — this is where the hunger-need rate in D-03 comes from. Cooking matters: a good cook's stew is a small daily happiness that compounds.
- **Work:** dawn to dusk with a midday break, six days a week; Restday is for rest, worship, and the tavern. Children work from about age eight (chores, then apprenticeships). Nobody is idle in harvest season; winter is for mending, carving, weaving, and storytelling.
- **Beliefs:** the folk faith of **the Hearth** — not a church but a habit. Every home keeps a small shelf-shrine for its household spirits; travelers leave a crust at the roadside shrines; the old shrine by the river is tended by whoever feels like it. The Hearth asks for nothing but remembrance and small kindnesses, which makes it the perfect background belief: it shapes daily behavior (hospitality, funerals, festivals) without ever needing gods to intervene. A few villagers are quietly skeptical, and nobody minds.
- **Entertainment:** the tavern (songs, dice, news), wrestling and archery at festivals, storytelling on Longnight, courtship walks along the river in spring, gossip — always gossip, which is the rumor system wearing a human face.
- **Law:** Millbrook governs itself through a **village council**: the elder, the miller, and the guard. There are no written laws, only known customs — and everyone knows them.
  - **Theft:** the victim complains to the guard, who investigates *only with evidence* (a witness, the stolen goods found, a confession). Suspicion alone is not proof — this is load-bearing for the Apple Test: the shopkeeper may *believe* apples are missing while the guard *does nothing* until evidence reaches him.
  - **Punishment:** first offense means full restitution plus a public apology at the next Restday gathering (shame is the real penalty). Repeat or serious theft means the stocks for a day, a fine to the village, or banishment for the worst cases. Violence against a villager is answered in kind by the guard, immediately.
  - **Debts:** owed money is a civil matter; the council witnesses agreements, and a debtor who won't pay finds no one will trade with them — the simulation's reputation system does the enforcing.

## 9. Later: ideas for the wider world (kept out of the prototype)

- The capital, King's Rest — politics, guilds, and the price of everything Millbrook can't make.
- Two more villages on the Alder Road: one friendly rival, one struggling.
- The royal garrison a day west, and whatever the "eastern war" rumors turn out to be.
- The ruins in the deep forest and the truth about "the Grey."
- The sea, three weeks south — fish, salt, ships, and people who've seen stranger things than a reincarnated stranger.
- Other reincarnated people: is the player the only one? (A question for Phase 7+, not now.)

## 10. Decisions made (2026-10-02, with the human)

1. **The reincarnation stays secret.** Rebirth is not part of the Hearth faith — claiming it would get the player laughed at or feared. The default play is secrecy: whether the player tells anyone is itself a social risk with real consequences.
2. **The healer is one of the 20 simulated villagers.** She gets a full NPC profile in D-03 (remedies, hut, daily routine) — magic becomes visible through items and routines, never through spells in the simulation.
3. **Failure can be harsh.** In a truly bad winter, villagers can go genuinely hungry; stores can run out and people suffer. Hardship is real, but never scripted to punish the player.
4. **Seed the Grey.** Hunters occasionally mention "the Grey" in the deep forest in the tavern — a rumor thread for later phases, never encountered in the prototype.
5. **Old-world knowledge is usable but visible.** The player may freely use modern knowledge (crop rotation, basic medicine, literacy), but conspicuous displays draw attention: notoriety can attract thieves, kidnappers, or people who want to make the player work for them. Visibility has a cost, enforced through the reputation and rumor systems (designed in D-09).
