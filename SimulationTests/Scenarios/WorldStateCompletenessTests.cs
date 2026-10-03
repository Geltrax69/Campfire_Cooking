using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Scenarios
{
    /// <summary>
    /// Proves the Apple Test village can be assembled solely through WorldState registration
    /// APIs (no harness-owned game state) with outcomes identical to the harness-built world.
    /// </summary>
    public sealed class WorldStateCompletenessTests
    {
        private const ulong Seed = 424242;
        private const ulong NoWitnessSeed = 987654321;
        private static readonly GameTime StartTime = new GameTime(5 * 60 + 59);
        private static readonly GameTime EndTime = new GameTime(2 * 1440 + 23 * 60 + 59);

        [TestCase(Seed)]
        [TestCase(NoWitnessSeed)]
        public void WorldStateBuiltVillageMatchesHarnessOutcomesExactly(ulong seed)
        {
            AppleTestResult expected = AppleTestHarness.Run(seed);
            WorldState state = BuildVillage(seed);
            var scenarioState = new AppleScenarioState(new AppleTestConfiguration().StartingStock);
            var world = new World(state);
            AppleTestHarness.RegisterAppleSystems(state, world, new AppleTestConfiguration(), scenarioState);
            while (state.Clock.TotalMinutes < EndTime.TotalMinutes) world.Tick();

            Shop shop = state.Shops[ScenarioVillage.Stall];
            Assert.That(shop.Stock.Count(ScenarioVillage.Apple), Is.EqualTo(expected.FinalShopStock),
                "shop stock");
            Assert.That(shop.UnitPrice(ScenarioVillage.Apple), Is.EqualTo(expected.FinalRetailCopper),
                "retail price");
            int missing = state.Knowledge.Get(ScenarioVillage.Mira)
                .Query(BeliefClaimKind.StockMissing, ScenarioVillage.Stall, ScenarioVillage.Apple)
                .Select(belief => belief.Claim.Quantity ?? 0).SingleOrDefault();
            Assert.That(missing, Is.EqualTo(expected.MiraMissingAppleQuantity), "missing-apple belief");
            bool witnessed = state.Knowledge.Get(ScenarioVillage.Witness).Query()
                .Any(belief => belief.Claim.Kind == BeliefClaimKind.TheftObserved);
            Assert.That(witnessed, Is.EqualTo(expected.WitnessObservedTheft), "witness observation");
            SuspicionResult suspicion = SuspicionEvaluator.Evaluate(state.Knowledge, ScenarioVillage.Guard,
                ActorId.Player, 70, new ApprovedEvidencePolicy());
            Assert.That((suspicion.TotalEvidence, suspicion.MayAct),
                Is.EqualTo((expected.GuardSuspicion.TotalEvidence, expected.GuardSuspicion.MayAct)),
                "guard suspicion");
            Assert.That(state.Clock, Is.EqualTo(expected.EndTime), "end time");
            Assert.That(TotalApples(state), Is.EqualTo(expected.FinalAppleTotal), "apple conservation");
            Assert.That(TotalCopper(state), Is.EqualTo(expected.FinalCopperTotal), "copper conservation");
            Assert.That(state.Events.Query().Select(CanonicalEvent).ToArray(),
                Is.EqualTo(expected.Events.Select(CanonicalEvent).ToArray()),
                "the full world-truth event sequence must match exactly");
        }

        [Test]
        public void WorldStateBuiltVillageIsDeterministicAcrossRuns()
        {
            string first = EventTrace(BuildAndRun(Seed));
            string second = EventTrace(BuildAndRun(Seed));
            Assert.That(second, Is.EqualTo(first));
        }

        private static WorldState BuildAndRun(ulong seed)
        {
            WorldState state = BuildVillage(seed);
            var configuration = new AppleTestConfiguration();
            var scenarioState = new AppleScenarioState(configuration.StartingStock);
            var world = new World(state);
            AppleTestHarness.RegisterAppleSystems(state, world, configuration, scenarioState);
            while (state.Clock.TotalMinutes < EndTime.TotalMinutes) world.Tick();
            return state;
        }

        /// <summary>
        /// Assembles the Apple Test village through WorldState registration APIs only, mirroring
        /// the approved scenario values without using ScenarioVillage.
        /// </summary>
        private static WorldState BuildVillage(ulong seed)
        {
            var configuration = new AppleTestConfiguration();
            var state = new WorldState(seed, StartTime);
            var catalog = new ItemCatalog(new[]
            {
                new ItemDefinition(ScenarioVillage.Apple, "Apple", "food", 3, 1)
            });

            var shopStock = new Inventory(catalog);
            shopStock.Add(ScenarioVillage.Apple, configuration.StartingStock);
            var miraWallet = new Wallet(850);
            state.Shops.Register(new Shop(ScenarioVillage.Stall, ScenarioVillage.Mira, shopStock, miraWallet,
                new[] { new KeyValuePair<ItemTypeId, int>(ScenarioVillage.Apple, configuration.RetailCopper) }));
            state.Belongings.Register(ActorId.ForNpc(ScenarioVillage.Mira), new Inventory(catalog), miraWallet);

            var farmerStock = new Inventory(catalog);
            var farmerWallet = new Wallet(1200);
            state.Belongings.Register(ActorId.ForNpc(ScenarioVillage.Farmer), farmerStock, farmerWallet);
            state.Belongings.Register(ActorId.Player, new Inventory(catalog), new Wallet());
            var registeredBuyers = new HashSet<NpcId>();
            foreach (BuyerPlan plan in ScenarioVillage.BuyerPlans(configuration))
                if (registeredBuyers.Add(plan.Buyer))
                    state.Belongings.Register(ActorId.ForNpc(plan.Buyer), new Inventory(catalog),
                        new Wallet(100));

            foreach (NpcId npc in new[]
                { ScenarioVillage.Mira, ScenarioVillage.Witness, ScenarioVillage.Contact, ScenarioVillage.Guard })
                state.Knowledge.Register(npc);
            return state;
        }

        private static int TotalApples(WorldState state) =>
            state.Belongings.Entries.Sum(entry => entry.Inventory.Count(ScenarioVillage.Apple))
            + state.Shops.Shops.Sum(shop => shop.Stock.Count(ScenarioVillage.Apple));

        private static int TotalCopper(WorldState state) =>
            state.Belongings.Entries.Sum(entry => entry.Wallet.Balance);

        private static string EventTrace(WorldState state) =>
            string.Join("\n", state.Events.Query().Select(CanonicalEvent));

        private static string CanonicalEvent(WorldEvent entry)
        {
            string actor = entry.Actor.HasValue
                ? entry.Actor.Value.IsPlayer ? "player" : "npc:" + entry.Actor.Value.Npc.Value
                : "-";
            string targets = string.Join(",", entry.Targets.Select(target =>
                target.IsPlayer ? "player" : "npc:" + target.Npc.Value).OrderBy(name => name,
                StringComparer.Ordinal));
            return string.Join("|",
                entry.Id.Value.ToString(CultureInfo.InvariantCulture),
                entry.Time.TotalMinutes.ToString(CultureInfo.InvariantCulture),
                entry.Type.ToString(), actor, targets, entry.Location.ToString(),
                entry.Visibility.ToString(), entry.ItemType?.ToString() ?? "-",
                (entry.Quantity?.ToString(CultureInfo.InvariantCulture)) ?? "-",
                (entry.Copper?.ToString(CultureInfo.InvariantCulture)) ?? "-",
                entry.ReputationGroup?.ToString() ?? "-",
                (entry.ReputationDelta?.ToString(CultureInfo.InvariantCulture)) ?? "-");
        }
    }
}
