using System.Linq;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Scenarios
{
    /// <summary>Exercises the reusable Apple Test village through its real runtime systems.</summary>
    public sealed class AppleTestHarnessTests
    {
        [Test]
        public void SmokeRunReachesDayThreeAndRecordsTheScenarioChain()
        {
            AppleTestResult result = AppleTestHarness.Run(42);

            Assert.That(result.EndTime.Day, Is.EqualTo(3));
            Assert.That(result.Events.Count(entry => entry.Type == WorldEventType.Theft), Is.EqualTo(1));
            Assert.That(result.Events.Count(entry => entry.Type == WorldEventType.StockCounted), Is.EqualTo(1));
            Assert.That(result.Events.Count(entry => entry.Type == WorldEventType.Purchase), Is.EqualTo(3));
            Assert.That(result.Events.Count(entry => entry.Type == WorldEventType.PartialPurchase), Is.EqualTo(1));
            Assert.That(result.Events.Count(entry => entry.Type == WorldEventType.FailedPurchase), Is.EqualTo(1));
            Assert.That(result.Events.Any(entry => entry.Type == WorldEventType.RestockOrdered), Is.True);
            Assert.That(result.Events.Any(entry => entry.Type == WorldEventType.Produced), Is.True);
            Assert.That(result.Events.Any(entry => entry.Type == WorldEventType.Restocked), Is.True);
            Assert.That(result.Events.Any(entry => entry.Type == WorldEventType.PriceChanged), Is.True);
            var sales = result.Events.Where(entry => entry.Type == WorldEventType.Purchase
                || entry.Type == WorldEventType.PartialPurchase || entry.Type == WorldEventType.FailedPurchase)
                .Select(entry => (entry.Time.Day, entry.Time.Hour, entry.Quantity)).ToArray();
            Assert.That(sales, Is.EqualTo(new[] { (1L, 8, (int?)2), (1L, 10, (int?)3),
                (1L, 15, (int?)5), (2L, 9, (int?)4), (2L, 10, (int?)0) }));
            Assert.That(result.Events.Single(entry => entry.Type == WorldEventType.Produced).Time,
                Is.EqualTo(new GameTime(2 * 1440 + 8 * 60)));
            Assert.That(result.PlayerApples, Is.EqualTo(6));
            Assert.That(result.MiraMissingAppleQuantity, Is.EqualTo(6));
            Assert.That((result.FinalShopStock, result.FinalRetailCopper), Is.EqualTo((45, 4)));
            Assert.That(result.WitnessObservedTheft, Is.True);
            Assert.That((result.GuardSuspicion.TotalEvidence, result.GuardSuspicion.ProofThreshold),
                Is.EqualTo((50, 70)));
            Assert.That(result.GuardSuspicion.MayAct, Is.False);
            Assert.That(result.Report, Does.StartWith("WORLD TRUTH REPORT"));
        }

        [Test]
        public void SetupUsesApprovedAppleValuesAndConservesStartingCopper()
        {
            AppleTestResult result = AppleTestHarness.Run(42);

            Assert.That((result.Configuration.StartingStock, result.Configuration.RetailCopper,
                result.Configuration.StolenQuantity), Is.EqualTo((20, 3, 6)));
            Assert.That((result.Configuration.LowStockThreshold, result.Configuration.DeliveryQuantity,
                result.Configuration.WholesaleCopper, result.Configuration.MaximumRetailCopper),
                Is.EqualTo((10, 45, 1, 4)));
            Assert.That(result.InitialAppleTotal, Is.EqualTo(20));
            Assert.That(result.FinalAppleTotal, Is.EqualTo(65), "45 apples are added by a real production event.");
            Assert.That(result.InitialCopperTotal, Is.EqualTo(result.FinalCopperTotal));
            Assert.That(result.ProductionState.CompletedIds, Has.Count.EqualTo(1));
            Assert.That(result.RestockState.PendingOrders, Is.Empty);
            Assert.That(result.PriceState.Progress, Has.Count.EqualTo(1));
            var restored = new AppleScenarioState(result.ScenarioState.ExpectedShopStock,
                result.ScenarioState.CompletedBuyerIds, result.ScenarioState.StockCounted,
                result.ScenarioState.CompletedMeetingIds, result.ScenarioState.TheftQueued);
            Assert.That(restored.CompletedBuyerIds, Is.EqualTo(result.ScenarioState.CompletedBuyerIds));
            Assert.That(restored.CompletedMeetingIds, Is.EqualTo(result.ScenarioState.CompletedMeetingIds));
            Assert.That((restored.ExpectedShopStock, restored.StockCounted, restored.TheftQueued),
                Is.EqualTo((result.ScenarioState.ExpectedShopStock, true, true)));
        }

        [Test]
        public void SameSeedProducesByteIdenticalReportAndSnapshot()
        {
            AppleTestResult first = AppleTestHarness.Run(987654321);
            AppleTestResult second = AppleTestHarness.Run(987654321);

            Assert.That(second.Report, Is.EqualTo(first.Report));
            Assert.That(second.Snapshot, Is.EqualTo(first.Snapshot));
            Assert.That(second.WitnessObservedTheft, Is.EqualTo(first.WitnessObservedTheft));
        }
    }
}
