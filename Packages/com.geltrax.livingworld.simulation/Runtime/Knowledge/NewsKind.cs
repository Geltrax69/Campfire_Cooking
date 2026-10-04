namespace LivingWorld.Simulation.Knowledge
{
    /// <summary>
    /// What a piece of inter-village news is about (P6-03). The first ten values
    /// match EmergentEventId declaration order, so an EmergentEventFired truth
    /// event's quantity maps directly onto a news kind.
    /// </summary>
    public enum NewsKind
    {
        FoodShortage = 0,
        WolfAttack = 1,
        Festival = 2,
        Fire = 3,
        TheftWave = 4,
        MerchantArrival = 5,
        Fever = 6,
        Drought = 7,
        WheelFailure = 8,
        BridgeProject = 9,
        GoodHarvest = 10,
        NewcomersArrived = 11,
    }

    /// <summary>Whether news lifts or drags a destination village's mood.</summary>
    public enum NewsValence
    {
        Good,
        Bad,
    }

    /// <summary>
    /// Per-kind tuning for inter-village news (P6-03). Severities are judgment
    /// calls: how much the road talks about each kind of happening.
    /// </summary>
    public static class NewsKindInfo
    {
        /// <summary>Good news lifts mood on arrival; bad news drags it.</summary>
        public static NewsValence Valence(NewsKind kind)
        {
            switch (kind)
            {
                case NewsKind.Festival:
                case NewsKind.MerchantArrival:
                case NewsKind.BridgeProject:
                case NewsKind.GoodHarvest:
                case NewsKind.NewcomersArrived:
                    return NewsValence.Good;
                default:
                    return NewsValence.Bad;
            }
        }

        /// <summary>Default severity 0-100 when news is raised from a world event.</summary>
        public static int DefaultSeverity(NewsKind kind)
        {
            switch (kind)
            {
                case NewsKind.FoodShortage: return 70;
                case NewsKind.WolfAttack: return 60;
                case NewsKind.Festival: return 40;
                case NewsKind.Fire: return 55;
                case NewsKind.TheftWave: return 45;
                case NewsKind.MerchantArrival: return 35;
                case NewsKind.Fever: return 65;
                case NewsKind.Drought: return 55;
                case NewsKind.WheelFailure: return 50;
                case NewsKind.BridgeProject: return 45;
                case NewsKind.GoodHarvest: return 40;
                case NewsKind.NewcomersArrived: return 30;
                default: return 50;
            }
        }
    }
}
