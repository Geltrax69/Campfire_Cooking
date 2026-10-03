using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;
using static LivingWorld.Simulation.Tests.Economy.ItemCatalogTests;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>Proves the world-owned shop and belongings registries validate single-assignment
    /// registration, deterministic ordering and atomic restores.</summary>
    public sealed class WorldStateOwnershipTests
    {
        private static readonly LocationId Stall = new LocationId("loc_stall");
        private static readonly LocationId Field = new LocationId("loc_field");
        private static readonly NpcId Mira = new NpcId("npc_mira");
        private static readonly NpcId Tom = new NpcId("npc_tom");
        private static readonly NpcId Amy = new NpcId("npc_amy");

        [Test]
        public void FreshWorldStateOwnsEmptyShopsBelongingsAndProgress()
        {
            var state = new WorldState(7);

            Assert.That(state.Shops.Shops, Is.Empty);
            Assert.That(state.Belongings.Entries, Is.Empty);
            Assert.That(state.Production.CompletedIds, Is.Empty);
            Assert.That(state.Restock.TriggeredIds, Is.Empty);
            Assert.That(state.Restock.PendingOrders, Is.Empty);
            Assert.That(state.Prices.Progress, Is.Empty);
        }

        [Test]
        public void ShopRegistryRejectsDuplicatesNullsAndUnknownLookups()
        {
            var state = new WorldState(7);
            state.Shops.Register(NewShop(Stall, Mira));

            Assert.Throws<ArgumentException>(() => state.Shops.Register(NewShop(Stall, Tom)));
            Assert.Throws<ArgumentNullException>(() => state.Shops.Register(null));
            Assert.Throws<ArgumentException>(() => { _ = state.Shops[Field]; });
            Assert.Throws<ArgumentException>(() => { _ = state.Shops[default]; });
            Assert.That(state.Shops.TryGet(Stall, out Shop found) && ReferenceEquals(found,
                state.Shops[Stall]), Is.True);
            Assert.That(state.Shops.TryGet(Field, out _), Is.False);
            Assert.Throws<ArgumentException>(() => state.Shops.TryGet(default, out _));
        }

        [Test]
        public void ShopRegistryListsShopsInOrdinalLocationOrder()
        {
            var state = new WorldState(7);
            state.Shops.Register(NewShop(Stall, Mira));
            state.Shops.Register(NewShop(Field, Tom));

            Assert.That(state.Shops.Shops.Select(shop => shop.Location),
                Is.EqualTo(new[] { Field, Stall }));
        }

        [Test]
        public void ShopRegistryRestoreIsAtomic()
        {
            var state = new WorldState(7);
            Shop original = NewShop(Stall, Mira);
            state.Shops.Register(original);

            Assert.Throws<ArgumentException>(() => state.Shops.Restore(new[]
            {
                NewShop(Field, Tom), NewShop(Field, Amy)
            }));
            Assert.That(state.Shops.Shops.Single(), Is.SameAs(original));
            Assert.Throws<ArgumentNullException>(() => state.Shops.Restore(null));

            Shop replacement = NewShop(Field, Tom);
            state.Shops.Restore(new[] { replacement });
            Assert.That(state.Shops.Shops.Single(), Is.SameAs(replacement));
        }

        [Test]
        public void BelongingsRegistryRejectsDuplicatesNullsAndUnknownLookups()
        {
            var state = new WorldState(7);
            state.Belongings.Register(ActorId.ForNpc(Mira), new Inventory(TinyCatalog()), new Wallet(10));

            Assert.Throws<ArgumentException>(() => state.Belongings.Register(ActorId.ForNpc(Mira),
                new Inventory(TinyCatalog()), new Wallet()));
            Assert.Throws<ArgumentNullException>(() => state.Belongings.Register(ActorId.ForNpc(Tom),
                null, new Wallet()));
            Assert.Throws<ArgumentNullException>(() => state.Belongings.Register(ActorId.ForNpc(Tom),
                new Inventory(TinyCatalog()), null));
            Assert.Throws<ArgumentException>(() => state.Belongings.Register(default,
                new Inventory(TinyCatalog()), new Wallet()));
            Assert.Throws<ArgumentException>(() => { _ = state.Belongings[ActorId.ForNpc(Tom)]; });
            Assert.That(state.Belongings.TryGet(ActorId.ForNpc(Mira), out NpcBelongingsEntry entry)
                && entry.Wallet.Balance == 10, Is.True);
            Assert.That(state.Belongings.TryGet(ActorId.ForNpc(Tom), out _), Is.False);
        }

        [Test]
        public void BelongingsListPlayerFirstThenNpcsInOrdinalOrder()
        {
            var state = new WorldState(7);
            state.Belongings.Register(ActorId.ForNpc(Tom), new Inventory(TinyCatalog()), new Wallet());
            state.Belongings.Register(ActorId.Player, new Inventory(TinyCatalog()), new Wallet());
            state.Belongings.Register(ActorId.ForNpc(Amy), new Inventory(TinyCatalog()), new Wallet());

            Assert.That(state.Belongings.Entries.Select(entry => entry.Owner),
                Is.EqualTo(new[] { ActorId.Player, ActorId.ForNpc(Amy), ActorId.ForNpc(Tom) }));
        }

        [Test]
        public void BelongingsRestoreIsAtomic()
        {
            var state = new WorldState(7);
            state.Belongings.Register(ActorId.ForNpc(Mira), new Inventory(TinyCatalog()), new Wallet(10));

            Assert.Throws<ArgumentException>(() => state.Belongings.Restore(new[]
            {
                new NpcBelongingsEntry(ActorId.ForNpc(Tom), new Inventory(TinyCatalog()), new Wallet()),
                new NpcBelongingsEntry(ActorId.ForNpc(Tom), new Inventory(TinyCatalog()), new Wallet())
            }));
            Assert.That(state.Belongings.Entries.Single().Owner, Is.EqualTo(ActorId.ForNpc(Mira)));
            Assert.Throws<ArgumentNullException>(() => state.Belongings.Restore(null));
        }

        [Test]
        public void ProgressRestoreRejectsNull()
        {
            var state = new WorldState(7);
            Assert.Throws<ArgumentNullException>(() => state.RestoreProduction(null));
            Assert.Throws<ArgumentNullException>(() => state.RestoreRestock(null));
            Assert.Throws<ArgumentNullException>(() => state.RestorePrices(null));
        }

        [Test]
        public void ProgressRestoreInstallsValidatedState()
        {
            var state = new WorldState(7);
            var production = new ProductionState(new[] { "p1" });
            var restock = new RestockState(new[] { "r1" });
            var prices = new PriceAdjustmentState(new[]
            {
                new PriceAdjustmentProgress("price1", 3, 2, true)
            });

            state.RestoreProduction(production);
            state.RestoreRestock(restock);
            state.RestorePrices(prices);

            Assert.That(state.Production, Is.SameAs(production));
            Assert.That(state.Restock, Is.SameAs(restock));
            Assert.That(state.Prices, Is.SameAs(prices));
        }

        private static Shop NewShop(LocationId location, NpcId owner) =>
            new Shop(location, owner, new Inventory(TinyCatalog()), new Wallet(),
                new[] { new KeyValuePair<ItemTypeId, int>(Apple, 3) });
    }
}
