using System;
using System.Collections.Generic;
using System.Text.Json;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using LivingWorld.Simulation.Persistence;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Persistence
{
    /// <summary>Proves the saver emits the full schema, exact spot values and byte-identical output.</summary>
    public sealed class WorldSaverTests
    {
        private static readonly NpcId Mira = new NpcId("npc_mira");
        private static readonly NpcId Tom = new NpcId("npc_tom");
        private static readonly LocationId Stall = new LocationId("loc_apple_stall");
        private static readonly LocationId Home = new LocationId("loc_home");
        private static readonly LocationId Square = new LocationId("loc_square");
        private static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        private static readonly ItemTypeId Bread = new ItemTypeId("item_bread_rye");
        private static readonly ReputationGroupId Townsfolk = new ReputationGroupId("group_townsfolk");

        /// <summary>Builds a world that touches every saved section exactly once.</summary>
        internal static WorldState RichWorld()
        {
            var catalog = new ItemCatalog(new[]
            {
                new ItemDefinition(Apple, "apple", "food", 3, 1),
                new ItemDefinition(Bread, "rye bread", "food", 5, 1)
            });
            var state = new WorldState(12345, new GameTime(1400));
            _ = state.Rng.NextInt(100);

            // NPCs with exact need sixtieths and intentions (one sleeping).
            state.Npcs.Register(Npc(Mira, new NeedRates(61, 0, 0)));
            state.Npcs.Register(Npc(Tom, new NeedRates(0, 0, 0)));
            state.Npcs[Mira].AdvanceNeedsOneMinute();
            state.Npcs[Mira].SetIntention(new NpcIntention(ActivityKind.Work, Stall, new GameTime(1380)));
            state.Npcs[Tom].SetIntention(new NpcIntention(ActivityKind.Sleep, Home, new GameTime(1390)));

            // Beliefs covering every source kind; memories with exact records.
            BeliefStore miraBeliefs = state.Knowledge.Register(Mira);
            state.Knowledge.Register(Tom);
            var missing = new BeliefClaim(BeliefClaimKind.StockMissing, Stall, Apple, quantity: 6);
            miraBeliefs.Set(new Belief(missing,
                new BeliefSource(BeliefSourceKind.Inferred, originEventId: new WorldEventId(2)),
                70, new GameTime(1390)));
            var rumor = new BeliefClaim(BeliefClaimKind.TheftObserved, Stall, Apple, ActorId.Player, 6);
            miraBeliefs.Set(new Belief(rumor,
                new BeliefSource(BeliefSourceKind.ToldBy, Tom, new WorldEventId(2), new[] { Tom }),
                40, new GameTime(1395)));
            var seen = new BeliefClaim(BeliefClaimKind.StockAvailable, Stall, Apple, quantity: 14);
            miraBeliefs.Set(new Belief(seen,
                new BeliefSource(BeliefSourceKind.Seen), 90, new GameTime(1100)));
            state.Knowledge.GetMemories(Mira).Remember(missing, 80, new GameTime(1390), new WorldEventId(2));
            state.Knowledge.RestorePerceptionCursor(2);

            // Truth: events with every optional field shape.
            state.Events.Append(new GameTime(1385), Stall, WorldEventType.Purchase,
                ActorId.ForNpc(Tom), new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal,
                Apple, 3, 9);
            state.Events.Append(new GameTime(1386), Stall, WorldEventType.Theft,
                ActorId.Player, new[] { ActorId.ForNpc(Mira) }, EventVisibility.Normal, Apple, 6);
            state.Events.Append(new GameTime(1387), Square, WorldEventType.ReputationChanged,
                ActorId.Player, visibility: EventVisibility.Quiet,
                reputationGroup: Townsfolk, reputationDelta: -5);

            // Economy: one shop, player + NPC belongings, system progress.
            var stock = new Inventory(catalog);
            stock.Add(Apple, 9);
            stock.Add(Bread, 4);
            var ownerWallet = new Wallet(31);
            var shop = new Shop(Stall, Mira, stock, ownerWallet,
                new Dictionary<ItemTypeId, int> { [Apple] = 3, [Bread] = 5 });
            state.Shops.Register(shop);
            var playerInventory = new Inventory(catalog);
            playerInventory.Add(Apple, 6);
            state.Belongings.Register(ActorId.Player, playerInventory, new Wallet(17));
            state.Belongings.Register(ActorId.ForNpc(Tom), new Inventory(catalog), new Wallet(8));
            state.EnqueueCommand(new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                shop.Stock, playerInventory, Apple, 2, EventVisibility.Normal));
            state.RestoreProduction(new ProductionState(new[] { "farm_apples" }));
            state.RestoreRestock(new RestockState(new[] { "stall_restock" },
                new[] { new PendingRestockOrder("stall_order", new GameTime(1300)) }));
            state.RestorePrices(new PriceAdjustmentState(
                new[] { new PriceAdjustmentProgress("stall_prices", 2, 1, true) }));

            state.Knowledge.InitializeReputation(
                new[] { new ReputationStanding(Townsfolk, 45) });

            var map = new LocationMap(
                new[]
                {
                    new LocationDefinition(Stall, "stall", "shop", 0, 0),
                    new LocationDefinition(Home, "home", "house", 1, 1),
                    new LocationDefinition(Square, "square", "plaza", 2, 2)
                },
                new[] { new TravelLink(Home, Stall, 5), new TravelLink(Stall, Square, 3) });
            state.InitializeTravel(map);
            state.Travel.RegisterNpc(Mira, Home);
            state.Travel.RegisterNpc(Tom, Square);
            state.StartTravel(Mira, Stall);
            return state;
        }

        private static NpcState Npc(NpcId id, NeedRates rates)
        {
            var definition = new NpcDefinition(id, id.Value, 30, "test", "tester", Home, Stall, 0,
                new Dictionary<string, int> { ["honest"] = 50 }, rates,
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            return new NpcState(definition, 50, 60, 70);
        }

        [Test]
        public void SaveOutputParsesAndContainsEveryRequiredSection()
        {
            string json = WorldSaver.Save(RichWorld());

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                string[] sections =
                {
                    "formatVersion", "clock", "rngState", "eventLog", "pendingCommands", "npcs",
                    "beliefs", "memories", "perceptionCursor", "shops", "belongings",
                    "production", "restock", "prices", "reputation", "travel",
                    "relationships", "attributedMemories", "smithy", "merchantSchedule",
                    "travelerSpend", "wolfBounty", "villageFund", "harvest", "tax",
                    "communityFund", "economyBaseline", "spoilage", "debtLedger",
                    "skills", "tavernPopularity", "ingredientDemand",
                    "animals", "predation", "breeding", "winterPressure", "eggProduction",
                    "townStats", "migration", "emergentEvents"
                };
                foreach (string section in sections)
                    Assert.That(root.TryGetProperty(section, out _), Is.True, section);
                Assert.That(root.GetProperty("eventLog").GetProperty("events").GetArrayLength(),
                    Is.EqualTo(4), "3 truth events plus the departure from starting travel");
                Assert.That(root.GetProperty("pendingCommands").GetArrayLength(), Is.EqualTo(1));
                Assert.That(root.GetProperty("npcs").GetArrayLength(), Is.EqualTo(2));
                Assert.That(root.GetProperty("beliefs").GetProperty("npc_mira").GetArrayLength(),
                    Is.EqualTo(3));
                Assert.That(root.GetProperty("memories").GetProperty("npc_mira").GetArrayLength(),
                    Is.EqualTo(1));
                Assert.That(root.GetProperty("shops").GetArrayLength(), Is.EqualTo(1));
                Assert.That(root.GetProperty("belongings").GetArrayLength(), Is.EqualTo(2));
                Assert.That(root.GetProperty("travel").GetProperty("npcs").GetArrayLength(),
                    Is.EqualTo(2));
            }
        }

        [Test]
        public void FormatVersionIsFive()
        {
            using (JsonDocument document = JsonDocument.Parse(WorldSaver.Save(RichWorld())))
            {
                Assert.That(document.RootElement.GetProperty("formatVersion").GetInt32(), Is.EqualTo(5));
                Assert.That(WorldSaver.FormatVersion, Is.EqualTo(5));
            }
        }

        [Test]
        public void SpotValuesMatchLiveWorldState()
        {
            WorldState state = RichWorld();
            string json = WorldSaver.Save(state);

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                Assert.That(root.GetProperty("clock").GetInt64(), Is.EqualTo(1400));
                Assert.That(root.GetProperty("rngState").GetUInt64(), Is.EqualTo(state.Rng.State));
                Assert.That(root.GetProperty("perceptionCursor").GetInt64(), Is.EqualTo(2));

                JsonElement mira = root.GetProperty("npcs")[0];
                Assert.That(mira.GetProperty("definitionId").GetString(), Is.EqualTo("npc_mira"));
                JsonElement needs = mira.GetProperty("needs");
                Assert.That(needs.GetProperty("hungerSixtieths").GetInt32(),
                    Is.EqualTo(state.Npcs[Mira].Needs.HungerSixtieths));
                Assert.That(needs.GetProperty("hungerSixtieths").GetInt32(), Is.EqualTo(3061),
                    "50*60 + 61 from one awake minute with a 61-per-hour rate");
                Assert.That(mira.GetProperty("isSleeping").GetBoolean(), Is.False);
                JsonElement intention = mira.GetProperty("intention");
                Assert.That(
                    (intention.GetProperty("kind").GetString(),
                        intention.GetProperty("destination").GetString(),
                        intention.GetProperty("chosenAt").GetInt64()),
                    Is.EqualTo(("Work", "loc_apple_stall", 1380L)));
                Assert.That(root.GetProperty("npcs")[1].GetProperty("isSleeping").GetBoolean(),
                    Is.True);

                JsonElement shop = root.GetProperty("shops")[0];
                Assert.That(shop.GetProperty("stock").GetProperty("item_apple").GetInt32(),
                    Is.EqualTo(9));
                Assert.That(shop.GetProperty("ownerCopper").GetInt32(), Is.EqualTo(31));
                Assert.That(shop.GetProperty("prices").GetProperty("item_apple").GetInt32(),
                    Is.EqualTo(3));

                JsonElement theft = root.GetProperty("pendingCommands")[0];
                Assert.That(theft.GetProperty("eligibleMinute").GetInt64(), Is.EqualTo(1401));
                JsonElement payload = theft.GetProperty("command").GetProperty("payload");
                Assert.That(
                    (theft.GetProperty("command").GetProperty("type").GetString(),
                        payload.GetProperty("thief").GetString(),
                        payload.GetProperty("source").GetProperty("shop").GetString(),
                        payload.GetProperty("destination").GetProperty("owner").GetString(),
                        payload.GetProperty("quantity").GetInt32()),
                    Is.EqualTo(("TheftCommand", "player", "loc_apple_stall", "player", 2)));

                JsonElement belief = root.GetProperty("beliefs").GetProperty("npc_mira")[0];
                Assert.That(belief.GetProperty("claim").GetProperty("kind").GetString(),
                    Is.EqualTo("StockAvailable"), "beliefs serialize in claim order");
                JsonElement memory = root.GetProperty("memories").GetProperty("npc_mira")[0];
                Assert.That(
                    (memory.GetProperty("importance").GetInt32(),
                        memory.GetProperty("strength").GetInt32(),
                        memory.GetProperty("originEventId").GetInt32()),
                    Is.EqualTo((80, 80, 2)));

                JsonElement journey = root.GetProperty("travel").GetProperty("npcs")[0];
                Assert.That(journey.GetProperty("id").GetString(), Is.EqualTo("npc_mira"));
                Assert.That(journey.GetProperty("currentLocation").ValueKind,
                    Is.EqualTo(JsonValueKind.Null), "travelling NPC has no current location");
                Assert.That(journey.GetProperty("journey").GetProperty("arrival").GetInt64(),
                    Is.EqualTo(1405));

                JsonElement reputation = root.GetProperty("reputation");
                Assert.That(reputation.GetProperty("standings")[0].GetProperty("value").GetInt32(),
                    Is.EqualTo(45));
            }
        }

        [Test]
        public void SavingTwiceProducesByteIdenticalOutput()
        {
            WorldState state = RichWorld();
            string first = WorldSaver.Save(state);
            string second = WorldSaver.Save(state);
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void EmptyWorldSavesWithNullOptionalSections()
        {
            string json = WorldSaver.Save(new WorldState(7));

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                Assert.That(root.GetProperty("reputation").ValueKind, Is.EqualTo(JsonValueKind.Null));
                Assert.That(root.GetProperty("travel").ValueKind, Is.EqualTo(JsonValueKind.Null));
                Assert.That(root.GetProperty("eventLog").GetProperty("events").GetArrayLength(),
                    Is.EqualTo(0));
                Assert.That(root.GetProperty("pendingCommands").GetArrayLength(), Is.EqualTo(0));
            }
        }

        [Test]
        public void UnresolvableCommandInventoryFailsLoudly()
        {
            var state = new WorldState(7);
            var catalog = new ItemCatalog(new[]
                { new ItemDefinition(Apple, "apple", "food", 3, 1) });
            var loose = new Inventory(catalog);
            loose.Add(Apple, 6);
            state.EnqueueCommand(new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                loose, new Inventory(catalog), Apple, 1, EventVisibility.Hidden));

            Assert.Throws<SaveException>(() => WorldSaver.Save(state));
        }

        [Test]
        public void NullWorldIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => WorldSaver.Save(null));
        }
    }
}
