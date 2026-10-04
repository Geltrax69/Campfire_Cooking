using System;
using System.Collections.Generic;
using System.IO;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using LivingWorld.Simulation.Persistence;
using LivingWorld.Simulation.Tests.Scenarios;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Persistence
{
    /// <summary>
    /// Proves the P1-21 pass condition: run(1000) == save→load→run(1000), plus round-trip
    /// byte stability and atomic rejection of corrupt documents.
    /// </summary>
    public sealed class WorldLoaderTests
    {
        private static readonly NpcId Mira = new NpcId("npc_mira_holt");
        private static readonly NpcId Bram = new NpcId("npc_bram_stone");
        private static readonly LocationId Stall = new LocationId("loc_apple_stall");
        private static readonly LocationId Farm = new LocationId("loc_farm");
        private static readonly LocationId Square = new LocationId("loc_square");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly ItemTypeId Bread = new ItemTypeId("item_bread_rye");
        private static readonly ReputationGroupId Villagers = new ReputationGroupId("group_villagers");

        private const ulong WitnessSeed = 42;
        private const ulong NoWitnessSeed = 987654321;

        internal static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                !File.Exists(Path.Combine(directory.FullName, "Content/world/locations.json")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate approved Content.");
            return directory.FullName;
        }

        /// <summary>Rich world with real Content IDs touching every saved section.</summary>
        internal static WorldState RichWorld(ContentBundle bundle)
        {
            var state = new WorldState(12345, new GameTime(1400));
            _ = state.Rng.NextInt(100);

            // NPCs with exact need sixtieths and intentions (one sleeping).
            state.Npcs.Register(NpcState.Restore(bundle.NpcDefinitions[Mira],
                NeedState.FromSixtieths(3061, 4200, 5100), false,
                new NpcIntention(ActivityKind.Work, Stall, new GameTime(1380))));
            state.Npcs.Register(NpcState.Restore(bundle.NpcDefinitions[Bram],
                NeedState.FromSixtieths(3000, 3000, 3000), true,
                new NpcIntention(ActivityKind.Sleep, Farm, new GameTime(1390))));

            // Beliefs covering every source kind; one exact memory record.
            BeliefStore miraBeliefs = state.Knowledge.Register(Mira);
            state.Knowledge.Register(Bram);
            var missing = new BeliefClaim(BeliefClaimKind.StockMissing, Stall, Apple, quantity: 6);
            miraBeliefs.Set(new Belief(missing,
                new BeliefSource(BeliefSourceKind.Inferred, originEventId: new WorldEventId(2)),
                70, new GameTime(1390)));
            var rumor = new BeliefClaim(BeliefClaimKind.TheftObserved, Stall, Apple, ActorId.Player, 6);
            miraBeliefs.Set(new Belief(rumor,
                new BeliefSource(BeliefSourceKind.ToldBy, Bram, new WorldEventId(2), new[] { Bram }),
                40, new GameTime(1395)));
            var seen = new BeliefClaim(BeliefClaimKind.StockAvailable, Stall, Apple, quantity: 14);
            miraBeliefs.Set(new Belief(seen, new BeliefSource(BeliefSourceKind.Seen), 90, new GameTime(1100)));
            state.Knowledge.GetMemories(Mira).Restore(new[]
                { new Memory(missing, 80, new GameTime(1380), new GameTime(1390), 72, new WorldEventId(2)) });
            state.Knowledge.RestorePerceptionCursor(2);

            // Truth: purchase, theft and reputation-change events.
            state.Events.Append(new GameTime(1385), Stall, WorldEventType.Purchase,
                ActorId.ForNpc(Bram), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal,
                Apple, 3, 9);
            state.Events.Append(new GameTime(1386), Stall, WorldEventType.Theft,
                ActorId.Player, new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 6);
            state.Events.Append(new GameTime(1387), Square, WorldEventType.ReputationChanged,
                ActorId.Player, visibility: EventVisibility.Quiet,
                reputationGroup: Villagers, reputationDelta: -5);

            // Economy: one shop, NPC + player belongings, a pending theft, system progress.
            var stock = new Inventory(bundle.Catalog);
            stock.Add(Apple, 9);
            stock.Add(Bread, 4);
            var shop = new Shop(Stall, Mira, stock, new Wallet(31),
                new Dictionary<ItemTypeId, int> { [Apple] = 3, [Bread] = 5 });
            state.Shops.Register(shop);
            // Mira's personal wallet is the shop's owner wallet: one object, registered once.
            state.Belongings.Register(ActorId.ForNpc(Mira), new Inventory(bundle.Catalog), shop.OwnerWallet);
            var playerInventory = new Inventory(bundle.Catalog);
            playerInventory.Add(Apple, 6);
            state.Belongings.Register(ActorId.Player, playerInventory, new Wallet(17));
            state.Belongings.Register(ActorId.ForNpc(Bram), new Inventory(bundle.Catalog), new Wallet(8));
            state.EnqueueCommand(new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                shop.Stock, playerInventory, Apple, 2, EventVisibility.Normal));
            state.RestoreProduction(new ProductionState(new[] { "farm_apples" }));
            state.RestoreRestock(new RestockState(new[] { "stall_restock" },
                new[] { new PendingRestockOrder("stall_order", new GameTime(1300)) }));
            state.RestorePrices(new PriceAdjustmentState(
                new[] { new PriceAdjustmentProgress("stall_prices", 2, 1, true) }));

            state.Knowledge.RestoreReputation(new ReputationState(
                new[] { new ReputationStanding(Villagers, 45) }));

            state.InitializeTravel(bundle.Map);
            state.Travel.RegisterNpc(Mira, Farm);
            state.Travel.RegisterNpc(Bram, Square);
            state.StartTravel(Mira, Stall);
            return state;
        }

        /// <summary>Drives the Apple Test village for a fixed tick budget (mirrors the harness).</summary>
        private sealed class DrivenVillage
        {
            public DrivenVillage(ulong seed, AppleScenarioState progress)
            {
                Configuration = new AppleTestConfiguration();
                State = new WorldState(seed, new GameTime(5 * 60 + 59));
                _ = new ScenarioVillage(Configuration, State);
                World = new World(State);
                AppleTestHarness.RegisterAppleSystems(State, World, Configuration, progress);
                Progress = progress;
            }

            public AppleTestConfiguration Configuration { get; }
            public WorldState State { get; }
            public World World { get; }
            public AppleScenarioState Progress { get; }

            public void Tick(int count)
            {
                for (int i = 0; i < count; i++) World.Tick();
            }
        }

        private static AppleScenarioState CopyProgress(AppleScenarioState progress) =>
            new AppleScenarioState(progress.ExpectedShopStock, progress.CompletedBuyerIds,
                progress.StockCounted, progress.CompletedMeetingIds, progress.TheftQueued);

        [TestCase(WitnessSeed)]
        [TestCase(NoWitnessSeed)]
        public void SaveLoadMidRunProducesIdenticalFuture(ulong seed)
        {
            string root = RepositoryRoot();

            // Side A: 1000 ticks with no interruption.
            var villageA = new DrivenVillage(seed, new AppleScenarioState(20));
            villageA.Tick(1000);
            string digestA = WorldDigest.Compute(villageA.State);

            // Side B: 400 ticks, save, load into a fresh world, 600 more ticks with the
            // same driver code and the same harness-owned scenario progress.
            var villageB = new DrivenVillage(seed, new AppleScenarioState(20));
            villageB.Tick(400);
            string json = WorldSaver.Save(villageB.State);
            AppleScenarioState resumed = CopyProgress(villageB.Progress);
            WorldState loaded = WorldLoader.Load(json, root);
            var worldB = new World(loaded);
            AppleTestHarness.RegisterAppleSystems(loaded, worldB, new AppleTestConfiguration(), resumed);
            for (int i = 0; i < 600; i++) worldB.Tick();
            string digestB = WorldDigest.Compute(loaded);

            Assert.That(digestB, Is.EqualTo(digestA),
                "run(1000) must equal save→load→run(1000) for seed " + seed);
            Assert.That(WorldSaver.Save(loaded), Is.EqualTo(WorldSaver.Save(villageA.State)),
                "canonical save documents must be byte-identical for seed " + seed);
        }

        [Test]
        public void SaveLoadSaveIsByteIdentical()
        {
            string root = RepositoryRoot();
            WorldState state = RichWorld(ContentBundle.Load(root));
            string first = WorldSaver.Save(state);

            WorldState loaded = WorldLoader.Load(first, root);
            string second = WorldSaver.Save(loaded);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(WorldDigest.Compute(loaded), Is.EqualTo(WorldDigest.Compute(state)));
        }

        [Test]
        public void LoadedStateMatchesSavedValues()
        {
            string root = RepositoryRoot();
            WorldState state = RichWorld(ContentBundle.Load(root));

            WorldState loaded = WorldLoader.Load(WorldSaver.Save(state), root);

            Assert.That(loaded.Clock.TotalMinutes, Is.EqualTo(1400));
            Assert.That(loaded.Rng.State, Is.EqualTo(state.Rng.State));
            Assert.That(loaded.Npcs.Count, Is.EqualTo(2));
            Assert.That(loaded.Npcs[Mira].Needs.HungerSixtieths, Is.EqualTo(3061));
            Assert.That(loaded.Npcs[Bram].IsSleeping, Is.True);
            Assert.That(loaded.Shops[Stall].Stock.Count(Apple), Is.EqualTo(9));
            Assert.That(loaded.Shops[Stall].OwnerWallet.Balance, Is.EqualTo(31));
            Assert.That(loaded.Belongings[ActorId.Player].Wallet.Balance, Is.EqualTo(17));
            Assert.That(loaded.Knowledge.Get(Mira).Count, Is.EqualTo(3));
            Assert.That(loaded.Knowledge.GetMemories(Mira).Count, Is.EqualTo(1));
            Assert.That(loaded.Knowledge.Reputation.Get(Villagers), Is.EqualTo(45));
            Assert.That(loaded.Travel[Mira].Journey.Arrival.TotalMinutes, Is.EqualTo(1401));
            Assert.That(loaded.Events.Count, Is.EqualTo(4), "3 truth events plus the travel departure");
            Assert.That(loaded.Belongings[ActorId.ForNpc(Mira)].Wallet.Balance, Is.EqualTo(31));
            Assert.That(ReferenceEquals(loaded.Shops[Stall].OwnerWallet,
                loaded.Belongings[ActorId.ForNpc(Mira)].Wallet), Is.True,
                "the shop owner wallet is the owner's personal wallet: one shared object");
        }

        [Test]
        public void EmptyWorldRoundTrips()
        {
            string root = RepositoryRoot();
            var state = new WorldState(7);
            string first = WorldSaver.Save(state);

            WorldState loaded = WorldLoader.Load(first, root);

            Assert.That(WorldSaver.Save(loaded), Is.EqualTo(first));
            Assert.That(loaded.Clock.TotalMinutes, Is.EqualTo(0));
            Assert.That(loaded.Rng.State, Is.EqualTo(state.Rng.State));
            Assert.That(loaded.Knowledge.Reputation, Is.Null);
            Assert.That(loaded.Travel, Is.Null);
        }

        [Test]
        public void DigestIsStableAndSensitive()
        {
            string root = RepositoryRoot();
            WorldState state = RichWorld(ContentBundle.Load(root));

            Assert.That(WorldDigest.Compute(state), Is.EqualTo(WorldDigest.Compute(state)),
                "same state, same digest");
            string before = WorldDigest.Compute(state);
            state.Events.Append(new GameTime(1400), Square, WorldEventType.Conversation,
                ActorId.Player, visibility: EventVisibility.Quiet);
            Assert.That(WorldDigest.Compute(state), Is.Not.EqualTo(before),
                "any truth change must change the digest");
        }

        [Test]
        public void RejectsCorruptJson()
        {
            Assert.Throws<LoadException>(() => WorldLoader.Load("{oops", RepositoryRoot()));
        }

        [Test]
        public void RejectsUnknownVersion()
        {
            string root = RepositoryRoot();
            string json = WorldSaver.Save(RichWorld(ContentBundle.Load(root)));
            string bad = json.Replace("\"formatVersion\": 4", "\"formatVersion\": 99");
            Assert.That(bad, Is.Not.EqualTo(json));
            Assert.Throws<LoadException>(() => WorldLoader.Load(bad, root));
        }

        [Test]
        public void RejectsUnknownNpcDefinition()
        {
            string root = RepositoryRoot();
            string json = WorldSaver.Save(RichWorld(ContentBundle.Load(root)));
            string bad = json.Replace(
                "\"definitionId\": \"npc_mira_holt\"", "\"definitionId\": \"npc_nobody\"");
            Assert.That(bad, Is.Not.EqualTo(json));
            Assert.Throws<LoadException>(() => WorldLoader.Load(bad, root));
        }

        [Test]
        public void RejectsUnknownItem()
        {
            string root = RepositoryRoot();
            string json = WorldSaver.Save(RichWorld(ContentBundle.Load(root)));
            string bad = json.Replace("\"item_apple\": 9", "\"item_nope\": 9");
            Assert.That(bad, Is.Not.EqualTo(json));
            Assert.Throws<LoadException>(() => WorldLoader.Load(bad, root));
        }

        [Test]
        public void RejectsUnknownLocation()
        {
            string root = RepositoryRoot();
            string json = WorldSaver.Save(RichWorld(ContentBundle.Load(root)));
            string bad = json.Replace("loc_apple_stall", "loc_nowhere");
            Assert.That(bad, Is.Not.EqualTo(json));
            Assert.Throws<LoadException>(() => WorldLoader.Load(bad, root));
        }

        [Test]
        public void SleepingFlagAndIntentionRestoreIndependently()
        {
            // The running world drives IsSleeping from the schedule while the
            // intention driver is unwired, so a save that disagrees (sleeping
            // with no Sleep intention) is honest, not corrupt — it must load,
            // with both values restored exactly as saved.
            string root = RepositoryRoot();
            string json = WorldSaver.Save(RichWorld(ContentBundle.Load(root)));
            string night = json.Replace("\"isSleeping\": false", "\"isSleeping\": true");
            Assert.That(night, Is.Not.EqualTo(json));

            WorldState loaded = WorldLoader.Load(night, root);

            NpcState mira = loaded.Npcs[Mira];
            Assert.That(mira.IsSleeping, Is.True);
            Assert.That(mira.CurrentIntention.Kind, Is.EqualTo(ActivityKind.Work),
                "The Work intention restores untouched alongside the flipped flag.");
        }

        [Test]
        public void RejectsNegativeStock()
        {
            string root = RepositoryRoot();
            string json = WorldSaver.Save(RichWorld(ContentBundle.Load(root)));
            string bad = json.Replace("\"item_apple\": 9", "\"item_apple\": -5");
            Assert.That(bad, Is.Not.EqualTo(json));
            Assert.Throws<LoadException>(() => WorldLoader.Load(bad, root));
        }

        [Test]
        public void RejectsUnknownCommandType()
        {
            string root = RepositoryRoot();
            string json = WorldSaver.Save(RichWorld(ContentBundle.Load(root)));
            string bad = json.Replace("\"type\": \"TheftCommand\"", "\"type\": \"TeleportCommand\"");
            Assert.That(bad, Is.Not.EqualTo(json));
            Assert.Throws<LoadException>(() => WorldLoader.Load(bad, root));
        }

        [Test]
        public void RejectsUnknownEnumName()
        {
            string root = RepositoryRoot();
            string json = WorldSaver.Save(RichWorld(ContentBundle.Load(root)));
            string bad = json.Replace("\"kind\": \"Work\"", "\"kind\": \"Nap\"");
            Assert.That(bad, Is.Not.EqualTo(json));
            Assert.Throws<LoadException>(() => WorldLoader.Load(bad, root));
        }

        [Test]
        public void RejectsContradictoryWalletBalances()
        {
            string root = RepositoryRoot();
            string json = WorldSaver.Save(RichWorld(ContentBundle.Load(root)));
            // Mira's shop till is shared with her personal wallet: if the saved
            // balances disagree, the document is corrupt.
            string bad = json.Replace("\"ownerCopper\": 31", "\"ownerCopper\": 32");
            Assert.That(bad, Is.Not.EqualTo(json));
            Assert.Throws<LoadException>(() => WorldLoader.Load(bad, root));
        }

        [Test]
        public void RejectsUnknownReputationGroup()
        {
            string root = RepositoryRoot();
            string json = WorldSaver.Save(RichWorld(ContentBundle.Load(root)));
            string bad = json.Replace("group_villagers", "group_nobodies");
            Assert.That(bad, Is.Not.EqualTo(json));
            Assert.Throws<LoadException>(() => WorldLoader.Load(bad, root));
        }

        [Test]
        public void FailedLoadsNeverLeaveAHalfBuiltWorld()
        {
            string root = RepositoryRoot();
            string valid = WorldSaver.Save(RichWorld(ContentBundle.Load(root)));

            Assert.Throws<LoadException>(() => WorldLoader.Load("{oops", root));
            Assert.Throws<LoadException>(() => WorldLoader.Load(
                valid.Replace("\"formatVersion\": 4", "\"formatVersion\": 99"), root));
            Assert.Throws<LoadException>(() => WorldLoader.Load(
                valid.Replace("\"happiness\": 50", "\"happiness\": 101"), root));

            // The loader either returns a complete world or throws: a valid document still loads.
            WorldState loaded = WorldLoader.Load(valid, root);
            Assert.That(WorldSaver.Save(loaded), Is.EqualTo(valid));
        }

        [Test]
        public void NullArgumentsAndBadContentRootAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => WorldLoader.Load(null, RepositoryRoot()));
            Assert.Throws<ArgumentNullException>(() => WorldLoader.Load("{}", null));
            Assert.Throws<LoadException>(() => WorldLoader.Load("{}", Path.Combine(
                Path.GetTempPath(), "no-such-content-root")));
        }
    }
}
