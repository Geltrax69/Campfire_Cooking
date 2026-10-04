using System;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Adds the trade-route ledger (merchant journeys and day cursors) to the shared world state (P6-04).</summary>
    public sealed partial class WorldState
    {
        public TradeRouteLedger TradeLedger { get; } = new TradeRouteLedger();

        /// <summary>Installs a validated trade ledger snapshot for Persistence.</summary>
        internal void RestoreTradeLedger(TradeRouteLedgerSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            TradeLedger.Restore(snapshot);
        }
    }
}
