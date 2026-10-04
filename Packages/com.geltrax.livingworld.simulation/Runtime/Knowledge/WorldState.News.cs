using System;
using LivingWorld.Simulation.Knowledge;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds the inter-village news store to the shared world state (P6-03).</summary>
    public sealed partial class WorldState
    {
        public NewsStore News { get; } = new NewsStore();

        /// <summary>Installs a validated news snapshot for Persistence.</summary>
        internal void RestoreNews(NewsStoreSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            News.Restore(snapshot);
        }
    }
}
