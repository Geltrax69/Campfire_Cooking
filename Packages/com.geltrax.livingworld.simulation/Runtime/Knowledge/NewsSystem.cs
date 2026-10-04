using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Carries news between villages once per day (P6-03). Millbrook's emergent
    /// events become news that travels to abstract neighbors over the road
    /// network; on arrival the destination's mood moves with the news valence,
    /// its opinion of the origin shifts, and the (possibly distorted) copy is
    /// filed as truth. Distortion is seeded: 20% of deliveries have their
    /// severity exaggerated or minimized by 20, the road's rumor margin.
    ///
    /// Draw order is fixed (event ID order, then ordinal VillageId order, then
    /// publish order for deliveries), so the RNG stream is deterministic.
    /// </summary>
    public sealed class NewsSystem : IWorldSystem
    {
        /// <summary>Percent chance a delivery is distorted in transit.</summary>
        public const int DistortionChancePercent = 20;

        /// <summary>Severity points added or removed when distortion hits.</summary>
        public const int DistortionSeverityDelta = 20;

        /// <summary>Reputation used for opinion shifts when the origin has no town stats.</summary>
        public const int FallbackReputation = 50;

        public string Id => "knowledge.news-travel";
        public SimulationPhase Phase => SimulationPhase.Memory;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            long day = state.Clock.Day;
            if (state.News.LastDeliveryDay >= day) return;

            state.News.LastDeliveryDay = day;
            ScanEmergentEvents(state, day);
            DeliverDue(state, day);
        }

        /// <summary>
        /// Whole travel days between two villages. Roads radiate from the full
        /// village, so pairs route via it: Millbrook to a neighbor is that
        /// neighbor's travel days; neighbor to neighbor is the sum.
        /// </summary>
        public static int TravelDays(VillageRegistry registry, VillageId from, VillageId to)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (!from.IsValid) throw new ArgumentException("A village ID must be valid.", nameof(from));
            if (!to.IsValid) throw new ArgumentException("A village ID must be valid.", nameof(to));
            if (from == to) return 0;
            return registry[from].TravelDaysFromMillbrook + registry[to].TravelDaysFromMillbrook;
        }

        private static void ScanEmergentEvents(WorldState state, long day)
        {
            AbstractVillageState origin = state.Villages.GetByLod(VillageLod.Full).SingleOrDefault();
            if (origin == null) return; // No full village: no emergent events to report.

            long cursor = state.News.LastProcessedEventId;
            foreach (WorldEvent e in state.Events.Query(type: WorldEventType.EmergentEventFired))
            {
                if (e.Id.Value <= cursor) continue;
                cursor = e.Id.Value;
                // The first ten news kinds match EmergentEventId declaration order,
                // which is also the truth event's quantity convention.
                if (!e.Quantity.HasValue || e.Quantity.Value < 0
                    || e.Quantity.Value > (int)NewsKind.BridgeProject) continue;
                var kind = (NewsKind)e.Quantity.Value;
                int severity = NewsKindInfo.DefaultSeverity(kind);
                foreach (AbstractVillageState dest in state.Villages.GetByLod(VillageLod.Abstract))
                {
                    long arrival = day + TravelDays(state.Villages, origin.Id, dest.Id);
                    state.News.Publish(origin.Id, origin.Id, kind, day, severity,
                        origin.Id, dest.Id, arrival);
                }
            }
            state.News.LastProcessedEventId = cursor;
        }

        private static void DeliverDue(WorldState state, long day)
        {
            // Snapshot first: RecordDelivery mutates the store mid-loop.
            List<NewsInTransit> due = state.News.GetDue(day).ToList();
            foreach (NewsInTransit transit in due)
            {
                VillageNews delivered = MaybeDistort(transit.News, state.Rng);
                state.News.RecordDelivery(transit, delivered);
                ApplyArrivalEffects(state, transit, delivered);
            }
        }

        private static VillageNews MaybeDistort(VillageNews news, SimRng rng)
        {
            if (rng.NextInt(100) >= DistortionChancePercent) return news;
            int delta = rng.NextInt(2) == 0 ? -DistortionSeverityDelta : DistortionSeverityDelta;
            int severity = Math.Max(VillageNews.MinSeverity,
                Math.Min(VillageNews.MaxSeverity, news.Severity + delta));
            return new VillageNews(news.Id, news.Origin, news.About, news.Kind,
                news.DayCreated, severity);
        }

        private static void ApplyArrivalEffects(WorldState state, NewsInTransit transit,
            VillageNews delivered)
        {
            AbstractVillageState dest = state.Villages[transit.To];
            int direction = NewsKindInfo.Valence(delivered.Kind) == NewsValence.Good ? 1 : -1;
            int magnitude = 1 + delivered.Severity / 25;
            dest.SetMood(dest.Mood + direction * magnitude);

            // A well-regarded origin's news carries more weight.
            int originReputation = OriginReputation(state, transit.From);
            int shift = (int)Math.Round(direction * magnitude * (originReputation / 50.0));
            state.News.ShiftOpinion(transit.From, transit.To, shift);
        }

        private static int OriginReputation(WorldState state, VillageId origin)
        {
            if (state.Villages[origin].Lod == VillageLod.Full && state.TownStats.Values != null)
                return state.TownStats.Values.Reputation;
            return FallbackReputation;
        }
    }
}
