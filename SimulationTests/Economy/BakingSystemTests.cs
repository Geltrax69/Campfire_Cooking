using System;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>Proves the once-a-day morning bake and the oven constraint.</summary>
    public sealed class BakingSystemTests
    {
        private static readonly LocationId Bakery = new LocationId("loc_bakery");
        private static readonly NpcId Baker = new NpcId("npc_oda_fenn");

        private sealed class BakedWorld
        {
            internal BakedWorld(World world, Inventory stock)
            {
                World = world;
                Stock = stock;
            }

            internal World World { get; }
            internal Inventory Stock { get; }
        }

        private static BakedWorld BakeWorld(int flourSacks, long startMinutes)
        {
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var stock = new Inventory(catalog);
            if (flourSacks > 0) stock.Add(BakeryChainSetup.Flour, flourSacks);
            var configuration = new BakingConfiguration("bakery", Bakery, Baker, stock,
                BakeryChainSetup.Flour, BakeryChainSetup.RyeBread, BakeryChainSetup.BarleyBread,
                2, 10, 10, 5, EventVisibility.Normal);
            var state = new WorldState(11, new GameTime(startMinutes));
            var world = new World(state);
            world.RegisterSystem(new BakingSystem(new[] { configuration }));
            return new BakedWorld(world, stock);
        }

        [Test]
        public void MorningBakeConvertsFlourToRyeAndBarley()
        {
            BakedWorld baked = BakeWorld(2, 4 * 60 + 59);

            baked.World.Tick(); // 05:00 — the oven fires.

            Assert.That((baked.Stock.Count(BakeryChainSetup.Flour),
                    baked.Stock.Count(BakeryChainSetup.RyeBread),
                    baked.Stock.Count(BakeryChainSetup.BarleyBread)),
                Is.EqualTo((0, 20, 20)));
            var produced = baked.World.State.Events
                .Query(null, null, Bakery, WorldEventType.Produced).ToList();
            Assert.That(produced.Count, Is.EqualTo(2));
            Assert.That(produced.Select(entry => (entry.ItemType, entry.Quantity)),
                Is.EquivalentTo(new[]
                {
                    ((ItemTypeId?)BakeryChainSetup.RyeBread, (int?)20),
                    ((ItemTypeId?)BakeryChainSetup.BarleyBread, (int?)20),
                }));
        }

        [Test]
        public void ShortBakeWhenFlourIsLow()
        {
            BakedWorld baked = BakeWorld(1, 4 * 60 + 59);

            baked.World.Tick();

            Assert.That((baked.Stock.Count(BakeryChainSetup.Flour),
                    baked.Stock.Count(BakeryChainSetup.RyeBread),
                    baked.Stock.Count(BakeryChainSetup.BarleyBread)),
                Is.EqualTo((0, 10, 10)));
        }

        [Test]
        public void NoFlourStillLogsTheAttempt()
        {
            BakedWorld baked = BakeWorld(0, 4 * 60 + 59);

            baked.World.Tick();

            var produced = baked.World.State.Events
                .Query(null, null, Bakery, WorldEventType.Produced).ToList();
            Assert.That(produced.Count, Is.EqualTo(2));
            Assert.That(produced.All(entry => entry.Quantity == 0), Is.True,
                "A flourless morning logs zero-loaf attempts so the oven stays cold.");
        }

        [Test]
        public void BakeWaitsForTheBakeHour()
        {
            BakedWorld baked = BakeWorld(2, 3 * 60);

            for (int i = 0; i < 60; i++) baked.World.Tick(); // 03:01 .. 04:00.
            Assert.That(baked.Stock.Count(BakeryChainSetup.RyeBread), Is.EqualTo(0),
                "No bake before 05:00.");
        }

        [Test]
        public void NoSecondBakeIntradayEvenWithFlour()
        {
            BakedWorld baked = BakeWorld(4, 4 * 60 + 59);

            baked.World.Tick(); // 05:00 — full bake: 4 flour -> 2 flour, 20/20 loaves.
            Assert.That(baked.Stock.Count(BakeryChainSetup.RyeBread), Is.EqualTo(20));

            baked.Stock.Add(BakeryChainSetup.Flour, 4); // A delivery arrives; the oven is cooling.
            int eventsBefore = baked.World.State.Events.Count;
            for (int i = 0; i < 18 * 60; i++) baked.World.Tick(); // tick out the whole day.

            Assert.That((baked.Stock.Count(BakeryChainSetup.RyeBread),
                    baked.Stock.Count(BakeryChainSetup.BarleyBread)),
                Is.EqualTo((20, 20)));
            Assert.That(baked.World.State.Events.Count, Is.EqualTo(eventsBefore),
                "No second bake fired intraday.");
        }

        [Test]
        public void RecreatedSystemDoesNotBakeTwice()
        {
            // The oven constraint survives system recreation because it reads the event log,
            // which is also what makes it survive save/load.
            BakedWorld baked = BakeWorld(4, 4 * 60 + 59);
            baked.World.Tick(); // 05:00 — bakes.
            Assert.That(baked.Stock.Count(BakeryChainSetup.RyeBread), Is.EqualTo(20));

            var configuration = new BakingConfiguration("bakery", Bakery, Baker, baked.Stock,
                BakeryChainSetup.Flour, BakeryChainSetup.RyeBread, BakeryChainSetup.BarleyBread,
                2, 10, 10, 5, EventVisibility.Normal);
            var revived = new World(baked.World.State);
            revived.RegisterSystem(new BakingSystem(new[] { configuration }));
            int eventsBefore = baked.World.State.Events.Count;
            for (int i = 0; i < 60; i++) revived.Tick();

            Assert.That(baked.World.State.Events.Count, Is.EqualTo(eventsBefore));
            Assert.That(baked.Stock.Count(BakeryChainSetup.RyeBread), Is.EqualTo(20));
        }

        [Test]
        public void BakesAgainTheNextMorning()
        {
            BakedWorld baked = BakeWorld(4, 4 * 60 + 59);

            for (int i = 0; i < 24 * 60 + 1; i++) baked.World.Tick(); // through 05:00 next day.

            Assert.That((baked.Stock.Count(BakeryChainSetup.RyeBread),
                    baked.Stock.Count(BakeryChainSetup.BarleyBread)),
                Is.EqualTo((40, 40)));
            Assert.That(baked.Stock.Count(BakeryChainSetup.Flour), Is.EqualTo(0));
        }

        [Test]
        public void InputsAreValidated()
        {
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var stock = new Inventory(catalog);
            Assert.Throws<ArgumentException>(() => new BakingConfiguration("", Bakery, Baker, stock,
                BakeryChainSetup.Flour, BakeryChainSetup.RyeBread, BakeryChainSetup.BarleyBread,
                2, 10, 10, 5, EventVisibility.Normal));
            Assert.Throws<ArgumentNullException>(() => new BakingConfiguration("bakery", Bakery, Baker, null,
                BakeryChainSetup.Flour, BakeryChainSetup.RyeBread, BakeryChainSetup.BarleyBread,
                2, 10, 10, 5, EventVisibility.Normal));
            Assert.Throws<ArgumentException>(() => new BakingConfiguration("bakery", Bakery, Baker, stock,
                BakeryChainSetup.Flour, BakeryChainSetup.RyeBread, BakeryChainSetup.RyeBread,
                2, 10, 10, 5, EventVisibility.Normal));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BakingConfiguration("bakery", Bakery, Baker, stock,
                BakeryChainSetup.Flour, BakeryChainSetup.RyeBread, BakeryChainSetup.BarleyBread,
                0, 10, 10, 5, EventVisibility.Normal));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BakingConfiguration("bakery", Bakery, Baker, stock,
                BakeryChainSetup.Flour, BakeryChainSetup.RyeBread, BakeryChainSetup.BarleyBread,
                2, 10, 10, 24, EventVisibility.Normal));
            Assert.Throws<ArgumentNullException>(() => new BakingSystem(null));
            Assert.Throws<ArgumentException>(() => new BakingSystem(new[]
            {
                new BakingConfiguration("bakery", Bakery, Baker, stock,
                    BakeryChainSetup.Flour, BakeryChainSetup.RyeBread, BakeryChainSetup.BarleyBread,
                    2, 10, 10, 5, EventVisibility.Normal),
                new BakingConfiguration("bakery", Bakery, Baker, stock,
                    BakeryChainSetup.Flour, BakeryChainSetup.RyeBread, BakeryChainSetup.BarleyBread,
                    2, 10, 10, 5, EventVisibility.Normal),
            }));
        }
    }
}
