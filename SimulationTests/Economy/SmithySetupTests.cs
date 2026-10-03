using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>
    /// Proves Doran's smithy opens with the brief's documented values: 20 kg of iron,
    /// a 900-copper till, an empty rack, and the approved retail prices.
    /// </summary>
    public sealed class SmithySetupTests
    {
        [Test]
        public void SetupStocksIronTillAndPrices()
        {
            ItemCatalog catalog = MillingSystemTests.TestCatalog();
            var state = new WorldState(7, new GameTime(0));
            SmithySetup.SmithyHandle smithy = SmithySetup.Stock(state, catalog);
            Shop shop = smithy.SmithyShop;

            Assert.That(shop.Location, Is.EqualTo(SmithySetup.Smithy));
            Assert.That(shop.Owner, Is.EqualTo(SmithySetup.Smith));
            Assert.That(shop.Stock.Count(SmithySetup.Iron), Is.EqualTo(20),
                "Doran opens with 20 kg of imported iron (ECONOMY.md).");
            Assert.That((shop.Stock.Count(SmithySetup.Nails),
                    shop.Stock.Count(SmithySetup.Tools),
                    shop.Stock.Count(SmithySetup.Horseshoes)),
                Is.EqualTo((0, 0, 0)), "The rack starts empty: yesterday's work sold through.");
            Assert.That(shop.OwnerWallet.Balance, Is.EqualTo(900),
                "Doran's till opens at 900 copper (P2-07 brief).");
            Assert.That((shop.UnitPrice(SmithySetup.Nails),
                    shop.UnitPrice(SmithySetup.Tools),
                    shop.UnitPrice(SmithySetup.Horseshoes)),
                Is.EqualTo((1, 60, 12)),
                "Nails at 1 and horseshoes at 12 (baseValue); tools at the fixed 60 documented in the setup.");
            Assert.That(state.Shops[SmithySetup.Smithy], Is.SameAs(shop),
                "The smithy is registered as a shop on the world.");
        }
    }
}
