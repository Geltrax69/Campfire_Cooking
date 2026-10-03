using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>One villager's base tax assessment and the wallet it is collected from.</summary>
    public sealed class TaxAssessment
    {
        public TaxAssessment(NpcId taxpayer, Wallet wallet, int baseCopper)
        {
            if (!taxpayer.IsValid) throw new ArgumentException("A taxpayer is required.", nameof(taxpayer));
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (baseCopper < 1) throw new ArgumentOutOfRangeException(nameof(baseCopper));
            Taxpayer = taxpayer;
            Wallet = wallet;
            BaseCopper = baseCopper;
        }

        public NpcId Taxpayer { get; }
        public Wallet Wallet { get; }
        public int BaseCopper { get; }
    }

    /// <summary>Immutable caller configuration for the reeve's tax collection.</summary>
    public sealed class TaxConfiguration
    {
        public TaxConfiguration(string id, IReadOnlyList<TaxAssessment> assessments,
            LocationId collectionLocation, EventVisibility visibility)
        {
            ProductionConfiguration.RequireId(id, nameof(id));
            if (assessments == null) throw new ArgumentNullException(nameof(assessments));
            if (assessments.Count == 0) throw new ArgumentException("At least one assessment is required.", nameof(assessments));
            if (!collectionLocation.IsValid) throw new ArgumentException("A collection location is required.", nameof(collectionLocation));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            var ordered = new List<TaxAssessment>(assessments);
            var seen = new HashSet<NpcId>();
            foreach (TaxAssessment assessment in ordered)
            {
                if (assessment == null) throw new ArgumentException("Assessments cannot contain null.", nameof(assessments));
                if (!seen.Add(assessment.Taxpayer))
                    throw new ArgumentException("Each taxpayer must be assessed once.", nameof(assessments));
            }
            ordered.Sort((left, right) => left.Taxpayer.CompareTo(right.Taxpayer));
            Id = id;
            Assessments = new ReadOnlyCollection<TaxAssessment>(ordered);
            CollectionLocation = collectionLocation;
            Visibility = visibility;
        }

        public string Id { get; }
        public IReadOnlyList<TaxAssessment> Assessments { get; }
        public LocationId CollectionLocation { get; }
        public EventVisibility Visibility { get; }
    }

    /// <summary>
    /// Caller-owned, restorable record of tax progress: the last collection day. Starts
    /// uninitialized; the system stays quiet until the world-build step installs it, so a
    /// world opened mid-year never pays back-taxes. P2-12 extends the saver to write this
    /// state; until then use RestoreTax.
    /// </summary>
    public sealed class TaxState
    {
        public TaxState(bool initialized = false, long lastCollectionDay = 0)
        {
            if (initialized && lastCollectionDay < 0)
                throw new ArgumentOutOfRangeException(nameof(lastCollectionDay));
            IsInitialized = initialized;
            LastCollectionDay = lastCollectionDay;
        }

        public bool IsInitialized { get; }
        public long LastCollectionDay { get; internal set; }
    }

    /// <summary>
    /// The reeve's collection (economy.tax, Economy phase), twice a year: the 15th day of
    /// spring and the 15th day of autumn (ECONOMY.md §6). Each assessment scales with
    /// visible prosperity — good years pay more — via <see cref="ProsperityIndex"/>. The
    /// copper leaves the village to the crown: it is debited, not transferred. Taxes paid
    /// are zero-quantity <see cref="WorldEventType.Purchase"/> events (the Payroll
    /// convention for money without goods); a taxpayer who cannot cover the assessment
    /// logs a <see cref="WorldEventType.FailedPurchase"/> and the shortfall stays visible
    /// for the debt system (P2-10) — never covered silently.
    /// </summary>
    public sealed class TaxSystem : IWorldSystem
    {
        private readonly List<TaxConfiguration> _configurations;

        public TaxSystem(IEnumerable<TaxConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<TaxConfiguration>();
            foreach (TaxConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Tax configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "economy.tax";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.Tax.IsInitialized) return;
            foreach (TaxConfiguration configuration in _configurations)
            {
                while (true)
                {
                    long next = NextCollectionAfter(state.Tax.LastCollectionDay);
                    if (next > state.Clock.Day) break;
                    Collect(state, configuration);
                    state.Tax.LastCollectionDay = next;
                }
            }
        }

        /// <summary>
        /// The next collection day after the given 1-based day: day-of-year 15 (autumn)
        /// and 195 (spring). Pure and deterministic.
        /// </summary>
        internal static long NextCollectionAfter(long day)
        {
            long yearStart = (day - 1) / VillageCalendar.DaysPerYear * VillageCalendar.DaysPerYear + 1;
            long autumn = yearStart + 14;
            long spring = yearStart + 194;
            if (day < autumn) return autumn;
            if (day < spring) return spring;
            return yearStart + VillageCalendar.DaysPerYear + 14;
        }

        private static void Collect(WorldState state, TaxConfiguration configuration)
        {
            long total = ProsperityIndex.TotalVillageCopper(state);
            long baseline = state.EconomyBaseline.IsInitialized ? state.EconomyBaseline.BaselineCopper : 0;
            foreach (TaxAssessment assessment in configuration.Assessments)
            {
                int amount = baseline < 1 ? assessment.BaseCopper :
                    ProsperityIndex.ScaleByProsperity(assessment.BaseCopper, total, baseline);
                if (assessment.Wallet.TryDebit(amount))
                {
                    state.Events.Append(state.Clock, configuration.CollectionLocation,
                        WorldEventType.Purchase, ActorId.ForNpc(assessment.Taxpayer),
                        visibility: configuration.Visibility, quantity: 0, copper: amount);
                }
                else
                {
                    state.Events.Append(state.Clock, configuration.CollectionLocation,
                        WorldEventType.FailedPurchase, ActorId.ForNpc(assessment.Taxpayer),
                        visibility: configuration.Visibility, quantity: 0, copper: amount);
                }
            }
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TaxConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Tax configuration IDs must be unique.", "configurations");
        }
    }
}
