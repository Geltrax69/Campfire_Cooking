using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// A paid repair at the smithy: the customer pays Doran copper for the work. Item condition
    /// is not modeled — the repaired item stays in the customer's inventory throughout; the
    /// command records the service, moves the money, and consumes iron for major work.
    ///
    /// Event vocabulary: Core defines no repair event type (owned by the orchestrator), so a
    /// completed repair is recorded as a <see cref="WorldEventType.Purchase"/> of the repair
    /// service — copper moved, zero items changed hands, the repaired item carried in the
    /// item field. A refused repair (broke customer, missing item, no iron) is a
    /// <see cref="WorldEventType.FailedPurchase"/>, mirroring <see cref="Shop.Purchase"/>.
    /// </summary>
    public sealed class RepairCommand : IWorldCommand
    {
        // ECONOMY.md: "repairs at 8–25". Tiered by the repaired item's baseValue, keeping the
        // documented floor and ceiling: cheap goods (nails, horseshoes, rope) get the 8-copper
        // small repair with no iron; durable tools get the 25-copper major repair costing
        // 1 kg of iron. The boundary sits at 20 copper so ordinary hardware stays cheap.
        public const int SmallRepairMaxBaseValue = 20;
        public const int SmallRepairPriceCopper = 8;
        public const int MajorRepairPriceCopper = 25;
        public const int MajorRepairIronKg = 1;

        private readonly LocationId _location;
        private readonly ActorId _customer;
        private readonly Inventory _customerInventory;
        private readonly Wallet _customerWallet;
        private readonly NpcId _smith;
        private readonly Inventory _ironStock;
        private readonly ItemTypeId _iron;
        private readonly Wallet _smithWallet;
        private readonly ItemTypeId _item;
        private readonly ItemCatalog _catalog;
        private readonly EventVisibility _visibility;

        public RepairCommand(LocationId location, ActorId customer, Inventory customerInventory,
            Wallet customerWallet, NpcId smith, Inventory ironStock, ItemTypeId iron,
            Wallet smithWallet, ItemTypeId item, ItemCatalog catalog, EventVisibility visibility)
        {
            if (!location.IsValid) throw new ArgumentException("A valid smithy location is required.", nameof(location));
            if (!customer.IsValid) throw new ArgumentException("A valid customer is required.", nameof(customer));
            if (customerInventory == null) throw new ArgumentNullException(nameof(customerInventory));
            if (customerWallet == null) throw new ArgumentNullException(nameof(customerWallet));
            if (!smith.IsValid) throw new ArgumentException("A valid smith is required.", nameof(smith));
            if (ironStock == null) throw new ArgumentNullException(nameof(ironStock));
            if (!iron.IsValid) throw new ArgumentException("A valid iron stock item is required.", nameof(iron));
            if (smithWallet == null) throw new ArgumentNullException(nameof(smithWallet));
            if (!item.IsValid) throw new ArgumentException("A valid repaired item is required.", nameof(item));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            // Catalogs are immutable, so validating support once is enough for every later execution.
            _ = ironStock.Count(iron);
            _ = customerInventory.Count(item);
            _ = catalog[item].BaseValue;

            if (visibility < EventVisibility.Hidden || visibility > EventVisibility.Loud)
                throw new ArgumentOutOfRangeException(nameof(visibility));

            _location = location;
            _customer = customer;
            _customerInventory = customerInventory;
            _customerWallet = customerWallet;
            _smith = smith;
            _ironStock = ironStock;
            _iron = iron;
            _smithWallet = smithWallet;
            _item = item;
            _catalog = catalog;
            _visibility = visibility;
        }

        /// <summary>The documented repair price for an item of the given base value.</summary>
        public static int PriceForItemValue(int baseValue) =>
            baseValue <= SmallRepairMaxBaseValue ? SmallRepairPriceCopper : MajorRepairPriceCopper;

        /// <summary>The iron a repair of an item of the given base value consumes.</summary>
        public static int IronKgForItemValue(int baseValue) =>
            baseValue <= SmallRepairMaxBaseValue ? 0 : MajorRepairIronKg;

        public void Execute(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            int baseValue = _catalog[_item].BaseValue;
            int price = PriceForItemValue(baseValue);
            int ironKg = IronKgForItemValue(baseValue);

            if (_customerWallet.Balance < price
                || _customerInventory.Count(_item) < 1
                || _ironStock.Count(_iron) < ironKg)
            {
                state.Events.Append(state.Clock, _location, WorldEventType.FailedPurchase, _customer,
                    new[] { ActorId.ForNpc(_smith) }, EventVisibility.Normal, _item, 0, 0);
                return;
            }

            // Append first: the following single-threaded transfers were preflighted and cannot fail.
            state.Events.Append(state.Clock, _location, WorldEventType.Purchase, _customer,
                new[] { ActorId.ForNpc(_smith) }, _visibility, _item, 0, price);
            if (ironKg > 0 && !_ironStock.TryRemove(_iron, ironKg))
                throw new InvalidOperationException("Preflighted iron removal unexpectedly failed.");
            if (!_customerWallet.TransferTo(_smithWallet, price))
                throw new InvalidOperationException("Preflighted copper transfer unexpectedly failed.");
        }
    }
}
