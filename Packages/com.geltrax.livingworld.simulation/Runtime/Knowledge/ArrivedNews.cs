using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Immutable record of news that reached a village (P6-03). The delivered
    /// copy carries the severity the destination actually heard, which may have
    /// been distorted in transit; the ID is unchanged so copies trace back to
    /// the same truth.
    /// </summary>
    public sealed class ArrivedNews
    {
        public ArrivedNews(VillageNews news, VillageId deliveredTo)
        {
            if (news == null) throw new ArgumentNullException(nameof(news));
            if (!deliveredTo.IsValid)
                throw new ArgumentException("Arrival needs a valid destination village.", nameof(deliveredTo));

            News = news;
            DeliveredTo = deliveredTo;
        }

        public VillageNews News { get; }
        public VillageId DeliveredTo { get; }
    }
}
