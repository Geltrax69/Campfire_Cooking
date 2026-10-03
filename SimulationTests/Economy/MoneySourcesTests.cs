using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Proves the money gates: the merchant buys village goods and sells imports (iron for
    /// Doran, Tilda's order list), traveler spend follows the seasons, wolf bounties pay in
    /// winter only, the village fund stays solvent, and every copper is conserved.
    /// </summary>
    public sealed class MoneySourcesTests
    {
        private const int MinutesPerDay = 1440;

        private static WorldState MoneyWorld(out World world,
            out MoneySourcesSetup.MoneySourcesHandle money,
            out Shop store, out Shop smithy, ulong seed, long startDay = 1,
            bool registerSystems = true)
        {
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var state = new WorldState(seed, new GameTime((startDay - 1) * MinutesPerDay));
            store = GeneralStoreSetup.Stock(state, catalog);
            smithy = SmithySetup.Stock(state, catalog).SmithyShop;
            money = MoneySourcesSetup.Stock(state, catalog, store, smithy);
            world = new World(state);
            if (registerSystems)
            {
                world.RegisterSystem(new MerchantSystem(new[] { money.MerchantVisits() }));
                world.RegisterSystem(new TravelerSpendSystem(new[] { money.TravelerSpend() }));
                world.RegisterSystem(new WolfBountySystem(new[] { money.WolfBounties() }));
                world.RegisterSystem(new VillageFundSystem(new[] { money.VillageFund() }));
            }
            return state;
        }

        private static void TickDays(World world, int days)
        {
            for (int i = 0; i < days * MinutesPerDay; i++) world.Tick();
        }

        private static int CountEvents(WorldState state, WorldEventType type) =>
            state.Events.Query(null, null, null, type).Count;

        private static int CountArrivals(WorldState state) =>
            state.Events.Query(null, null, MoneySourcesSetup.Market, WorldEventType.Arrival).Count;

        [Test]
        public void VillageCalendarSeasons()
        {
            Assert.That(VillageCalendar.SeasonAt(new GameTime(0)), Is.EqualTo(Season.Autumn));
            Assert.That(VillageCalendar.SeasonAt(new GameTime(90 * MinutesPerDay)), Is.EqualTo(Season.Winter));
            Assert.That(VillageCalendar.SeasonAt(new GameTime(180 * MinutesPerDay)), Is.EqualTo(Season.Spring));
            Assert.That(VillageCalendar.SeasonAt(new GameTime(270 * MinutesPerDay)), Is.EqualTo(Season.Summer));
            Assert.That(VillageCalendar.SeasonAt(new GameTime(360 * MinutesPerDay)), Is.EqualTo(Season.Autumn),
                "The year wraps back to autumn.");
            Assert.That(VillageCalendar.FirstSpringDayOnOrAfter(100), Is.EqualTo(181));
            Assert.That(VillageCalendar.FirstSpringDayOnOrAfter(200), Is.EqualTo(541));
        }

        [Test]
        public void MerchantVisitBuysGoodsAndSellsIron()
        {
            // Isolated: the merchant's own economics, without levies or traveler coin.
            WorldState state = MoneyWorld(out World world, out MoneySourcesSetup.MoneySourcesHandle money,
                out Shop store, out Shop smithy, 42, registerSystems: false);
            world.RegisterSystem(new MerchantSystem(new[] { money.MerchantVisits() }));

            // Doran's forge is cold: the exhaustion order is pending and the iron is gone.
            Assert.That(smithy.Stock.TryRemove(SmithySetup.Iron, 20), Is.True);
            state.RestoreSmithy(new SmithyState(ironExhaustionOrdered: true));
            // Tilda has sold 10 salt since opening; the merchant should top her back up.
            Assert.That(store.Stock.TryRemove(MoneySourcesSetup.Salt, 10), Is.True);

            int miraBefore = money.Wallets[MoneySourcesSetup.Mira].Balance;
            int tildaBefore = money.TildaTill.Balance;
            int doranBefore = money.DoranTill.Balance;

            TickDays(world, 25);
            Assert.That(CountArrivals(state), Is.EqualTo(1), "One merchant visit in 25 autumn days.");

            // The merchant bought every export stock: 45 apples @2, 10 logs @6, 10 honey @6,
            // 5 pelts @8, 10 smoked fish @3 = 280 copper, inside the ~800 budget.
            Assert.That(money.ExportStocks[MoneySourcesSetup.Mira].Count(MoneySourcesSetup.Apples), Is.EqualTo(0));
            Assert.That(money.ExportStocks[MoneySourcesSetup.Tam].Count(MoneySourcesSetup.Logs), Is.EqualTo(0));
            Assert.That(money.Wallets[MoneySourcesSetup.Mira].Balance, Is.EqualTo(miraBefore + 90));
            Assert.That(money.Wallets[MoneySourcesSetup.Tam].Balance, Is.EqualTo(350 + 60));
            Assert.That(money.Wallets[MoneySourcesSetup.Maren].Balance, Is.EqualTo(200 + 60));
            Assert.That(money.Wallets[MoneySourcesSetup.Ralf].Balance, Is.EqualTo(250 + 40));
            Assert.That(money.Wallets[MoneySourcesSetup.Bessa].Balance, Is.EqualTo(1100 + 30));

            // The iron order is fulfilled: 20 kg arrive, 200 copper leave, exhaustion clears.
            Assert.That(smithy.Stock.Count(SmithySetup.Iron), Is.EqualTo(20));
            Assert.That(money.DoranTill.Balance, Is.EqualTo(doranBefore - 200));
            Assert.That(state.Smithy.IronExhaustionOrdered, Is.False);

            // Tilda's salt is restocked to its opening level at baseValue.
            Assert.That(store.Stock.Count(MoneySourcesSetup.Salt), Is.EqualTo(24));
            Assert.That(money.TildaTill.Balance, Is.EqualTo(tildaBefore - 60));
        }

        [Test]
        public void MerchantSkipsWinter()
        {
            WorldState state = MoneyWorld(out World world, out _, out _, out _, 7, startDay: 85);
            state.RestoreMerchantSchedule(new MerchantScheduleState(initialized: true, nextVisitDay: 95));

            TickDays(world, 20); // to day 105, mid-winter.

            Assert.That(CountArrivals(state), Is.EqualTo(0), "No merchant crosses the high winter ford.");
            Assert.That(state.MerchantSchedule.NextVisitDay, Is.EqualTo(181),
                "The missed visit is pushed to the first day of spring.");
        }

        [Test]
        public void TravelerSpendFollowsTheSeasons()
        {
            // Isolated: traveler coin only, so the monthly totals are exact.
            // Summer month: 29 days of ticks pays days 271-300, exactly one 30-day month.
            WorldState summer = MoneyWorld(out World summerWorld, out MoneySourcesSetup.MoneySourcesHandle summerMoney,
                out _, out _, 11, startDay: 271, registerSystems: false);
            summerWorld.RegisterSystem(new TravelerSpendSystem(new[] { summerMoney.TravelerSpend() }));
            int bessaBefore = summerMoney.Wallets[MoneySourcesSetup.Bessa].Balance;
            int tildaBefore = summerMoney.TildaTill.Balance;
            TickDays(summerWorld, 29);
            int summerTotal = (summerMoney.Wallets[MoneySourcesSetup.Bessa].Balance - bessaBefore)
                + (summerMoney.TildaTill.Balance - tildaBefore);
            Assert.That(summerTotal, Is.EqualTo(900), "A summer month brings ~900 copper.");

            // Winter month: the road is empty.
            WorldState winter = MoneyWorld(out World winterWorld, out MoneySourcesSetup.MoneySourcesHandle winterMoney,
                out _, out _, 11, startDay: 91, registerSystems: false);
            winterWorld.RegisterSystem(new TravelerSpendSystem(new[] { winterMoney.TravelerSpend() }));
            int wbBefore = winterMoney.Wallets[MoneySourcesSetup.Bessa].Balance;
            int wtBefore = winterMoney.TildaTill.Balance;
            TickDays(winterWorld, 29);
            Assert.That((winterMoney.Wallets[MoneySourcesSetup.Bessa].Balance - wbBefore)
                + (winterMoney.TildaTill.Balance - wtBefore), Is.EqualTo(0));
        }

        [Test]
        public void TravelerSpendIsDeterministic()
        {
            WorldState a = MoneyWorld(out World worldA, out MoneySourcesSetup.MoneySourcesHandle moneyA,
                out _, out _, 99, startDay: 271);
            WorldState b = MoneyWorld(out World worldB, out MoneySourcesSetup.MoneySourcesHandle moneyB,
                out _, out _, 99, startDay: 271);
            TickDays(worldA, 10);
            TickDays(worldB, 10);
            Assert.That(moneyA.Wallets[MoneySourcesSetup.Bessa].Balance,
                Is.EqualTo(moneyB.Wallets[MoneySourcesSetup.Bessa].Balance));
            Assert.That(CountEvents(a, WorldEventType.Purchase),
                Is.EqualTo(CountEvents(b, WorldEventType.Purchase)));
        }

        [Test]
        public void WolfBountyPaysInWinterOnly()
        {
            // Isolated: bounty coin only, so Ralf's wallet proves the 50-copper rate.
            WorldState state = MoneyWorld(out World world, out MoneySourcesSetup.MoneySourcesHandle money,
                out _, out _, 5, startDay: 91, registerSystems: false);
            world.RegisterSystem(new WolfBountySystem(new[] { money.WolfBounties() }));
            int ralfBefore = money.Wallets[MoneySourcesSetup.Ralf].Balance;

            TickDays(world, 89); // the full winter: days 91-180.
            int bountyEvents = state.Events.Query(null, null, MoneySourcesSetup.ForestEdge,
                WorldEventType.Purchase).Count;
            Assert.That(bountyEvents, Is.InRange(2, 4), "2-4 wolf incidents per winter (ECONOMY.md).");
            Assert.That(money.Wallets[MoneySourcesSetup.Ralf].Balance,
                Is.EqualTo(ralfBefore + bountyEvents * 50),
                "50 copper per pelt, from the crown's gate.");
            foreach (WorldEvent e in state.Events.Query(null, null, MoneySourcesSetup.ForestEdge,
                WorldEventType.Purchase))
                Assert.That(VillageCalendar.SeasonAt(e.Time), Is.EqualTo(Season.Winter));

            // Summer control: the system runs, but the season guard pays nothing.
            WorldState summer = MoneyWorld(out World summerWorld, out MoneySourcesSetup.MoneySourcesHandle summerMoney,
                out _, out _, 5, startDay: 271, registerSystems: false);
            summerWorld.RegisterSystem(new WolfBountySystem(new[] { summerMoney.WolfBounties() }));
            TickDays(summerWorld, 30);
            Assert.That(summer.Events.Query(null, null, MoneySourcesSetup.ForestEdge,
                WorldEventType.Purchase).Count, Is.EqualTo(0));
        }

        [Test]
        public void VillageFundStaysSolventForAMonth()
        {
            WorldState state = MoneyWorld(out World world, out MoneySourcesSetup.MoneySourcesHandle money,
                out _, out _, 3);
            int bramBefore = money.Wallets[MoneySourcesSetup.Bram].Balance;
            int elswithBefore = money.Wallets[MoneySourcesSetup.Elswith].Balance;

            TickDays(world, 29); // 29 days of ticks pays 30 days of wages (days 1-30).

            // Income: 4 weekly rounds of (20 payers x 2) + 185 background = 900.
            // Outflow: 30 days of (20 + 8) wages = 840. 600 + 900 - 840 = 660.
            Assert.That(state.VillageFund.Funds.Balance, Is.EqualTo(660),
                "The fund stays solvent through a normal month.");
            Assert.That(money.Wallets[MoneySourcesSetup.Bram].Balance,
                Is.EqualTo(bramBefore + 600 - 8), "30 days' wages minus 4 weekly levies.");
            Assert.That(money.Wallets[MoneySourcesSetup.Elswith].Balance,
                Is.EqualTo(elswithBefore + 240 - 8));
            Assert.That(state.Events.Query(null, null, null, WorldEventType.FailedPurchase).Count,
                Is.EqualTo(0), "Everyone covers the 2-copper levy this month.");
        }

        [Test]
        public void MoneyIsConservedAcrossAllGates()
        {
            WorldState state = MoneyWorld(out World world, out MoneySourcesSetup.MoneySourcesHandle money,
                out Shop store, out Shop smithy, 13);
            // Deplete some imports so the merchant sells on its visit.
            Assert.That(store.Stock.TryRemove(MoneySourcesSetup.Salt, 24), Is.True);
            Assert.That(store.Stock.TryRemove(MoneySourcesSetup.LampOil, 20), Is.True);

            long TotalWealth()
            {
                long total = state.VillageFund.Funds.Balance;
                foreach (NpcBelongingsEntry entry in state.Belongings.Entries)
                    total += entry.Wallet.Balance;
                foreach (Shop shop in state.Shops.Shops)
                    total += shop.OwnerWallet.Balance;
                return total;
            }

            long before = TotalWealth();
            TickDays(world, 30);
            long after = TotalWealth();

            // Every copper in or out passes a named gate; wages and levies are internal.
            long merchantIn = 0, merchantOut = 0, travelerIn = 0, bountyIn = 0, backgroundIn = 0;
            foreach (WorldEvent e in state.Events.Query())
            {
                if (e.Type == WorldEventType.FailedPurchase) continue; // no money moved.
                if (!e.Copper.HasValue || e.Copper.Value == 0) continue;
                bool isMerchantBuy = e.Type == WorldEventType.Purchase
                    && e.Location == MoneySourcesSetup.Market && e.ItemType.HasValue;
                bool isMerchantSale = e.Type == WorldEventType.Restocked;
                bool isTraveler = e.Type == WorldEventType.Purchase
                    && (e.Location == MoneySourcesSetup.Tavern || e.Location == store.Location)
                    && !e.ItemType.HasValue;
                bool isBounty = e.Type == WorldEventType.Purchase
                    && e.Location == MoneySourcesSetup.ForestEdge;
                bool isBackground = e.Type == WorldEventType.Purchase && !e.Actor.HasValue;
                // Wage and levy events (quantity 0, village actors) are internal transfers.
                bool isInternal = e.Type == WorldEventType.Purchase && e.Quantity == 0
                    && e.Actor.HasValue;
                if (isMerchantBuy) merchantIn += e.Copper.Value;
                else if (isMerchantSale) merchantOut += e.Copper.Value;
                else if (isTraveler) travelerIn += e.Copper.Value;
                else if (isBounty) bountyIn += e.Copper.Value;
                else if (isBackground) backgroundIn += e.Copper.Value;
                else if (!isInternal)
                    Assert.Fail($"Unaccounted copper movement: {e.Type} {e.Copper} at {e.Location}.");
            }
            long expected = merchantIn - merchantOut + travelerIn + bountyIn + backgroundIn;
            Assert.That(after - before, Is.EqualTo(expected),
                $"Wealth delta {after - before} must equal the net gate flows {expected}.");
            Assert.That(expected, Is.GreaterThan(0), "The gates are net positive in autumn.");
        }

        [Test]
        public void HarvestPaysHandsInAutumnOnly()
        {
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var state = new WorldState(17, new GameTime(0));
            Shop store = GeneralStoreSetup.Stock(state, catalog);
            Shop smithy = SmithySetup.Stock(state, catalog).SmithyShop;
            MoneySourcesSetup.MoneySourcesHandle money =
                MoneySourcesSetup.Stock(state, catalog, store, smithy);
            var hand = new Wallet(0);
            var config = new HarvestConfiguration("harvest-test", MoneySourcesSetup.Corvin,
                money.Wallets[MoneySourcesSetup.Corvin],
                new List<HarvestHand> { new HarvestHand(MoneySourcesSetup.Jory, hand) }.AsReadOnly(),
                8, MoneySourcesSetup.Farm, EventVisibility.Normal);
            var world = new World(state);
            world.RegisterSystem(new HarvestSystem(new[] { config }));
            int corvinBefore = money.Wallets[MoneySourcesSetup.Corvin].Balance;

            TickDays(world, 9); // 10 days of autumn wages: days 1-10.

            Assert.That(hand.Balance, Is.EqualTo(80), "8 copper/day for 10 autumn days.");
            Assert.That(money.Wallets[MoneySourcesSetup.Corvin].Balance,
                Is.EqualTo(corvinBefore - 80));

            // Winter: the harvest is over, no wages.
            var winterState = new WorldState(17, new GameTime(90 * MinutesPerDay));
            Shop winterStore = GeneralStoreSetup.Stock(winterState, catalog);
            Shop winterSmithy = SmithySetup.Stock(winterState, catalog).SmithyShop;
            MoneySourcesSetup.MoneySourcesHandle winterMoney =
                MoneySourcesSetup.Stock(winterState, catalog, winterStore, winterSmithy);
            var winterHand = new Wallet(0);
            var winterWorld = new World(winterState);
            winterWorld.RegisterSystem(new HarvestSystem(new[]
            {
                new HarvestConfiguration("harvest-winter", MoneySourcesSetup.Corvin,
                    winterMoney.Wallets[MoneySourcesSetup.Corvin],
                    new List<HarvestHand> { new HarvestHand(MoneySourcesSetup.Jory, winterHand) }.AsReadOnly(),
                    8, MoneySourcesSetup.Farm, EventVisibility.Normal)
            }));
            for (int i = 0; i < 10 * MinutesPerDay; i++) winterWorld.Tick();
            Assert.That(winterHand.Balance, Is.EqualTo(0), "No harvest wages outside autumn.");
        }

        [Test]
        public void PayrollTransfersAndLogs()
        {
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var state = new WorldState(1, new GameTime(0));
            var from = new Wallet(100);
            var to = new Wallet(10);
            var at = MoneySourcesSetup.Market;
            var worker = MoneySourcesSetup.Bram;

            Assert.That(Payroll.PayWage(state, from, to, worker, at, 20, EventVisibility.Normal), Is.True);
            Assert.That((from.Balance, to.Balance), Is.EqualTo((80, 30)));
            WorldEvent paid = state.Events.Query().Single();
            Assert.That((paid.Type, paid.Quantity, paid.Copper),
                Is.EqualTo((WorldEventType.Purchase, (int?)0, (int?)20)));

            var poor = new Wallet(5);
            Assert.That(Payroll.ChargeLevy(state, poor, to, worker, at, 20, EventVisibility.Normal), Is.False);
            Assert.That((poor.Balance, to.Balance), Is.EqualTo((5, 30)),
                "A failed levy moves nothing.");
            Assert.That(state.Events.Query(null, null, null, WorldEventType.FailedPurchase).Count,
                Is.EqualTo(1));
        }

        [Test]
        public void RestoredProgressKeepsTheGatesHonest()
        {
            // Until P2-12 writes the new states into the save JSON, the capture/restore
            // convention itself is what this test proves: restored progress drives the
            // systems exactly as live progress would.
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var state = new WorldState(9, new GameTime(0));
            Shop store = GeneralStoreSetup.Stock(state, catalog);
            Shop smithy = SmithySetup.Stock(state, catalog).SmithyShop;
            MoneySourcesSetup.MoneySourcesHandle money =
                MoneySourcesSetup.Stock(state, catalog, store, smithy);
            var world = new World(state);
            world.RegisterSystem(new MerchantSystem(new[] { money.MerchantVisits() }));
            world.RegisterSystem(new VillageFundSystem(new[] { money.VillageFund() }));

            // A restored merchant schedule fires on its day, not before.
            state.RestoreMerchantSchedule(new MerchantScheduleState(initialized: true, nextVisitDay: 5));
            TickDays(world, 3); // days 1-3.
            Assert.That(CountArrivals(state), Is.EqualTo(0));
            TickDays(world, 2); // days 4-5: the visit fires on day 5.
            Assert.That(CountArrivals(state), Is.EqualTo(1));

            // A restored fund resumes from its day cursor: no double-pay for covered days.
            ItemCatalog catalog2 = MillingSystemTests.TestCatalog();
            var state2 = new WorldState(9, new GameTime(9 * MinutesPerDay)); // day 10.
            Shop store2 = GeneralStoreSetup.Stock(state2, catalog2);
            Shop smithy2 = SmithySetup.Stock(state2, catalog2).SmithyShop;
            MoneySourcesSetup.MoneySourcesHandle money2 =
                MoneySourcesSetup.Stock(state2, catalog2, store2, smithy2);
            // Wipe the setup's fresh fund and install one that already paid through day 10.
            state2.RestoreVillageFund(new VillageFundState(initialized: true, startingCopper: 600));
            state2.VillageFund.LastWageDay = 10;
            state2.VillageFund.LastLevyDay = 10;
            state2.VillageFund.LastRetainerDay = 10;
            var world2 = new World(state2);
            world2.RegisterSystem(new VillageFundSystem(new[] { money2.VillageFund() }));
            int bramBefore = money2.Wallets[MoneySourcesSetup.Bram].Balance;
            for (int i = 0; i < MinutesPerDay; i++) world2.Tick(); // day 11.
            Assert.That(money2.Wallets[MoneySourcesSetup.Bram].Balance,
                Is.EqualTo(bramBefore + 20), "Exactly one day's wage: the cursor holds.");
        }

        [Test]
        public void SaveLoadKeepsWalletsTillsAndStocks()
        {
            string root = MillingSystemTests.RepositoryRoot();
            WorldState state = MoneyWorld(out World world, out MoneySourcesSetup.MoneySourcesHandle money,
                out Shop store, out Shop smithy, 21);
            TickDays(world, 5); // wages, levies, traveler payouts move copper around.

            string json = WorldSaver.Save(state);
            WorldState loaded = WorldLoader.Load(json, root);

            // Everything the saver already persists survives: personal wallets, shop tills,
            // export stocks. (Fund/merchant/bounty/traveler progress states follow in P2-12
            // via the Restore convention proven above.)
            foreach (KeyValuePair<NpcId, Wallet> row in money.Wallets)
            {
                Assert.That(loaded.Belongings.TryGet(ActorId.ForNpc(row.Key), out NpcBelongingsEntry entry),
                    Is.True, $"Belongings for {row.Key} survive.");
                Assert.That(entry.Wallet.Balance, Is.EqualTo(row.Value.Balance));
            }
            Assert.That(loaded.Shops[store.Location].OwnerWallet.Balance,
                Is.EqualTo(store.OwnerWallet.Balance));
            Assert.That(loaded.Shops[smithy.Location].OwnerWallet.Balance,
                Is.EqualTo(smithy.OwnerWallet.Balance));
            foreach (KeyValuePair<NpcId, Inventory> row in money.ExportStocks)
            {
                Assert.That(loaded.Belongings.TryGet(ActorId.ForNpc(row.Key), out NpcBelongingsEntry entry),
                    Is.True);
                foreach (KeyValuePair<ItemTypeId, int> content in row.Value.Contents)
                    Assert.That(entry.Inventory.Count(content.Key), Is.EqualTo(content.Value));
            }
        }
    }
}
