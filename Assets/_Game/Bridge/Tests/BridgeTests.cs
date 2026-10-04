using System.IO;
using NUnit.Framework;
using UnityEngine;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Persistence;
namespace LivingWorld.Game.Bridge.Tests
{
    public sealed class BridgeTests
    {
        [Test] public void ClockPreservesRemainderAndCatchUpDebt()
        { var clock = new BridgeClock(); Assert.AreEqual(0, clock.Accumulate(.6)); Assert.AreEqual(1, clock.Accumulate(.6)); Assert.AreEqual(8, clock.Accumulate(10)); Assert.AreEqual(2, clock.Accumulate(0)); Assert.AreEqual(1, clock.Accumulate(.8)); }
        [Test] public void ClockDoesNotAdvanceForMateriallyIncompleteMinute()
        { var clock = new BridgeClock(); Assert.AreEqual(0, clock.Accumulate(.999999)); Assert.AreEqual(1, clock.Accumulate(.000001)); Assert.AreEqual(0, clock.Accumulate(0)); }
        [Test] public void PausedClockDoesNotAccumulateTime()
        { var clock = new BridgeClock(); clock.Accumulate(.5); clock.Paused = true; Assert.AreEqual(0, clock.Accumulate(100)); clock.Paused = false; Assert.AreEqual(1, clock.Accumulate(.5)); }
        [Test] public void TheftWaitsForNextTickAndSnapshotIsDetached()
        { var slice = Create(); var before = slice.Capture(); slice.QueueStealApples(6); Assert.AreEqual(20, slice.Capture().ShopApples); slice.Tick(); Assert.AreEqual(14, slice.Capture().ShopApples); Assert.AreEqual(6, slice.Capture().PlayerApples); Assert.AreEqual(20, before.ShopApples); }
        [Test] public void PurchaseConservesPlayerGoodsAndChargesApprovedCopper()
        { var slice = Create(); slice.QueueBuyApples(2); slice.Tick(); Assert.AreEqual(18, slice.Capture().ShopApples); Assert.AreEqual(2, slice.Capture().PlayerApples); Assert.AreEqual(9, slice.Capture().PlayerCopper); }
        [Test] public void StockCountDoesNotInventCulpritKnowledge()
        { var slice = Create(); slice.QueueStealApples(6); for (int i = 0; i < 750; i++) slice.Tick(); var mira = System.Linq.Enumerable.Single(slice.Capture().Npcs, n => n.Id == "npc_mira_holt"); StringAssert.Contains("6 apples are missing", mira.Dialogue); StringAssert.Contains("don't know who", mira.Dialogue); }
        [Test] public void ApprovedRecipesParseUsingSystemTextJsonInUnity()
        { var recipes = RecipeCatalog.Load(Path.GetFullPath(Path.Combine(Application.dataPath, ".."))); Assert.Greater(recipes.Count, 0); Assert.AreEqual(recipes.Count, recipes.All.Count); }
        [Test] public void WorldSaverExecutesSystemTextJsonWriterInUnity()
        { var state = new WorldState(42, new GameTime(390)); string json = WorldSaver.Save(state); StringAssert.Contains("\"formatVersion\": 7", json); StringAssert.Contains("\"clock\": 390", json); Assert.AreEqual(json, WorldSaver.Save(state)); }
        private static AppleSlice Create() => new AppleSlice(Path.GetFullPath(Path.Combine(Application.dataPath, "../Content")));
    }
}
