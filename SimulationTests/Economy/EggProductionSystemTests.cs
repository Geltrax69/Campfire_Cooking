using System;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Hens lay ~0.7 eggs/day in warm months and ~0.2 in winter; the eggs land in
    /// the coop's inventory each day and are recorded as Produced truth events.
    /// </summary>
    public sealed class EggProductionSystemTests
    {
        private static readonly ItemTypeId Egg = new ItemTypeId("item_egg");

        private static ItemCatalog EggCatalog() => new ItemCatalog(new[]
        {
            new ItemDefinition(Egg, "Egg", "animal_product", 1, 1)
        });

        private static (World world, WorldState state, Inventory farmCoop, Inventory fennCoop)
            LayingVillage(ulong seed, long startDay)
        {
            var state = new WorldState(seed, new GameTime((startDay - 1) * 1440));
            var farm = new LocationId("loc_farm");
            var fennHome = new LocationId("loc_home_fenn");
            var chicken = new SpeciesId("species_chicken");
            for (int i = 1; i <= 14; i++)
                state.Animals.Add(AnimalState.Create(new AnimalId("animal_chicken_" + i.ToString("D3")),
                    chicken, farm, 25, null, AnimalAge.Adult, AnimalState.MaxHealth));
            for (int i = 15; i <= 20; i++)
                state.Animals.Add(AnimalState.Create(new AnimalId("animal_chicken_" + i.ToString("D3")),
                    chicken, fennHome, 25, null, AnimalAge.Adult, AnimalState.MaxHealth));

            var farmCoop = new Inventory(EggCatalog());
            var fennCoop = new Inventory(EggCatalog());
            var world = new World(state);
            world.RegisterSystem(new EggProductionSystem(new[]
            {
                new EggConfiguration("alder-coop", farm, farmCoop, EventVisibility.Quiet),
                new EggConfiguration("fenn-coop", fennHome, fennCoop, EventVisibility.Quiet),
            }));
            state.RestoreEggProduction(new EggProductionState(initialized: true,
                lastLayDay: startDay - 1));
            return (world, state, farmCoop, fennCoop);
        }

        [Test]
        public void EggProductionSeasonal()
        {
            var (summerWorld, summerState, summerFarm, summerFenn) =
                LayingVillage(seed: 31, startDay: 271); // summer
            var (winterWorld, winterState, winterFarm, winterFenn) =
                LayingVillage(seed: 31, startDay: 91); // winter
            for (int day = 0; day < 30; day++)
            {
                summerWorld.State.Clock = summerWorld.State.Clock.Advance(1440);
                summerWorld.Tick();
                winterWorld.State.Clock = winterWorld.State.Clock.Advance(1440);
                winterWorld.Tick();
            }

            int summerEggs = summerFarm.Count(Egg) + summerFenn.Count(Egg);
            int winterEggs = winterFarm.Count(Egg) + winterFenn.Count(Egg);
            Assert.That(summerEggs, Is.GreaterThan(winterEggs),
                "Hens lay ~0.7/day in warm months but only ~0.2 in winter.");
            Assert.That(winterEggs, Is.GreaterThan(0), "Hens still lay a little in winter.");
            _ = summerState;
            _ = winterState;
        }

        [Test]
        public void EggsGoToCoopInventory()
        {
            var (world, state, farmCoop, fennCoop) = LayingVillage(seed: 31, startDay: 271);
            for (int day = 0; day < 10; day++)
            {
                world.State.Clock = world.State.Clock.Advance(1440);
                world.Tick();
            }

            int produced = state.Events.Query(type: WorldEventType.Produced)
                .Where(e => e.ItemType == Egg)
                .Sum(e => e.Quantity ?? 0);
            Assert.That(farmCoop.Count(Egg) + fennCoop.Count(Egg), Is.EqualTo(produced),
                "Every laid egg lands in a coop inventory.");
            Assert.That(produced, Is.GreaterThan(0));
        }

        [Test]
        public void EggProductionIsDeterministic()
        {
            var (firstWorld, firstState, firstFarm, firstFenn) = LayingVillage(77, 271);
            var (secondWorld, secondState, secondFarm, secondFenn) = LayingVillage(77, 271);
            for (int day = 0; day < 30; day++)
            {
                firstWorld.State.Clock = firstWorld.State.Clock.Advance(1440);
                firstWorld.Tick();
                secondWorld.State.Clock = secondWorld.State.Clock.Advance(1440);
                secondWorld.Tick();
            }

            Assert.That(secondFarm.Count(Egg) + secondFenn.Count(Egg),
                Is.EqualTo(firstFarm.Count(Egg) + firstFenn.Count(Egg)));
            _ = firstState;
            _ = secondState;
        }

        [Test]
        public void ConfigurationValidation()
        {
            var inventory = new Inventory(EggCatalog());
            var farm = new LocationId("loc_farm");
            Assert.Throws<ArgumentNullException>(() => new EggProductionSystem(null));
            Assert.Throws<ArgumentException>(() => new EggProductionSystem(
                new EggConfiguration[] { null }));
            Assert.Throws<ArgumentException>(() => new EggProductionSystem(new[]
            {
                new EggConfiguration("dup", farm, inventory, EventVisibility.Quiet),
                new EggConfiguration("dup", farm, inventory, EventVisibility.Quiet),
            }));
            Assert.Throws<ArgumentNullException>(() =>
                new EggConfiguration("x", farm, null, EventVisibility.Quiet));
        }
    }
}
