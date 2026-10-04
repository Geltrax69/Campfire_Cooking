using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Cooking effects on the world (P3-03, SKILLS.md section 2): a skilled cook raises
    /// tavern popularity (which decays back toward 50 when forgotten); cooking raises
    /// ingredient demand, which decays daily and nudges shop prices up while high;
    /// level-2+ cooks waste less (a seeded 50% refund of one input unit); popular
    /// taverns keep travelers longer, raising ale/bed payouts; and cooked dishes sell
    /// at a quality-adjusted price. Statistical tests use fixed seeds, mirroring the
    /// RecipeExecutionTests style.
    /// </summary>
    [TestFixture]
    public sealed class CookingEffectsTests
    {
        private static readonly SkillId Cooking = new SkillId("skill_cooking");
        private static readonly NpcId Cook = new NpcId("npc_test_cook");
        private static readonly NpcId Shopkeeper = new NpcId("npc_test_keeper");
        private static readonly NpcId Buyer = new NpcId("npc_test_buyer");
        private static readonly LocationId Tavern = new LocationId("loc_tavern");
        private static readonly LocationId Stall = new LocationId("loc_stall");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly ItemTypeId Flour = new ItemTypeId("item_flour");
        private static readonly ItemTypeId Fish = new ItemTypeId("item_fish");
        private static readonly ItemTypeId Firewood = new ItemTypeId("item_firewood");
        private static readonly ItemTypeId Stew = new ItemTypeId("item_stew");
        private static readonly ItemTypeId ApplePie = new ItemTypeId("item_apple_pie");

        private RecipeCatalog _recipes;
        private ItemCatalog _items;

        [OneTimeSetUp]
        public void LoadContent()
        {
            string root = RepositoryRoot();
            _recipes = RecipeCatalog.Load(root);
            _items = ContentBundle.Load(root).Catalog;
        }

        [Test]
        public void TavernPopularityRisesWithSkilledCook()
        {
            WorldState state = new WorldState(42, new GameTime(0));
            RecipeDefinition pie = _recipes.Get(new RecipeId("recipe_apple_pie"));

            CookingEffects.RecordCook(state, pie, Cook, SkillsAt(4), Success(pie, 80), Tavern);
            Assert.That(state.TavernPopularity.Popularity, Is.EqualTo(52),
                "Level 4: popularity += (4 - 2).");

            CookingEffects.RecordCook(state, pie, Cook, SkillsAt(1), Success(pie, 50), Tavern);
            Assert.That(state.TavernPopularity.Popularity, Is.EqualTo(51),
                "Level 1: popularity += (1 - 2); a poor cook costs the tavern a little.");
        }

        [Test]
        public void CookingElsewhereLeavesPopularityAlone()
        {
            WorldState state = new WorldState(42, new GameTime(0));
            RecipeDefinition stew = _recipes.Get(new RecipeId("recipe_campfire_stew"));

            CookingEffects.RecordCook(state, stew, Cook, SkillsAt(5), Success(stew, 54),
                new LocationId("loc_river_alder"));

            Assert.That(state.TavernPopularity.Popularity, Is.EqualTo(50));
        }

        [Test]
        public void PopularityDecaysTowardBaselineWhenForgotten()
        {
            WorldState state = new WorldState(42, new GameTime(0));
            var system = new TavernPopularitySystem();
            state.TavernPopularity.Shift(20);
            Assert.That(state.TavernPopularity.Popularity, Is.EqualTo(70));

            system.Tick(state);
            Assert.That(state.TavernPopularity.Popularity, Is.EqualTo(69),
                "Decays on the first tick too: one point per forgotten day, toward 50.");
            state.Clock = new GameTime(1440); // day 2
            system.Tick(state);
            Assert.That(state.TavernPopularity.Popularity, Is.EqualTo(68));

            for (int day = 3; day <= 30; day++)
            {
                state.Clock = new GameTime((day - 1) * 1440);
                system.Tick(state);
            }
            Assert.That(state.TavernPopularity.Popularity, Is.EqualTo(50),
                "Decay stops at the 50 baseline; it never overshoots.");
        }

        [Test]
        public void SkilledCookingPausesPopularityDecay()
        {
            WorldState state = new WorldState(42, new GameTime(0));
            var system = new TavernPopularitySystem();
            RecipeDefinition pie = _recipes.Get(new RecipeId("recipe_apple_pie"));
            state.TavernPopularity.Shift(10); // 60

            CookingEffects.RecordCook(state, pie, Cook, SkillsAt(3), Success(pie, 52), Tavern);
            Assert.That(state.TavernPopularity.Popularity, Is.EqualTo(61));

            state.Clock = new GameTime(1440); // day 2: yesterday had a skilled cook
            system.Tick(state);
            Assert.That(state.TavernPopularity.Popularity, Is.EqualTo(61),
                "No decay the morning after a skilled cook worked.");

            state.Clock = new GameTime(2 * 1440); // day 3: forgotten again
            system.Tick(state);
            Assert.That(state.TavernPopularity.Popularity, Is.EqualTo(60));
        }

        [Test]
        public void ApplePieRaisesAppleDemand()
        {
            WorldState state = new WorldState(42, new GameTime(0));
            RecipeDefinition pie = _recipes.Get(new RecipeId("recipe_apple_pie"));
            var skills = SkillsAt(5); // difficulty 3: never fails, so every pie is real demand
            var inventory = new Inventory(_items);
            RegisterBelongings(state, Cook, inventory);

            for (int bake = 0; bake < 3; bake++)
            {
                foreach (RecipeIngredient ingredient in pie.Inputs)
                    inventory.Add(ingredient.Item, ingredient.Count);
                CookResult result = RecipeExecution.Cook(pie, Cook, skills, inventory,
                    state.Clock.Day, state.Rng);
                Assert.That(result.Outcome, Is.EqualTo(CookOutcome.Success));
                CookingEffects.RecordCook(state, pie, Cook, skills, result, Tavern);
            }

            Assert.That(state.IngredientDemand.Demand(Apple), Is.EqualTo(12),
                "Three pies x 4 apples: the recipe note says apple demand visibly rises.");
            Assert.That(state.IngredientDemand.Demand(Flour), Is.EqualTo(3));
            Assert.That(state.IngredientDemand.IsHigh(Apple), Is.True);
        }

        [Test]
        public void DemandDecaysDaily()
        {
            WorldState state = new WorldState(42, new GameTime(0));
            var system = new IngredientDemandSystem();
            state.IngredientDemand.AddDemand(Apple, 9);

            system.Tick(state);
            system.Tick(state); // same day: no double decay
            Assert.That(state.IngredientDemand.Demand(Apple), Is.EqualTo(4),
                "Halved once per day, rounded down.");

            state.Clock = new GameTime(1440);
            system.Tick(state);
            Assert.That(state.IngredientDemand.Demand(Apple), Is.EqualTo(2));
        }

        [Test]
        public void HighDemandNudgesPriceUp()
        {
            Scenario scenario = PriceScenario(stock: 20, price: 3);
            var configuration = PriceConfiguration(scenario.Shop);
            var system = new PriceAdjustmentSystem(new[] { configuration }, scenario.State,
                scenario.State.IngredientDemand);

            scenario.State.IngredientDemand.AddDemand(Apple, 10);
            system.Tick(scenario.State);

            Assert.That(scenario.Shop.UnitPrice(Apple), Is.EqualTo(4),
                "High ingredient demand nudges the price up one step, like a missed sale.");
            Assert.That(scenario.State.Events.Query(type: WorldEventType.PriceChanged),
                Has.Count.EqualTo(1));
        }

        [Test]
        public void Level2CookWastesLess()
        {
            // Campfire stew at difficulty 1: level 2 never fails, so every trial is a
            // real waste roll; level 1 never gets the refund.
            RecipeDefinition stew = _recipes.Get(new RecipeId("recipe_campfire_stew"));
            int level2Refunds = CountRefunds(stew, 2, _items, trials: 100);
            int level1Refunds = CountRefunds(stew, 1, _items, trials: 100);

            Assert.That(level2Refunds, Is.InRange(30, 70),
                "Level 2+: ~50% of successful cooks refund one input unit, got " + level2Refunds + "%.");
            Assert.That(level1Refunds, Is.EqualTo(0),
                "Level 1 never wastes less: no refund, ever.");
        }

        [Test]
        public void ExtraNightsRaiseTavernPayout()
        {
            // Summer day 300: the monthly traveler rate is 900 copper; popularity 90
            // keeps travelers 2 extra nights, scaling the tavern's share by 32/30.
            long quietPayout = TravelerPayout(popularity: 50);
            long busyPayout = TravelerPayout(popularity: 90);

            Assert.That(quietPayout, Is.EqualTo(900));
            Assert.That(busyPayout, Is.EqualTo(960),
                "900 x 32 / 30: longer stays mean more ale and bed sales.");
        }

        [Test]
        public void ExtraNightsFormula()
        {
            Assert.That(TavernPopularitySystem.ExtraNights(50), Is.EqualTo(0));
            Assert.That(TavernPopularitySystem.ExtraNights(69), Is.EqualTo(0));
            Assert.That(TavernPopularitySystem.ExtraNights(70), Is.EqualTo(1));
            Assert.That(TavernPopularitySystem.ExtraNights(90), Is.EqualTo(2));
            Assert.That(TavernPopularitySystem.ExtraNights(30), Is.EqualTo(0),
                "Below 50 the tavern is unremarkable, not repellent: no penalty.");
        }

        [Test]
        public void QualitySalePriceFollowsFormula()
        {
            Assert.That(CookingEffects.QualitySalePrice(10, 50), Is.EqualTo(10),
                "Average quality sells at base value.");
            Assert.That(CookingEffects.QualitySalePrice(10, 100), Is.EqualTo(12));
            Assert.That(CookingEffects.QualitySalePrice(10, 0), Is.EqualTo(8));
            Assert.That(CookingEffects.QualitySalePrice(5, 75), Is.EqualTo(6));
            Assert.That(CookingEffects.QualitySalePrice(1, 0), Is.EqualTo(1),
                "Sale price never drops below 1 copper.");
        }

        [Test]
        public void SellCookedDishConservesMoney()
        {
            WorldState state = new WorldState(42, new GameTime(0));
            var sellerWallet = new Wallet(0);
            var buyerWallet = new Wallet(100);
            var sellerStock = new Inventory(_items);
            var buyerStock = new Inventory(_items);
            state.Belongings.Register(ActorId.ForNpc(Cook), sellerStock, sellerWallet);
            state.Belongings.Register(ActorId.ForNpc(Buyer), buyerStock, buyerWallet);
            sellerStock.Add(Stew, 1);

            // Stew base value 4, quality 80: 4 + round(30/25) = 5 copper.
            CookingEffects.SellCookedDish(state, _items, Cook, Buyer, Stew, 80, Tavern);

            Assert.That(buyerWallet.Balance, Is.EqualTo(95));
            Assert.That(sellerWallet.Balance, Is.EqualTo(5),
                "100 total before, 100 after: money is conserved.");
            Assert.That(buyerStock.Count(Stew), Is.EqualTo(1));
            Assert.That(sellerStock.Count(Stew), Is.EqualTo(0));
            WorldEvent sale = state.Events.Query(type: WorldEventType.Purchase).Single();
            Assert.That((sale.Actor, sale.ItemType, sale.Quantity, sale.Copper),
                Is.EqualTo(((ActorId?)ActorId.ForNpc(Buyer), (ItemTypeId?)Stew, (int?)1, (int?)5)));
        }

        [Test]
        public void SellCookedDishFailsCleanlyWhenBuyerIsBroke()
        {
            WorldState state = new WorldState(42, new GameTime(0));
            var sellerStock = new Inventory(_items);
            var buyerStock = new Inventory(_items);
            state.Belongings.Register(ActorId.ForNpc(Cook), sellerStock, new Wallet(0));
            state.Belongings.Register(ActorId.ForNpc(Buyer), buyerStock, new Wallet(3));
            sellerStock.Add(Stew, 1);

            Assert.Throws<InvalidOperationException>(() =>
                CookingEffects.SellCookedDish(state, _items, Cook, Buyer, Stew, 80, Tavern));

            Assert.That(sellerStock.Count(Stew), Is.EqualTo(1), "Nothing moved on a failed sale.");
            Assert.That(state.Events.Count, Is.EqualTo(0));
        }

        private static CookResult Success(RecipeDefinition recipe, int quality) =>
            new CookResult(CookOutcome.Success, quality,
                new[] { new CookedOutput(ApplePie, 2) }, 0, -1, null);

        private static SkillStore SkillsAt(int level)
        {
            var store = new SkillStore();
            store.Restore(new[] { SkillState.Restore(Cooking, level, 0, 0, -1) });
            return store;
        }

        private static void RegisterBelongings(WorldState state, NpcId npc, Inventory inventory) =>
            state.Belongings.Register(ActorId.ForNpc(npc), inventory, new Wallet(0));

        private static int CountRefunds(RecipeDefinition stew, int level, ItemCatalog items, int trials)
        {
            int refunds = 0;
            for (ulong seed = 1; seed <= (ulong)trials; seed++)
            {
                var state = new WorldState(seed, new GameTime(0));
                var skills = SkillsAt(level);
                var inventory = new Inventory(items);
                foreach (RecipeIngredient ingredient in stew.Inputs)
                    inventory.Add(ingredient.Item, ingredient.Count);
                RegisterBelongings(state, Cook, inventory);

                CookResult result = RecipeExecution.Cook(stew, Cook, skills, inventory,
                    state.Clock.Day, state.Rng);
                CookingEffects.RecordCook(state, stew, Cook, skills, result,
                    new LocationId("loc_river_alder"));
                int left = 0;
                foreach (RecipeIngredient ingredient in stew.Inputs)
                    left += inventory.Count(ingredient.Item);
                if (left > 0) refunds++;
            }
            return refunds;
        }

        private static long TravelerPayout(int popularity)
        {
            var state = new WorldState(42, new GameTime((300 - 1) * 1440)); // summer day 300
            var wallet = new Wallet();
            var payee = new TravelerPayee(new NpcId("npc_bessa_marlowe"), wallet, Tavern, 1);
            var configuration = new TravelerConfiguration("travel_test", new[] { payee },
                EventVisibility.Hidden);
            state.TavernPopularity.Shift(popularity - 50);
            state.RestoreTravelerSpend(new TravelerSpendState(
                initialized: true, lastPayoutDay: 299, monthIndex: 9,
                paidThisMonth: new[] { 0 }));
            new TravelerSpendSystem(new[] { configuration }).Tick(state);
            return wallet.Balance;
        }

        private sealed class Scenario
        {
            public Scenario(Shop shop, WorldState state) { Shop = shop; State = state; }
            public Shop Shop { get; }
            public WorldState State { get; }
        }

        private static Scenario PriceScenario(int stock, int price)
        {
            ItemCatalog catalog = TinyCatalog();
            var state = new WorldState(42, new GameTime(100));
            var inventory = new Inventory(catalog);
            if (stock > 0) inventory.Add(Apple, stock);
            var shop = new Shop(Stall, Shopkeeper, inventory, new Wallet(), new[]
            {
                new KeyValuePair<ItemTypeId, int>(Apple, price),
            });
            state.Shops.Register(shop);
            return new Scenario(shop, state);
        }

        private static PriceAdjustmentConfiguration PriceConfiguration(Shop shop) =>
            new PriceAdjustmentConfiguration("price_apples", shop, Apple, new GameTime(100),
                60, 10, 30, 1, 1, 5, EventVisibility.Normal);

        private static ItemCatalog TinyCatalog() => new ItemCatalog(new[]
        {
            new ItemDefinition(Apple, "Apple", "food", 3, 1, hungerEffect: -15),
            new ItemDefinition(Flour, "Flour", "food", 2, 1),
        });

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                !File.Exists(Path.Combine(directory.FullName, "Content/world/locations.json")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate approved Content.");
            return directory.FullName;
        }
    }
}
