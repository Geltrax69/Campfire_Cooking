# Phase 4 acceptance log — ninety days with the animals

Seed 20261004. The full village opens in mid-autumn (game day 46) with the design's starting animal populations: 20 chickens, 12 pigs, 40 deer, a 6-wolf pack and 25 bramblebacks. Ninety days run from late autumn into mid-winter (days 46-135) while the player hand-feeds one Alder Farm hen for the first 7 days. A save at the autumn/winter boundary (day 90), loaded and continued, reaches byte-identical final state to an uninterrupted run — the determinism proof is in the test, not just in this log.

Design decisions taken for this scenario (for human review):
- The 90-day window starts mid-autumn so one run covers both seasons the brief asks about (autumn egg-laying vs winter losses). A day-1 start would spend the whole run in autumn.
- The village ticks minute-by-minute like the other acceptance scenarios. The ecosystem cursors start at the build day, so the opening day (46) has no hunts or laying — the world opens at 04:00 with the morning's animal business done — and no system ever backdates an event before one another system appended (the event log rejects backwards time).
- The two laying coops (Alder Farm, Fenn's) are caller-owned configuration from P4-02, not world state: laid eggs are not in the save document (see SCHEMA.md). The Produced truth events are, so egg totals below come from the event log and survive save/load.
- The player tames animal_chicken_001, the first adult hen at Alder Farm (starting trust 21).

## Taming diary — one hen, seven hand-feedings

- Day 1 (game day 47): trust 21 → 29 (TrustChanged).
- Day 2 (game day 48): trust 29 → 37 (TrustChanged).
- Day 3 (game day 49): trust 37 → 45 (TrustChanged).
- Day 4 (game day 50): trust 45 → 53 (TrustChanged).
- Day 5 (game day 51): trust 53 → 61 (Bonded). **BONDED to the player.**
- Day 6 (game day 52): trust 61 → 69 (TrustChanged).
- Day 7 (game day 53): trust 69 → 77 (TrustChanged).

The design's chicken bond threshold is 60 trust (species.json); 8 trust per daily hand-feeding bonds her on the fifth day. The bond persists through the save/load: the owner is part of the saved animal state.

## Week by week — populations, losses, eggs

| Days | Season | Chickens | Pigs | Deer | Wolves | Bramblebacks | Livestock lost (wk) | Eggs laid (wk) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 46-52 | Autumn | 20 | 12 | 40 | 6 | 25 | 0 | 82 |
| 53-59 | Autumn | 20 | 12 | 40 | 6 | 25 | 0 | 92 |
| 60-66 | Autumn | 20 | 12 | 40 | 6 | 25 | 0 | 85 |
| 67-73 | Autumn | 20 | 12 | 40 | 6 | 25 | 0 | 71 |
| 74-80 | Autumn | 20 | 12 | 40 | 6 | 25 | 0 | 77 |
| 81-87 | Autumn | 20 | 12 | 40 | 6 | 25 | 0 | 72 |
| 88-94 | Winter | 20 | 12 | 39 | 6 | 25 | 0 | 38 |
| 95-101 | Winter | 20 | 12 | 38 | 6 | 25 | 0 | 21 |
| 102-108 | Winter | 19 | 12 | 38 | 6 | 25 | 1 | 18 |
| 109-115 | Winter | 19 | 12 | 38 | 6 | 25 | 0 | 15 |
| 116-122 | Winter | 19 | 12 | 38 | 6 | 25 | 0 | 23 |
| 123-129 | Winter | 19 | 12 | 38 | 6 | 25 | 0 | 28 |
| 130-135 | Winter | 19 | 12 | 38 | 6 | 25 | 0 | 21 |

## Wolf incidents — winter livestock losses

- Game day 108: wolves took a chicken at loc_farm (recorded value 25 copper).

## Final state (game day 135)

- Chickens: 19, pigs: 12, deer: 38 (down from 40 — wolf predation), wolves: 6, bramblebacks: 25.
- Eggs laid: 498 in autumn, 145 in winter — laying is strongly seasonal.
- Bonded animals: animal_chicken_001 (species_chicken, trust 77, owner player).
- WorldDigest (save/load-continued run): f998f2b1eb5b1ae3ba1edce1cbd61b4104f089b076084de9b24b1eda3234e6f2.
