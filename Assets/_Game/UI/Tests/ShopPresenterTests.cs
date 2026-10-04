using NUnit.Framework;
namespace LivingWorld.Game.UI.Tests
{
    public sealed class ShopPresenterTests
    {
        private sealed class Commands : IShopCommands
        {
            public int Bought, Taken, Waited;
            public bool Paused;
            public void Buy(int quantity) { Bought += quantity; }
            public void Take(int quantity) { Taken += quantity; }
            public void Wait(int minutes) { Waited += minutes; }
            public void Pause(bool paused) { Paused = paused; }
        }
        [Test] public void MissingWorldDisablesActions()
        {
            var commands = new Commands(); var shop = new ShopPresenter(commands);
            shop.Buy(); shop.Take(); shop.Wait(); shop.TogglePause();
            Assert.That(shop.CanBuy, Is.False); Assert.That(shop.CanTake, Is.False);
            Assert.That(commands.Bought + commands.Taken + commands.Waited, Is.Zero);
        }
        [Test] public void InsufficientStockOrCopperDoesNotDispatch()
        {
            var commands = new Commands(); var shop = new ShopPresenter(commands);
            shop.Update(5, 3, 2, false); shop.Buy(); shop.Take();
            Assert.That(commands.Bought + commands.Taken, Is.Zero);
            Assert.That(shop.Message, Does.Contain("stock"));
        }
        [Test] public void QueuedPurchaseBlocksRepeatUntilSnapshotArrives()
        {
            var commands = new Commands(); var shop = new ShopPresenter(commands);
            shop.Update(20, 3, 10, false); shop.Buy(); shop.Buy(); shop.Take();
            Assert.That(commands.Bought, Is.EqualTo(1)); Assert.That(commands.Taken, Is.Zero);
            Assert.That(shop.Pending, Is.True); Assert.That(shop.CanBuy, Is.False);
            shop.Update(19, 3, 7, false);
            Assert.That(shop.Pending, Is.False); Assert.That(shop.CanBuy, Is.True);
        }
        [Test] public void TakeDispatchesExactSixWithoutOptimisticInventoryChange()
        {
            var commands = new Commands(); var shop = new ShopPresenter(commands);
            shop.Update(20, 3, 10, true); shop.Take();
            Assert.That(commands.Taken, Is.EqualTo(6)); Assert.That(shop.Pending, Is.True);
            Assert.That(shop.Message, Does.Contain("Resume"));
        }
        [Test] public void PauseAndPrototypeWaitUseBridgeCommands()
        {
            var commands = new Commands(); var shop = new ShopPresenter(commands);
            shop.Update(20, 3, 10, false); shop.TogglePause();
            Assert.That(commands.Paused, Is.True); Assert.That(shop.Paused, Is.True);
            shop.TogglePause(); shop.Wait(); Assert.That(commands.Waited, Is.EqualTo(60));
        }
        [Test] public void FailedQueuedActionShowsRecoveryMessage()
        {
            var commands = new Commands(); var shop = new ShopPresenter(commands);
            shop.Update(20, 3, 10, false); shop.Buy();
            shop.Update(0, 3, 10, false);
            Assert.That(shop.Message, Does.Contain("did not complete"));
            Assert.That(shop.CanBuy, Is.False);
        }
        [Test] public void ExternalPauseSyncPreservesQueuedAction()
        {
            var commands = new Commands(); var shop = new ShopPresenter(commands);
            shop.Update(20, 3, 10, false); shop.Buy(); shop.SyncPaused(true);
            Assert.That(shop.Paused, Is.True); Assert.That(shop.Pending, Is.True);
            Assert.That(shop.CanBuy, Is.False); Assert.That(commands.Paused, Is.False);
        }
        [Test] public void FaultClearsPendingAndDisablesActions()
        {
            var commands = new Commands(); var shop = new ShopPresenter(commands);
            shop.Update(20, 3, 10, false); shop.Buy(); shop.Unavailable("Content missing.");
            Assert.That(shop.Pending, Is.False); Assert.That(shop.CanBuy, Is.False);
            Assert.That(shop.Message, Is.EqualTo("Content missing."));
        }
    }
}
