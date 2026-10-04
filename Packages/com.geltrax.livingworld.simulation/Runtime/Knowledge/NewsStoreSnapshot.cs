using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// One village's opinion of another, 0-100 (P6-03). A deliberate
    /// simplification: the full reputation-by-group machinery (D-09) is
    /// Millbrook-local; between villages the prototype tracks one number.
    /// </summary>
    public sealed class OpinionRecord
    {
        public OpinionRecord(VillageId from, VillageId to, int opinion)
        {
            if (!from.IsValid) throw new ArgumentException("Opinion needs a valid source village.", nameof(from));
            if (!to.IsValid) throw new ArgumentException("Opinion needs a valid subject village.", nameof(to));
            if (opinion < NewsStore.MinOpinion || opinion > NewsStore.MaxOpinion)
                throw new ArgumentOutOfRangeException(nameof(opinion), "Opinion is 0-100.");

            From = from;
            To = to;
            Opinion = opinion;
        }

        public VillageId From { get; }
        public VillageId To { get; }
        public int Opinion { get; }
    }

    /// <summary>
    /// Immutable persistence snapshot for the inter-village news store (P6-03).
    /// Validated on construction so a bad snapshot is rejected before it can
    /// touch the live store.
    /// </summary>
    public sealed class NewsStoreSnapshot
    {
        public NewsStoreSnapshot(long nextId, IEnumerable<NewsInTransit> inTransit,
            IEnumerable<ArrivedNews> arrived, IEnumerable<OpinionRecord> opinions,
            long lastProcessedEventId, long lastDeliveryDay)
        {
            if (nextId < 1) throw new ArgumentOutOfRangeException(nameof(nextId));
            if (inTransit == null) throw new ArgumentNullException(nameof(inTransit));
            if (arrived == null) throw new ArgumentNullException(nameof(arrived));
            if (opinions == null) throw new ArgumentNullException(nameof(opinions));
            if (lastProcessedEventId < 0) throw new ArgumentOutOfRangeException(nameof(lastProcessedEventId));
            if (lastDeliveryDay < 0) throw new ArgumentOutOfRangeException(nameof(lastDeliveryDay));

            var seenIds = new HashSet<long>();
            var stagedTransit = new List<NewsInTransit>();
            foreach (NewsInTransit transit in inTransit)
            {
                if (transit == null) throw new ArgumentException("In-transit news cannot contain null.", nameof(inTransit));
                if (!seenIds.Add(transit.News.Id.Value))
                    throw new ArgumentException("Duplicate news ID in snapshot.", nameof(inTransit));
                if (transit.News.Id.Value >= nextId)
                    throw new ArgumentException("News ID must be below the next-ID cursor.", nameof(inTransit));
                stagedTransit.Add(transit);
            }
            var stagedArrived = new List<ArrivedNews>();
            foreach (ArrivedNews record in arrived)
            {
                if (record == null) throw new ArgumentException("Arrived news cannot contain null.", nameof(arrived));
                if (!seenIds.Add(record.News.Id.Value))
                    throw new ArgumentException("Duplicate news ID in snapshot.", nameof(arrived));
                if (record.News.Id.Value >= nextId)
                    throw new ArgumentException("News ID must be below the next-ID cursor.", nameof(arrived));
                stagedArrived.Add(record);
            }
            var stagedOpinions = new List<OpinionRecord>();
            var seenPairs = new HashSet<string>();
            foreach (OpinionRecord record in opinions)
            {
                if (record == null) throw new ArgumentException("Opinions cannot contain null.", nameof(opinions));
                string key = record.From.Value + "|" + record.To.Value;
                if (!seenPairs.Add(key))
                    throw new ArgumentException("Duplicate opinion pair in snapshot.", nameof(opinions));
                stagedOpinions.Add(record);
            }

            NextId = nextId;
            InTransit = stagedTransit.AsReadOnly();
            Arrived = stagedArrived.AsReadOnly();
            Opinions = stagedOpinions.AsReadOnly();
            LastProcessedEventId = lastProcessedEventId;
            LastDeliveryDay = lastDeliveryDay;
        }

        public long NextId { get; }
        public IReadOnlyList<NewsInTransit> InTransit { get; }
        public IReadOnlyList<ArrivedNews> Arrived { get; }
        public IReadOnlyList<OpinionRecord> Opinions { get; }
        public long LastProcessedEventId { get; }
        public long LastDeliveryDay { get; }
    }
}
