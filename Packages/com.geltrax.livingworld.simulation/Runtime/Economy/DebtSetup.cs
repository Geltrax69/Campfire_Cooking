using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// World-build step that opens Tilda's tab ledger (ECONOMY.md §8): the five
    /// outstanding tabs, 340 copper total, no interest. The ledger opens with every
    /// debt current — the weekly clock and the ~60-day overdue clock start at the
    /// world's first day; the design's "last winter" and "over the summer" are the
    /// reason the tabs exist, not a head start on the consequences. A world opened
    /// after this year's feast does not forgive on its first tick. Run after
    /// MoneySourcesSetup — repayments need the personal wallets it registers.
    /// </summary>
    public static class DebtSetup
    {
        /// <summary>Tilda is in the business of tabs: her trust bar is lower.</summary>
        public const int TildaTabTrustThreshold = 40;
        /// <summary>Doran extends short credit only to people he trusts.</summary>
        public const int DoranTabTrustThreshold = 50;

        private static readonly NpcId Tilda = new NpcId("npc_tilda_bray");
        private static readonly NpcId Doran = new NpcId("npc_doran_kettle");
        private static readonly NpcId Bessa = new NpcId("npc_bessa_marlowe");
        private static readonly NpcId Garrick = new NpcId("npc_garrick_alder");
        private static readonly NpcId Jory = new NpcId("npc_jory_reed");
        private static readonly NpcId Tam = new NpcId("npc_tam_oakes");

        private static readonly LocationId TildaStore = new LocationId("loc_general_store");
        private static readonly ItemTypeId SmokedFish = new ItemTypeId("item_fish_smoked");

        // Doran: iron bought on credit before the autumn merchant visit, 10/week.
        private static readonly DebtTerms DoranTerms = new DebtTerms(copperPerWeek: 10);
        // Bessa: salt and lamp oil over the summer; ~5/week, irregularly (seeded roll).
        private static readonly DebtTerms BessaTerms = new DebtTerms(copperPerWeek: 5, payChancePercent: 60);
        // Garrick: nails and pitch for wheel patching. Behind — he often cannot pay —
        // and Tilda is patient (Tansy's sake), so his overdue grace is longer.
        private static readonly DebtTerms GarrickTerms =
            new DebtTerms(copperPerWeek: 5, payChancePercent: 50, overdueAfterDays: 90);
        // Jory: rope and tar, repaid in smoked fish — 2 fish a week, ~8 copper of value.
        private static readonly DebtTerms JoryTerms =
            new DebtTerms(itemPerWeek: SmokedFish, itemsPerWeek: 2, itemCreditCopper: 8);
        // Tam: salt pork last winter, 3/week.
        private static readonly DebtTerms TamTerms = new DebtTerms(copperPerWeek: 3);

        /// <summary>
        /// Installs the five starting tabs and returns the debt system's configuration.
        /// </summary>
        public static DebtConfiguration OpenLedger(WorldState state, long startDay)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (startDay < 1) throw new ArgumentOutOfRangeException(nameof(startDay));
            var ledger = new DebtLedgerState(initialized: true);
            Add(ledger, Doran, 120, DoranTerms, startDay);
            Add(ledger, Bessa, 80, BessaTerms, startDay);
            Add(ledger, Garrick, 60, GarrickTerms, startDay);
            Add(ledger, Jory, 45, JoryTerms, startDay);
            Add(ledger, Tam, 35, TamTerms, startDay);
            if (startDay > VillageCalendar.HarvestFeastDay(startDay))
                ledger.LastFeastYear = (startDay - 1) / VillageCalendar.DaysPerYear + 1;
            state.RestoreDebtLedger(ledger);
            return new DebtConfiguration("debts-tilda-ledger", TildaStore, EventVisibility.Normal);
        }

        private static void Add(DebtLedgerState ledger, NpcId debtor, int owed, DebtTerms terms, long startDay)
        {
            var record = new DebtRecord(debtor, Tilda, owed, terms, startDay);
            record.LastPaymentDay = startDay;
            ledger.Add(record);
        }
    }
}
