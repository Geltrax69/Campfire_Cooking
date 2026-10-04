using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Distributes a deceased NPC's estate to heirs (P7-03). Heir priority:
    /// designated heir (the will) → living spouse → living children (eldest
    /// first, money split equally) → living parents → the village fund.
    /// Money is conserved: every copper leaves the deceased's wallet and
    /// lands in a heir's wallet or the fund. Items go to the primary heir
    /// as one bundle. Each transfer is recorded as an Inheritance truth
    /// event with the deceased as actor and the recipient as target.
    /// </summary>
    /// <remarks>
    /// Simplifications, all documented here and in the tests:
    /// - A will names one heir for everything (real wills can split bequests).
    /// - A designated heir who is already dead is skipped; the normal
    ///   priority applies (the will fails, it does not disinherit the family).
    /// - Children split money equally (integer division, remainder to the
    ///   eldest); all items go to the eldest child. Parents split the same way.
    /// - A child heir under 15 never holds money directly: the share is held
    ///   by the eldest living adult in the child's household (the guardian),
    ///   mingled in the guardian's wallet. With no guardian, the share goes
    ///   to the village fund.
    /// - With no heirs at all, money goes to the village fund and items go to
    ///   the eldest living adult in the deceased's household (the household
    ///   absorbs the goods). If there is no such adult, items stay in the
    ///   deceased's inventory, documented as unclaimed pending a future
    ///   auction phase — nothing is destroyed and no money is invented.
    /// </remarks>
    public static class Inheritance
    {
        /// <summary>Children below this age have their share held by a guardian.</summary>
        public const int AdultAge = 15;

        /// <summary>
        /// Distributes the deceased NPC's wallet and inventory to heirs.
        /// Throws if the NPC is not registered, is still alive, or has no
        /// registered belongings. Idempotent only through InheritanceSystem's
        /// processed-set; calling twice moves nothing the second time only
        /// because the wallet is already empty (heirs are resolved again, so
        /// do not call twice — use the system).
        /// </summary>
        public static void Distribute(WorldState state, NpcId deceasedId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!deceasedId.IsValid) throw new ArgumentException("A deceased NPC ID is required.", nameof(deceasedId));
            NpcState deceased = state.Npcs[deceasedId];
            if (!deceased.IsDeceased)
                throw new InvalidOperationException("The living have no estate to distribute.");
            if (!state.Belongings.TryGet(ActorId.ForNpc(deceasedId), out NpcBelongingsEntry estate))
                throw new ArgumentException("The deceased has no registered belongings.", nameof(deceasedId));

            List<NpcId> heirs = ResolveHeirs(state, deceased);
            var home = deceased.Definition.Home;
            var deceasedActor = ActorId.ForNpc(deceasedId);

            int money = estate.Wallet.Balance;
            if (money > 0)
            {
                if (heirs.Count == 0)
                {
                    TransferMoney(state, estate.Wallet, state.VillageFund.Funds, money,
                        deceasedActor, null, home);
                }
                else if (heirs.Count == 1)
                {
                    DistributeShare(state, estate.Wallet, heirs[0], money,
                        deceasedActor, home);
                }
                else
                {
                    // Split equally; integer remainder goes to the eldest (first).
                    int share = money / heirs.Count;
                    int remainder = money % heirs.Count;
                    for (int i = 0; i < heirs.Count; i++)
                    {
                        int amount = share + (i == 0 ? remainder : 0);
                        if (amount <= 0) continue;
                        DistributeShare(state, estate.Wallet, heirs[i], amount,
                            deceasedActor, home);
                    }
                }
            }

            var items = estate.Inventory.Contents;
            if (items.Count > 0)
            {
                NpcId? itemHeir = heirs.Count > 0
                    ? (NpcId?)heirs[0]
                    : EldestLivingAdultInHousehold(state, deceased);
                if (itemHeir.HasValue)
                {
                    TransferItems(state, estate.Inventory, InventoryOf(state, itemHeir.Value),
                        deceasedActor, itemHeir.Value, home);
                }
                // Else: items stay in the deceased's inventory, unclaimed (see remarks).
            }
        }

        /// <summary>
        /// Heir priority: designated heir → spouse → children (eldest first) →
        /// parents (mother, then father). Returns an empty list when nobody
        /// qualifies; the caller then falls back to the village fund.
        /// </summary>
        internal static List<NpcId> ResolveHeirs(WorldState state, NpcState deceased)
        {
            if (deceased.DesignatedHeirId.HasValue &&
                IsLiving(state, deceased.DesignatedHeirId.Value))
                return new List<NpcId> { deceased.DesignatedHeirId.Value };

            if (deceased.PartnerId.HasValue && IsLiving(state, deceased.PartnerId.Value))
                return new List<NpcId> { deceased.PartnerId.Value };

            var children = deceased.ChildrenIds
                .Where(id => IsLiving(state, id))
                .Select(id => state.Npcs[id])
                .OrderByDescending(npc => npc.Age)
                .ThenBy(npc => npc.Definition.Id.Value, StringComparer.Ordinal)
                .Select(npc => npc.Definition.Id)
                .ToList();
            if (children.Count > 0) return children;

            var parents = new List<NpcId>();
            if (deceased.MotherId.HasValue && IsLiving(state, deceased.MotherId.Value))
                parents.Add(deceased.MotherId.Value);
            if (deceased.FatherId.HasValue && IsLiving(state, deceased.FatherId.Value))
                parents.Add(deceased.FatherId.Value);
            return parents;
        }

        /// <summary>
        /// A child heir under 15 has their share held by the eldest living
        /// adult in their household (the guardian), mingled in the guardian's
        /// wallet. With no living adult in the household, the share goes to
        /// the village fund. The event always names the true heir as target.
        /// </summary>
        private static void DistributeShare(WorldState state, Wallet estate,
            NpcId heirId, int amount, ActorId deceasedActor, LocationId home)
        {
            NpcState heir = state.Npcs[heirId];
            Wallet destination;
            if (heir.Age >= AdultAge)
            {
                destination = WalletOf(state, heirId);
            }
            else
            {
                NpcId? guardian = EldestLivingAdultInHousehold(state, heir);
                destination = guardian.HasValue
                    ? WalletOf(state, guardian.Value)
                    : state.VillageFund.Funds;
            }
            TransferMoney(state, estate, destination, amount,
                deceasedActor, heirId, home);
        }

        /// <summary>Eldest living adult (age 15+) in the NPC's household, if any.</summary>
        private static NpcId? EldestLivingAdultInHousehold(WorldState state, NpcState npc)
        {
            if (!npc.HouseholdId.HasValue) return null;
            if (!state.Households.Contains(npc.HouseholdId.Value)) return null;
            Household household = state.Households[npc.HouseholdId.Value];
            NpcId best = default;
            int bestAge = -1;
            foreach (NpcId memberId in household.Members)
            {
                NpcState member = state.Npcs[memberId];
                if (member.IsDeceased || member.Age < AdultAge) continue;
                bool older = member.Age > bestAge ||
                    (member.Age == bestAge && string.CompareOrdinal(
                        memberId.Value, best.Value) < 0);
                if (older)
                {
                    best = memberId;
                    bestAge = member.Age;
                }
            }
            return bestAge >= 0 ? (NpcId?)best : null;
        }

        private static bool IsLiving(WorldState state, NpcId id) =>
            state.Npcs[id].IsDeceased == false;

        private static Wallet WalletOf(WorldState state, NpcId npc) =>
            state.Belongings[ActorId.ForNpc(npc)].Wallet;

        private static Inventory InventoryOf(WorldState state, NpcId npc) =>
            state.Belongings[ActorId.ForNpc(npc)].Inventory;

        private static void TransferMoney(WorldState state, Wallet from, Wallet to,
            int amount, ActorId deceasedActor, NpcId? heirId, LocationId home)
        {
            if (!from.TransferTo(to, amount))
                throw new InvalidOperationException(
                    "Inheritance money transfer failed; the estate was over-drawn.");
            var targets = heirId.HasValue
                ? new[] { ActorId.ForNpc(heirId.Value) }
                : Array.Empty<ActorId>();
            state.Events.Append(state.Clock, home, WorldEventType.Inheritance,
                actor: deceasedActor, targets: targets,
                visibility: EventVisibility.Normal, copper: amount);
        }

        private static void TransferItems(WorldState state, Inventory from, Inventory to,
            ActorId deceasedActor, NpcId heirId, LocationId home)
        {
            int totalUnits = 0;
            foreach (var lot in from.Contents)
            {
                if (!from.TransferTo(to, lot.Key, lot.Value))
                    throw new InvalidOperationException(
                        "Inheritance item transfer failed; the estate was over-drawn.");
                totalUnits += lot.Value;
            }
            state.Events.Append(state.Clock, home, WorldEventType.Inheritance,
                actor: deceasedActor, targets: new[] { ActorId.ForNpc(heirId) },
                visibility: EventVisibility.Normal, quantity: totalUnits);
        }
    }
}
