using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using LivingWorld.Simulation.Persistence;
using LivingWorld.Simulation.Tests.Tools;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Persistence
{
    /// <summary>
    /// Proves every Phase 2 state section survives the version 2 save format:
    /// relationships, attributed memories, all economy progress states and
    /// inventory lot ages, plus the determinism proofs and version compatibility.
    /// </summary>
    public sealed class Phase2PersistenceTests
    {
        private static readonly NpcId Mira = new NpcId("npc_mira_holt");
        private static readonly NpcId Ralf = new NpcId("npc_ralf_hale");
        private static readonly NpcId Doran = new NpcId("npc_doran_kettle");
        private static readonly NpcId Tilda = new NpcId("npc_tilda_bray");
        private static readonly LocationId Stall = new LocationId("loc_apple_stall");
        private static readonly LocationId Square = new LocationId("loc_square");
        private static readonly LocationId Tavern = new LocationId("loc_tavern");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly ItemTypeId Bread = new ItemTypeId("item_bread_rye");

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                !File.Exists(Path.Combine(directory.FullName, "Content/world/locations.json")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate approved Content.");
            return directory.FullName;
        }

        /// <summary>World with non-default values in every Phase 2 section.</summary>
        private static WorldState Phase2RichWorld(ContentBundle bundle)
        {
            ItemCatalog catalog = bundle.Catalog;
            var state = new WorldState(777, new GameTime(5000));
            foreach (NpcId npc in new[] { Mira, Ralf, Doran, Tilda })
            {
                state.Npcs.Register(NpcState.Restore(bundle.NpcDefinitions[npc],
                    NeedState.FromSixtieths(3000, 3000, 3000), false, null));
                state.Knowledge.Register(npc);
            }

            // Relationships: pairs, baselines, cursors, day.
            state.Knowledge.RestoreRelationships(new[]
            {
                new Relationship(Mira, Ralf, 73, 61, "Steady trade"),
                new Relationship(Ralf, Mira, 68, 70, "Fond of his landlady"),
                new Relationship(Doran, Tilda, 40, 45, "Owes her money"),
            });
            state.Knowledge.RestoreRelationshipBaselines(new[]
            {
                new RelationshipBaseline(Mira, Ralf, 50, 50, "Strangers"),
                new RelationshipBaseline(Doran, Tilda, 55, 50, "Old account"),
            });
            state.Knowledge.RestoreDynamicsCursor(42);
            state.Knowledge.RestoreDynamicsDay(3);
            state.Knowledge.RestoreRecallCursor(41);

            // Attributed memories, one with recall movement.
            var wronged = new BeliefClaim(BeliefClaimKind.WrongedBy, Stall, subject: ActorId.ForNpc(Ralf));
            var theftMemory = new AttributedMemory(Mira, wronged, -4, 0);
            theftMemory.AddRecall(-2, 0);
            var gift = new BeliefClaim(BeliefClaimKind.GiftFrom, Tavern, subject: ActorId.ForNpc(Doran));
            state.Knowledge.RestoreAttributedMemories(new[]
            {
                theftMemory,
                new AttributedMemory(Tilda, gift, 1, 3),
            });

            // Economy progress states, all non-default.
            state.RestoreSmithy(new SmithyState(ironExhaustionOrdered: true));
            state.RestoreMerchantSchedule(new MerchantScheduleState(true, 25));
            state.RestoreTravelerSpend(new TravelerSpendState(true, 10, 2, new[] { 100, 200 }));
            state.RestoreWolfBounty(new WolfBountyState(true, 1, new long[] { 5, 10 }));
            var fund = new VillageFundState(true, 600);
            fund.LastLevyDay = 7; fund.LastWageDay = 7; fund.LastRetainerDay = 1;
            state.RestoreVillageFund(fund);
            state.RestoreHarvest(new HarvestState(true, 8));
            state.RestoreTax(new TaxState(true, 15));
            var community = new CommunityFundState(true, 30, 2);
            community.CommunityPot.Credit(150);
            community.FeastPot.Credit(200);
            state.RestoreCommunityFund(community);
            state.RestoreEconomyBaseline(new EconomyBaselineState(true, 9000));
            state.RestoreSpoilage(new SpoilageState(true, 7));

            // Debt ledger with a copper debt and an in-kind debt.
            var ledger = new DebtLedgerState(true);
            ledger.LastFeastYear = 1;
            var doranDebt = new DebtRecord(Doran, Tilda, 100, new DebtTerms(copperPerWeek: 10), 1);
            doranDebt.LastPaymentDay = 8; doranDebt.LastWeeklyDay = 8;
            var bessaDebt = new DebtRecord(Ralf, Tilda, 45,
                new DebtTerms(0, Apple, 2, 8, 100, 60), 1);
            bessaDebt.OverdueDeclared = true;
            ledger.ReplaceAll(new[] { doranDebt, bessaDebt });
            state.RestoreDebtLedger(ledger);

            // Shop and belongings with aged lots.
            var shopStock = new Inventory(catalog);
            shopStock.RestoreLots(new[]
            {
                new StockLotRecord(Apple, 5, 2),
                new StockLotRecord(Apple, 4, 0),
                new StockLotRecord(Bread, 6, 1),
            });
            var shopWallet = new Wallet(31);
            state.Shops.Restore(new[]
            {
                new Shop(Stall, Mira, shopStock, shopWallet,
                    new[] { new KeyValuePair<ItemTypeId, int>(Apple, 3) }),
            });
            var pantry = new Inventory(catalog);
            pantry.RestoreLots(new[] { new StockLotRecord(Bread, 2, 3) });
            state.Belongings.Restore(new[]
            {
                new NpcBelongingsEntry(ActorId.ForNpc(Ralf), pantry, new Wallet(17)),
            });
            return state;
        }

        [Test]
        public void RelationshipsRoundTrip()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase2RichWorld(bundle);

            WorldState loaded = WorldLoader.Load(WorldSaver.Save(state), root);

            Assert.That(loaded.Knowledge.CaptureRelationships().Count, Is.EqualTo(3));
            Relationship pair = loaded.Knowledge.Relationships.Query()[0];
            Assert.That(pair.From, Is.EqualTo(Doran));
            Assert.That(pair.Trust, Is.EqualTo(40));
            Assert.That(pair.Reason, Is.EqualTo("Owes her money"));
            Assert.That(loaded.Knowledge.CaptureRelationshipBaselines().Count, Is.EqualTo(2));
            Assert.That(loaded.Knowledge.CaptureDynamicsCursor(), Is.EqualTo(42));
            Assert.That(loaded.Knowledge.CaptureDynamicsDay(), Is.EqualTo(3));
            Assert.That(loaded.Knowledge.CaptureRecallCursor(), Is.EqualTo(41));
        }

        [Test]
        public void AttributedMemoriesRoundTrip()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase2RichWorld(bundle);

            WorldState loaded = WorldLoader.Load(WorldSaver.Save(state), root);

            var records = loaded.Knowledge.CaptureAttributedMemories();
            Assert.That(records.Count, Is.EqualTo(2));
            AttributedMemory theft = records[0];
            Assert.That(theft.Owner, Is.EqualTo(Mira));
            Assert.That(theft.Claim.Kind, Is.EqualTo(BeliefClaimKind.WrongedBy));
            Assert.That(theft.OriginalTrustDelta, Is.EqualTo(-4));
            Assert.That(theft.RecalledTrustDelta, Is.EqualTo(-2));
            Assert.That(theft.RecalledAffectionDelta, Is.EqualTo(0));
            Assert.That(records[1].OriginalAffectionDelta, Is.EqualTo(3));
        }

        [Test]
        public void EconomyStatesRoundTrip()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase2RichWorld(bundle);

            WorldState loaded = WorldLoader.Load(WorldSaver.Save(state), root);

            Assert.That(loaded.Smithy.IronExhaustionOrdered, Is.True);
            Assert.That(loaded.MerchantSchedule.IsInitialized, Is.True);
            Assert.That(loaded.MerchantSchedule.NextVisitDay, Is.EqualTo(25));
            Assert.That(loaded.TravelerSpend.LastPayoutDay, Is.EqualTo(10));
            Assert.That(loaded.TravelerSpend.MonthIndex, Is.EqualTo(2));
            Assert.That(loaded.TravelerSpend.PaidThisMonth, Is.EqualTo(new[] { 100, 200 }));
            Assert.That(loaded.WolfBounty.WinterYear, Is.EqualTo(1));
            Assert.That(loaded.WolfBounty.BountyDays, Is.EqualTo(new long[] { 5, 10 }));
            Assert.That(loaded.VillageFund.Funds.Balance, Is.EqualTo(600));
            Assert.That(loaded.VillageFund.LastLevyDay, Is.EqualTo(7));
            Assert.That(loaded.Harvest.LastWageDay, Is.EqualTo(8));
            Assert.That(loaded.Tax.LastCollectionDay, Is.EqualTo(15));
            Assert.That(loaded.CommunityFund.CommunityPot.Balance, Is.EqualTo(150));
            Assert.That(loaded.CommunityFund.FeastPot.Balance, Is.EqualTo(200));
            Assert.That(loaded.CommunityFund.LastFeastYear, Is.EqualTo(2));
            Assert.That(loaded.EconomyBaseline.BaselineCopper, Is.EqualTo(9000));
            Assert.That(loaded.Spoilage.LastAgedDay, Is.EqualTo(7));

            Assert.That(loaded.DebtLedger.IsInitialized, Is.True);
            Assert.That(loaded.DebtLedger.LastFeastYear, Is.EqualTo(1));
            Assert.That(loaded.DebtLedger.Debts.Count, Is.EqualTo(2));
            DebtRecord doran = loaded.DebtLedger.Debts[0];
            Assert.That(doran.Debtor, Is.EqualTo(Doran));
            Assert.That(doran.OwedCopper, Is.EqualTo(100));
            Assert.That(doran.Terms.CopperPerWeek, Is.EqualTo(10));
            Assert.That(doran.LastPaymentDay, Is.EqualTo(8));
            DebtRecord bessa = loaded.DebtLedger.Debts[1];
            Assert.That(bessa.Terms.ItemPerWeek, Is.EqualTo((ItemTypeId?)Apple));
            Assert.That(bessa.Terms.ItemsPerWeek, Is.EqualTo(2));
            Assert.That(bessa.OverdueDeclared, Is.True);
        }

        [Test]
        public void InventoryLotsRoundTrip()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase2RichWorld(bundle);

            WorldState loaded = WorldLoader.Load(WorldSaver.Save(state), root);

            Shop shop = loaded.Shops[Stall];
            Assert.That(shop.Stock.Count(Apple), Is.EqualTo(9));
            var lots = shop.Stock.CaptureLots();
            Assert.That(lots.Count, Is.EqualTo(3));
            Assert.That(lots[0].Quantity, Is.EqualTo(5));
            Assert.That(lots[0].AgeDays, Is.EqualTo(2));
            Assert.That(lots[1].AgeDays, Is.EqualTo(0));
            var pantry = loaded.Belongings[ActorId.ForNpc(Ralf)].Inventory;
            Assert.That(pantry.CaptureLots()[0].AgeDays, Is.EqualTo(3));
        }

        [Test]
        public void SaveLoadSaveIsByteIdentical_Phase2()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase2RichWorld(bundle);
            string first = WorldSaver.Save(state);

            WorldState loaded = WorldLoader.Load(first, root);
            string second = WorldSaver.Save(loaded);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(WorldDigest.Compute(loaded), Is.EqualTo(WorldDigest.Compute(state)));
        }

        [Test]
        public void Version1DocumentLoadsWithPhase2Defaults()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase2RichWorld(bundle);
            string v2 = WorldSaver.Save(state);
            string v1 = StripToVersion1(v2);

            WorldState loaded = WorldLoader.Load(v1, root);

            // Phase 2 sections default: relationships empty, cursors zero, fresh states.
            Assert.That(loaded.Knowledge.CaptureRelationships().Count, Is.EqualTo(0));
            Assert.That(loaded.Knowledge.CaptureDynamicsCursor(), Is.EqualTo(0));
            Assert.That(loaded.Knowledge.CaptureAttributedMemories().Count, Is.EqualTo(0));
            Assert.That(loaded.Smithy.IronExhaustionOrdered, Is.False);
            Assert.That(loaded.MerchantSchedule.IsInitialized, Is.False);
            Assert.That(loaded.DebtLedger.IsInitialized, Is.False);
            Assert.That(loaded.DebtLedger.Debts.Count, Is.EqualTo(0));
            // Pre-existing sections still load.
            Assert.That(loaded.Shops[Stall].Stock.Count(Apple), Is.EqualTo(9));
            // Lots default to age 0.
            foreach (StockLotRecord lot in loaded.Shops[Stall].Stock.CaptureLots())
                Assert.That(lot.AgeDays, Is.EqualTo(0));
        }

        [Test]
        public void FutureVersionIsRejected()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase2RichWorld(bundle);
            string json = WorldSaver.Save(state);
            string v5 = json.Replace("\"formatVersion\": 4", "\"formatVersion\": 5");

            Assert.Throws<LoadException>(() => WorldLoader.Load(v5, root));
        }

        [Test]
        public void ContradictoryLotsAreRejected()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);
            WorldState state = Phase2RichWorld(bundle);
            string json = WorldSaver.Save(state);
            // Corrupt: claim one fewer apple in the lots than in the counts.
            string corrupt = json.Replace(
                "\"item\": \"item_apple\",\n          \"quantity\": 5,",
                "\"item\": \"item_apple\",\n          \"quantity\": 4,");

            Assert.Throws<LoadException>(() => WorldLoader.Load(corrupt, root));
        }

        /// <summary>
        /// The P2-12 determinism proof: a world with live Phase 2 systems ticks 400,
        /// saves, loads, ticks 600 more, and reaches the identical digest as a world
        /// that ticked 1000 uninterrupted. The systems mutate the restored state
        /// (relationship shifts from trade events, debt processing), so this proves
        /// the restored Phase 2 state is functionally identical, not just equal bytes.
        /// </summary>
        [Test]
        public void Phase2DeterminismProof()
        {
            string root = RepositoryRoot();
            ContentBundle bundle = ContentBundle.Load(root);

            WorldState uninterrupted = Phase2RichWorld(bundle);
            WorldState reloaded = Phase2RichWorld(bundle);
            // The dynamics system processes events from the cursor: rewind it so the
            // seeded trade events below are actually processed during the proof.
            uninterrupted.Knowledge.RestoreDynamicsCursor(0);
            reloaded.Knowledge.RestoreDynamicsCursor(0);
            var systems = new Func<WorldState, World>(state =>
            {
                var world = new World(state);
                world.RegisterSystem(new RelationshipDynamicsSystem());
                world.RegisterSystem(new DebtSystem(new[]
                {
                    new DebtConfiguration("proof-debts", Tavern, EventVisibility.Hidden),
                }));
                return world;
            });

            World worldA = systems(uninterrupted);
            World worldB = systems(reloaded);

            // Seed trade events: the dynamics system will shift relationships.
            SeedTradeEvents(uninterrupted);
            SeedTradeEvents(reloaded);

            for (int i = 0; i < 400; i++) { worldA.Tick(); worldB.Tick(); }
            string saved = WorldSaver.Save(reloaded);
            WorldState loaded = WorldLoader.Load(saved, root);
            Assert.That(WorldDigest.Compute(loaded), Is.EqualTo(WorldDigest.Compute(reloaded)),
                "Save/load must preserve the exact state before the future-evolution proof.");
            World worldC = systems(loaded);

            SeedTradeEvents(uninterrupted);
            SeedTradeEvents(loaded);

            for (int i = 0; i < 600; i++) { worldA.Tick(); worldC.Tick(); }

            Assert.That(WorldDigest.Compute(loaded), Is.EqualTo(WorldDigest.Compute(uninterrupted)));
            Assert.That(WorldSaver.Save(loaded), Is.EqualTo(WorldSaver.Save(uninterrupted)));
        }

        /// <summary>
        /// The literal full-village proof from the P2-12 brief: the assembled village
        /// ticks 1000 uninterrupted, versus 400, save, load, re-register every system
        /// against the loaded state, then 600 more. Equal digests and byte-identical
        /// final saves prove a save/load cycle changes nothing about the world's future.
        /// </summary>
        [Test]
        public void FullVillageSaveLoadMidRunProducesIdenticalFuture()
        {
            string root = RepositoryRoot();
            const ulong seed = 20261004;

            VillageAssembly.Village villageA = VillageAssembly.Build(root, seed);
            VillageAssembly.Village villageB = VillageAssembly.Build(root, seed);

            for (int i = 0; i < 400; i++)
            {
                villageA.World.Tick();
                villageB.World.Tick();
            }

            string saved = WorldSaver.Save(villageB.State);
            WorldState loaded = WorldLoader.Load(saved, root);
            Assert.That(WorldDigest.Compute(loaded), Is.EqualTo(WorldDigest.Compute(villageB.State)),
                "Save/load must preserve the exact village state before the future-evolution proof.");

            VillageAssembly.Village reassembled = VillageAssembly.Reassemble(loaded, root);

            for (int i = 0; i < 600; i++)
            {
                villageA.World.Tick();
                reassembled.World.Tick();
            }

            Assert.That(WorldDigest.Compute(loaded), Is.EqualTo(WorldDigest.Compute(villageA.State)),
                "A save/load cycle at tick 400 must not change the village's future.");
            Assert.That(WorldSaver.Save(loaded), Is.EqualTo(WorldSaver.Save(villageA.State)),
                "Final save documents must be byte-identical.");
        }

        private static void SeedTradeEvents(WorldState state)
        {
            state.Events.Append(state.Clock, Stall, WorldEventType.Purchase,
                ActorId.ForNpc(Ralf), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 3, 9);
            state.Events.Append(state.Clock, Tavern, WorldEventType.Gift,
                ActorId.ForNpc(Doran), new[] { ActorId.ForNpc(Tilda) }, EventVisibility.Normal, Bread, 1, 2);
        }

        /// <summary>
        /// Rewrites a version 2 document as version 1 by dropping the Phase 2
        /// sections and the per-lot ages, proving old saves still load.
        /// </summary>
        private static string StripToVersion1(string v2)
        {
            using (JsonDocument document = JsonDocument.Parse(v2))
            {
                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream))
                    {
                        writer.WriteStartObject();
                        foreach (JsonProperty property in document.RootElement.EnumerateObject())
                        {
                            switch (property.Name)
                            {
                                case "formatVersion":
                                    writer.WriteNumber("formatVersion", 1);
                                    break;
                                case "relationships":
                                case "attributedMemories":
                                case "smithy":
                                case "merchantSchedule":
                                case "travelerSpend":
                                case "wolfBounty":
                                case "villageFund":
                                case "harvest":
                                case "tax":
                                case "communityFund":
                                case "economyBaseline":
                                case "spoilage":
                                case "debtLedger":
                                    break;
                                case "shops":
                                case "belongings":
                                    writer.WritePropertyName(property.Name);
                                    writer.WriteStartArray();
                                    foreach (JsonElement entry in property.Value.EnumerateArray())
                                    {
                                        writer.WriteStartObject();
                                        foreach (JsonProperty field in entry.EnumerateObject())
                                        {
                                            if (field.Name == "lots") continue;
                                            field.WriteTo(writer);
                                        }
                                        writer.WriteEndObject();
                                    }
                                    writer.WriteEndArray();
                                    break;
                                default:
                                    property.WriteTo(writer);
                                    break;
                            }
                        }
                        writer.WriteEndObject();
                        writer.Flush();
                    }
                    return System.Text.Encoding.UTF8.GetString(stream.ToArray());
                }
            }
        }
    }
}
