# Living World — Knowledge, Rumors and Reputation (D-09)

## Summary

Millbrook runs on two ledgers: what happened (world truth, owned by the simulation) and what each villager *believes* happened (knowledge, owned by each NPC). This document designs the three systems that connect them: **perception** (who notices an event, with a notice-chance formula built from distance, light, attention and traits), **memory** (five importance levels, from 3-day trivia to permanent wounds), and **rumors** (how stories travel through the tavern, the well and the square, distorting with every retelling and filtered by trust). It also defines the **reputation groups** the player meets, the **notoriety** mechanic that makes conspicuous old-world knowledge dangerous, and the **evidence-first law** that lets a shopkeeper *know* apples are missing while the guard *does nothing*. The Apple Test's social half — suspicion without proof, the wrong suspect, a traceable rumor chain — emerges from these rules, never from a script.

---

## 1. Perception: who notices what

Every world event (a theft, a purchase, a fight, a wolf at the treeline) has a **visibility**: how loud, visible and long it is. Every NPC nearby gets a **notice chance**, rolled on the simulation's seeded RNG. An NPC may only know something if they perceived it or were told it — this is the rule the whole Apple Test rests on.

### The notice-chance formula

```
chance = clamp( distanceBand × light × attention × loudness
                + (cautious − 50) × 0.2
                + (curious − 50) × 0.1   [novel events only],
                0, 95 )
```

All values are 0–100 integers (AGENTS.md conventions). Every number below has a reason.

**Distance bands** (the village core is ~130 m across, so nothing is truly far):
- Same spot (0–5 m): 90 — you're standing next to it.
- Nearby (5–30 m): 60 — across the square, at the next stall.
- Same open area (30–130 m): 25 — the far side of the square.
- Far (>130 m): 5 — the farm from the square; only the loudest events.

**Light** (the theft at 14:00 is full daylight):
- Daylight: 1.0. Dusk/dawn: 0.7. Night, moonlit: 0.35. Dark indoors: 0.2.
- Reason: perception is mostly sight; the iron rule "not past the treeline after dusk" exists because night halves what anyone can see.

**Attention** (what the NPC is doing):
- Watching/idle/socializing: 1.0. Working: 0.6 (reason: hands and eyes are busy — the miller at the mill doesn't watch the road). In conversation: 0.5. Traveling: 0.7. Asleep: 0.0 for quiet events, 0.3 for loud ones (reason: a scream wakes you; a pocketed apple doesn't).

