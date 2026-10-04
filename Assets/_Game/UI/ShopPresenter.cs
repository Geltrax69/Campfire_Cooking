using System;
namespace LivingWorld.Game.UI
{
    /// <summary>Commands accepted by the shop interface, with no writable world state.</summary>
    public interface IShopCommands
    {
        void Buy(int quantity);
        void Take(int quantity);
        void Wait(int minutes);
        void Pause(bool paused);
    }

    /// <summary>Display eligibility and queued-action feedback, independent of the Unity panel.</summary>
    public sealed class ShopPresenter
    {
        private readonly IShopCommands _commands;
        private int _stock, _price, _coins, _apples;
        private bool _pendingBuy;
        public bool Ready { get; private set; }
        public bool Pending { get; private set; }
        public bool Paused { get; private set; }
        public string Message { get; private set; } = "Connecting to the village…";
        public bool CanBuy => Ready && !Pending && _stock > 0 && _price > 0 && _coins >= _price;
        public bool CanTake => Ready && !Pending && _stock >= 6;
        public ShopPresenter(IShopCommands commands) { _commands = commands; }
        public void Update(int stock, int price, int coins, bool paused, int playerApples = 0)
        {
            if (Pending)
            {
                bool completed = _pendingBuy ? coins < _coins : playerApples >= _apples + 6;
                Message = completed ? "Stock and inventory refreshed." : "Action did not complete. Check refreshed stock and copper, then try again.";
            }
            _apples = playerApples;
            _stock = stock; _price = price; _coins = coins; Paused = paused; Ready = true;
            if (Message == "Connecting to the village…") Message = "Choose an action. The village keeps living while you browse.";
            Pending = false;
        }
        public void Unavailable(string message) { Ready = false; Pending = false; Message = message; }
        public void Buy() { if (!Pending) _pendingBuy = true; Dispatch(CanBuy, () => _commands.Buy(1), "Cannot buy: check stock and copper."); }
        public void Take() { if (!Pending) _pendingBuy = false; Dispatch(CanTake, () => _commands.Take(6), "Six apples must be in stock to take six."); }
        public void Wait()
        {
            if (!Ready) return;
            try { _commands.Wait(60); }
            catch (Exception error) { Message = error.Message; }
        }
        public void SyncPaused(bool paused) { Paused = paused; }
        public void TogglePause()
        {
            if (!Ready) return;
            try { _commands.Pause(!Paused); Paused = !Paused; }
            catch (Exception error) { Message = error.Message; }
        }
        private void Dispatch(bool allowed, Action action, string error)
        {
            if (!allowed) { Message = error; return; }
            try { action(); Pending = true; Message = Paused ? "Queued. Resume to process this action." : "Queued for the next game minute."; }
            catch (Exception failure) { Message = failure.Message; }
        }
    }
}
