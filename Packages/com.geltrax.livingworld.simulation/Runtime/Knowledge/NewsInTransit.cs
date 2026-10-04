using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// Immutable news traveling between villages (P6-03): the news itself plus
    /// its route and the day it reaches the destination. What happens in
    /// Millbrook eventually reaches King's Rest, but late.
    /// </summary>
    public sealed class NewsInTransit
    {
        public NewsInTransit(VillageNews news, VillageId from, VillageId to, long arrivalDay)
        {
            if (news == null) throw new ArgumentNullException(nameof(news));
            if (!from.IsValid) throw new ArgumentException("News needs a valid source village.", nameof(from));
            if (!to.IsValid) throw new ArgumentException("News needs a valid destination village.", nameof(to));
            if (from == to) throw new ArgumentException("News does not travel within one village.", nameof(to));
            if (arrivalDay < news.DayCreated) throw new ArgumentOutOfRangeException(nameof(arrivalDay),
                "News cannot arrive before it was created.");

            News = news;
            From = from;
            To = to;
            ArrivalDay = arrivalDay;
        }

        public VillageNews News { get; }
        public VillageId From { get; }
        public VillageId To { get; }
        public long ArrivalDay { get; }
    }
}
