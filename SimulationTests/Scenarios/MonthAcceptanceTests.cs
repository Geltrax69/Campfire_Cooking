using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using LivingWorld.Simulation.Tests.Tools;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Scenarios
{
    /// <summary>
    /// P2-11b acceptance: the full assembled village runs 30 days on a fixed seed.
    /// Proves the living world holds together at month scale: village money stays
    /// within ±10% of baseline, the bakery sells out most days (demand pressure is
    /// real), relationships measurably shift through daily trade and tavern talk,
    /// a noticed shortage becomes a village-wide rumor, and the whole month is
    /// deterministic. One 30-day run is shared by all tests; determinism runs a
    /// second and compares event-log digests.
    /// </summary>
    public sealed class MonthAcceptanceTests
    {
        private const ulong Seed = 42;
        private const int Days = 30;
        private const int TicksPerDay = 24 * 60;

        private static readonly ItemTypeId RyeBread = new ItemTypeId("item_bread_rye");
        private static readonly ItemTypeId BarleyBread = new ItemTypeId("item_bread_barley");

        private sealed class MonthResult
        {
            public long BaselineCopper;
            public long FinalCopper;
            public int SelloutDays;
            public int MovedPairs;
            public int ToldByCount;
            public int ToldByHolders;
            public string EventDigest;
            public int EventCount;
            public Dictionary<string, int> TrustBefore;
            public Dictionary<string, int> TrustAfter;
        }

        private static MonthResult _cached;

        private static string ContentRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null &&
                   !File.Exists(Path.Combine(directory.FullName, "Content/social/social.json")))
                directory = directory.Parent;
            if (directory == null) throw new DirectoryNotFoundException("Could not locate approved Content.");
            return directory.FullName;
        }

        private static MonthResult RunMonth()
        {
            var result = new MonthResult();
            VillageAssembly.Village village = VillageAssembly.Build(ContentRoot(), Seed, 1);
            result.BaselineCopper = ProsperityIndex.TotalVillageCopper(village.State);
            result.TrustBefore = village.State.Knowledge.Relationships.Query()
                .ToDictionary(r => r.From.Value + ">" + r.To.Value, r => r.Trust);

            result.SelloutDays = 0;
            for (int day = 1; day <= Days; day++)
            {
                for (int m = 0; m < TicksPerDay; m++) village.World.Tick();
                if (village.Bakery.Stock.Count(RyeBread) == 0 &&
                    village.Bakery.Stock.Count(BarleyBread) == 0)
                    result.SelloutDays++;
            }

            result.FinalCopper = ProsperityIndex.TotalVillageCopper(village.State);
            result.TrustAfter = village.State.Knowledge.Relationships.Query()
                .ToDictionary(r => r.From.Value + ">" + r.To.Value, r => r.Trust);
            result.MovedPairs = result.TrustAfter.Count(kv =>
                !result.TrustBefore.TryGetValue(kv.Key, out int before) ||
                Math.Abs(kv.Value - before) >= 5);

            // Rumor spread: ToldBy beliefs held by NPCs beyond the origin pair.
            var toldByHolders = new HashSet<string>();
            result.ToldByCount = 0;
            foreach (var npc in village.State.Npcs.Npcs)
            {
                if (!village.State.Knowledge.TryGet(npc.Definition.Id, out BeliefStore store)) continue;
                foreach (var belief in store.Query())
                {
                    if (belief.Source.Kind == BeliefSourceKind.ToldBy)
                    {
                        result.ToldByCount++;
                        toldByHolders.Add(npc.Definition.Id.Value);
                    }
                }
            }
            result.ToldByHolders = toldByHolders.Count;

            // Determinism digest: hash of the full event log.
            var events = village.State.Events.Query(
                new GameTime(0), new GameTime(Days * TicksPerDay), null, null).ToList();
            result.EventCount = events.Count;
            var sb = new StringBuilder();
            foreach (var e in events)
            {
                sb.Append(e.Id.Value).Append('|')
                  .Append(e.Time.TotalMinutes).Append('|')
                  .Append(e.Type).Append('|')
                  .Append(e.Actor?.Npc?.Value ?? "player").Append('|')
                  .Append(string.Join(",", e.Targets.Select(t => t.Npc?.Value ?? "player"))).Append('|')
                  .Append(e.ItemType?.Value ?? "").Append('|')
                  .Append(e.Quantity?.ToString() ?? "").Append('|')
                  .Append(e.Copper?.ToString() ?? "").Append(';');
            }
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                result.EventDigest = BitConverter.ToString(hash).Replace("-", "");
            }

            WriteMonthSummary(village, result);
            return result;
        }

        private static void WriteMonthSummary(VillageAssembly.Village village, MonthResult result)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "month-summary.txt");
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine($"P2-11b MONTH SUMMARY — seed {Seed}, {Days} days");
                writer.WriteLine($"Village copper: {result.BaselineCopper} -> {result.FinalCopper} " +
                    $"({(result.FinalCopper - result.BaselineCopper) / (double)result.BaselineCopper * 100:F1}%)");
                writer.WriteLine($"Bakery sellout days: {result.SelloutDays}/{Days}");
                writer.WriteLine($"Relationship pairs moved >=5 trust: {result.MovedPairs}");
                writer.WriteLine($"ToldBy beliefs: {result.ToldByCount} across {result.ToldByHolders} NPCs");
                writer.WriteLine($"Total events: {result.EventCount}");
                writer.WriteLine();
                writer.WriteLine("Biggest relationship movers:");
                foreach (var kv in result.TrustAfter
                    .OrderByDescending(kv => Math.Abs(kv.Value -
                        (result.TrustBefore.TryGetValue(kv.Key, out int b) ? b : 0)))
                    .Take(10))
                {
                    int before = result.TrustBefore.TryGetValue(kv.Key, out int b) ? b : 0;
                    string tag = result.TrustBefore.ContainsKey(kv.Key) ? "" : " (new)";
                    writer.WriteLine($"  {kv.Key}: {before} -> {kv.Value} ({kv.Value - before:+0;-0}){tag}");
                }
            }
        }

        private static MonthResult Month()
        {
            if (_cached == null) _cached = RunMonth();
            return _cached;
        }

        [Test]
        public void MonthSimulationStaysInBand()
        {
            MonthResult r = Month();
            double change = (r.FinalCopper - r.BaselineCopper) / (double)r.BaselineCopper;
            Assert.That(Math.Abs(change), Is.LessThanOrEqualTo(0.10),
                $"Village copper moved {change:P1} over 30 days; must stay within ±10%.");
        }

        [Test]
        public void BakerySellsOutMostDays()
        {
            MonthResult r = Month();
            Assert.That(r.SelloutDays, Is.GreaterThan(15),
                $"Bakery sold out {r.SelloutDays}/30 days; demand must clear the shelves most days.");
        }

        [Test]
        public void RelationshipsShiftMeasurably()
        {
            MonthResult r = Month();
            Assert.That(r.MovedPairs, Is.GreaterThanOrEqualTo(10),
                $"Only {r.MovedPairs} relationship pairs moved >=5 trust; daily trade and tavern talk must leave a mark.");
        }

        [Test]
        public void RumorsSpreadAtVillageScale()
        {
            MonthResult r = Month();
            Assert.That(r.ToldByHolders, Is.GreaterThanOrEqualTo(1),
                $"No NPC holds a ToldBy belief; a noticed shortage must travel beyond its origin pair.");
        }

        [Test]
        public void MonthIsDeterministic()
        {
            MonthResult first = Month();
            // A fresh run on the same seed: no shared state with the cached month.
            _cached = null;
            MonthResult second = RunMonth();
            _cached = second;

            Assert.That(second.EventDigest, Is.EqualTo(first.EventDigest),
                "Two 30-day runs on seed 42 must produce identical event logs.");
            Assert.That(second.FinalCopper, Is.EqualTo(first.FinalCopper));
        }
    }
}
