using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Verifies Tilda's tab ledger: scheduled repayments, missed-payment trust
    /// consequences, overdue freezing and shaming, feast forgiveness, and new-tab gating.
    /// </summary>
    [TestFixture]
    public sealed class DebtSystemTests
    {
        private const int MinutesPerDay = 1440;

        private static readonly NpcId Tilda = new NpcId("npc_tilda_bray");
        private static readonly NpcId Doran = new NpcId("npc_doran_kettle");
        private static readonly NpcId Bessa = new NpcId("npc_bessa_marlowe");
        private static readonly NpcId Garrick = new NpcId("npc_garrick_alder");
        private static readonly NpcId Jory = new NpcId("npc_jory_reed");
        private static readonly NpcId Tam = new NpcId("npc_tam_oakes");
        private static readonly NpcId Elswith = new NpcId("npc_elswith_alder");
        private static readonly NpcId Mira = new NpcId("npc_mira_holt");
        private static readonly NpcId Bram = new NpcId("npc_bram_stone");

        private static readonly LocationId Store = new LocationId("loc_general_store");
        private static readonly LocationId Tavern = new LocationId("loc_tavern");
        private static readonly ItemTypeId SmokedFish = new ItemTypeId("item_fish_smoked");

        [Test]
        public void LedgerOpensWithFiveDebtsTotaling340()
        {
            WorldState state = LedgerState(42, 1);

            Assert.That(state.DebtLedger.IsInitialized, Is.True);
            Assert.That(state.DebtLedger.Debts, Has.Count.EqualTo(5));
            Assert.That(state.DebtLedger.Debts.Sum(d => d.OwedCopper), Is.EqualTo(340));
            Assert.That(state.DebtLedger.Debts.Select(d => (d.Debtor, d.OwedCopper)),
                Is.EquivalentTo(new[]
                {
                    (Doran, 120), (Bessa, 80), (Garrick, 60), (Jory, 45), (Tam, 35),
                }));
            Assert.That(state.DebtLedger.Debts.Select(d => d.Creditor).Distinct().Single(),
                Is.EqualTo(Tilda));
        }

        [Test]
        public void DoranRepaysTenPerWeekAndClearsInTwelveWeeks()
        {
            World world = LedgerWorld(42, 1, withDynamics: false);
            WorldState state = world.State;
            int totalBefore = TotalCopper(state);

            TickDays(world, 85);

            Assert.That(state.DebtLedger.Find(Doran, Tilda), Is.Null, "120 at 10/week clears.");
            Assert.That(WalletOf(state, Doran).Balance, Is.EqualTo(380));
            Assert.That(TotalCopper(state), Is.EqualTo(totalBefore), "Repayments only move copper.");
        }

        [Test]
        public void MissedPaymentEmitsDebtMissedAndLogsShortfall()
        {
            World world = LedgerWorld(42, 1, withDynamics: false,
                wallets: new Dictionary<NpcId, int> { { Tam, 0 } });
            WorldState state = world.State;

            TickDays(world, 8);

            WorldEvent missed = state.Events.Query(type: WorldEventType.DebtMissed).SingleOrDefault();
            Assert.That(missed, Is.Not.Null, "The first emitter of the P2-02 convention fires.");
            Assert.That(missed.Actor, Is.EqualTo((ActorId?)ActorId.ForNpc(Tam)));
            Assert.That(missed.Targets, Is.EqualTo(new[] { ActorId.ForNpc(Tilda) }));
            Assert.That(state.Events.Query(type: WorldEventType.FailedPurchase),
                Has.Count.EqualTo(1), "The shortfall stays visible.");
        }

        [Test]
        public void MissedPaymentDropsCreditorTrustByRule()
        {
            World world = LedgerWorld(42, 1, withDynamics: true,
                wallets: new Dictionary<NpcId, int> { { Tam, 0 } },
                trust: new[] { (Tilda, Tam, 60, 50) });
            WorldState state = world.State;

            TickDays(world, 9);

            Assert.That(state.Events.Query(type: WorldEventType.DebtMissed), Has.Count.EqualTo(1));
            WorldEvent shift = state.Events.Query(type: WorldEventType.RelationshipShift)
                .SingleOrDefault(e => e.Targets.Contains(ActorId.ForNpc(Tam)));
            Assert.That(shift, Is.Not.Null, "The P2-02 rule consumed the DebtMissed event.");
            Assert.That(shift.Actor, Is.EqualTo((ActorId?)ActorId.ForNpc(Tilda)));
            Assert.That(state.Knowledge.Relationships.Trust(Tilda, Tam), Is.LessThan(60),
                "Trust fell: -3 for the miss, partly healed by daily decay.");
        }

        [Test]
        public void SixtyDaysOverdueFreezesTabsTellsCouncilAndReachesTavern()
        {
            World world = LedgerWorld(42, 1, withDynamics: false,
                wallets: new Dictionary<NpcId, int> { { Tam, 0 } },
                trust: new[] { (Tilda, Tam, 60, 50), (Bessa, Tilda, 80, 50) });
            WorldState state = world.State;

            TickDays(world, 62);

            DebtRecord debt = state.DebtLedger.Find(Tam, Tilda);
            Assert.That(debt.OverdueDeclared, Is.True, "60 days without payment declares overdue.");
            Assert.That(state.Events.Query(type: WorldEventType.Conversation)
                .Any(e => e.Actor.Equals(ActorId.ForNpc(Tilda)) &&
                          e.Targets.Contains(ActorId.ForNpc(Elswith))),
                Is.True, "Tilda tells Elswith; the council witnesses.");
            Belief tavernRumor = state.Knowledge.Get(Bessa).Query(kind: BeliefClaimKind.WrongedBy)
                .SingleOrDefault(b => b.Claim.Subject.Equals(ActorId.ForNpc(Tam)));
            Assert.That(tavernRumor, Is.Not.Null, "Bessa's tavern heard about it.");
            Assert.That(tavernRumor.Source.Kind, Is.EqualTo(BeliefSourceKind.ToldBy));
            Assert.That(DebtLedger.OpenTab(state, Tilda, Tam, 10, DebtSetup.TildaTabTrustThreshold,
                Store, EventVisibility.Normal), Is.False, "No new tabs while overdue.");
        }

        [Test]
        public void GarrickPatienceMeansLongerOverdueGrace()
        {
            World world = LedgerWorld(42, 1, withDynamics: false,
                wallets: new Dictionary<NpcId, int> { { Garrick, 0 } });
            WorldState state = world.State;

            TickDays(world, 61);
            Assert.That(state.DebtLedger.Find(Garrick, Tilda).OverdueDeclared, Is.False,
                "Tilda is patient: 90 days for Garrick, not 60.");

            TickDays(world, 31);
            Assert.That(state.DebtLedger.Find(Garrick, Tilda).OverdueDeclared, Is.True);
        }

        [Test]
        public void HarvestFeastForgivesSubTwentyDebt()
        {
            World world = LedgerWorld(42, 80, withDynamics: false,
                wallets: new Dictionary<NpcId, int> { { Mira, 0 }, { Bram, 0 } },
                trust: new[] { (Tilda, Mira, 60, 50), (Tilda, Bram, 60, 50) });
            WorldState state = world.State;
            Assert.That(DebtLedger.OpenTab(state, Tilda, Mira, 15, DebtSetup.TildaTabTrustThreshold,
                Store, EventVisibility.Normal), Is.True);
            Assert.That(DebtLedger.OpenTab(state, Tilda, Bram, 25, DebtSetup.TildaTabTrustThreshold,
                Store, EventVisibility.Normal), Is.True);

            TickDays(world, 10); // day 80 -> 90; the feast is day 89

            Assert.That(state.DebtLedger.Find(Mira, Tilda), Is.Null, "15 < 20 is forgiven.");
            Assert.That(state.DebtLedger.Find(Bram, Tilda).OwedCopper, Is.EqualTo(25),
                "25 stays.");
            Assert.That(state.Events.Query(type: WorldEventType.Purchase)
                .Any(e => e.Actor.Equals(ActorId.ForNpc(Mira)) && e.Copper == 15 && e.Quantity == 0),
                Is.True, "Forgiveness is logged as a ledger movement.");
        }

        [Test]
        public void JoryRepaysInSmokedFishNotCopper()
        {
            World world = LedgerWorld(42, 1, withDynamics: false,
                wallets: new Dictionary<NpcId, int> { { Jory, 0 } });
            WorldState state = world.State;
            int tildaFishBefore = InventoryOf(state, Tilda).Count(SmokedFish);
            int joryFishBefore = InventoryOf(state, Jory).Count(SmokedFish);

            TickDays(world, 8);

            Assert.That(InventoryOf(state, Tilda).Count(SmokedFish), Is.EqualTo(tildaFishBefore + 2));
            Assert.That(InventoryOf(state, Jory).Count(SmokedFish), Is.EqualTo(joryFishBefore - 2));
            Assert.That(state.DebtLedger.Find(Jory, Tilda).OwedCopper, Is.EqualTo(37));
            Assert.That(WalletOf(state, Jory).Balance, Is.EqualTo(0), "No copper moved.");
        }

        [Test]
        public void NewTabGatedByCreditorTrust()
        {
            WorldState state = LedgerState(42, 1,
                trust: new[] { (Tilda, Mira, 60, 50), (Tilda, Bram, 30, 50) });

            Assert.That(DebtLedger.OpenTab(state, Tilda, Mira, 10, DebtSetup.TildaTabTrustThreshold,
                Store, EventVisibility.Normal), Is.True);
            Assert.That(state.DebtLedger.Find(Mira, Tilda).OwedCopper, Is.EqualTo(10));
            Assert.That(DebtLedger.OpenTab(state, Tilda, Bram, 10, DebtSetup.TildaTabTrustThreshold,
                Store, EventVisibility.Normal), Is.False, "Trust 30 < 40: no tab.");
            Assert.That(state.DebtLedger.Find(Bram, Tilda), Is.Null);
            Assert.That(DebtLedger.OpenTab(state, Tilda, Mira, DebtLedger.MaxNewTabCopper + 1,
                DebtSetup.TildaTabTrustThreshold, Store, EventVisibility.Normal), Is.False,
                "Short credit only.");
        }

        [Test]
        public void IrregularPayerSkipsWeeksWithoutMiss()
        {
            World world = LedgerWorld(42, 1, withDynamics: false);
            WorldState state = world.State;

            TickDays(world, 56); // 8 weeks; Bessa pays ~5/week irregularly

            int payments = state.Events.Query(type: WorldEventType.Purchase)
                .Count(e => e.Actor.Equals(ActorId.ForNpc(Bessa)));
            int missed = state.Events.Query(type: WorldEventType.DebtMissed)
                .Count(e => e.Actor.Equals(ActorId.ForNpc(Bessa)));
            Assert.That(payments, Is.LessThan(8).And.GreaterThan(0),
                "Seed 42: Bessa skips some weeks but pays others.");
            Assert.That(missed, Is.EqualTo(0), "A skipped week is within her terms, not a miss.");
        }

        [Test]
        public void LedgerRoundTripsThroughCaptureRestore()
        {
            WorldState state = LedgerState(42, 1);
            DebtRecord tam = state.DebtLedger.Find(Tam, Tilda);
            tam.OwedCopper = 32;
            tam.LastPaymentDay = 15;
            tam.LastWeeklyDay = 15;
            tam.OverdueDeclared = true;

            var copies = new List<DebtRecord>();
            foreach (DebtRecord debt in state.DebtLedger.Debts)
            {
                var copy = new DebtRecord(debt.Debtor, debt.Creditor, debt.OwedCopper,
                    debt.Terms, debt.OpenedDay);
                copy.LastPaymentDay = debt.LastPaymentDay;
                copy.LastWeeklyDay = debt.LastWeeklyDay;
                copy.OverdueDeclared = debt.OverdueDeclared;
                copies.Add(copy);
            }
            var restored = new DebtLedgerState(initialized: true);
            restored.ReplaceAll(copies);
            state.RestoreDebtLedger(restored);

            Assert.That(state.DebtLedger.Debts, Has.Count.EqualTo(5));
            DebtRecord back = state.DebtLedger.Find(Tam, Tilda);
            Assert.That((back.OwedCopper, back.LastPaymentDay, back.LastWeeklyDay, back.OverdueDeclared),
                Is.EqualTo((32, 15L, 15L, true)));
        }

        [Test]
        public void HarvestFeastDayIsLastRestdayOfAutumn()
        {
            Assert.That(VillageCalendar.HarvestFeastDay(1), Is.EqualTo(89));
            Assert.That(VillageCalendar.HarvestFeastDay(400), Is.EqualTo(446));
        }

        [Test]
        public void DebtValidation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DebtTerms(copperPerWeek: -1));
            Assert.Throws<ArgumentException>(() =>
                new DebtTerms(itemPerWeek: SmokedFish, itemsPerWeek: 0, itemCreditCopper: 8));
            Assert.Throws<ArgumentException>(() =>
                new DebtRecord(Tilda, Tilda, 10, DebtTerms.StandardTab, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DebtRecord(Tam, Tilda, 0, DebtTerms.StandardTab, 1));
            Assert.Throws<ArgumentException>(() => new DebtConfiguration("", Store, EventVisibility.Normal));
            Assert.Throws<ArgumentNullException>(() => new DebtSystem(null));
            WorldState state = LedgerState(42, 1);
            Assert.Throws<ArgumentNullException>(() =>
                DebtLedger.OpenTab(null, Tilda, Mira, 10, 40, Store, EventVisibility.Normal));
        }

        private static World LedgerWorld(ulong seed, long startDay, bool withDynamics,
            Dictionary<NpcId, int> wallets = null,
            (NpcId from, NpcId to, int trust, int affection)[] trust = null)
        {
            WorldState state = LedgerState(seed, startDay, wallets, trust);
            var world = new World(state);
            world.RegisterSystem(new DebtSystem(new[]
            {
                new DebtConfiguration("debts-test", Store, EventVisibility.Normal),
            }));
            if (withDynamics) world.RegisterSystem(new RelationshipDynamicsSystem());
            return world;
        }

        private static WorldState LedgerState(ulong seed, long startDay,
            Dictionary<NpcId, int> wallets = null,
            (NpcId from, NpcId to, int trust, int affection)[] trust = null)
        {
            var state = new WorldState(seed, new GameTime((startDay - 1) * MinutesPerDay));
            var catalog = new ItemCatalog(new[]
            {
                new ItemDefinition(SmokedFish, "Smoked fish", "food", 4, 1),
            });
            foreach (NpcId npc in new[] { Tilda, Doran, Bessa, Garrick, Jory, Tam, Elswith, Mira, Bram })
            {
                state.Knowledge.Register(npc);
                int copper = 500;
                if (wallets != null && wallets.TryGetValue(npc, out int set)) copper = set;
                state.Belongings.Register(ActorId.ForNpc(npc), new Inventory(catalog), new Wallet(copper));
            }
            if (trust != null)
                state.Knowledge.InitializeRelationships(trust.Select(t =>
                    new Relationship(t.from, t.to, t.trust, t.affection, "test")));
            // Jory repays in kind: stock his boat so his weekly fish payments succeed
            // unless a test says otherwise.
            state.Belongings[ActorId.ForNpc(Jory)].Inventory.Add(SmokedFish, 100);
            DebtSetup.OpenLedger(state, startDay);
            return state;
        }

        private static void TickDays(World world, int days)
        {
            for (int i = 0; i < days * MinutesPerDay; i++) world.Tick();
        }

        private static Wallet WalletOf(WorldState state, NpcId npc) =>
            state.Belongings[ActorId.ForNpc(npc)].Wallet;

        private static Inventory InventoryOf(WorldState state, NpcId npc) =>
            state.Belongings[ActorId.ForNpc(npc)].Inventory;

        private static int TotalCopper(WorldState state)
        {
            int total = 0;
            foreach (NpcBelongingsEntry entry in state.Belongings.Entries) total += entry.Wallet.Balance;
            return total;
        }
    }
}
