using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable caller configuration for one bakery's morning bake.</summary>
    public sealed class BakingConfiguration
    {
        public BakingConfiguration(string id, LocationId bakery, NpcId baker, Inventory bakeryStock,
            ItemTypeId flour, ItemTypeId ryeBread, ItemTypeId barleyBread,
            int sacksPerBake, int ryeLoavesPerSack, int barleyLoavesPerSack, int bakeHour,
            EventVisibility visibility)
        {
            RequireId(id, nameof(id));
            if (!bakery.IsValid) throw new ArgumentException("A bakery location is required.", nameof(bakery));
            if (!baker.IsValid) throw new ArgumentException("A baker is required.", nameof(baker));
            if (bakeryStock == null) throw new ArgumentNullException(nameof(bakeryStock));
            if (!flour.IsValid) throw new ArgumentException("A flour item is required.", nameof(flour));
            if (!ryeBread.IsValid) throw new ArgumentException("A rye bread item is required.", nameof(ryeBread));
            if (!barleyBread.IsValid)
                throw new ArgumentException("A barley bread item is required.", nameof(barleyBread));
            if (ryeBread == barleyBread)
                throw new ArgumentException("The two loaf types must be different items.", nameof(barleyBread));
            bakeryStock.Count(flour);
            bakeryStock.Count(ryeBread);
            bakeryStock.Count(barleyBread);
            if (sacksPerBake < 1) throw new ArgumentOutOfRangeException(nameof(sacksPerBake));
            if (ryeLoavesPerSack < 1) throw new ArgumentOutOfRangeException(nameof(ryeLoavesPerSack));
            if (barleyLoavesPerSack < 1) throw new ArgumentOutOfRangeException(nameof(barleyLoavesPerSack));
            if (bakeHour < 0 || bakeHour > 23) throw new ArgumentOutOfRangeException(nameof(bakeHour));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            Id = id;
            Bakery = bakery;
            Baker = baker;
            BakeryStock = bakeryStock;
            Flour = flour;
            RyeBread = ryeBread;
            BarleyBread = barleyBread;
            SacksPerBake = sacksPerBake;
            RyeLoavesPerSack = ryeLoavesPerSack;
            BarleyLoavesPerSack = barleyLoavesPerSack;
            BakeHour = bakeHour;
            Visibility = visibility;
        }

        public string Id { get; }
        public LocationId Bakery { get; }
        public NpcId Baker { get; }
        public Inventory BakeryStock { get; }
        public ItemTypeId Flour { get; }
        public ItemTypeId RyeBread { get; }
        public ItemTypeId BarleyBread { get; }
        public int SacksPerBake { get; }
        public int RyeLoavesPerSack { get; }
        public int BarleyLoavesPerSack { get; }
        public int BakeHour { get; }
        public EventVisibility Visibility { get; }

        internal static void RequireId(string id, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A stable baking configuration ID is required.", parameterName);
        }
    }

    /// <summary>
    /// Fires each bakery's oven once per day at the first tick at or after the bake hour.
    /// The oven constraint (no second bake intraday, even with flour on hand) is enforced
    /// against world truth: the day's bake is recorded as Produced events in the event log,
    /// which is persisted, so a reloaded world — or a recreated system — never bakes twice.
    /// A morning with no flour still logs zero-loaf Produced events, so the attempt itself
    /// is visible in the log (villagers notice the cold oven) and the oven stays cold.
    /// </summary>
    public sealed class BakingSystem : IWorldSystem
    {
        private readonly List<BakingConfiguration> _configurations;
        private readonly Dictionary<string, long> _bakedDays;

        public BakingSystem(IEnumerable<BakingConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<BakingConfiguration>();
            foreach (BakingConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Baking configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort(Compare);
            ValidateConfigurations();
            _bakedDays = new Dictionary<string, long>(StringComparer.Ordinal);
        }

        public string Id => "economy.baking";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (BakingConfiguration configuration in _configurations)
            {
                long today = state.Clock.Day;
                if (BakedOn(configuration.Id, today)) continue;
                if (state.Clock.Hour < configuration.BakeHour) continue;
                if (HasBakedToday(state, configuration, today))
                {
                    MarkBaked(configuration.Id, today);
                    continue;
                }
                Bake(state, configuration);
                MarkBaked(configuration.Id, today);
            }
        }

        private bool BakedOn(string id, long day) =>
            _bakedDays.TryGetValue(id, out long bakedDay) && bakedDay == day;

        private void MarkBaked(string id, long day) => _bakedDays[id] = day;

        private static bool HasBakedToday(WorldState state, BakingConfiguration configuration, long today)
        {
            var dayStart = new GameTime((today - 1) * 1440);
            foreach (WorldEvent entry in state.Events.Query(dayStart, null,
                         configuration.Bakery, WorldEventType.Produced))
                if (entry.ItemType == configuration.RyeBread || entry.ItemType == configuration.BarleyBread)
                    return true;
            return false;
        }

        private static void Bake(WorldState state, BakingConfiguration configuration)
        {
            int sacks = Math.Min(configuration.SacksPerBake, configuration.BakeryStock.Count(configuration.Flour));
            int rye = checked(sacks * configuration.RyeLoavesPerSack);
            int barley = checked(sacks * configuration.BarleyLoavesPerSack);
            if (sacks > 0)
            {
                configuration.BakeryStock.EnsureCanReceive(configuration.RyeBread, rye);
                configuration.BakeryStock.EnsureCanReceive(configuration.BarleyBread, barley);
            }
            // Append first: the following single-threaded transfers were preflighted and cannot fail.
            state.Events.Append(state.Clock, configuration.Bakery, WorldEventType.Produced,
                ActorId.ForNpc(configuration.Baker), visibility: configuration.Visibility,
                itemType: configuration.RyeBread, quantity: rye);
            state.Events.Append(state.Clock, configuration.Bakery, WorldEventType.Produced,
                ActorId.ForNpc(configuration.Baker), visibility: configuration.Visibility,
                itemType: configuration.BarleyBread, quantity: barley);
            if (sacks == 0) return;
            if (!configuration.BakeryStock.TryRemove(configuration.Flour, sacks))
                throw new InvalidOperationException("Preflighted flour removal unexpectedly failed.");
            configuration.BakeryStock.Add(configuration.RyeBread, rye);
            configuration.BakeryStock.Add(configuration.BarleyBread, barley);
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (BakingConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Baking configuration IDs must be unique.", "configurations");
            for (int index = 1; index < _configurations.Count; index++)
                if (SameBakery(_configurations[index - 1], _configurations[index]))
                    throw new ArgumentException("Bakery configurations must be unique.", "configurations");
        }

        private static int Compare(BakingConfiguration left, BakingConfiguration right)
        {
            int comparison = left.Bakery.CompareTo(right.Bakery);
            if (comparison != 0) return comparison;
            comparison = left.Baker.CompareTo(right.Baker);
            return comparison != 0 ? comparison : string.CompareOrdinal(left.Id, right.Id);
        }

        private static bool SameBakery(BakingConfiguration left, BakingConfiguration right) =>
            left.Bakery == right.Bakery && left.Baker == right.Baker;
    }
}
