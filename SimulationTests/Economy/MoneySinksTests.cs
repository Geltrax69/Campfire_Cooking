using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// P2-09 (money sinks): the reeve's taxes, adaptive import orders, the community
    /// fund and feast reserve, and two-stage per-lot spoilage with stale pricing.
    /// </summary>
    [TestFixture]
    public sealed class MoneySinksTests
    {
        private const int MinutesPerDay = 1440;

        private static readonly ItemTypeId Apples = new ItemTypeId("item_apple");
        private static readonly ItemTypeId Bread = new ItemTypeId("item_bread_rye");
        private static readonly ItemTypeId Iron = new ItemTypeId("item_iron_stock");
        private static readonly LocationId Market = new LocationId("loc_square");
        private static readonly NpcId NpcA = new NpcId("npc_test_alda");
        private static readonly NpcId NpcB = new NpcId("npc_test_bram");

        private static ItemCatalog SinkCatalog() => new ItemCatalog(new[]
        {
            // Approved numbers from Content/items/items.json: apples 10 fresh / 10 stale
            // at half price, rye bread 4 fresh / 4 stale at half price.
            new ItemDefinition(Apples, "Apple", "food", 4, 1, hungerEffect: 10,
                perishable: new PerishableInfo(10, 10, 50, "mushy apple", "rotten apple",
                    staleHungerEffect: -5)),
            new ItemDefinition(Bread, "Rye bread", "food", 8, 1, hungerEffect: 20,
                perishable: new PerishableInfo(4, 4, 50, "stale loaf", "moldy loaf",
                    staleHungerEffect: -10)),
            new ItemDefinition(Iron, "Iron stock", "material", 10, 1),
        });

        private static WorldState SinkWorld(out World world, ulong seed = 42, long startDay = 1)
        {
            var state = new WorldState(seed, new GameTime((startDay - 1) * MinutesPerDay));
            world = new World(state);
            return state;
        }

        private static void TickDays(World world, int days)
        {
            for (int i = 0; i < days * MinutesPerDay; i++) world.Tick();
        }

        private static int CountEvents(WorldState state, WorldEventType type) =>
            state.Events.Query(null, null, null, type).Count;

        // -- PerishableInfo -----------------------------------------------------

        [Test]
        public void FreshnessBoundaries()
        {
            var apples = new PerishableInfo(10, 10, 50);
            Assert.That(apples.FreshnessAtAge(0), Is.EqualTo(Freshness.Fresh));
            Assert.That(apples.FreshnessAtAge(10), Is.EqualTo(Freshness.Fresh));
            Assert.That(apples.FreshnessAtAge(11), Is.EqualTo(Freshness.Stale));
            Assert.That(apples.FreshnessAtAge(20), Is.EqualTo(Freshness.Stale));
            Assert.That(apples.IsSpoiledAtAge(20), Is.False);
            Assert.That(apples.IsSpoiledAtAge(21), Is.True);

            Assert.Throws<ArgumentOutOfRangeException>(() => new PerishableInfo(0, 10, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PerishableInfo(10, 0, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PerishableInfo(10, 10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PerishableInfo(10, 10, 100));
        }

        // -- Inventory lots -----------------------------------------------------

        [Test]
        public void LotsTurnStaleThenSpoilAndVanish()
        {
            var inventory = new Inventory(SinkCatalog());
            inventory.Add(Apples, 5);

            // 10 days fresh...
            for (int day = 0; day < 10; day++)
            {
                inventory.AgeOneDay(out var stale, out var spoiled);
                Assert.That(stale, Is.Empty);
                Assert.That(spoiled, Is.Empty);
            }
            Assert.That(inventory.Count(Apples), Is.EqualTo(5));

            // ...day 11 turns stale, all five at once (one lot, one age).
            inventory.AgeOneDay(out var turnedStale, out var spoiledNow);
            Assert.That(turnedStale, Has.Count.EqualTo(1));
            Assert.That(turnedStale[0].Key, Is.EqualTo(Apples));
            Assert.That(turnedStale[0].Value, Is.EqualTo(5));
            Assert.That(spoiledNow, Is.Empty);

            // Days 12-20 stale but present; day 21 spoils them into the compost.
            for (int day = 0; day < 9; day++)
            {
                inventory.AgeOneDay(out var s, out var sp);
                Assert.That(s, Is.Empty);
                Assert.That(sp, Is.Empty);
            }
            Assert.That(inventory.Count(Apples), Is.EqualTo(5));
            inventory.AgeOneDay(out var staleLast, out var spoiledLast);
            Assert.That(staleLast, Is.Empty);
            Assert.That(spoiledLast[0].Value, Is.EqualTo(5));
            Assert.That(inventory.Count(Apples), Is.EqualTo(0));
        }

        [Test]
        public void LotsAgeIndependentlyAndRemoveOldestFirst()
        {
            var inventory = new Inventory(SinkCatalog());
            inventory.Add(Apples, 3);
            for (int day = 0; day < 5; day++)
                inventory.AgeOneDay(out _, out _);
            inventory.Add(Apples, 4); // a fresher lot joins an older one

            Assert.That(inventory.TryRemove(Apples, 2), Is.True); // takes from the old lot
            Assert.That(inventory.Count(Apples), Is.EqualTo(5));

            // The old lot (now age 5 + 6 more days) turns stale on day 11 of its life...
            for (int day = 0; day < 5; day++)
                inventory.AgeOneDay(out _, out _);
            inventory.AgeOneDay(out var turnedStale, out _);
            Assert.That(turnedStale[0].Value, Is.EqualTo(1)); // only the old lot's survivor
        }

        [Test]
        public void TransferPreservesLotAges()
        {
            var catalog = SinkCatalog();
            var source = new Inventory(catalog);
            var destination = new Inventory(catalog);
            source.Add(Apples, 6);
            for (int day = 0; day < 11; day++)
                source.AgeOneDay(out _, out _); // the whole lot is stale now

            Assert.That(source.TransferTo(destination, Apples, 6), Is.True);
            // The buyer receives stale apples: the oldest lot prices the sale down.
            Assert.That(destination.EffectiveUnitPrice(Apples, 4), Is.EqualTo(2));
        }

        [Test]
        public void NonPerishableGoodsNeverAge()
        {
            var inventory = new Inventory(SinkCatalog());
            inventory.Add(Iron, 7);
            for (int day = 0; day < 400; day++)
            {
                inventory.AgeOneDay(out var stale, out var spoiled);
                Assert.That(stale, Is.Empty);
                Assert.That(spoiled, Is.Empty);
            }
            Assert.That(inventory.Count(Iron), Is.EqualTo(7));
            Assert.That(inventory.EffectiveUnitPrice(Iron, 10), Is.EqualTo(10));
        }

        [Test]
        public void LotRoundTripThroughCapture()
        {
            var catalog = SinkCatalog();
            var inventory = new Inventory(catalog);
            inventory.Add(Apples, 3);
            for (int day = 0; day < 6; day++)
                inventory.AgeOneDay(out _, out _);
            inventory.Add(Bread, 2);

            var restored = new Inventory(catalog);
            restored.RestoreLots(inventory.CaptureLots());
            Assert.That(restored.Count(Apples), Is.EqualTo(3));
            Assert.That(restored.Count(Bread), Is.EqualTo(2));
            // Ages survived the trip: the apples are 4 days from stale, the bread fresh.
            for (int day = 0; day < 4; day++)
                restored.AgeOneDay(out var stale, out _);
            restored.AgeOneDay(out var turnedStale, out _);
            Assert.That(turnedStale[0].Key, Is.EqualTo(Apples));
        }

        // -- Stale pricing and effects ------------------------------------------

        [Test]
        public void StaleOldestLotMarksDownThePrice()
        {
            var catalog = SinkCatalog();
            var stock = new Inventory(catalog);
            stock.Add(Apples, 4);
            Assert.That(stock.EffectiveUnitPrice(Apples, 4), Is.EqualTo(4)); // fresh

            for (int day = 0; day < 11; day++)
                stock.AgeOneDay(out _, out _);
            Assert.That(stock.EffectiveUnitPrice(Apples, 4), Is.EqualTo(2)); // half price
            Assert.That(stock.EffectiveUnitPrice(Apples, 1), Is.EqualTo(1)); // never below 1
        }

        [Test]
        public void ShopChargesStalePriceForStaleStock()
        {
            var catalog = SinkCatalog();
            var stock = new Inventory(catalog);
            stock.Add(Apples, 4);
            for (int day = 0; day < 11; day++)
                stock.AgeOneDay(out _, out _);
            var till = new Wallet();
            var shop = new Shop(Market, NpcA, stock, till,
                new[] { new KeyValuePair<ItemTypeId, int>(Apples, 4) });

            var buyer = new Inventory(catalog);
            var buyerWallet = new Wallet(100);
            WorldState purchaseState = SinkWorld(out World _);
            PurchaseResult result = shop.Purchase(purchaseState, new PurchaseRequest(
                ActorId.ForNpc(NpcB), buyer, buyerWallet, Apples, 2, allowPartial: false));

            Assert.That(result.Outcome, Is.EqualTo(PurchaseOutcome.Full));
            Assert.That(result.PaidCopper, Is.EqualTo(4)); // 2 stale apples at 2, not 8
            Assert.That(buyerWallet.Balance, Is.EqualTo(96));
        }

        [Test]
        public void ConsumptionReplacesEffectsWhenStale()
        {
            ItemDefinition apples = SinkCatalog()[Apples];
            Consumption.EffectiveEffects(apples, Freshness.Fresh, out int? freshHunger, out _);
            Assert.That(freshHunger, Is.EqualTo(10));
            Consumption.EffectiveEffects(apples, Freshness.Stale, out int? staleHunger, out _);
            Assert.That(staleHunger, Is.EqualTo(-5)); // the approved staleEffect, not added

            ItemDefinition iron = SinkCatalog()[Iron];
            Consumption.EffectiveEffects(iron, Freshness.Stale, out int? ironHunger, out _);
            Assert.That(ironHunger, Is.Null); // iron has no stale effect
        }

        // -- Taxes ---------------------------------------------------------------

        private static WorldState TaxWorld(out World world, out Wallet walletA, out Wallet walletB,
            int baseA = 100, int baseB = 200, int balanceA = 1000, int balanceB = 1000)
        {
            WorldState state = SinkWorld(out world);
            walletA = new Wallet(balanceA);
            walletB = new Wallet(balanceB);
            state.Belongings.Register(ActorId.ForNpc(NpcA), new Inventory(SinkCatalog()), walletA);
            state.Belongings.Register(ActorId.ForNpc(NpcB), new Inventory(SinkCatalog()), walletB);
            long baseline = ProsperityIndex.TotalVillageCopper(state);
            state.RestoreEconomyBaseline(new EconomyBaselineState(initialized: true, baselineCopper: baseline));
            state.RestoreTax(new TaxState(initialized: true, lastCollectionDay: 0));
            world.RegisterSystem(new TaxSystem(new[]
            {
                new TaxConfiguration("tax", new[]
                {
                    new TaxAssessment(NpcA, walletA, baseA),
                    new TaxAssessment(NpcB, walletB, baseB),
                }, Market, EventVisibility.Normal),
            }));
            return state;
        }

        [Test]
        public void TaxCollectedOnDayFifteenAnd195()
        {
            WorldState state = TaxWorld(out World world, out Wallet walletA, out Wallet walletB);
            // Ticking 13 days reaches the start of day 14; the day-15 boundary is the
            // first collection, so nothing may be collected before it.
            TickDays(world, 13);
            Assert.That(CountEvents(state, WorldEventType.Purchase), Is.EqualTo(0));

            TickDays(world, 1); // day 15: the reeve collects
            Assert.That(walletA.Balance, Is.EqualTo(900));
            Assert.That(walletB.Balance, Is.EqualTo(800));
            var payments = state.Events.Query(null, null, null, WorldEventType.Purchase).ToList();
            Assert.That(payments, Has.Count.EqualTo(2));
            Assert.That(payments.All(e => e.Quantity == 0), Is.True); // money without goods

            // No second collection until the spring date (day 195).
            TickDays(world, 179);
            Assert.That(CountEvents(state, WorldEventType.Purchase), Is.EqualTo(2));
            TickDays(world, 1);
            Assert.That(CountEvents(state, WorldEventType.Purchase), Is.EqualTo(4));
            // The spring assessment is smaller: the village is poorer after the autumn
            // collection, so the stabilizer scales it down (100 x 1700/2000 = 85).
            Assert.That(walletA.Balance, Is.EqualTo(815));
        }

        [Test]
        public void TaxScalesWithProsperityAndClamps()
        {
            // A rich year doubles the assessment; a poor year halves it — never beyond.
            WorldState rich = TaxWorld(out World richWorld, out Wallet richA, out _);
            richA.Credit(18000); // total now 10x the captured baseline
            TickDays(richWorld, 15);
            Assert.That(richA.Balance, Is.EqualTo(19000 - 200)); // 100 base, doubled

            WorldState poor = TaxWorld(out World poorWorld, out _, out Wallet poorB,
                balanceB: 5000);
            Assert.That(poorB.TryDebit(4500), Is.True); // lean year: total far below baseline
            TickDays(poorWorld, 15);
            Assert.That(poorB.Balance, Is.EqualTo(500 - 100)); // 200 base, halved to 100
        }

        [Test]
        public void TaxShortfallIsVisibleNotSilent()
        {
            WorldState state = TaxWorld(out World world, out Wallet walletA, out _,
                balanceA: 30); // cannot cover the 100 assessment
            TickDays(world, 15);
            Assert.That(walletA.Balance, Is.EqualTo(30)); // untouched
            var failures = state.Events.Query(null, null, null, WorldEventType.FailedPurchase).ToList();
            Assert.That(failures, Has.Count.EqualTo(1));
            Assert.That(failures[0].Copper, Is.EqualTo(100));
            Assert.That(CountEvents(state, WorldEventType.Purchase), Is.EqualTo(1)); // B still paid
        }

        [Test]
        public void TaxScheduleIsDeterministic()
        {
            Assert.That(TaxSystem.NextCollectionAfter(0), Is.EqualTo(15));
            Assert.That(TaxSystem.NextCollectionAfter(1), Is.EqualTo(15));
            Assert.That(TaxSystem.NextCollectionAfter(15), Is.EqualTo(195));
            Assert.That(TaxSystem.NextCollectionAfter(194), Is.EqualTo(195));
            Assert.That(TaxSystem.NextCollectionAfter(195), Is.EqualTo(375)); // next autumn
            Assert.That(TaxSystem.NextCollectionAfter(360), Is.EqualTo(375));
        }

        // -- Adaptive imports -----------------------------------------------------

        [Test]
        public void ImportTargetsScaleWithProsperity()
        {
            WorldState state = SinkWorld(out _);
            var till = new Wallet(10000);
            var stock = new Inventory(SinkCatalog());
            var offer = new MerchantImportOffer(NpcA, stock, till, Iron, 20, 10, Market);
            var policy = new ProsperityImportPolicy();

            state.RestoreEconomyBaseline(new EconomyBaselineState(initialized: true, baselineCopper: 1000));
            state.Belongings.Register(ActorId.ForNpc(NpcA), new Inventory(SinkCatalog()), new Wallet(2000));
            // Total 12000 vs baseline 1000 -> clamped to 150%.
            Assert.That(policy.ScaledTarget(offer, state), Is.EqualTo(30));

            state.Belongings.Register(ActorId.ForNpc(NpcB), new Inventory(SinkCatalog()), new Wallet(0));
            // Still rich: the floor only binds on the poor side.
            var poorState = SinkWorld(out _);
            poorState.Belongings.Register(ActorId.ForNpc(NpcA), new Inventory(SinkCatalog()), new Wallet(100));
            poorState.RestoreEconomyBaseline(new EconomyBaselineState(initialized: true, baselineCopper: 1000));
            // Total 100 vs baseline 1000 -> 10%, clamped to 50%.
            Assert.That(policy.ScaledTarget(offer, poorState), Is.EqualTo(10));

            // Without a baseline the fixed target stands.
            var bareState = SinkWorld(out _);
            Assert.That(policy.ScaledTarget(offer, bareState), Is.EqualTo(20));
        }

        [Test]
        public void ExhaustionGatedIronIgnoresProsperity()
        {
            WorldState state = SinkWorld(out _);
            state.Belongings.Register(ActorId.ForNpc(NpcA), new Inventory(SinkCatalog()), new Wallet(5000));
            state.RestoreEconomyBaseline(new EconomyBaselineState(initialized: true, baselineCopper: 1000));
            var offer = new MerchantImportOffer(NpcA, new Inventory(SinkCatalog()), new Wallet(),
                Iron, 20, 10, Market, requiresExhaustionOrder: true);
            // Doran's iron is need-based: forty bars at need, whatever the weather.
            Assert.That(new ProsperityImportPolicy().ScaledTarget(offer, state), Is.EqualTo(20));
        }

        // -- Community fund --------------------------------------------------------

        [Test]
        public void CommunityFundMovesMonthlyAndFeastsInAutumn()
        {
            WorldState state = SinkWorld(out World world);
            var fund = new VillageFundState(initialized: true, startingCopper: 10000);
            state.RestoreVillageFund(fund);
            state.RestoreCommunityFund(new CommunityFundState(initialized: true,
                lastMonthlyDay: 1, lastFeastYear: 0));
            world.RegisterSystem(new CommunityFundSystem(new[]
            {
                new CommunityFundConfiguration("fund", 150, 200, Market, EventVisibility.Normal),
            }));

            TickDays(world, 30); // one month: 150 set aside
            Assert.That(state.CommunityFund.CommunityPot.Balance, Is.EqualTo(150));
            // Autumn day 1 is also the feast reserve: 200 more, in the feast pot.
            Assert.That(state.CommunityFund.FeastPot.Balance, Is.EqualTo(200));

            TickDays(world, 30); // second month
            Assert.That(state.CommunityFund.CommunityPot.Balance, Is.EqualTo(300));
            Assert.That(state.CommunityFund.FeastPot.Balance, Is.EqualTo(200)); // once a year
        }

        [Test]
        public void CommunityFundTakesOnlyWhatIsThere()
        {
            WorldState state = SinkWorld(out World world);
            state.RestoreVillageFund(new VillageFundState(initialized: true, startingCopper: 100));
            state.RestoreCommunityFund(new CommunityFundState(initialized: true,
                lastMonthlyDay: 1, lastFeastYear: 0));
            world.RegisterSystem(new CommunityFundSystem(new[]
            {
                new CommunityFundConfiguration("fund", 150, 200, Market, EventVisibility.Normal),
            }));

            TickDays(world, 30);
            // The feast reserve fires first on autumn day 1 and takes the whole lean
            // fund; the monthly share then finds nothing left — and the fund never goes
            // negative.
            Assert.That(state.CommunityFund.FeastPot.Balance, Is.EqualTo(100));
            Assert.That(state.CommunityFund.CommunityPot.Balance, Is.EqualTo(0));
            Assert.That(state.VillageFund.Funds.Balance, Is.EqualTo(0));
        }

        [Test]
        public void FundPotsStayInTheVillageTotal()
        {
            WorldState state = SinkWorld(out World world);
            var wallet = new Wallet(5000);
            state.Belongings.Register(ActorId.ForNpc(NpcA), new Inventory(SinkCatalog()), wallet);
            long before = ProsperityIndex.TotalVillageCopper(state);
            state.RestoreVillageFund(new VillageFundState(initialized: true, startingCopper: 1000));
            state.RestoreCommunityFund(new CommunityFundState(initialized: true,
                lastMonthlyDay: 1, lastFeastYear: 0));
            world.RegisterSystem(new CommunityFundSystem(new[]
            {
                new CommunityFundConfiguration("fund", 150, 200, Market, EventVisibility.Normal),
            }));

            TickDays(world, 30);
            // Saving is not spending: the village total only moved by the feast? No — the
            // feast pot is savings too. The total is unchanged by either movement.
            Assert.That(ProsperityIndex.TotalVillageCopper(state), Is.EqualTo(before + 1000));
        }

        // -- Spoilage system ---------------------------------------------------------

        [Test]
        public void SpoilageSystemAgesEveryInventoryOnce()
        {
            WorldState state = SinkWorld(out World world);
            ItemCatalog catalog = SinkCatalog();
            var home = new Inventory(catalog);
            home.Add(Apples, 5);
            state.Belongings.Register(ActorId.ForNpc(NpcA), home, new Wallet(100));
            state.RestoreSpoilage(new SpoilageState(initialized: true, lastAgedDay: 1));
            world.RegisterSystem(new SpoilageSystem(new[]
            {
                new SpoilageConfiguration("spoilage", catalog, Market, EventVisibility.Quiet),
            }));

            TickDays(world, 11);
            var staleEvents = state.Events.Query(null, null, null, WorldEventType.TurnedStale).ToList();
            Assert.That(staleEvents, Has.Count.EqualTo(1));
            Assert.That(staleEvents[0].Quantity, Is.EqualTo(5));
            Assert.That(staleEvents[0].Visibility, Is.EqualTo(EventVisibility.Quiet));

            TickDays(world, 10);
            var spoiledEvents = state.Events.Query(null, null, null, WorldEventType.Spoiled).ToList();
            Assert.That(spoiledEvents, Has.Count.EqualTo(1));
            Assert.That(spoiledEvents[0].Quantity, Is.EqualTo(5));
            // Destroyed value is the last realizable one: 5 stale apples at 2 copper.
            Assert.That(spoiledEvents[0].Copper, Is.EqualTo(10));
            Assert.That(home.Count(Apples), Is.EqualTo(0));
        }

        [Test]
        public void SpoilageDedupesSharedInventories()
        {
            WorldState state = SinkWorld(out World world);
            ItemCatalog catalog = SinkCatalog();
            var shared = new Inventory(catalog);
            shared.Add(Bread, 4);
            state.Belongings.Register(ActorId.ForNpc(NpcA), shared, new Wallet(100));
            var shop = new Shop(Market, NpcA, shared, new Wallet(),
                new[] { new KeyValuePair<ItemTypeId, int>(Bread, 8) });
            state.Shops.Register(shop);
            state.RestoreSpoilage(new SpoilageState(initialized: true, lastAgedDay: 1));
            world.RegisterSystem(new SpoilageSystem(new[]
            {
                new SpoilageConfiguration("spoilage", catalog, Market, EventVisibility.Quiet),
            }));

            // Bread: 4 fresh, 4 stale, then spoiled. One shared inventory ages once.
            TickDays(world, 9);
            Assert.That(CountEvents(state, WorldEventType.Spoiled), Is.EqualTo(1));
            Assert.That(shared.Count(Bread), Is.EqualTo(0));
        }

        [Test]
        public void SpoilageStaysQuietUntilInitialized()
        {
            WorldState state = SinkWorld(out World world);
            ItemCatalog catalog = SinkCatalog();
            var home = new Inventory(catalog);
            home.Add(Apples, 5);
            state.Belongings.Register(ActorId.ForNpc(NpcA), home, new Wallet(100));
            world.RegisterSystem(new SpoilageSystem(new[]
            {
                new SpoilageConfiguration("spoilage", catalog, Market, EventVisibility.Quiet),
            }));

            TickDays(world, 30); // uninitialized: nothing ages
            Assert.That(home.Count(Apples), Is.EqualTo(5));
            Assert.That(CountEvents(state, WorldEventType.Spoiled), Is.EqualTo(0));
        }

        // -- Month-long stability -----------------------------------------------------

        [Test]
        public void AutumnMonthStaysWithinTenPercentBand()
        {
            // The ECONOMY.md acceptance run: a full autumn month with every source and
            // sink wired must leave the village total within ±10% of the baseline.
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            WorldState state = SinkWorld(out World world, seed: 7);
            Shop store = GeneralStoreSetup.Stock(state, catalog);
            Shop smithy = SmithySetup.Stock(state, catalog).SmithyShop;
            MoneySourcesSetup.MoneySourcesHandle money =
                MoneySourcesSetup.Stock(state, catalog, store, smithy);
            MoneySinksSetup.MoneySinksHandle sinks =
                MoneySinksSetup.Stock(state, catalog, money);

            world.RegisterSystem(new MerchantSystem(new[] { sinks.MerchantVisitsAdaptive() }));
            world.RegisterSystem(new TravelerSpendSystem(new[] { money.TravelerSpend() }));
            world.RegisterSystem(new WolfBountySystem(new[] { money.WolfBounties() }));
            world.RegisterSystem(new VillageFundSystem(new[] { money.VillageFund() }));
            world.RegisterSystem(new TaxSystem(new[] { sinks.Taxes() }));
            world.RegisterSystem(new CommunityFundSystem(new[] { sinks.CommunityFund() }));
            world.RegisterSystem(new SpoilageSystem(new[] { sinks.Spoilage() }));

            long baseline = state.EconomyBaseline.BaselineCopper;
            Assert.That(baseline, Is.GreaterThan(0));
            long band = baseline / 10;

            // Sanity: the assessment table really is the documented ~1200.
            int tableTotal = sinks.Taxes().Assessments.Sum(a => a.BaseCopper);
            Assert.That(tableTotal, Is.GreaterThanOrEqualTo(1150).And.LessThanOrEqualTo(1250));

            TickDays(world, 30);

            long total = ProsperityIndex.TotalVillageCopper(state);
            Assert.That(total, Is.GreaterThanOrEqualTo(baseline - band),
                $"Village total {total} fell more than 10% below baseline {baseline}.");
            Assert.That(total, Is.LessThanOrEqualTo(baseline + band),
                $"Village total {total} rose more than 10% above baseline {baseline}.");
        }

        [Test]
        public void MoneySinksSetupInitializesEveryCursor()
        {
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            WorldState state = SinkWorld(out World _, seed: 3);
            Shop store = GeneralStoreSetup.Stock(state, catalog);
            Shop smithy = SmithySetup.Stock(state, catalog).SmithyShop;
            var money = MoneySourcesSetup.Stock(state, catalog, store, smithy);
            MoneySinksSetup.MoneySinksHandle sinks = MoneySinksSetup.Stock(state, catalog, money);

            Assert.That(state.EconomyBaseline.IsInitialized, Is.True);
            Assert.That(state.EconomyBaseline.BaselineCopper,
                Is.EqualTo(ProsperityIndex.TotalVillageCopper(state)));
            Assert.That(state.Tax.IsInitialized, Is.True);
            Assert.That(state.Tax.LastCollectionDay, Is.EqualTo(0));
            Assert.That(state.CommunityFund.IsInitialized, Is.True);
            Assert.That(state.CommunityFund.LastMonthlyDay, Is.EqualTo(1));
            Assert.That(state.Spoilage.IsInitialized, Is.True);
            Assert.That(state.Spoilage.LastAgedDay, Is.EqualTo(1));

            // The handle's configurations are all valid and complete.
            Assert.That(sinks.Taxes().Assessments, Has.Count.EqualTo(20));
            Assert.That(sinks.CommunityFund().MonthlyCopper, Is.EqualTo(150));
            Assert.That(sinks.CommunityFund().FeastCopper, Is.EqualTo(200));
            Assert.That(sinks.Spoilage().Visibility, Is.EqualTo(EventVisibility.Quiet));
            Assert.That(sinks.MerchantVisitsAdaptive().OrderPolicy, Is.Not.Null);
            // ...while the plain P2-08 configuration keeps its fixed targets.
            Assert.That(money.MerchantVisits().OrderPolicy, Is.Null);
        }
    }
}
