# Assets

All game art and audio. Units in GLB models are metres, Y-up, unless a pack says otherwise.

## Folder layout

| Folder | What's in it |
|---|---|
| `Models/` | Single loose models sorted by use (buildings, props, nature, characters, terrain, food, dungeon) |
| `Packs/` | Complete third-party packs, kept intact with their license files |
| `Audio/SFX/` | Sound effects and short jingles |
| `UI/` | Touch controls, icons, panels and borders |
| `Sky/` | Sky domes, cloud models and skybox textures |
| `_OffTheme/` | Modern or sci-fi assets that don't fit a fantasy village (cars, trains, guns, skyscrapers). Kept, not deleted |
| `Models/_Unsorted/` | Two large multi-mesh scenes of unknown content (originally `model.glb` and `model (1).glb`) |

## Sources and licenses

| Asset | Source | License |
|---|---|---|
| `Packs/Kenney/*`, `UI/Kenney_*`, `Sky/Kenney_Skyboxes`, `_OffTheme/Packs/Kenney_*` | [Kenney](https://kenney.nl) (the new ones came from the [shorepine/kenney](https://github.com/shorepine/kenney) mirror) | CC0 (public domain) |
| `Packs/CrayonCityProps` | Crayon | Free for games, including commercial. You may not resell the assets themselves. See `LICENSE.txt` |
| `Models/Characters/Animals/Quaternius/` (wolf, horse, stag, fox, all animated) | [Quaternius](https://quaternius.com) Ultimate Animated Animals, via [yusib147/3d-animal-website](https://github.com/yusib147/3d-animal-website) | CC0 |
| `Audio/SFX/rpg-audio`, `impact-sounds`, `interface-sounds`, `ui-audio`, `music-jingles` | Kenney, via [open-game-sfx-index](https://github.com/Mcamento8/open-game-sfx-index) | CC0 |
| `Audio/SFX/oga-*` | OpenGameArt authors listed in `Audio/SFX/SOURCES.md` | CC0 (as verified by the index) |
| `Models/Characters/Humanoids`, `Monsters`, `Animals/wolf-static.glb` | Generated with Tripo AI. Static meshes with no skeleton or animations | Check your Tripo plan's terms |
| All other loose files in `Models/` | **Unknown.** They were uploaded without license files | **Verify before shipping** |

## Highlights for the prototype

- **Animated villagers:** `Packs/Kenney/MiniCharacters` has 12 characters with idle, walk, sprint, sit, pick-up, interact, melee attack, die and emote animations.
- **Animals:** `Packs/Kenney/CubePets` (cow, pig, deer, fox, fish, chick, bunny, dog, cat and more), plus the animated Quaternius wolf, horse, stag and fox.
- **Village:** `FantasyTownKit` (walls, roofs, roads, fountains, market carts), `CastleKit`, and the farmhouse, barn, coop, palisade and great hall in `Models/Buildings`.
- **Nature:** `NatureKit` (329 trees, rocks, plants, cliffs, river pieces and crops), plus the apple tree in `Models/Nature/Trees`.
- **Cooking and food:** `FoodKit`, `Models/Food`, and the campfire and cooking hearth in `Models/Props/Cooking`.
- **Swords and tools:** `WeaponKit`, `SurvivalKit`, `RetroFantasyKit`.
- **iPad controls:** `UI/Kenney_MobileControls` (joystick and touch buttons).
