# Phase 3 acceptance log — seven days of cooking

Seed 20261004. The full village ticks 7 days while Bessa and the
player cook every morning (scripted premise, unscripted village).

Design decisions taken for this scenario (for human review):
- Bessa cooks campfire stew over the tavern hearth. SKILLS.md section 1
  permits campfire recipes at the river campfire *or any hearth*; the
  recipe's loc_river_alder names the public campfire.
- The player is taught by Bessa, so each practice session grants 2 points.
- Meal happiness was recalibrated in P3-04 (see MealEffects): an
  above-average meal (quality 51+) now lifts happiness by 1, because
  ordinary cooking (quality 50-54) could never reach the old curve's
  63+ threshold — a good cook's stew is meant to be a small daily
  happiness (WORLD.md section 8, SKILLS.md section 1).
- The player's cook does not route through RecipeExecution.Cook: its
  actor parameter is NpcId-typed (and unused), and the player has no
  NpcId. The scenario performs Cook's exact steps instead.

Premise: Bessa Marlowe (tavern keeper, cooking level 3) cooks two
campfire stews every morning over the tavern hearth and feeds Bram Stone and Brynn Oakes from the fresh pot.
The player (cooking level 1) practices one stew a day at the river
campfire, taught by Bessa. Seed: 20261004.

## Day 1 (game day 1)

- Morning delivery: 2 fish for Bessa, 1 for the player.
- Bessa cooks campfire stew #1: quality 52 (2 stews in the pot). Practice now 1 point(s).
- Bessa cooks campfire stew #2: quality 52 (2 stews in the pot). Practice now 2 point(s).
- Bessa feeds Bram Stone a stew (quality 52): happiness 50 -> 51.
- Bessa feeds Brynn Oakes a stew (quality 52): happiness 50 -> 51.
- The player practices campfire stew at the river: success, +2 practice points (taught by Bessa). Total now 2 point(s).
- Dusk: tavern popularity 52, fish demand 0, firewood demand 0.

## Day 2 (game day 2)

- Morning delivery: 2 fish for Bessa, 1 for the player.
- Bessa cooks campfire stew #1: quality 52 (2 stews in the pot). Practice now 3 point(s).
- Bessa cooks campfire stew #2: quality 52 (2 stews in the pot). Practice now 4 point(s).
- Bessa feeds Bram Stone a stew (quality 52): happiness 51 -> 52.
- Bessa feeds Brynn Oakes a stew (quality 52): happiness 51 -> 52.
- The player practices campfire stew at the river: success, +2 practice points (taught by Bessa). Total now 4 point(s).
- Dusk: tavern popularity 54, fish demand 1, firewood demand 1.

## Day 3 (game day 3)

- Morning delivery: 2 fish for Bessa, 1 for the player.
- Bessa cooks campfire stew #1: quality 52 (2 stews in the pot). Practice now 5 point(s).
- Bessa cooks campfire stew #2: quality 52 (2 stews in the pot). Practice now 6 point(s).
- Bessa feeds Bram Stone a stew (quality 52): happiness 52 -> 53.
- Bessa feeds Brynn Oakes a stew (quality 52): happiness 52 -> 53.
- The player practices campfire stew at the river: success, +2 practice points (taught by Bessa). Total now 6 point(s).
- Dusk: tavern popularity 56, fish demand 2, firewood demand 2.

## Day 4 (game day 4)

- Morning delivery: 2 fish for Bessa, 1 for the player.
- Bessa cooks campfire stew #1: quality 52 (2 stews in the pot). Practice now 7 point(s).
- Bessa cooks campfire stew #2: quality 52 (2 stews in the pot). Practice now 8 point(s).
- Bessa feeds Bram Stone a stew (quality 52): happiness 53 -> 54.
- Bessa feeds Brynn Oakes a stew (quality 52): happiness 53 -> 54.
- The player practices campfire stew at the river: success, +2 practice points (taught by Bessa). Total now 8 point(s).
- Dusk: tavern popularity 58, fish demand 2, firewood demand 2.

## Day 5 (game day 5)

- Morning delivery: 2 fish for Bessa, 1 for the player.
- Bessa cooks campfire stew #1: quality 52 (2 stews in the pot). Practice now 9 point(s).
- Bessa cooks campfire stew #2: quality 52 (2 stews in the pot). Practice now 10 point(s).
- Bessa feeds Bram Stone a stew (quality 52): happiness 54 -> 55.
- Bessa feeds Brynn Oakes a stew (quality 52): happiness 54 -> 55.
- The player practices campfire stew at the river: success, +2 practice points (taught by Bessa). Total now 10 point(s).
- Dusk: tavern popularity 60, fish demand 2, firewood demand 2.

## Day 6 (game day 6)

- Morning delivery: 2 fish for Bessa, 1 for the player.
- Bessa cooks campfire stew #1: quality 52 (2 stews in the pot). Practice now 11 point(s).
- Bessa cooks campfire stew #2: quality 52 (2 stews in the pot). Practice now 12 point(s).
- Bessa feeds Bram Stone a stew (quality 52): happiness 55 -> 56.
- Bessa feeds Brynn Oakes a stew (quality 52): happiness 55 -> 56.
- The player practices campfire stew at the river: success, +2 practice points (taught by Bessa). Total now 12 point(s).
- Dusk: tavern popularity 62, fish demand 2, firewood demand 2.

## Day 7 (game day 7)

- Morning delivery: 2 fish for Bessa, 1 for the player.
- Bessa cooks campfire stew #1: quality 52 (2 stews in the pot). Practice now 13 point(s).
- Bessa cooks campfire stew #2: quality 52 (2 stews in the pot). Practice now 14 point(s).
- Bessa feeds Bram Stone a stew (quality 52): happiness 56 -> 57.
- Bessa feeds Brynn Oakes a stew (quality 52): happiness 56 -> 57.
- The player practices campfire stew at the river: success, +2 practice points (taught by Bessa). Total now 14 point(s).
- Dusk: tavern popularity 64, fish demand 2, firewood demand 2.

## Final state

- Player cooking: level 1, 14 practice points.
- Bessa cooking: level 3, 14 practice points.
- Tavern popularity: 64.
- Ingredient demand: fish 2, firewood 2.
- Bram Stone happiness: 57.
- Brynn Oakes happiness: 57.
- Village copper: 9265 -> 9690.
- MealEaten truth events: 14.
- World digest: ecfe80912b46aad14cc6c623aba4e94cdaee52379801db6bc6d9ca1ea715e041
