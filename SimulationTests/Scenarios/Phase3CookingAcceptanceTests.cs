using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Persistence;
using LivingWorld.Simulation.Tests.Tools;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Scenarios
{
    /// <summary>
    /// P3-04 acceptance: a 7-day village simulation proving Phase 3 (skills,
    /// crafting and cooking) works end to end. Bessa Marlowe, the tavern keeper
    /// (cooking level 3), cooks two campfire stews every morning over the tavern
    /// hearth and feeds two villagers from the fresh pot. The player (cooking
    /// level 1, the approved "what were you good at" skill choice) practices one
    /// stew a day at the river campfire, taught by Bessa. The week verifies
    /// skill accrual (doubled when taught), tavern popularity, happiness from
    /// good meals, ingredient demand, money conservation — and a mid-week
    /// save/load round-trip that must reproduce the uninterrupted week exactly.
    /// A readable day-by-day log is written to Phase3_Acceptance_Log.md.
    /// </summary>
    public sealed class Phase3CookingAcceptanceTests
    {
        private const ulong Seed = 20261004;
        private const int Days = 7;
        private const int TicksPerDay = 24 * 60;

        private static readonly NpcId Bessa = new NpcId("npc_bessa_marlowe");
        private static readonly SkillId Cooking = new SkillId("skill_cooking");
        private static readonly RecipeId CampfireStew = new RecipeId("recipe_campfire_stew");
        private static readonly ItemTypeId Fish = new ItemTypeId("item_fish");
        private static readonly ItemTypeId Firewood = new ItemTypeId("item_firewood");
        private static readonly ItemTypeId Stew = new ItemTypeId("item_stew");
        private static readonly LocationId Tavern = new LocationId("loc_tavern");

        /// <summary>Everything one scripted week needs: the village, the stew recipe,
        /// the two villagers Bessa feeds, and the log being written.</summary>
        private sealed class CookingWeek
        {
            public CookingWeek(VillageAssembly.Village village, RecipeDefinition stew,
                List<NpcId> fedVillagers, StringBuilder log)
            {
                Village = village;
                Stew = stew;
                FedVillagers = fedVillagers;
                Log = log;
            }

            public VillageAssembly.Village Village { get; }
            public RecipeDefinition Stew { get; }
            public List<NpcId> FedVillagers { get; }
            public StringBuilder Log { get; }
            public WorldState State => Village.State;
        }

        private static string ContentRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                !File.Exists(Path.Combine(directory.FullName, "Content/world/locations.json")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate approved Content.");
            return directory.FullName;
        }

        private static string LogPath(string contentRoot) => Path.Combine(contentRoot,
            "SimulationTests", "Scenarios", "Phase3_Acceptance_Log.md");

        /// <summary>
        /// Builds the full village and applies the scenario premise: Bessa is an
        /// established level-3 cook, the player arrives with the approved 15 copper
        /// and cooking level 1, and both get a week's firewood as a scenario
        /// grant (like the world's starting bread — a premise, not a purchase).
        /// Fish arrives fresh every morning (see RunDay).
        /// </summary>
        private static CookingWeek SetUpWeek(string contentRoot, ulong seed, StringBuilder log)
        {
            VillageAssembly.Village village = VillageAssembly.Build(contentRoot, seed, 1);
            WorldState state = village.State;
            ItemCatalog catalog = village.Catalog;
            RecipeDefinition stew = RecipeCatalog.Load(contentRoot).Get(CampfireStew);

            NpcState bessa = state.Npcs[Bessa];
            bessa.Skills.Restore(new[] { SkillState.Restore(Cooking, 3, 0, 0, -1) });

            state.PlayerSkills.Restore(new[] { SkillState.Restore(Cooking, 1, 0, 0, -1) });
            state.Belongings.Register(ActorId.Player, new Inventory(catalog), new Wallet(15));

            NpcBelongingsEntry bessaHoldings = state.Belongings[ActorId.ForNpc(Bessa)];
            bessaHoldings.Inventory.Add(Firewood, 2 * Days);
            NpcBelongingsEntry playerHoldings = state.Belongings[ActorId.Player];
            playerHoldings.Inventory.Add(Firewood, Days);
            // Fish is delivered fresh every morning inside RunDay: a week's fish
            // would rot by day 5 (P2-09 spoilage: fresh 2 days, stale 2), so
            // stockpiling it would cheat the world's own rules.

            // The two villagers Bessa feeds each morning: the first two NPCs in
            // ordinal id order, excluding Bessa herself. Deterministic.
            var fed = new List<NpcId>();
            var ids = new List<NpcId>();
            foreach (NpcState npc in state.Npcs.Npcs) ids.Add(npc.Definition.Id);
            ids.Sort();
            foreach (NpcId id in ids)
            {
                if (id == Bessa) continue;
                fed.Add(id);
                if (fed.Count == 2) break;
            }

            log.AppendLine("Premise: Bessa Marlowe (tavern keeper, cooking level 3) cooks two");
            log.AppendLine("campfire stews every morning over the tavern hearth and feeds " +
                state.Npcs[fed[0]].Definition.Name + " and " +
                state.Npcs[fed[1]].Definition.Name + " from the fresh pot.");
            log.AppendLine("The player (cooking level 1) practices one stew a day at the river");
            log.AppendLine("campfire, taught by Bessa. Seed: " + seed + ".");
            log.AppendLine();
            return new CookingWeek(village, stew, fed, log);
        }

        /// <summary>
        /// One scripted day: morning cooking and feeding, then 1440 ticks of the
        /// living village. Every number below comes from the seeded world state,
        /// so the same week replays identically after a save/load.
        /// </summary>
        private static void RunDay(CookingWeek week, int day)
        {
            WorldState state = week.State;
            StringBuilder log = week.Log;
            long gameDay = state.Clock.Day;
            log.AppendLine("## Day " + day + " (game day " + gameDay + ")");
            log.AppendLine();

            // Jory's morning fish delivery: 2 for Bessa, 1 for the player.
            // (Fresh fish every day — see the setup note on spoilage.)
            NpcBelongingsEntry bessaHoldings = state.Belongings[ActorId.ForNpc(Bessa)];
            NpcBelongingsEntry playerHoldings = state.Belongings[ActorId.Player];
            bessaHoldings.Inventory.Add(Fish, 2);
            playerHoldings.Inventory.Add(Fish, 1);
            log.AppendLine("- Morning delivery: 2 fish for Bessa, 1 for the player.");

            // Bessa's morning: two campfire stews over the tavern hearth.
            // SKILLS.md section 1 lets campfire recipes use the river campfire or
            // any hearth; the recipe's loc_river_alder names the public campfire,
            // so the location gate is not consulted and Cook enforces the inputs.
            NpcState bessa = state.Npcs[Bessa];
            Inventory bessaPantry = bessaHoldings.Inventory;
            var qualities = new List<int>();
            for (int meal = 1; meal <= 2; meal++)
            {
                CookResult result = RecipeExecution.Cook(week.Stew, Bessa, bessa.Skills,
                    bessaPantry, gameDay, state.Rng);
                if (result.Outcome == CookOutcome.Success)
                {
                    qualities.Add(result.Quality);
                    CookingEffects.RecordCook(state, week.Stew, Bessa, bessa.Skills,
                        result, Tavern);
                    log.AppendLine("- Bessa cooks campfire stew #" + meal + ": quality " +
                        result.Quality + " (2 stews in the pot). Practice now " +
                        bessa.Skills.Get(Cooking).PracticePoints + " point(s).");
                }
                else
                {
                    log.AppendLine("- Bessa burns campfire stew #" + meal + " (inputs lost).");
                }
            }

            // Bessa feeds two villagers from the fresh pot.
            if (qualities.Count > 0)
            {
                int quality = qualities[0];
                foreach (NpcId villager in week.FedVillagers)
                {
                    NpcState eater = state.Npcs[villager];
                    int before = eater.Happiness;
                    MealEffects.GiveMeal(state, week.Village.Catalog, Bessa, villager,
                        Stew, quality, Tavern);
                    log.AppendLine("- Bessa feeds " + eater.Definition.Name + " a stew " +
                        "(quality " + quality + "): happiness " + before + " -> " +
                        eater.Happiness + ".");
                }
            }

            // The player's practice at the river campfire.
            // RecipeExecution.Cook's actor is NpcId-typed and unused beyond
            // validation; the player has no NpcId, so the scenario performs
            // Cook's exact steps — consume inputs, roll the public failure
            // chance on the world RNG, grant practice and outputs, record
            // demand — without inventing a cook identity. (API gap, noted in
            // the acceptance log.)
            Inventory playerPantry = playerHoldings.Inventory;
            if (!playerPantry.TryRemove(Fish, 1) || !playerPantry.TryRemove(Firewood, 1))
                throw new InvalidOperationException("The player's ingredients ran out.");
            int playerLevel = state.PlayerSkills.GetLevel(Cooking);
            bool failed = state.Rng.NextInt(100) <
                RecipeExecution.FailureChance(week.Stew.Difficulty, playerLevel);
            if (!failed)
            {
                // Taught by someone better: the 1-point session gain doubles.
                int gained = state.PlayerSkills.Practice(Cooking, 1, true, gameDay, state.Rng);
                playerPantry.Add(Stew, 2);
                state.IngredientDemand.AddDemand(Fish, 1);
                state.IngredientDemand.AddDemand(Firewood, 1);
                log.AppendLine("- The player practices campfire stew at the river: success, " +
                    "+" + gained + " practice points (taught by Bessa). Total now " +
                    state.PlayerSkills.Get(Cooking).PracticePoints + " point(s).");
            }
            else
            {
                log.AppendLine("- The player burns their campfire stew (inputs lost, no practice).");
            }

            for (int m = 0; m < TicksPerDay; m++) week.Village.World.Tick();

            log.AppendLine("- Dusk: tavern popularity " + state.TavernPopularity.Popularity +
                ", fish demand " + state.IngredientDemand.Demand(Fish) +
                ", firewood demand " + state.IngredientDemand.Demand(Firewood) + ".");
            log.AppendLine();
        }

        private static void AssertNoNegativeInventories(WorldState state)
        {
            foreach (Shop shop in state.Shops.Shops)
                foreach (KeyValuePair<ItemTypeId, int> pair in shop.Stock.Contents)
                    Assert.That(pair.Value, Is.GreaterThanOrEqualTo(0),
                        "Negative stock of " + pair.Key.Value + " in " + shop.Location.Value + ".");
            foreach (NpcBelongingsEntry entry in state.Belongings.Entries)
                foreach (KeyValuePair<ItemTypeId, int> pair in entry.Inventory.Contents)
                    Assert.That(pair.Value, Is.GreaterThanOrEqualTo(0),
                        "Negative inventory of " + pair.Key.Value + " for " + entry.Owner + ".");
        }

        [Test]
        public void SevenDayCookingAcceptance()
        {
            string root = ContentRoot();
            var log = new StringBuilder();
            log.AppendLine("# Phase 3 acceptance log — seven days of cooking");
            log.AppendLine();
            log.AppendLine("Seed 20261004. The full village ticks 7 days while Bessa and the");
            log.AppendLine("player cook every morning (scripted premise, unscripted village).");
            log.AppendLine();
            log.AppendLine("Design decisions taken for this scenario (for human review):");
            log.AppendLine("- Bessa cooks campfire stew over the tavern hearth. SKILLS.md section 1");
            log.AppendLine("  permits campfire recipes at the river campfire *or any hearth*; the");
            log.AppendLine("  recipe's loc_river_alder names the public campfire.");
            log.AppendLine("- The player is taught by Bessa, so each practice session grants 2 points.");
            log.AppendLine("- Meal happiness was recalibrated in P3-04 (see MealEffects): an");
            log.AppendLine("  above-average meal (quality 51+) now lifts happiness by 1, because");
            log.AppendLine("  ordinary cooking (quality 50-54) could never reach the old curve's");
            log.AppendLine("  63+ threshold — a good cook's stew is meant to be a small daily");
            log.AppendLine("  happiness (WORLD.md section 8, SKILLS.md section 1).");
            log.AppendLine("- The player's cook does not route through RecipeExecution.Cook: its");
            log.AppendLine("  actor parameter is NpcId-typed (and unused), and the player has no");
            log.AppendLine("  NpcId. The scenario performs Cook's exact steps instead.");
            log.AppendLine();

            CookingWeek week = SetUpWeek(root, Seed, log);
            WorldState state = week.State;
            long copperBefore = ProsperityIndex.TotalVillageCopper(state);

            for (int day = 1; day <= Days; day++) RunDay(week, day);

            // Skill accrual: the player is taught, so each success grants 2 points.
            SkillState playerCooking = state.PlayerSkills.Get(Cooking);
            int playerPoints = playerCooking.PracticePoints;
            Assert.That(playerPoints, Is.GreaterThan(7),
                "Taught practice doubles the 1-point gain: successes beat the untaught 7-point ceiling.");
            Assert.That(playerPoints, Is.LessThanOrEqualTo(14),
                "At most 2 points per day over 7 days.");
            Assert.That(playerPoints % 2, Is.EqualTo(0),
                "Each successful taught session grants exactly 2 points.");
            Assert.That(playerCooking.Level, Is.EqualTo(1),
                "14 points is far from the 100-point level-2 threshold.");

            // Bessa: 2 stews a day, 7 days, never fails difficulty 1 at level 3.
            SkillState bessaCooking = state.Npcs[Bessa].Skills.Get(Cooking);
            Assert.That(bessaCooking.PracticePoints, Is.EqualTo(14));
            Assert.That(bessaCooking.Level, Is.EqualTo(3));

            // Tavern popularity: Bessa cooks at the tavern daily (+1 per meal);
            // daily skilled cooking pauses the decay, so it never slips.
            Assert.That(state.TavernPopularity.Popularity, Is.EqualTo(64),
                "50 + 2/day x 7 days.");

            // Happiness: two villagers ate a quality-52 stew every day (+1 each).
            foreach (NpcId villager in week.FedVillagers)
                Assert.That(state.Npcs[villager].Happiness, Is.EqualTo(57),
                    state.Npcs[villager].Definition.Name + " ate 7 good stews.");

            // Demand: fish and firewood were bid up every day (the nightly decay
            // halves it, so it settles instead of exploding).
            Assert.That(state.IngredientDemand.Demand(Fish), Is.GreaterThan(0));
            Assert.That(state.IngredientDemand.Demand(Firewood), Is.GreaterThan(0));

            // The week's feedings are truth events carrying the meal quality.
            Assert.That(state.Events.Query(type: WorldEventType.MealEaten).Count, Is.EqualTo(14),
                "2 feedings/day x 7 days.");

            // Money conserved, no negative inventories, no exceptions (implicit).
            long copperAfter = ProsperityIndex.TotalVillageCopper(state);
            Assert.That(copperAfter, Is.InRange(copperBefore * 9 / 10, copperBefore * 11 / 10),
                "The cooking week must not break the village economy.");
            Assert.That(state.Belongings[ActorId.Player].Wallet.Balance, Is.EqualTo(15),
                "The player spent nothing: ingredients were granted, meals were free.");
            AssertNoNegativeInventories(state);

            log.AppendLine("## Final state");
            log.AppendLine();
            log.AppendLine("- Player cooking: level " + playerCooking.Level + ", " +
                playerPoints + " practice points.");
            log.AppendLine("- Bessa cooking: level " + bessaCooking.Level + ", " +
                bessaCooking.PracticePoints + " practice points.");
            log.AppendLine("- Tavern popularity: " + state.TavernPopularity.Popularity + ".");
            log.AppendLine("- Ingredient demand: fish " + state.IngredientDemand.Demand(Fish) +
                ", firewood " + state.IngredientDemand.Demand(Firewood) + ".");
            foreach (NpcId villager in week.FedVillagers)
                log.AppendLine("- " + state.Npcs[villager].Definition.Name +
                    " happiness: " + state.Npcs[villager].Happiness + ".");
            log.AppendLine("- Village copper: " + copperBefore + " -> " + copperAfter + ".");
            log.AppendLine("- MealEaten truth events: " +
                state.Events.Query(type: WorldEventType.MealEaten).Count + ".");
            log.AppendLine("- World digest: " + WorldDigest.Compute(state));
            File.WriteAllText(LogPath(root), log.ToString());
        }

        [Test]
        public void SevenDayCookingSaveLoadDeterminism()
        {
            string root = ContentRoot();

            // The uninterrupted week.
            CookingWeek whole = SetUpWeek(root, Seed, new StringBuilder());
            for (int day = 1; day <= Days; day++) RunDay(whole, day);
            string wholeSave = WorldSaver.Save(whole.State);
            string wholeDigest = WorldDigest.Compute(whole.State);

            // The same week with a save/load after day 4.
            CookingWeek first = SetUpWeek(root, Seed, new StringBuilder());
            for (int day = 1; day <= 4; day++) RunDay(first, day);
            WorldState loaded = WorldLoader.Load(WorldSaver.Save(first.State), root);
            VillageAssembly.Village reassembled = VillageAssembly.Reassemble(loaded, root);
            CookingWeek second = RebuildWeek(reassembled, first, root);
            for (int day = 5; day <= Days; day++) RunDay(second, day);
            string resumedSave = WorldSaver.Save(second.State);
            string resumedDigest = WorldDigest.Compute(second.State);

            Assert.That(resumedDigest, Is.EqualTo(wholeDigest),
                "Save at day 4, load, and continue must equal the uninterrupted week.");
            Assert.That(resumedSave, Is.EqualTo(wholeSave),
                "The resumed week ends byte-identical to the uninterrupted week.");
        }

        /// <summary>
        /// Rebuilds the scripted-week handle around a reassembled village: the
        /// recipe is reloaded from Content and the fed villagers recomputed the
        /// same deterministic way.
        /// </summary>
        private static CookingWeek RebuildWeek(VillageAssembly.Village reassembled,
            CookingWeek before, string contentRoot)
        {
            RecipeDefinition stew = RecipeCatalog.Load(contentRoot).Get(CampfireStew);
            WorldState state = reassembled.State;
            var fed = new List<NpcId>();
            var ids = new List<NpcId>();
            foreach (NpcState npc in state.Npcs.Npcs) ids.Add(npc.Definition.Id);
            ids.Sort();
            foreach (NpcId id in ids)
            {
                if (id == Bessa) continue;
                fed.Add(id);
                if (fed.Count == 2) break;
            }
            Assert.That(fed, Is.EqualTo(before.FedVillagers),
                "The fed villagers must be the same after the round-trip.");
            return new CookingWeek(reassembled, stew, fed, before.Log);
        }
    }
}
