using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable persistence snapshot for the trade-route ledger.</summary>
    public sealed class TradeRouteLedgerSnapshot
    {
        public TradeRouteLedgerSnapshot(long lastProcessedDay, long nextDepartureDay,
            IEnumerable<MerchantJourneySnapshot> journeys)
        {
            if (lastProcessedDay < 0) throw new ArgumentOutOfRangeException(nameof(lastProcessedDay));
            if (nextDepartureDay < 1) throw new ArgumentOutOfRangeException(nameof(nextDepartureDay));
            var copied = new List<MerchantJourneySnapshot>();
            if (journeys != null) copied.AddRange(journeys);
            foreach (var journey in copied)
                if (journey == null) throw new ArgumentException("Journey snapshots cannot contain null.", nameof(journeys));
            LastProcessedDay = lastProcessedDay;
            NextDepartureDay = nextDepartureDay;
            Journeys = new ReadOnlyCollection<MerchantJourneySnapshot>(copied);
        }

        public long LastProcessedDay { get; }
        public long NextDepartureDay { get; }
        public IReadOnlyList<MerchantJourneySnapshot> Journeys { get; }
    }

    /// <summary>
    /// Caller-owned, restorable trade-route state (P6-02): the last processed
    /// day, the next scheduled departure day, and every merchant journey. The
    /// owning system mutates it; <see cref="Capture"/>/<see cref="Restore"/>
    /// carry it across save/load (validated atomically: a bad snapshot leaves
    /// the ledger untouched). The P6 acceptance task wires these into Persistence.
    /// </summary>
    public sealed class TradeRouteLedger
    {
        private readonly List<MerchantJourney> _journeys = new List<MerchantJourney>();

        /// <summary>Absolute day number of the last processed day; 0 when never ticked.</summary>
        public long LastProcessedDay { get; private set; }

        /// <summary>Absolute day number of the next scheduled departure; starts at 1.</summary>
        public long NextDepartureDay { get; private set; } = 1;

        /// <summary>Every journey ever launched, in departure order (completed ones included).</summary>
        public IReadOnlyList<MerchantJourney> Journeys =>
            new ReadOnlyCollection<MerchantJourney>(_journeys);

        internal void RecordProcessedDay(long day)
        {
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            if (day > LastProcessedDay) LastProcessedDay = day;
        }

        internal void AddJourney(MerchantJourney journey)
        {
            if (journey == null) throw new ArgumentNullException(nameof(journey));
            _journeys.Add(journey);
        }

        internal void ScheduleNextDeparture(long day)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            NextDepartureDay = day;
        }

        /// <summary>Captures the day cursors and every journey for Persistence.</summary>
        public TradeRouteLedgerSnapshot Capture()
        {
            var snapshots = new List<MerchantJourneySnapshot>(_journeys.Count);
            foreach (MerchantJourney journey in _journeys) snapshots.Add(journey.Capture());
            return new TradeRouteLedgerSnapshot(LastProcessedDay, NextDepartureDay, snapshots);
        }

        /// <summary>
        /// Installs a validated snapshot atomically: every journey is rebuilt
        /// before anything is replaced, so a rejected snapshot leaves the ledger
        /// unchanged.
        /// </summary>
        public void Restore(TradeRouteLedgerSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var rebuilt = new List<MerchantJourney>(snapshot.Journeys.Count);
            foreach (MerchantJourneySnapshot journeySnapshot in snapshot.Journeys)
                rebuilt.Add(MerchantJourney.Restore(journeySnapshot));
            _journeys.Clear();
            _journeys.AddRange(rebuilt);
            LastProcessedDay = snapshot.LastProcessedDay;
            NextDepartureDay = snapshot.NextDepartureDay;
        }
    }
}
