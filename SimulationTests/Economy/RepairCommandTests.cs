using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Proves the repair pricing rule (8 copper small / 25 copper + 1 kg iron major, tiered by
    /// base value), that money is conserved, that failures move nothing, and that the service
    /// is recorded as a purchase event.
    /// </summary>
    public sealed class RepairCommandTests
    {
        private sealed class Customer
        {
            internal Customer(int copper, ItemCatalog catalog, ItemTypeId item, int count)
            {
                Inventory = new Inventory(catalog);
                Inventory.Add(item, count);
                Wallet = new Wallet(copper);
            }

            internal Inventory Inventory { get; }
            internal Wallet Wallet { get; }
        }

        private static RepairCommand Command(WorldState state, Customer customer, ItemTypeId item,
            int ironKg, ItemCatalog catalog, Wallet smithWallet)
        {
            var ironStock = new Inventory(catalog);
            if (ironKg > 0) ironStock.Add(SmithySetup.Iron, ironKg);
            return new RepairCommand(SmithySetup.Smithy, ActorId.Player, customer.Inventory,
                customer.Wallet, SmithySetup.Smith, ironStock, SmithySetup.Iron,
                smithWallet, item, catalog, EventVisibility.Normal);
        }

        private static WorldEvent LastPurchase(WorldState state, WorldEventType type)
        {
            var events = state.Events.Query(null, null, SmithySetup.Smithy, type);
            Assert.That(events.Count, Is.GreaterThan(0), "Expected a repair event in the log.");
            return events[events.Count - 1];
        }

        [Test]
        public void PricingFollowsTheDocumentedTiers()
        {
            // Boundary: baseValue 20 (item_dye) is still the small tier.
            Assert.That((RepairCommand.PriceForItemValue(1), RepairCommand.IronKgForItemValue(1)),
                Is.EqualTo((8, 0)), "Nails: 8 copper, no iron.");
            Assert.That((RepairCommand.PriceForItemValue(12), RepairCommand.IronKgForItemValue(12)),
                Is.EqualTo((8, 0)), "Horseshoes (12): 8 copper, no iron.");
            Assert.That((RepairCommand.PriceForItemValue(20), RepairCommand.IronKgForItemValue(20)),
                Is.EqualTo((8, 0)), "The 20-copper boundary stays in the small tier.");
            Assert.That((RepairCommand.PriceForItemValue(21), RepairCommand.IronKgForItemValue(21)),
                Is.EqualTo((25, 1)), "Above 20: 25 copper and 1 kg of iron.");
            Assert.That((RepairCommand.PriceForItemValue(40), RepairCommand.IronKgForItemValue(40)),
                Is.EqualTo((25, 1)), "Tools (40): 25 copper and 1 kg of iron.");
        }

        [Test]
        public void SmallRepairChargesEightCopperAndNoIron()
        {
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var state = new WorldState(3, new GameTime(0));
            var customer = new Customer(100, catalog, SmithySetup.Horseshoes, 1);
            var smithWallet = new Wallet(900);
            RepairCommand command = Command(state, customer, SmithySetup.Horseshoes, 5, catalog, smithWallet);

            command.Execute(state);

            Assert.That((customer.Wallet.Balance, smithWallet.Balance), Is.EqualTo((92, 908)),
                "8 copper moved from customer to smith: money conserved.");
            Assert.That(customer.Inventory.Count(SmithySetup.Horseshoes), Is.EqualTo(1),
                "The repaired item stays with its owner.");
            WorldEvent paid = LastPurchase(state, WorldEventType.Purchase);
            Assert.That((paid.ItemType, paid.Quantity, paid.Copper),
                Is.EqualTo((SmithySetup.Horseshoes, 0, 8)),
                "A repair is logged as a service purchase: money moved, no items changed hands.");
        }

        [Test]
        public void MajorRepairChargesTwentyFiveAndConsumesOneKgIron()
        {
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var state = new WorldState(3, new GameTime(0));
            var customer = new Customer(100, catalog, SmithySetup.Tools, 1);
            var smithWallet = new Wallet(900);
            var ironStock = new Inventory(catalog);
            ironStock.Add(SmithySetup.Iron, 5);
            var command = new RepairCommand(SmithySetup.Smithy, ActorId.Player, customer.Inventory,
                customer.Wallet, SmithySetup.Smith, ironStock, SmithySetup.Iron,
                smithWallet, SmithySetup.Tools, catalog, EventVisibility.Normal);

            command.Execute(state);

            Assert.That((customer.Wallet.Balance, smithWallet.Balance), Is.EqualTo((75, 925)));
            Assert.That(ironStock.Count(SmithySetup.Iron), Is.EqualTo(4),
                "The major repair consumed exactly 1 kg of iron.");
            WorldEvent paid = LastPurchase(state, WorldEventType.Purchase);
            Assert.That(paid.Copper, Is.EqualTo(25));
        }

        [Test]
        public void BrokeCustomerIsRefusedAndNothingMoves()
        {
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var state = new WorldState(3, new GameTime(0));
            var customer = new Customer(7, catalog, SmithySetup.Horseshoes, 1); // repair costs 8.
            var smithWallet = new Wallet(900);
            RepairCommand command = Command(state, customer, SmithySetup.Horseshoes, 5, catalog, smithWallet);

            command.Execute(state);

            Assert.That((customer.Wallet.Balance, smithWallet.Balance), Is.EqualTo((7, 900)));
            WorldEvent refused = LastPurchase(state, WorldEventType.FailedPurchase);
            Assert.That((refused.Copper, refused.Quantity), Is.EqualTo((0, 0)));
        }

        [Test]
        public void MajorRepairWithoutIronIsRefused()
        {
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var state = new WorldState(3, new GameTime(0));
            var customer = new Customer(100, catalog, SmithySetup.Tools, 1);
            var smithWallet = new Wallet(900);
            RepairCommand command = Command(state, customer, SmithySetup.Tools, 0, catalog, smithWallet);

            command.Execute(state);

            Assert.That((customer.Wallet.Balance, smithWallet.Balance), Is.EqualTo((100, 900)),
                "No money moves when the forge has no iron for a major repair.");
            Assert.That(state.Events.Query(null, null, SmithySetup.Smithy, WorldEventType.Purchase).Count,
                Is.EqualTo(0));
            Assert.That(state.Events.Query(null, null, SmithySetup.Smithy, WorldEventType.FailedPurchase).Count,
                Is.EqualTo(1));
        }

        [Test]
        public void RepairWithoutTheItemIsRefused()
        {
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var state = new WorldState(3, new GameTime(0));
            var customer = new Customer(100, catalog, SmithySetup.Nails, 20); // owns nails, not a tool.
            var smithWallet = new Wallet(900);
            RepairCommand command = Command(state, customer, SmithySetup.Tools, 5, catalog, smithWallet);

            command.Execute(state);

            Assert.That((customer.Wallet.Balance, smithWallet.Balance), Is.EqualTo((100, 900)),
                "Doran cannot repair a tool the customer does not have.");
        }
    }
}
