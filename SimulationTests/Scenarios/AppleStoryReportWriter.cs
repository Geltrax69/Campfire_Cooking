using System;
using System.Globalization;
using System.Linq;
using System.Text;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;

namespace LivingWorld.Simulation.Tests.Scenarios
{
    /// <summary>Turns observable Apple Test outcomes into a deterministic village story.</summary>
    internal static class AppleStoryReportWriter
    {
        internal static string Write(AppleTestResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var text = new StringBuilder();
            text.Append("APPLE TEST STORY — ")
                .Append(result.WitnessObservedTheft ? "witness branch" : "no-witness branch").Append('\n');

            int morning = Sales(result, 1, 0, 14);
            int afternoon = Sales(result, 1, 14, 24);
            int afterMorning = result.Configuration.StartingStock - morning;
            int afterTheft = afterMorning - result.Configuration.StolenQuantity;
            int atCount = afterTheft - afternoon;
            int expectedAtCount = result.Configuration.StartingStock - morning - afternoon;
            text.Append("Day 1 morning — Villagers bought ").Append(Number(morning)).Append(" apples; ")
                .Append(Number(afterMorning)).Append(" remained.\n")
                .Append("Day 1 14:00 — The player stole ").Append(Number(result.Configuration.StolenQuantity))
                .Append(" apples from the stall; ").Append(Number(afterTheft)).Append(" remained.\n")
                .Append("Day 1 19:00 — After ").Append(Number(afternoon))
                .Append(" more sales, Mira counted ").Append(Number(atCount))
                .Append(" instead of ").Append(Number(expectedAtCount)).Append(" and inferred that ")
                .Append(Number(result.MiraMissingAppleQuantity))
                .Append(" apples were missing without naming a culprit.\n");

            if (result.WitnessObservedTheft)
            {
                AppleBeliefView guardRumor = result.Beliefs.Single(belief =>
                    belief.Knower == ScenarioVillage.Guard && belief.Kind == BeliefClaimKind.TheftObserved);
                text.Append("Knowledge — Lida saw the theft. Conversations carried her account from Lida to Bessa to Bram")
                    .Append("; Bram's source chain is ").Append(Chain(guardRumor)).Append(".\n");
            }
            else
                text.Append("Knowledge — Lida did not notice. No NPC learned or repeated a claim that the player stole the apples.\n");

            int partial = result.Events.Single(entry => entry.Type == WorldEventType.PartialPurchase).Quantity ?? 0;
            text.Append("Day 2 morning — A customer bought the last ").Append(Number(partial))
                .Append(" apples, and the next purchase failed because the stall was empty.\n")
                .Append("Day 3 08:00 — Corvin produced and delivered ")
                .Append(Number(result.Configuration.DeliveryQuantity)).Append(" apples. The stall ended with ")
                .Append(Number(result.FinalShopStock)).Append(" apples; scarcity had raised the price from ")
                .Append(Number(result.Configuration.RetailCopper)).Append(" to ")
                .Append(Number(result.FinalRetailCopper)).Append(" copper.\n")
                .Append("Guard conclusion — Bram had ").Append(Number(result.GuardSuspicion.TotalEvidence))
                .Append(" of ").Append(Number(result.GuardSuspicion.ProofThreshold))
                .Append(" evidence, so he did not act against the player.\n")
                .Append("Conservation — Village copper began and ended at ")
                .Append(Number(result.InitialCopperTotal)).Append(".\n");
            return text.ToString();
        }

        private static int Sales(AppleTestResult result, long day, int firstHour, int lastHour) =>
            result.Events.Where(entry => entry.Time.Day == day && entry.Time.Hour >= firstHour
                && entry.Time.Hour < lastHour && (entry.Type == WorldEventType.Purchase
                    || entry.Type == WorldEventType.PartialPurchase))
                .Sum(entry => entry.Quantity ?? 0);

        private static string Chain(AppleBeliefView belief) => string.Join(" → ", belief.SourceChain
            .Select(npc => ScenarioVillage.Names.TryGetValue(npc, out string name) ? name : npc.ToString()));

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
