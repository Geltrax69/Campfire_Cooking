using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// The village's pay API, for the wage economy and for the Agents layer to use later.
    /// Wages and levies are services rendered, so they are recorded as zero-quantity
    /// <see cref="WorldEventType.Purchase"/> events — the same convention RepairCommand uses
    /// for paid repairs. Every copper moves through <see cref="Wallet"/>, so money is
    /// conserved by construction.
    /// </summary>
    public static class Payroll
    {
        /// <summary>
        /// Pays a wage from one wallet to another, logging the payment. Returns false and
        /// logs a <see cref="WorldEventType.FailedPurchase"/> when the source cannot cover it.
        /// </summary>
        public static bool PayWage(WorldState state, Wallet from, Wallet to, NpcId worker,
            LocationId at, int copper, EventVisibility visibility)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));
            if (!worker.IsValid) throw new ArgumentException("A worker is required.", nameof(worker));
            if (!at.IsValid) throw new ArgumentException("A location is required.", nameof(at));
            if (copper < 1) throw new ArgumentOutOfRangeException(nameof(copper));
            if (from.TransferTo(to, copper))
            {
                state.Events.Append(state.Clock, at, WorldEventType.Purchase,
                    ActorId.ForNpc(worker), visibility: visibility, quantity: 0, copper: copper);
                return true;
            }
            state.Events.Append(state.Clock, at, WorldEventType.FailedPurchase,
                ActorId.ForNpc(worker), visibility: visibility, quantity: 0, copper: copper);
            return false;
        }

        /// <summary>
        /// Collects a levy into the village fund, logging the payment. Returns false and logs
        /// a <see cref="WorldEventType.FailedPurchase"/> when the payer cannot cover it — the
        /// shortfall stays visible in the log for the debt system (P2-10) to act on later.
        /// </summary>
        public static bool ChargeLevy(WorldState state, Wallet from, Wallet fund, NpcId payer,
            LocationId at, int copper, EventVisibility visibility)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (fund == null) throw new ArgumentNullException(nameof(fund));
            if (!payer.IsValid) throw new ArgumentException("A payer is required.", nameof(payer));
            if (!at.IsValid) throw new ArgumentException("A location is required.", nameof(at));
            if (copper < 1) throw new ArgumentOutOfRangeException(nameof(copper));
            if (from.TransferTo(fund, copper))
            {
                state.Events.Append(state.Clock, at, WorldEventType.Purchase,
                    ActorId.ForNpc(payer), visibility: visibility, quantity: 0, copper: copper);
                return true;
            }
            state.Events.Append(state.Clock, at, WorldEventType.FailedPurchase,
                ActorId.ForNpc(payer), visibility: visibility, quantity: 0, copper: copper);
            return false;
        }
    }
}