**Loudness** (the event's own visibility):
- Quiet theft (pocketing apples): 1.0. Ordinary (a purchase, an argument): 1.2. Loud (a fight, a wolf attack, a collapsing cart): 1.6. Reason: the formula's distance bands assume ordinary loudness; quiet things shrink the world, loud things stretch it.

**Traits:** cautious NPCs notice more ((cautious−50)×0.2 — a cautious 80 gets +6); curious NPCs notice *novel* things more ((curious−50)×0.1). Children are penalized on attention (distracted: 0.6) but curious (Lida 95) — they notice odd things adults filter out.

**Special case — discovering hidden things** (Corvin vs. Piotr's savings, per the approved D-03 decision): a hidden thing is an event with loudness 0.3, a hiding-place penalty (−30 if well hidden), rolled once per day the NPC is in the same location and not rushed. No special trigger, no scripted discovery — over weeks, chance compounds. Piotr's coins are decently hidden (−20); Corvin is usually tired and distracted at home (attention 0.5). Discovery is *possible*, not scheduled.

### 5 worked examples (real villagers, real places)

1. **Lida at the square, 14:00, sees the apple theft (witness seed).** Lida plays at `loc_square` 12:30–17:00; the stall is ~17 m away → band 60. Daylight 1.0 × playing 0.6 × quiet 1.0 = 36, plus (30−50)×0.2 = −4, plus curious 95 → (95−50)×0.1 = +4.5 → **~37%**. Seeded: in the witness run she notices. She is honest 90 — her report is trusted, but she's a child: her belief is "the stranger in the funny clothes took apples" (a description, not a name — the village doesn't know the player yet).
2. **Mira at the stall, 14:00.** She tends the stall 07:00–19:00, but at 14:00 she might be at the back of the stall or briefly away → same spot 90 × daylight × working 0.6 = 54. Seeded: in the standard Apple Test she does *not* notice (she's serving a customer, back turned). Her knowledge comes from the dusk count instead — which is the design: she knows apples are *missing*, not who took them.
3. **Bram on rounds, daytime.** Walking the square (traveling 0.7): 60 × 1.0 × 0.7 = 42 + cautious 80 → +6 → 48. He often notices *that something happened* (a commotion) without seeing the act — which is why he investigates after complaints rather than catching thieves red-handed.
4. **Tam in the forest, midday, hears a wolf.** Distance 90 m → band 25; loud event 1.6 → 40 × daylight × working 0.6 = 24 + cautious 75 → +5 → ~29. Wolves are usually *sign* first (tracks, a dead lamb) rather than a sighting — which is why "wolves were here" spreads as inference, not testimony.
5. **Tansy asleep at 02:00, burglary at the miller's house.** Asleep → 0.0 for quiet events. Even loud gets only 0.3. This is why burglary is the crime that *can* go unseen — and why the guard investigates hardest when it does surface (the violation of safety matters more than the loss).

---

## 2. Memory: how long things last

Every belief an NPC forms carries an **importance** (0–100) and a **formed-at** time. Importance decays with time; when it drops below a threshold, the belief is forgotten (or degrades to a vague feeling). Importance is set by: personal involvement ×2, gain/loss size, threat, novelty. Five levels:

| Level | Range | Example | Lasts | Reason |
|---|---|---|---|---|
| Trivia | 0–20 | "The player bought bread." | 2–3 days | Daily commerce is noise; nobody tracks it. |
| Minor | 20–40 | "Met the stranger by the shrine." | ~1 week | Novelty fades fast in a busy life. |
| Notable | 40–60 | "Mira's apples went missing." | ~1 month | A village incident lives a month in gossip. |
| Important | 60–80 | "The player saved my daughter." | ~1 season (90 days) | Debts of gratitude last a season of telling. |
| Major | 80–100 | "The player killed my brother." | Years (720+ days) | Some things a village never forgets. |

- **Decay:** importance drops ~1 point per day for Trivia/Minor, ~1 per 3 days for Notable, ~1 per 10 days for Important, and barely moves for Major (reason: the forgetting curve is steep for small things, flat for life-changing ones — this is why the Apple Test's rumor fades in a month but a murder would echo for years).
- **Refresh:** retelling or re-experiencing resets the clock (reason: gossip *is* rehearsal — the tavern keeps memories alive, which is why Bessa's tavern is a memory engine as well as a rumor engine).
- **False beliefs decay the same way** (reason: a wrong suspicion fades like a right one — Mira's suspicion of Tom will soften in a few weeks if nothing new happens).

---

## 3. Rumors: how stories travel

Rumors are beliefs passed from NPC to NPC during conversation, each hop recorded with a source chain. No teleporting information — a belief exists in an NPC's head only after a conversation (or perception) put it there.

### Where and when people talk

| Meeting point | When | Who | Spread |
|---|---|---|---|
| `loc_tavern` (The Hearthside) | 18:00–23:00 | Ralf, Corvin, Oda, Doran, Bram (one ale), travelers | Fast — Bessa (gossipy 85) is the switchboard; one evening reaches most regulars. |
| `loc_well` (The Old Well) | 06:30–07:00, 17:00–18:00 | Maren, Sima, children fetching water | Medium — short exchanges, high repetition. |
| `loc_square` (Market Square) | Mornings, market days | Everyone passing through | Medium — broad but shallow; news, not detail. |
| `loc_farm` farmgate | 09:00–11:00 | Maren + egg customers | Slow, deep — Maren's customers hear the *full* version. |
| `loc_shrine` | Occasional | The pious, the worried, children | Slow — rumors arrive here already shaped. |

Reason for the tavern's dominance: it's the only place where 10+ villagers sit still together for hours, with ale lowering caution and Bessa actively "arranging" news where people can hear it.

### What spreads first (topic salience)

1. **Danger** (wolves, theft, fire) — spreads fastest, distorts least (reason: threat information is survival-relevant; the village prioritizes it).
2. **Novelty** (the stranger, the Grey, a merchant's tale) — spreads fast, distorts fast.
3. **Money** (prices, debts, the mill wheel fund) — spreads among the affected.
4. **Social** (quarrels, courtships, Piotr's restlessness) — spreads through friends.
5. **Routine** (deliveries, weather) — barely spreads; it's just life.

### Distortion: how stories change

Each retelling rolls for mutation: base 20% per hop, +2% per 10 points of the teller's gossipy above 50, +10% per hop beyond the third (reason: the further from the source, the less anyone checks). A mutation changes **one detail**: who, how many, where, or why. Confidence drops **10 points per hop** (reason: "a friend of a friend said" is worth less — this is what keeps the guard's 70-point proof threshold meaningful).

### The trust filter: how belief is adopted

When told something, the listener forms the belief with:
```
confidence = sourceTrust × topicPlausibility × (1 − skepticism)
```
- **sourceTrust** = the relationship's trust value (0–100): Maren trusts Bessa at 60 → Bessa's stories land at 60% weight.
- **topicPlausibility** = 1.0 for ordinary things (apples missing), 0.5 for odd things (the Grey), 0.2 for wild things (reason: extraordinary claims need extraordinary sourcing — Ralf half-doubts his own Grey story, so it travels at half confidence from the start).
- **skepticism** = (cautious − 50) × 0.005: Tilda (cautious 85) discounts everything by ~17% (reason: skeptics are the village's immune system against panic).

### Worked example A: the Grey story

Ralf's starting belief: "I glimpsed something large and quiet past the second ridge, twice" — status *unknown*, his own confidence ~40 (he half-doubts himself). He tells it at the tavern when the ale is good → Bessa (trust 60) adopts at ~24 → retells it to regulars at ~14 → a traveler carries "a beast in the deep forest" out of the village entirely. Detail mutates with hops: "large and quiet" → "big as a bear" → "a spirit of the forest". It never resolves — there is no encounter in the prototype, so no perception event can ever confirm or deny it. It lives in the rumor layer forever, exactly as designed (WORLD.md decision 4).

### Worked example B: Tom's windfalls (the truth-vs-knowledge fog)

World truth: Tom took windfalls twice. Who knows: Lida saw (children's grapevine), Sima and Maren know (mothers know), Ralf knows (won't tell Mira — "boys are boys"). Mira *believes* "the orchard boys take windfalls" (true, no proof, confidence ~35). Nobody has proof; it's never been worth Bram's time. This is the load-bearing fog: when Mira's real theft happens, her suspicion system ranks candidates by **opportunity + past behavior + motive** — and Tom, the only person she *knows* steals fruit, tops the list. Wrong person, right habit. The rules produce the misattribution; no script names Tom.
---

## 4. Gossips, trusted sources, skeptics (by name)

From the 20 villagers' traits (CHARACTERS.md):

**The gossips** (gossipy ≥ 60 — they spread; they also distort):
- **Bessa Marlowe (85)** — the switchboard. Hears everything at the tavern, repeats the harmless half. Her retellings are the fastest vector in the village.
- **Maren Alder (65)** — the farmgate. Egg customers hear the *full* version with commentary.
- **Sima Fenn (60)** — the soft channel. What she tells you, Oda hears; what Oda hears, the bakery queue hears.

**The quiet ones** (gossipy ≤ 35 — information goes in, rarely comes out): Corvin (30), Tam (30), Bram (30), Jory (35), Piotr (35), Garrick (35), Doran (25). Reason: Doran's forge and Tam's forest are information sinks — tell them something and it stays told.

**Trusted sources** (honest ≥ 85 — when they speak, the village believes):
- **Elswith Alder (95)** — the elder's word settles arguments.
- **Bram Stone (95)** — the guard's logbook is the closest thing to an official record.
- **Tilda Bray (90), Sella Wren (90), Doran Kettle (85), Tam Oakes (85), Mira Holt (85)** — each trusted in their domain (prices, health, iron, the forest, the stall).
- **Lida Alder (90)** — honest but eight: trusted on *facts* ("I saw"), doubted on *interpretation* ("what it means"). Tansy (85) the same.

**The skeptics** (cautious ≥ 75, or professional habit — they discount and demand proof):
- **Tilda Bray (85)** — "Credit ruins people" extends to gossip; she believes ledgers, not talk.
- **Elswith Alder (85)** — has seen panics before; slows them down.
- **Bram Stone (80)** — professional: suspicion is not evidence, and he says so out loud.
- **Tam Oakes (75), Ralf Hale (75)** — the forest teaches you not to trust every rustle.
- **Jory Reed (70), Sella Wren (70), Mira Holt (70)** — each skeptical in their own trade.

The village's information immune system is real: a rumor Bessa launches hits Tilda's skepticism and Bram's professionalism before it can become action. Panic is possible but not cheap.

---

## 5. Reputation groups and the player's standing

There is no good/evil meter. The player has a **standing** (0–100, starts at **45 — neutral-to-cool**, per WORLD.md: polite wariness) with each group. Standing moves through the reputation rules, never by script.

| Group | ID | Members (of the 20) | What raises standing | What lowers it |
|---|---|---|---|---|
| Villagers | `group_villagers` | Everyone not below (default) | Helping, fair dealing, showing up | Theft, lying caught, cruelty |
| Guards | `group_guards` | Bram Stone (+ winter night-watch volunteers, abstracted) | Reporting honestly, helping the watch | Suspicion (−15), proven theft (−30), violence (−40) |
| Merchants | `group_merchants` | Tilda Bray, Mira Holt, Oda Fenn, Bessa Marlowe | Paying promptly, fair haggling | Unpaid tabs (−15), shoplifting (−25), cheating (−20) |
| Farmers | `group_farmers` | Corvin & Maren Alder, Garrick Alder, Tam Oakes, Jory Reed | Honest day's work (+3/day), helping at harvest (+10) | Crop/field damage (−15), livestock harm (−25) |
| Children | `group_children` | Lida & Tansy Alder, Tom Fenn | Kindness (+5), play (+3), treats (+2) | Frightening (−15), stealing from (−30) |
| Council | `group_council` | Elswith Alder, Garrick Alder, Bram Stone | Public service, honesty under pressure | Disrespect (−10), proven crimes (−25) |

Standing effects (emergent, not scripted): below 30, shopkeepers watch you and tabs are refused; below 20, the tavern goes quiet when you enter; above 60, doors open — credit, invitations, confidences. The numbers are thresholds the simulation's agents read, not story beats.

### Notoriety: the cost of being conspicuous

Per WORLD.md decision 5, the player may freely use old-world knowledge (crop rotation, basic medicine, literacy) — but conspicuous displays are *visible*, and visibility has a cost. **Notoriety** (0–100, starts at 0) tracks how much the village — and its shadows — have noticed the stranger's tricks. It rises when the player demonstrates otherworldly knowledge publicly (a public cure, reading aloud, a lecture on crop rotation at the tavern) and decays slowly (−1 per quiet week, reason: the village's attention moves on — unless something keeps refreshing it).

| Band | What happens (through the systems, never scripted) |
|---|---|
| 0–20 | Nobody notices. |
| 20–40 | Curiosity: questions, invitations; standing +2 with curious NPCs (Maren, Brynn, Sella). |
| 40–60 | Talk: Bessa's tavern discusses "the stranger's tricks"; rumors carry the player's description; merchants get pushy about buying secrets. |
| 60–80 | Wrong-kind attention: the player becomes a *mark* — petty theft attempts against the player become possible through the normal theft rules; strangers ask pointed questions about money. |
| 80–100 | Dangerous attention: criminal opportunity activates — background travelers and desperate villagers may attempt coercion (kidnap threats, "work for us") through the normal NPC decision rules (needs + opportunity + low witness chance). Never scripted, never guaranteed — but the door is open, and the player opened it. |

Reason for the design: the human's instruction was explicit — "if you come too much in everyone's eyes, some thief might come, kidnap or make you work for them." The mechanic honors that without a single scripted villain: notoriety is just another input to NPC utility scoring, like hunger or greed.

---

## 6. Law: suspicion vs. proof

Millbrook's law is evidence-first (WORLD.md). Bram's procedure is a ladder; each rung needs the confidence the rules below provide.

**Evidence weights** (added to Bram's confidence in a suspect, 0–100):
- Direct witness (saw the act): +50. A child's testimony counts but is capped at +40 (reason: Lida is honest but eight — Bram listens carefully and discounts for interpretation).
- Stolen goods found on the suspect: +60 (reason: possession is the closest thing to certainty).
- Confession: +80.
- Consistent second witness: +25. Contradictory accounts: −15 each (reason: the trust filter cuts both ways — disagreement erodes confidence).
- Circumstantial (motive, opportunity, past behavior — e.g. Tom's windfalls): +10 each, capped at +30 total (reason: suspicion *accumulates* but can never, by itself, reach proof — the cap is the law made arithmetic).

**Proof threshold: 70.** At or above, Bram acts. Below, he logs the complaint, asks around, and watches — exactly what the Apple Test needs.

**Punishments** (WORLD.md law, made procedural):
- First offense, proven: full restitution + public apology at the next Restday gathering. Shame is the real penalty (reason: in a village of 120, everyone knows — the apology *is* the punishment, and the reputation hit is −25 with the victim's groups).
- Repeat offense: the stocks for a day + a fine to the village fund.
- Serious crimes (burglary, granary theft): heavy fine, and banishment for the worst cases. Granary theft proven = the village turns as one (reason: it threatens everyone's winter — the one crime with a collective response).
- Violence against a villager: answered in kind, immediately (reason: the guard's monopoly on force is the village's safety bargain).
- **Shrine-offering theft: condemned by rumor alone.** No proof needed — the whole village's standing drops −30 on *suspicion* (reason: D-02 — even unproven, it's the lowest theft; the Hearth faith makes it sacrilege, and sacrilege doesn't wait for evidence).

---

## 7. The Apple Test, socially: day by day

Setup (ECONOMY.md): Day 1 starts with 20 apples; ~5 sell in the morning; **at 14:00 the player steals 6 of the remaining 15**; Mira counts at 19:00. Two runs: one where Lida (playing at the square, ~37% notice chance) sees it, one where she doesn't.

### Without a witness

- **Day 1, 14:00:** theft. No perception event fires. World truth changes; no NPC's knowledge does.
- **Day 1, 19:00:** Mira counts — expects 10, sees 4. Belief formed: "6 apples missing" (confidence 90, source: own count). Her suspicion ranking (opportunity + past behavior + motive): Tom Fenn tops it — the only person she *knows* takes fruit (confidence in Tom's guilt ~35, well below Bram's 70). The stranger doesn't rank: no past behavior, no opportunity record.
- **Day 1, evening:** Mira tells Bessa at the tavern. Rumor launches: "someone stole from Mira's stall" (source chain: Mira → Bessa → regulars; "six apples" mutates to "a whole basket" by the third telling).
- **Day 2:** the stall sells out ~11:00; failed purchases fire. Maren hears at the well, tells the farmgate by noon. By evening, most villagers hold the rumor at confidence 30–50. Nobody knows *who*.
- **Day 3 (Fifthday):** Mira complains to Bram. He logs it, asks around (no witness; circumstantial on Tom capped at +30 → confidence ~30). He does nothing further — and says so, kindly. Mira watches every customer harder for a week (a behavior modifier, not a script).
- **After:** the rumor decays to background ("someone steals from the stall," Tom vaguely suspected) over ~1 month. The player's standing is untouched. The village changed — shortages, a price tick, a warier Mira — with **nothing scripted**.

### With a witness (Lida sees it)

- **Day 1, 14:00:** Lida notices (seeded). Belief: "the stranger in the funny clothes took apples" (confidence 70 — direct witness, honest 90, but a description, not a name).
- **Day 1, supper:** Lida tells Maren (trust 85/95 → adopted at ~60). Maren tells the farmgate next morning and mentions it to Bessa at the well.
- **Day 1, evening:** the two rumors merge in the tavern — "someone stole" + "the stranger took apples." The chain is traceable: Lida → Maren → farmgate → Bessa → tavern → Bram. Details mutate along the way ("six apples" → "a pile of apples" → "half the stall").
- **Day 2:** Bram hears it over his one ale. He interviews Lida (with Maren present): child's testimony +40, Mira's count +10 circumstantial → confidence ~50–60. **Below the 70 threshold.** He does not accuse. He *does* start watching the stranger "as a matter of routine" — and asks the player, casually, where they were at 14:00. The player may deny (no consequence — suspicion is not evidence), deflect, or confess (+80 → restitution and the Restday apology).
- **Reputation effects:** guards −15 (suspicion, not conviction), villagers −5 (gossip), merchants −5 (Mira watches the player at the stall). Small, recoverable — exactly the "watched a little" the design wants for the stranger.
- **Mira's suspicion:** Lida's testimony outranks Tom in her ranking now — but Mira never *saw* it, so her belief in the stranger's guilt sits at ~55: she is cold to the player without knowing why she's sure. Truth vs. knowledge, in one shopkeeper's frown.

Both runs satisfy the Apple Test's social pass conditions: the shopkeeper believes apples are missing without knowing who (without witness); a rumor spreads with a traceable source chain (with witness); the guard never treats the player as a thief below the evidence threshold; and the wrong suspect (Tom) emerges from the rules in the no-witness run.

---

## 8. Later (kept out of the prototype)

- Reputation with the capital, the garrison, and neighboring villages (Phase 6+).
- Organized crime: a fence, a gang — the prototype's criminals are opportunists, not an organization.
- Written records beyond Bram's logbook (a village chronicler, later phases).
- The player's notoriety interacting with reincarnation secrecy: what happens if the village connects the tricks to the *truth* (Phase 7+ story fuel, not prototype systems).
- Formal trials: the council judges, but the prototype's crimes are small enough for the Restday apology.

---

## 9. Decisions made without the human

(Per the standing instruction: I decided these myself, following README.md's vision and the shared conventions.)

1. **Notice-chance formula and bands** (90/60/25/5 by distance, light ×1.0–0.2, attention ×1.0–0.0): chosen so a quiet daytime theft in the village core is *sometimes* seen (~37% for a playing child nearby) — rare enough to usually go unseen, common enough that witnesses exist.
2. **Memory retention** (trivia 2–3 days → major 720+ days): the Apple Test's rumor lives ~1 month; a murder would echo for years. Decay rates chosen so gossip rehearsal (the tavern) measurably extends memory.
3. **Rumor mechanics** (20% base mutation per hop, −10 confidence per hop, salience order danger > novelty > money > social > routine): keeps the guard's 70-point proof threshold meaningful — third-hand gossip can never convict.
4. **Reputation groups and membership**: six groups covering all 20 villagers; player starts at 45 (neutral-to-cool, per WORLD.md's polite wariness).
5. **Notoriety bands** (0–100, decay −1/quiet week): implements the human's D-01 instruction that conspicuous old-world knowledge draws thieves, kidnappers, or coercers — as emergent NPC opportunity, never scripted villains. The 80+ band is deliberately *possible, not scheduled*.
6. **Evidence weights and the 70-point proof threshold**: direct witness +50 (child capped +40), goods found +60, confession +80, circumstantial capped +30 — the cap is the evidence-first law made arithmetic.
7. **The witness is Lida Alder**: her schedule (square play 12:30–17:00) and honesty (90) make her the natural witness; her vagueness (a description, not a name) preserves the truth-vs-knowledge gap even *with* a witness.
8. **Shrine-theft condemnation by rumor alone** (−30 standing on suspicion): this was approved in D-02's decisions; I kept the number modest enough to hurt without ending a playthrough.
