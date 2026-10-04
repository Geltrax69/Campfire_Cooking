using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Proves the town-support states (housing, infrastructure, night watch, reputation),
    /// monthly merchant-copper tracking and the wealth formula. P5-01.
    /// </summary>
    public sealed class TownSupportStatesTests
    {
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly ItemTypeId Grain = new ItemTypeId("item_grain");

        private static ItemCatalog Catalog() => new ItemCatalog(new[]
        {
            new ItemDefinition(Apple, "Apple", "food", 3, 1, hungerEffect: -15),
            new ItemDefinition(Grain, "Grain", "material", 8, 1),
        });

        [Test]
        public void HousingStateTracksRoofs()
        {
            var home = new LocationId("loc_home_a");
            var housing = new HousingState(backgroundHouseholds: 2, backgroundSoundRoofs: 1);
            housing.SetRoofCondition(home, 90);
            Assert.That(housing.RoofConditions[home], Is.EqualTo(90));
            // Unknown homes default to a sound roof rather than failing the stat.
            Assert.That(housing.RoofConditionOrDefault(new LocationId("loc_unknown")), Is.EqualTo(100));

            Assert.Throws<ArgumentOutOfRangeException>(() => housing.SetRoofCondition(home, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => housing.SetRoofCondition(home, 101));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HousingState(-1, 0));
            Assert.Throws<ArgumentException>(() => new HousingState(1, 2));
        }

        [Test]
        public void InfrastructureDefaultsMatchDesignStart()
        {
            var infrastructure = new TownInfrastructureState();
            // TOWN.md starting values: wheel 30, palisade 80, well & roads 70, granary 75.
            Assert.That(
                (infrastructure.WheelCondition, infrastructure.PalisadeCondition,
                    infrastructure.WellAndRoadsCondition, infrastructure.GranaryBuildingCondition),
                Is.EqualTo((30, 80, 70, 75)));

            infrastructure.SetWheelCondition(0);
            Assert.That(infrastructure.WheelCondition, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => infrastructure.SetPalisadeCondition(101));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TownInfrastructureState(wheelCondition: -1));
        }

        [Test]
        public void NightWatchDefaultsToInactive()
        {
            var watch = new NightWatchState();
            Assert.That(watch.IsActive, Is.False);
            watch.SetActive(true);
            Assert.That(watch.IsActive, Is.True);
        }

        [Test]
        public void ReputationShiftsAndClamps()
        {
            var reputation = new OutwardReputationState();
            // TOWN.md starting values: apple fame 70, road safety 60, hospitality 65.
            Assert.That((reputation.AppleFame, reputation.RoadSafety, reputation.Hospitality),
                Is.EqualTo((70, 60, 65)));

            reputation.Shift(appleFameDelta: 50, roadSafetyDelta: -80, hospitalityDelta: 10);
            Assert.That((reputation.AppleFame, reputation.RoadSafety, reputation.Hospitality),
                Is.EqualTo((100, 0, 75)));

            Assert.Throws<ArgumentOutOfRangeException>(() => new OutwardReputationState(appleFame: 101));
        }

        [Test]
        public void MerchantCopperTracksMonthlyTrade()
        {
            var schedule = new MerchantScheduleState(initialized: true, nextVisitDay: 50);
            Assert.That(schedule.CopperThisMonth, Is.Zero);

            schedule.RecordMerchantTrade(800, monthIndex: 2);
            schedule.RecordMerchantTrade(375, monthIndex: 2);
            Assert.That(schedule.CopperThisMonth, Is.EqualTo(1175));

            // A new month resets the accumulator before adding.
            schedule.RecordMerchantTrade(100, monthIndex: 3);
            Assert.That(schedule.CopperThisMonth, Is.EqualTo(100));

            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.RecordMerchantTrade(-1, 3));
        }

        [Test]
        public void WealthSumsMoneyAndValuedStock()
        {
            ItemCatalog catalog = Catalog();
            var state = new WorldState(42, new GameTime(0));

            var npcA = new NpcId("npc_a");
            var npcB = new NpcId("npc_b");
            var inventoryA = new Inventory(catalog);
            state.Belongings.Register(ActorId.ForNpc(npcA), inventoryA, new Wallet(300));
            state.Belongings.Register(ActorId.ForNpc(npcB), new Inventory(catalog), new Wallet(700));
            // The player's coin is not NPC money.
            state.Belongings.Register(ActorId.Player, new Inventory(catalog), new Wallet(10000));

            state.RestoreVillageFund(new VillageFundState(initialized: true, startingCopper: 1000));
            state.RestoreGranary(new GranaryState(sacks: 10)); // 10 x 8 base = 80

            var shopStock = new Inventory(catalog);
            shopStock.Add(Apple, 20); // 20 x 3 base = 60
            var separateTill = new Wallet(500);
            state.Shops.Register(new Shop(new LocationId("loc_shop"), npcA, shopStock, separateTill,
                new[] { new KeyValuePair<ItemTypeId, int>(Apple, 5) }));

            // 300 + 700 (NPCs) + 1000 (fund) + 80 (granary at base) + 60 (shop stock at base).
            // The player's 10000 and the shop's separate 500 till are excluded by the formula.
            Assert.That(WealthCalculator.ComputeWealthCopper(state, catalog), Is.EqualTo(2140));
        }

        [Test]
        public void WealthUsesBasePricesNotShopPrices()
        {
            ItemCatalog catalog = Catalog();
            var state = new WorldState(43, new GameTime(0));
            var shopStock = new Inventory(catalog);
            shopStock.Add(Apple, 10);
            state.Shops.Register(new Shop(new LocationId("loc_shop"), new NpcId("npc_owner"),
                shopStock, new Wallet(0),
                // The shop charges 5, but wealth values stock at the base price of 3.
                new[] { new KeyValuePair<ItemTypeId, int>(Apple, 5) }));
            Assert.That(WealthCalculator.ComputeWealthCopper(state, catalog), Is.EqualTo(30));
        }
    }
}
