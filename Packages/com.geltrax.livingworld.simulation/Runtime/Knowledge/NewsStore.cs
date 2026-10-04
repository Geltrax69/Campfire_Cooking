using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Owns inter-village news truth (P6-03): news traveling between villages
    /// and news that has arrived, plus each village's opinion of the others.
    /// News IDs are store-assigned ordinals; Capture/Restore carry the whole
    /// store across save/load (validated atomically: a bad snapshot leaves the
    /// store untouched).
    /// </summary>
    public sealed class NewsStore
    {
        public const int MinOpinion = 0;
        public const int MaxOpinion = 100;
        public const int DefaultOpinion = 50;

        private long _nextId = 1;
        private readonly List<NewsInTransit> _inTransit = new List<NewsInTransit>();
        private readonly List<ArrivedNews> _arrived = new List<ArrivedNews>();
        private readonly SortedDictionary<OpinionKey, int> _opinions =
            new SortedDictionary<OpinionKey, int>();
        private long _lastProcessedEventId;
        private long _lastDeliveryDay;

        /// <summary>Last world event ID the news system scanned; zero before any scan.</summary>
        internal long LastProcessedEventId
        {
            get => _lastProcessedEventId;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _lastProcessedEventId = value;
            }
        }

        /// <summary>Absolute day number news was last delivered; zero before the first run.</summary>
        internal long LastDeliveryDay
        {
            get => _lastDeliveryDay;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _lastDeliveryDay = value;
            }
        }

        /// <summary>
        /// Creates news and puts it in transit. The store assigns the NewsId;
        /// arrivalDay is computed by the caller from the route's travel days.
        /// </summary>
        public NewsInTransit Publish(VillageId origin, VillageId about, NewsKind kind,
            long dayCreated, int severity, VillageId from, VillageId to, long arrivalDay)
        {
            var news = new VillageNews(new NewsId(_nextId), origin, about, kind, dayCreated, severity);
            var transit = new NewsInTransit(news, from, to, arrivalDay);
            _nextId++;
            _inTransit.Add(transit);
            return transit;
        }

        /// <summary>Every news item still traveling, in publish order.</summary>
        public IReadOnlyList<NewsInTransit> GetInTransit() =>
            new List<NewsInTransit>(_inTransit).AsReadOnly();

        /// <summary>News due on or before the given day, in publish order.</summary>
        public IReadOnlyList<NewsInTransit> GetDue(long day)
        {
            var due = new List<NewsInTransit>();
            foreach (NewsInTransit transit in _inTransit)
                if (transit.ArrivalDay <= day) due.Add(transit);
            return due.AsReadOnly();
        }

        /// <summary>Every arrived news record, in delivery order.</summary>
        public IReadOnlyList<ArrivedNews> GetArrived() =>
            new List<ArrivedNews>(_arrived).AsReadOnly();

        /// <summary>News delivered to one village, in delivery order.</summary>
        public IReadOnlyList<ArrivedNews> GetArrivedFor(VillageId village)
        {
            if (!village.IsValid) throw new ArgumentException("A village ID must be valid.", nameof(village));
            var matching = new List<ArrivedNews>();
            foreach (ArrivedNews record in _arrived)
                if (record.DeliveredTo == village) matching.Add(record);
            return matching.AsReadOnly();
        }

        /// <summary>
        /// Records a delivery: removes the transit and files the (possibly
        /// distorted) copy as arrived truth. The delivered copy must keep the
        /// original news ID so copies trace back to one truth.
        /// </summary>
        public void RecordDelivery(NewsInTransit transit, VillageNews delivered)
        {
            if (transit == null) throw new ArgumentNullException(nameof(transit));
            if (delivered == null) throw new ArgumentNullException(nameof(delivered));
            if (delivered.Id != transit.News.Id)
                throw new ArgumentException("Delivered news must keep its news ID.", nameof(delivered));
            if (!_inTransit.Remove(transit))
                throw new ArgumentException("That news is not in transit.", nameof(transit));
            _arrived.Add(new ArrivedNews(delivered, transit.To));
        }

        /// <summary>One village's opinion of another, 0-100; 50 (neutral) until moved.</summary>
        public int GetOpinion(VillageId from, VillageId to)
        {
            ValidateOpinionPair(from, to, nameof(from));
            return _opinions.TryGetValue(new OpinionKey(from, to), out int opinion)
                ? opinion : DefaultOpinion;
        }

        /// <summary>Moves an opinion by a delta, clamped to 0-100.</summary>
        public void ShiftOpinion(VillageId from, VillageId to, int delta)
        {
            ValidateOpinionPair(from, to, nameof(from));
            int next = Math.Max(MinOpinion, Math.Min(MaxOpinion, GetOpinion(from, to) + delta));
            _opinions[new OpinionKey(from, to)] = next;
        }

        /// <summary>Captures the whole store plus cursors for Persistence.</summary>
        public NewsStoreSnapshot Capture()
        {
            var opinions = new List<OpinionRecord>();
            foreach (KeyValuePair<OpinionKey, int> pair in _opinions)
                opinions.Add(new OpinionRecord(pair.Key.From, pair.Key.To, pair.Value));
            return new NewsStoreSnapshot(_nextId, _inTransit, _arrived, opinions,
                _lastProcessedEventId, _lastDeliveryDay);
        }

        /// <summary>
        /// Installs a validated snapshot atomically: validation runs first (in the
        /// snapshot constructor), so a rejected snapshot leaves the store unchanged.
        /// </summary>
        public void Restore(NewsStoreSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            _nextId = snapshot.NextId;
            _inTransit.Clear();
            _inTransit.AddRange(snapshot.InTransit);
            _arrived.Clear();
            _arrived.AddRange(snapshot.Arrived);
            _opinions.Clear();
            foreach (OpinionRecord record in snapshot.Opinions)
                _opinions.Add(new OpinionKey(record.From, record.To), record.Opinion);
            _lastProcessedEventId = snapshot.LastProcessedEventId;
            _lastDeliveryDay = snapshot.LastDeliveryDay;
        }

        private static void ValidateOpinionPair(VillageId from, VillageId to, string parameterName)
        {
            if (!from.IsValid) throw new ArgumentException("A village ID must be valid.", parameterName);
            if (!to.IsValid) throw new ArgumentException("A village ID must be valid.", nameof(to));
        }

        private readonly struct OpinionKey : IComparable<OpinionKey>
        {
            public readonly VillageId From;
            public readonly VillageId To;

            public OpinionKey(VillageId from, VillageId to)
            {
                From = from;
                To = to;
            }

            public int CompareTo(OpinionKey other)
            {
                int byFrom = From.CompareTo(other.From);
                return byFrom != 0 ? byFrom : To.CompareTo(other.To);
            }
        }
    }
}
