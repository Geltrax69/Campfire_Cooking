using System;
using LivingWorld.Simulation.Economy;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Economy
{
    /// <summary>Checks nonnegative integer-copper accounting and atomic transfers.</summary>
    public sealed class WalletTests
    {
        [Test]
        public void CreditDebitAndInsufficientFundsAreExact()
        {
            var wallet = new Wallet(10);
            wallet.Credit(5);
            Assert.That(wallet.Balance, Is.EqualTo(15));
            Assert.That(wallet.TryDebit(16), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(15));
            Assert.That(wallet.TryDebit(6), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(9));
        }

        [Test]
        public void InvalidAmountsAndOverflowDoNotMutate()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wallet(-1));
            var wallet = new Wallet(int.MaxValue);
            foreach (int amount in new[] { 0, -1, int.MinValue })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Credit(amount));
                Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TryDebit(amount));
                Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TransferTo(new Wallet(), amount));
            }
            Assert.Throws<OverflowException>(() => wallet.Credit(1));
            Assert.That(wallet.Balance, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void TransfersAreAtomicAndSelfTransferIsAValidatedNoOp()
        {
            var source = new Wallet(20);
            var destination = new Wallet(int.MaxValue - 10);
            Assert.That(source.TransferTo(destination, 11), Is.False, "Destination overflow is a normal failed transfer.");
            Assert.That((source.Balance, destination.Balance), Is.EqualTo((20, int.MaxValue - 10)));
            Assert.That(source.TransferTo(destination, 10), Is.True);
            Assert.That((source.Balance, destination.Balance), Is.EqualTo((10, int.MaxValue)));
            Assert.That(source.TransferTo(destination, 1), Is.False);
            Assert.Throws<ArgumentNullException>(() => source.TransferTo(null, 1));
            Assert.That(source.TransferTo(source, 10), Is.True);
            Assert.That(source.TransferTo(source, 11), Is.False);
            Assert.That(source.Balance, Is.EqualTo(10));
        }
    }
}
