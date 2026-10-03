using System;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;
using static LivingWorld.Simulation.Tests.Economy.ItemCatalogTests;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>Checks atomic queued theft transfers and their world-truth events.</summary>
    public sealed class TheftCommandTests
    {
        private static readonly LocationId Stall = new LocationId("loc_apple_stall");
        private static readonly NpcId Mira = new NpcId("npc_mira_holt");
        private static readonly NpcId Tom = new NpcId("npc_tom_fenn");

        [Test]
        public void SuccessfulTheftMovesExactStockLogsTruthAndMovesNoCopper()
        {
            Scenario scenario = Create(stock: 20, thiefCopper: 17, ownerCopper: 31, start: new GameTime(12));
            var command = new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                scenario.Source, scenario.Thief, Apple, 6, EventVisibility.Quiet);

            command.Execute(scenario.State);

            Assert.That((scenario.Source.Count(Apple), scenario.Thief.Count(Apple)), Is.EqualTo((14, 6)));
            Assert.That((scenario.ThiefWallet.Balance, scenario.OwnerWallet.Balance), Is.EqualTo((17, 31)));
            WorldEvent entry = scenario.State.Events.Query().Single();
            Assert.Multiple(() =>
            {
                Assert.That(entry.Time, Is.EqualTo(new GameTime(12)));
                Assert.That(entry.Location, Is.EqualTo(Stall));
                Assert.That(entry.Type, Is.EqualTo(WorldEventType.Theft));
                Assert.That(entry.Actor, Is.EqualTo(ActorId.Player));
                Assert.That(entry.Visibility, Is.EqualTo(EventVisibility.Quiet));
                Assert.That(entry.ItemType, Is.EqualTo(Apple));
                Assert.That(entry.Quantity, Is.EqualTo(6));
                Assert.That(entry.Copper, Is.Null);
            });
            Assert.That(entry.Targets, Is.EqualTo(new[] { ActorId.ForNpc(Mira) }));
        }

        [Test]
        public void InvalidConstructionAndInsufficientExecutionAreAtomicAndLogNothing()
        {
            Scenario scenario = Create(stock: 5);
            Assert.Throws<ArgumentException>(() => new TheftCommand(default, ActorId.Player, ActorId.ForNpc(Mira),
                scenario.Source, scenario.Thief, Apple, 1, EventVisibility.Quiet));
            Assert.Throws<ArgumentException>(() => new TheftCommand(Stall, default, ActorId.ForNpc(Mira),
                scenario.Source, scenario.Thief, Apple, 1, EventVisibility.Quiet));
            Assert.Throws<ArgumentException>(() => new TheftCommand(Stall, ActorId.Player, default,
                scenario.Source, scenario.Thief, Apple, 1, EventVisibility.Quiet));
            Assert.Throws<ArgumentNullException>(() => new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                null, scenario.Thief, Apple, 1, EventVisibility.Quiet));
            Assert.Throws<ArgumentNullException>(() => new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                scenario.Source, null, Apple, 1, EventVisibility.Quiet));
            Assert.Throws<ArgumentException>(() => new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                scenario.Source, scenario.Source, Apple, 1, EventVisibility.Quiet));
            Assert.Throws<ArgumentException>(() => new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                scenario.Source, scenario.Thief, default, 1, EventVisibility.Quiet));
            Assert.Throws<ArgumentException>(() => new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                scenario.Source, scenario.Thief, new ItemTypeId("item_unknown"), 1, EventVisibility.Quiet));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                scenario.Source, scenario.Thief, Apple, 0, EventVisibility.Quiet));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                scenario.Source, scenario.Thief, Apple, 1, (EventVisibility)99));

            new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira), scenario.Source, scenario.Thief,
                Apple, 6, EventVisibility.Quiet).Execute(scenario.State);

            AssertUnchanged(scenario, source: 5, thief: 0);
        }

        [Test]
        public void UnsupportedOrFullDestinationAndEventFailureLeaveEverythingUnchanged()
        {
            Scenario incompatible = Create(stock: 5, thiefCatalog: new ItemCatalog(new[] { Define(Bread) }));
            Assert.Throws<ArgumentException>(() => new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                incompatible.Source, incompatible.Thief, Apple, 1, EventVisibility.Hidden));
            AssertUnchanged(incompatible, source: 5, thief: null);

            Scenario full = Create(stock: 5);
            full.Thief.Add(Apple, int.MaxValue);
            new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira), full.Source, full.Thief,
                Apple, 1, EventVisibility.Hidden).Execute(full.State);
            AssertUnchanged(full, source: 5, thief: int.MaxValue);

            Scenario logging = Create(stock: 5);
            logging.State.Events.Append(new GameTime(13), Stall, WorldEventType.Conversation);
            Assert.Throws<ArgumentException>(() => new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                logging.Source, logging.Thief, Apple, 1, EventVisibility.Hidden).Execute(logging.State));
            Assert.That(logging.State.Events.Count, Is.EqualTo(1));
            Assert.That((logging.Source.Count(Apple), logging.Thief.Count(Apple)), Is.EqualTo((5, 0)));
        }

        [Test]
        public void QueuedTheftsExecuteNextMinuteInFifoOrder()
        {
            Scenario scenario = Create(stock: 10, start: new GameTime(20));
            var secondThief = new Inventory(TinyCatalog());
            var world = new World(scenario.State);
            world.RegisterSystem(new CommandSystem());
            scenario.State.EnqueueCommand(new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira),
                scenario.Source, scenario.Thief, Apple, 6, EventVisibility.Quiet));
            scenario.State.EnqueueCommand(new TheftCommand(Stall, ActorId.ForNpc(Tom), ActorId.ForNpc(Mira),
                scenario.Source, secondThief, Apple, 4, EventVisibility.Normal));

            Assert.That((scenario.Source.Count(Apple), scenario.State.Events.Count), Is.EqualTo((10, 0)));
            world.Tick();

            Assert.That((scenario.State.Clock.TotalMinutes, scenario.Source.Count(Apple),
                scenario.Thief.Count(Apple), secondThief.Count(Apple)), Is.EqualTo((21L, 0, 6, 4)));
            WorldEvent[] events = scenario.State.Events.Query().ToArray();
            Assert.That(events.Select(entry => (entry.Id.Value, entry.Actor, entry.Quantity, entry.Visibility)),
                Is.EqualTo(new[]
                {
                    (1L, (ActorId?)ActorId.Player, (int?)6, EventVisibility.Quiet),
                    (2L, (ActorId?)ActorId.ForNpc(Tom), (int?)4, EventVisibility.Normal)
                }));
        }

        private static Scenario Create(int stock, int thiefCopper = 0, int ownerCopper = 0,
            ItemCatalog thiefCatalog = null, GameTime start = default)
        {
            ItemCatalog catalog = TinyCatalog();
            var source = new Inventory(catalog);
            source.Add(Apple, stock);
            return new Scenario(source, new Inventory(thiefCatalog ?? catalog),
                new Wallet(thiefCopper), new Wallet(ownerCopper), new WorldState(42, start));
        }

        private static void AssertUnchanged(Scenario scenario, int source, int? thief)
        {
            Assert.That(scenario.Source.Count(Apple), Is.EqualTo(source));
            if (thief.HasValue) Assert.That(scenario.Thief.Count(Apple), Is.EqualTo(thief.Value));
            else Assert.That(scenario.Thief.Contents, Is.Empty);
            Assert.That((scenario.ThiefWallet.Balance, scenario.OwnerWallet.Balance), Is.EqualTo((0, 0)));
            Assert.That(scenario.State.Events.Count, Is.Zero);
        }

        private sealed class Scenario
        {
            public Scenario(Inventory source, Inventory thief, Wallet thiefWallet, Wallet ownerWallet, WorldState state)
            {
                Source = source;
                Thief = thief;
                ThiefWallet = thiefWallet;
                OwnerWallet = ownerWallet;
                State = state;
            }

            public Inventory Source { get; }
            public Inventory Thief { get; }
            public Wallet ThiefWallet { get; }
            public Wallet OwnerWallet { get; }
            public WorldState State { get; }
        }
    }
}
