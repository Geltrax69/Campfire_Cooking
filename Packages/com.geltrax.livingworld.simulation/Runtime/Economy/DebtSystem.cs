using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable repayment terms for one debt (ECONOMY.md §8).</summary>
    public sealed class DebtTerms
    {
        public DebtTerms(int copperPerWeek = 0, ItemTypeId? itemPerWeek = null, int itemsPerWeek = 0,
            int itemCreditCopper = 0, int payChancePercent = 100, int overdueAfterDays = 60)
        {
            if (copperPerWeek < 0) throw new ArgumentOutOfRangeException(nameof(copperPerWeek));
            if (itemPerWeek.HasValue != (itemsPerWeek > 0))
                throw new ArgumentException("An in-kind schedule needs both an item and a weekly count.");
            if (itemPerWeek.HasValue && !itemPerWeek.Value.IsValid)
                throw new ArgumentException("Invalid in-kind item.", nameof(itemPerWeek));
            if (itemsPerWeek < 0) throw new ArgumentOutOfRangeException(nameof(itemsPerWeek));
            if (itemCreditCopper < 0) throw new ArgumentOutOfRangeException(nameof(itemCreditCopper));
            if (itemPerWeek.HasValue && itemCreditCopper < 1)
                throw new ArgumentException("An in-kind schedule needs a copper credit per weekly lot.");
            if (payChancePercent < 0 || payChancePercent > 100)
                throw new ArgumentOutOfRangeException(nameof(payChancePercent));
            if (overdueAfterDays < 1) throw new ArgumentOutOfRangeException(nameof(overdueAfterDays));
            CopperPerWeek = copperPerWeek;
            ItemPerWeek = itemPerWeek;
            ItemsPerWeek = itemsPerWeek;
            ItemCreditCopper = itemCreditCopper;
            PayChancePercent = payChancePercent;
            OverdueAfterDays = overdueAfterDays;
        }

        public int CopperPerWeek { get; }
        public ItemTypeId? ItemPerWeek { get; }
        public int ItemsPerWeek { get; }
        public int ItemCreditCopper { get; }
        public int PayChancePercent { get; }
        public int OverdueAfterDays { get; }

        /// <summary>Standard terms for a newly opened tab: 5 copper a week, every week.</summary>
        public static DebtTerms StandardTab => new DebtTerms(copperPerWeek: 5);
    }

    /// <summary>
    /// One entry in Tilda's tab ledger: who owes whom, how much is left, and the
    /// repayment schedule. Mutable progress lives here; P2-12 serializes it.
    /// </summary>
    public sealed class DebtRecord
    {
        public DebtRecord(NpcId debtor, NpcId creditor, int owedCopper, DebtTerms terms, long openedDay)
        {
            if (!debtor.IsValid) throw new ArgumentException("A debt needs a valid debtor.", nameof(debtor));
            if (!creditor.IsValid) throw new ArgumentException("A debt needs a valid creditor.", nameof(creditor));
            if (debtor == creditor) throw new ArgumentException("An NPC cannot owe itself.", nameof(debtor));
            if (owedCopper < 1) throw new ArgumentOutOfRangeException(nameof(owedCopper));
            if (terms == null) throw new ArgumentNullException(nameof(terms));
            if (openedDay < 1) throw new ArgumentOutOfRangeException(nameof(openedDay));
            Debtor = debtor;
            Creditor = creditor;
            OwedCopper = owedCopper;
            Terms = terms;
            OpenedDay = openedDay;
            LastWeeklyDay = openedDay;
        }

        public NpcId Debtor { get; }
        public NpcId Creditor { get; }
        public int OwedCopper { get; internal set; }
        public DebtTerms Terms { get; }
        public long OpenedDay { get; }
        /// <summary>Day of the last successful payment, or 0 if none yet.</summary>
        public long LastPaymentDay { get; internal set; }
        /// <summary>Last day the weekly schedule was processed.</summary>
        public long LastWeeklyDay { get; internal set; }
        /// <summary>True once the ~60-day overdue consequences have fired.</summary>
        public bool OverdueDeclared { get; internal set; }
    }

    /// <summary>
    /// Caller-owned, restorable record of the tab ledger. Debts are kept in deterministic
    /// (debtor, creditor) order. Starts uninitialized; the system stays quiet until the
    /// world-build step installs it. P2-12 extends the saver to write this state; until
    /// then use RestoreDebtLedger.
    /// </summary>
    public sealed class DebtLedgerState
    {
        private readonly List<DebtRecord> _debts = new List<DebtRecord>();

        public DebtLedgerState(bool initialized = false)
        {
            IsInitialized = initialized;
        }

        public bool IsInitialized { get; }
        public IReadOnlyList<DebtRecord> Debts => _debts.AsReadOnly();
        public long LastFeastYear { get; internal set; }

        internal void Add(DebtRecord debt)
        {
            if (debt == null) throw new ArgumentNullException(nameof(debt));
            if (Find(debt.Debtor, debt.Creditor) != null)
                throw new ArgumentException("The ledger already holds this pair's debt.");
            _debts.Add(debt);
            SortDebts();
        }

        internal bool Remove(DebtRecord debt)
        {
            if (debt == null) throw new ArgumentNullException(nameof(debt));
            return _debts.Remove(debt);
        }

        internal DebtRecord Find(NpcId debtor, NpcId creditor)
        {
            foreach (DebtRecord debt in _debts)
                if (debt.Debtor == debtor && debt.Creditor == creditor) return debt;
            return null;
        }

        internal void ReplaceAll(IEnumerable<DebtRecord> debts)
        {
            if (debts == null) throw new ArgumentNullException(nameof(debts));
            _debts.Clear();
            foreach (DebtRecord debt in debts)
            {
                if (debt == null) throw new ArgumentException("Debts cannot contain null.", nameof(debts));
                _debts.Add(debt);
            }
            SortDebts();
        }

        private void SortDebts()
        {
            _debts.Sort((left, right) =>
            {
                int byDebtor = string.CompareOrdinal(left.Debtor.Value, right.Debtor.Value);
                return byDebtor != 0 ? byDebtor :
                    string.CompareOrdinal(left.Creditor.Value, right.Creditor.Value);
            });
        }
    }

    /// <summary>Immutable caller configuration for the debt system.</summary>
    public sealed class DebtConfiguration
    {
        public DebtConfiguration(string id, LocationId ledgerLocation, EventVisibility visibility)
        {
            ProductionConfiguration.RequireId(id, nameof(id));
            if (!ledgerLocation.IsValid) throw new ArgumentException("A ledger location is required.", nameof(ledgerLocation));
            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));
            Id = id;
            LedgerLocation = ledgerLocation;
            Visibility = visibility;
        }

        public string Id { get; }
        public LocationId LedgerLocation { get; }
        public EventVisibility Visibility { get; }
    }

    /// <summary>
    /// The public tab-credit API: opening new tabs, gated by the creditor's trust.
    /// Tilda extends short credit to buyers she trusts (threshold 40 — she is in the
    /// business of tabs); Doran is more cautious (threshold 50). A debtor with an
    /// overdue-declared debt gets no new credit from anyone — the design's "no one
    /// extends them credit" — and Tilda refuses further tabs until the old one clears.
    /// New tabs are short: at most <see cref="MaxNewTabCopper"/> copper.
    /// </summary>
    public static class DebtLedger
    {
        /// <summary>Short credit cap for a newly opened tab (ECONOMY.md: tabs are small).</summary>
        public const int MaxNewTabCopper = 30;

        /// <summary>
        /// Opens a tab (or adds to the debtor's existing tab with this creditor).
        /// Returns false — and changes nothing — when the creditor's trust in the
        /// debtor is below <paramref name="minTrust"/>, the amount exceeds the short
        /// credit cap, or the debtor has an overdue-declared debt.
        /// </summary>
        public static bool OpenTab(WorldState state, NpcId creditor, NpcId debtor, int copper,
            int minTrust, LocationId at, EventVisibility visibility)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!creditor.IsValid) throw new ArgumentException("A creditor is required.", nameof(creditor));
            if (!debtor.IsValid) throw new ArgumentException("A debtor is required.", nameof(debtor));
            if (creditor == debtor) throw new ArgumentException("An NPC cannot tab itself.", nameof(debtor));
            if (copper < 1 || copper > MaxNewTabCopper) return false;
            if (minTrust < 0 || minTrust > 100) throw new ArgumentOutOfRangeException(nameof(minTrust));
            if (!at.IsValid) throw new ArgumentException("A location is required.", nameof(at));
            if (!state.DebtLedger.IsInitialized) return false;

            // Reason: Tilda believes credit ruins people — she only extends it to people
            // she trusts, and Doran is stricter still.
            if (state.Knowledge.Relationships.Trust(creditor, debtor) < minTrust) return false;
            foreach (DebtRecord debt in state.DebtLedger.Debts)
                if (debt.Debtor == debtor && debt.OverdueDeclared) return false;

            DebtRecord existing = state.DebtLedger.Find(debtor, creditor);
            if (existing != null)
            {
                existing.OwedCopper += copper;
            }
            else
            {
                var record = new DebtRecord(debtor, creditor, copper, DebtTerms.StandardTab, state.Clock.Day);
                state.DebtLedger.Add(record);
            }
            state.Events.Append(state.Clock, at, WorldEventType.Purchase, ActorId.ForNpc(debtor),
                new[] { ActorId.ForNpc(creditor) }, visibility, quantity: 0, copper: copper);
            return true;
        }
    }

    /// <summary>
    /// Tilda's tab ledger as a mechanical system (economy.debts, Economy phase,
    /// ECONOMY.md §8). Every seven days each debt's weekly payment comes due: copper
    /// moves wallet to wallet, Jory's smoked fish move inventory to inventory, and a
    /// debtor who cannot pay emits <see cref="WorldEventType.DebtMissed"/>
    /// (actor=debtor, targets=[creditor]) — the first emitter of the P2-02 convention,
    /// so the missed payment breaks the creditor's trust by rule. Irregular payers
    /// (Bessa) roll the seeded RNG each week: a skipped week is within her terms, not
    /// a miss. After ~60 days without payment the overdue consequences fire once:
    /// Tilda tells Elswith (a Conversation event — the council witnesses), a rumor
    /// reaches Bessa's tavern through the Knowledge rumor path, and the debtor is
    /// frozen out of new tabs. On Harvest Feast day, debts under 20 copper are
    /// forgiven, logged as zero-quantity ledger movements.
    /// </summary>
    public sealed class DebtSystem : IWorldSystem
    {
        // The council head Tilda tells when a debt goes overdue (ECONOMY.md §8).
        private static readonly NpcId CouncilHead = new NpcId("npc_elswith_alder");
        // Bessa keeps the tavern; overdue shame reaches her through the rumor path.
        private static readonly NpcId TavernKeeper = new NpcId("npc_bessa_marlowe");
        private static readonly LocationId Tavern = new LocationId("loc_tavern");

        // Debts under this many copper are forgiven at the Harvest Feast (symbolic).
        private const int FeastForgivenessThreshold = 20;
        // Tilda lived the missed payment, so her belief about it is strong.
        private const int MissedPaymentBeliefConfidence = 80;
        // A creditor's complaint about a known debtor is highly plausible, and the
        // claim travels intact: shame does not mutate into a different story.
        private const int TavernRumorPlausibility = 80;
        private const int TavernRumorConfidenceLoss = 10;

        private readonly List<DebtConfiguration> _configurations;

        public DebtSystem(IEnumerable<DebtConfiguration> configurations)
        {
            if (configurations == null) throw new ArgumentNullException(nameof(configurations));
            _configurations = new List<DebtConfiguration>();
            foreach (DebtConfiguration configuration in configurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Debt configurations cannot contain null.", nameof(configurations));
                _configurations.Add(configuration);
            }
            _configurations.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            ValidateConfigurations();
        }

        public string Id => "economy.debts";
        public SimulationPhase Phase => SimulationPhase.Economy;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.DebtLedger.IsInitialized) return;
            foreach (DebtConfiguration configuration in _configurations)
            {
                ProcessWeeks(state, configuration);
                DeclareOverdue(state, configuration);
                ForgiveAtFeast(state, configuration);
            }
        }

        private static void ProcessWeeks(WorldState state, DebtConfiguration configuration)
        {
            // Reason: iterate a copy — clearing a debt removes it from the ledger.
            foreach (DebtRecord debt in new List<DebtRecord>(state.DebtLedger.Debts))
            {
                while (debt.LastWeeklyDay + 7 <= state.Clock.Day && debt.OwedCopper > 0)
                {
                    debt.LastWeeklyDay += 7;
                    ProcessWeek(state, configuration, debt);
                }
            }
        }

        private static void ProcessWeek(WorldState state, DebtConfiguration configuration, DebtRecord debt)
        {
            // Reason: irregular payers (Bessa) roll each week; a skipped week is within
            // her terms — only a due payment that cannot be made is a miss. Steady
            // payers skip the roll so they never perturb the shared RNG stream.
            if (debt.Terms.PayChancePercent < 100 &&
                state.Rng.NextInt(100) >= debt.Terms.PayChancePercent) return;
            if (debt.Terms.ItemPerWeek.HasValue)
            {
                ProcessInKindWeek(state, configuration, debt);
                return;
            }
            if (debt.Terms.CopperPerWeek > 0) ProcessCopperWeek(state, configuration, debt);
        }

        private static void ProcessCopperWeek(WorldState state, DebtConfiguration configuration, DebtRecord debt)
        {
            int due = Math.Min(debt.Terms.CopperPerWeek, debt.OwedCopper);
            Wallet from = WalletOf(state, debt.Debtor);
            Wallet to = WalletOf(state, debt.Creditor);
            if (from != null && to != null && from.TransferTo(to, due))
            {
                debt.OwedCopper -= due;
                debt.LastPaymentDay = state.Clock.Day;
                state.Events.Append(state.Clock, configuration.LedgerLocation, WorldEventType.Purchase,
                    ActorId.ForNpc(debt.Debtor), new[] { ActorId.ForNpc(debt.Creditor) },
                    configuration.Visibility, quantity: 0, copper: due);
                if (debt.OwedCopper <= 0) state.DebtLedger.Remove(debt);
            }
            else
            {
                MissedPayment(state, configuration, debt, due);
            }
        }

        private static void ProcessInKindWeek(WorldState state, DebtConfiguration configuration, DebtRecord debt)
        {
            ItemTypeId item = debt.Terms.ItemPerWeek.Value;
            int count = debt.Terms.ItemsPerWeek;
            Inventory from = InventoryOf(state, debt.Debtor);
            Inventory to = InventoryOf(state, debt.Creditor);
            if (from != null && to != null && from.TransferTo(to, item, count))
            {
                // Reason: the weekly lot is fixed; the last lot clears any remainder.
                int credit = Math.Min(debt.Terms.ItemCreditCopper, debt.OwedCopper);
                debt.OwedCopper -= credit;
                debt.LastPaymentDay = state.Clock.Day;
                state.Events.Append(state.Clock, configuration.LedgerLocation, WorldEventType.Purchase,
                    ActorId.ForNpc(debt.Debtor), new[] { ActorId.ForNpc(debt.Creditor) },
                    configuration.Visibility, itemType: item, quantity: count, copper: credit);
                if (debt.OwedCopper <= 0) state.DebtLedger.Remove(debt);
            }
            else
            {
                MissedPayment(state, configuration, debt, debt.Terms.ItemCreditCopper);
            }
        }

        private static void MissedPayment(WorldState state, DebtConfiguration configuration,
            DebtRecord debt, int dueCopper)
        {
            // Reason: this is the P2-02 convention's first emitter — actor is the debtor,
            // targets[0] the creditor — so the missed payment breaks trust by rule.
            WorldEvent missed = state.Events.Append(state.Clock, configuration.LedgerLocation,
                WorldEventType.DebtMissed, ActorId.ForNpc(debt.Debtor),
                new[] { ActorId.ForNpc(debt.Creditor) }, configuration.Visibility);
            state.Events.Append(state.Clock, configuration.LedgerLocation, WorldEventType.FailedPurchase,
                ActorId.ForNpc(debt.Debtor), new[] { ActorId.ForNpc(debt.Creditor) },
                configuration.Visibility, quantity: 0, copper: dueCopper);
            // Reason: the creditor lived it — a direct participant's Seen-sourced belief,
            // so the overdue rumor has something true to spread later.
            if (state.Knowledge.TryGet(debt.Creditor, out BeliefStore store))
            {
                var claim = new BeliefClaim(BeliefClaimKind.WrongedBy, configuration.LedgerLocation,
                    subject: ActorId.ForNpc(debt.Debtor));
                store.Set(new Belief(claim,
                    new BeliefSource(BeliefSourceKind.Seen, originEventId: missed.Id),
                    MissedPaymentBeliefConfidence, state.Clock));
            }
        }

        private static void DeclareOverdue(WorldState state, DebtConfiguration configuration)
        {
            foreach (DebtRecord debt in new List<DebtRecord>(state.DebtLedger.Debts))
            {
                if (debt.OverdueDeclared || debt.OwedCopper <= 0) continue;
                long sinceActive = state.Clock.Day - Math.Max(debt.OpenedDay, debt.LastPaymentDay);
                if (sinceActive < debt.Terms.OverdueAfterDays) continue;
                debt.OverdueDeclared = true;
                // Reason: Tilda tells Elswith — the council witnesses the debt (world truth;
                // perception decides who notices).
                state.Events.Append(state.Clock, configuration.LedgerLocation, WorldEventType.Conversation,
                    ActorId.ForNpc(debt.Creditor), new[] { ActorId.ForNpc(CouncilHead) },
                    EventVisibility.Quiet);
                SpreadTavernRumor(state, debt);
            }
        }

        private static void SpreadTavernRumor(WorldState state, DebtRecord debt)
        {
            // Reason: use the Knowledge rumor path as a caller — no new rumor mechanics
            // here. Adoption stays trust-filtered and emergent: a distrusted creditor's
            // complaint can die unheard, which is the correct outcome.
            if (!state.Knowledge.TryGet(debt.Creditor, out _) ||
                !state.Knowledge.TryGet(TavernKeeper, out _)) return;
            int trust = state.Knowledge.Relationships.Trust(TavernKeeper, debt.Creditor);
            var context = new ConversationContext(debt.Creditor, TavernKeeper, Tavern,
                trust, TavernRumorPlausibility, TavernRumorConfidenceLoss, 0);
            RumorExchange.Share(state, context, new OverdueDebtPolicy(debt.Debtor), new NoDistortion());
        }

        private static void ForgiveAtFeast(WorldState state, DebtConfiguration configuration)
        {
            long year = (state.Clock.Day - 1) / VillageCalendar.DaysPerYear + 1;
            if (state.DebtLedger.LastFeastYear >= year) return;
            if (state.Clock.Day < VillageCalendar.HarvestFeastDay(state.Clock.Day)) return;
            state.DebtLedger.LastFeastYear = year;
            foreach (DebtRecord debt in new List<DebtRecord>(state.DebtLedger.Debts))
            {
                if (debt.OwedCopper <= 0 || debt.OwedCopper >= FeastForgivenessThreshold) continue;
                int forgiven = debt.OwedCopper;
                state.DebtLedger.Remove(debt);
                // Reason: forgiveness is logged like other ledger movements (the Payroll
                // convention) so the forgiven amount stays visible; nothing changes hands.
                state.Events.Append(state.Clock, configuration.LedgerLocation, WorldEventType.Purchase,
                    ActorId.ForNpc(debt.Debtor), new[] { ActorId.ForNpc(debt.Creditor) },
                    configuration.Visibility, quantity: 0, copper: forgiven);
            }
        }

        private static Wallet WalletOf(WorldState state, NpcId npc)
        {
            return state.Belongings.TryGet(ActorId.ForNpc(npc), out NpcBelongingsEntry entry)
                ? entry.Wallet : null;
        }

        private static Inventory InventoryOf(WorldState state, NpcId npc)
        {
            return state.Belongings.TryGet(ActorId.ForNpc(npc), out NpcBelongingsEntry entry)
                ? entry.Inventory : null;
        }

        private void ValidateConfigurations()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (DebtConfiguration configuration in _configurations)
                if (!ids.Add(configuration.Id))
                    throw new ArgumentException("Debt configuration IDs must be unique.", "configurations");
        }

        /// <summary>Selects the creditor's wronged-by belief about this debtor, strongest first.</summary>
        private sealed class OverdueDebtPolicy : IRumorSelectionPolicy
        {
            private readonly NpcId _debtor;

            public OverdueDebtPolicy(NpcId debtor)
            {
                _debtor = debtor;
            }

            public bool IsEligible(Belief belief, ConversationContext context)
            {
                return belief != null && context != null &&
                    belief.Claim.Kind == BeliefClaimKind.WrongedBy &&
                    belief.Claim.Subject.HasValue &&
                    belief.Claim.Subject.Value == ActorId.ForNpc(_debtor);
            }

            public int Salience(Belief belief, ConversationContext context) => belief.Confidence;
        }

        /// <summary>Shame travels intact: no distortion of the overdue claim.</summary>
        private sealed class NoDistortion : IRumorDistortionPolicy
        {
            public BeliefClaim Distort(BeliefClaim claim, ConversationContext context) => claim;
        }
    }
}
