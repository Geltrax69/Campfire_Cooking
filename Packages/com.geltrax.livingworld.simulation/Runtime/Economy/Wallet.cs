using System;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Owns a nonnegative balance measured in integer copper.</summary>
    public sealed class Wallet
    {
        public Wallet(int startingCopper = 0)
        {
            if (startingCopper < 0) throw new ArgumentOutOfRangeException(nameof(startingCopper));
            Balance = startingCopper;
        }

        public int Balance { get; private set; }

        public void Credit(int copper)
        {
            RequirePositive(copper);
            Balance = checked(Balance + copper);
        }

        public bool TryDebit(int copper)
        {
            RequirePositive(copper);
            if (Balance < copper) return false;
            Balance -= copper;
            return true;
        }

        public bool TransferTo(Wallet destination, int copper)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            RequirePositive(copper);
            if (Balance < copper) return false;
            if (ReferenceEquals(this, destination)) return true;

            int destinationBalance;
            try { destinationBalance = checked(destination.Balance + copper); }
            catch (OverflowException) { return false; }
            Balance -= copper;
            destination.Balance = destinationBalance;
            return true;
        }

        internal void EnsureCanReceive(int copper)
        {
            RequirePositive(copper);
            _ = checked(Balance + copper);
        }

        private static void RequirePositive(int copper)
        {
            if (copper <= 0) throw new ArgumentOutOfRangeException(nameof(copper));
        }
    }
}
