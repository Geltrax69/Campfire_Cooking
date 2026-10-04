using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;

namespace LivingWorld.Simulation.Persistence
{
    /// <summary>
    /// Serializes a complete <see cref="WorldState"/> to versioned, human-readable JSON.
    /// Definitions are referenced by content ID only and reloaded from approved Content/
    /// data at load time, so the document never duplicates immutable definitions.
    /// </summary>
    /// <remarks>
    /// Saving is deterministic: the same world state always produces byte-identical JSON.
    /// Every collection is written in an already-deterministic order (ordinal ID order,
    /// event ID order, FIFO command order) and the writer emits properties in a fixed
    /// sequence, so no dictionary or hash ordering can leak into the document.
    /// </remarks>
    public static class WorldSaver
    {
        /// <summary>The save format version written at the head of every document.</summary>
        public const int FormatVersion = 3;

        /// <summary>Serializes the whole world state. Throws <see cref="SaveException"/> on failure.</summary>
        public static string Save(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            try
            {
                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream,
                        new JsonWriterOptions { Indented = true }))
                    {
                        WriteWorld(writer, state);
                        writer.Flush();
                    }
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }
            catch (SaveException)
            {
                throw;
            }
            catch (Exception failure)
            {
                throw new SaveException("Saving the world state failed.", failure);
            }
        }

        private static void WriteWorld(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", FormatVersion);
            writer.WriteNumber("clock", state.Clock.TotalMinutes);
            writer.WriteNumber("rngState", state.Rng.State);
            WriteEventLog(writer, state.Events);
            WritePendingCommands(writer, state);
            WriteNpcs(writer, state);
            WriteBeliefs(writer, state);
            WriteMemories(writer, state);
            writer.WriteNumber("perceptionCursor", state.Knowledge.CapturePerceptionCursor());
            WriteShops(writer, state);
            WriteBelongings(writer, state);
            WriteProduction(writer, state);
            WriteRestock(writer, state);
            WritePrices(writer, state);
            WriteReputation(writer, state);
            WriteTravel(writer, state);
            WriteRelationships(writer, state);
            WriteAttributedMemories(writer, state);
            WriteSmithy(writer, state);
            WriteMerchantSchedule(writer, state);
            WriteTravelerSpend(writer, state);
            WriteWolfBounty(writer, state);
            WriteVillageFund(writer, state);
            WriteHarvest(writer, state);
            WriteTax(writer, state);
            WriteCommunityFund(writer, state);
            WriteEconomyBaseline(writer, state);
            WriteSpoilage(writer, state);
            WriteDebtLedger(writer, state);
            WriteSkills(writer, state);
            WriteTavernPopularity(writer, state);
            WriteIngredientDemand(writer, state);
            writer.WriteEndObject();
        }

        private static void WriteEventLog(Utf8JsonWriter writer, EventLog log)
        {
            EventLogState snapshot = log.CaptureState();
            writer.WriteStartObject("eventLog");
            writer.WriteNumber("lastIssuedId", snapshot.LastIssuedId);
            writer.WriteNumber("lastIssuedTime", snapshot.LastIssuedTime.TotalMinutes);
            writer.WriteStartArray("events");
            foreach (WorldEvent entry in snapshot.Events) WriteEvent(writer, entry);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteEvent(Utf8JsonWriter writer, WorldEvent entry)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", entry.Id.Value);
            writer.WriteNumber("time", entry.Time.TotalMinutes);
            writer.WriteString("location", entry.Location.Value);
            writer.WriteString("type", entry.Type.ToString());
            WriteActorValue(writer, "actor", entry.Actor);
            writer.WriteStartArray("targets");
            foreach (ActorId target in entry.Targets) WriteActor(writer, target);
            writer.WriteEndArray();
            writer.WriteString("visibility", entry.Visibility.ToString());
            WriteIdValue(writer, "itemType", entry.ItemType);
            WriteNullableInt(writer, "quantity", entry.Quantity);
            WriteNullableInt(writer, "copper", entry.Copper);
            WriteIdValue(writer, "reputationGroup", entry.ReputationGroup);
            WriteNullableInt(writer, "reputationDelta", entry.ReputationDelta);
            writer.WriteEndObject();
        }

        private static void WritePendingCommands(Utf8JsonWriter writer, WorldState state)
        {
            PendingCommandsState snapshot = state.CapturePendingCommands();
            writer.WriteStartArray("pendingCommands");
            foreach (PendingCommandEntry entry in snapshot.Entries)
            {
                writer.WriteStartObject();
                writer.WriteNumber("eligibleMinute", entry.EligibleMinute.TotalMinutes);
                WriteCommand(writer, state, entry.Command);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static void WriteCommand(Utf8JsonWriter writer, WorldState state, IWorldCommand command)
        {
            writer.WriteStartObject("command");
            var theft = command as TheftCommand;
            if (theft == null)
                throw new SaveException("Cannot save a pending command of type '" +
                    command.GetType().FullName + "': only TheftCommand is supported.");
            writer.WriteString("type", "TheftCommand");
            writer.WriteStartObject("payload");
            writer.WriteString("location", theft.Location.Value);
            WriteActor(writer, "thief", theft.Thief);
            WriteActor(writer, "sourceOwner", theft.SourceOwner);
            WriteInventoryReference(writer, "source", state, theft.Source);
            WriteInventoryReference(writer, "destination", state, theft.Destination);
            writer.WriteString("item", theft.Item.Value);
            writer.WriteNumber("quantity", theft.Quantity);
            writer.WriteString("visibility", theft.Visibility.ToString());
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        /// <summary>
        /// Identifies a command-held inventory by the world-owned container that holds it:
        /// a shop's stock (by shop location) or an actor's belongings (by owner actor).
        /// Inventories that belong to neither cannot be restored, so saving fails loudly.
        /// </summary>
        private static void WriteInventoryReference(Utf8JsonWriter writer, string name,
            WorldState state, Inventory inventory)
        {
            foreach (Shop shop in state.Shops.Shops)
                if (ReferenceEquals(shop.Stock, inventory))
                {
                    writer.WriteStartObject(name);
                    writer.WriteString("kind", "shopStock");
                    writer.WriteString("shop", shop.Location.Value);
                    writer.WriteEndObject();
                    return;
                }
            foreach (NpcBelongingsEntry entry in state.Belongings.Entries)
                if (ReferenceEquals(entry.Inventory, inventory))
                {
                    writer.WriteStartObject(name);
                    writer.WriteString("kind", "belongings");
                    WriteActor(writer, "owner", entry.Owner);
                    writer.WriteEndObject();
                    return;
                }
            throw new SaveException("Cannot save a command whose inventory is not registered " +
                "as a shop stock or an actor's belongings.");
        }

        private static void WriteNpcs(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartArray("npcs");
            foreach (NpcState npc in state.Npcs.Npcs)
            {
                writer.WriteStartObject();
                writer.WriteString("definitionId", npc.Definition.Id.Value);
                writer.WriteStartObject("needs");
                writer.WriteNumber("hungerSixtieths", npc.Needs.HungerSixtieths);
                writer.WriteNumber("energySixtieths", npc.Needs.EnergySixtieths);
                writer.WriteNumber("socialSixtieths", npc.Needs.SocialSixtieths);
                writer.WriteEndObject();
                writer.WriteBoolean("isSleeping", npc.IsSleeping);
                // Phase 3 (formatVersion 3): mood on the 0-100 happiness scale.
                writer.WriteNumber("happiness", npc.Happiness);
                NpcIntention intention = npc.CurrentIntention;
                if (intention == null)
                {
                    writer.WriteNull("intention");
                }
                else
                {
                    writer.WriteStartObject("intention");
                    writer.WriteString("kind", intention.Kind.ToString());
                    writer.WriteString("destination", intention.Destination.Value);
                    writer.WriteNumber("chosenAt", intention.ChosenAt.TotalMinutes);
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static void WriteBeliefs(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartObject("beliefs");
            foreach (BeliefStore store in state.Knowledge.Stores)
            {
                writer.WriteStartArray(store.Owner.Value);
                foreach (Belief belief in store.Query()) WriteBelief(writer, belief);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }

        private static void WriteBelief(Utf8JsonWriter writer, Belief belief)
        {
            writer.WriteStartObject();
            WriteClaim(writer, belief.Claim);
            writer.WriteStartObject("source");
            writer.WriteString("kind", belief.Source.Kind.ToString());
            if (belief.Source.Speaker.HasValue)
                writer.WriteString("speaker", EncodeActor(ActorId.ForNpc(belief.Source.Speaker.Value)));
            else
                writer.WriteNull("speaker");
            if (belief.Source.OriginEventId.HasValue)
                writer.WriteNumber("originEventId", belief.Source.OriginEventId.Value.Value);
            else
                writer.WriteNull("originEventId");
            writer.WriteStartArray("sourceChain");
            foreach (NpcId npc in belief.Source.SourceChain) writer.WriteStringValue(npc.Value);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteNumber("confidence", belief.Confidence);
            writer.WriteNumber("learnedAt", belief.LearnedAt.TotalMinutes);
            writer.WriteEndObject();
        }

        private static void WriteMemories(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartObject("memories");
            foreach (MemoryStore store in state.Knowledge.MemoryStores)
            {
                writer.WriteStartArray(store.Owner.Value);
                foreach (Memory memory in store.Query()) WriteMemory(writer, memory);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }

        private static void WriteMemory(Utf8JsonWriter writer, Memory memory)
        {
            writer.WriteStartObject();
            WriteClaim(writer, memory.Claim);
            writer.WriteNumber("importance", memory.Importance);
            writer.WriteNumber("formedAt", memory.FormedAt.TotalMinutes);
            writer.WriteNumber("lastReinforcedAt", memory.LastReinforcedAt.TotalMinutes);
            writer.WriteNumber("strength", memory.Strength);
            if (memory.OriginEventId.HasValue)
                writer.WriteNumber("originEventId", memory.OriginEventId.Value.Value);
            else
                writer.WriteNull("originEventId");
            writer.WriteEndObject();
        }

        private static void WriteClaim(Utf8JsonWriter writer, BeliefClaim claim)
        {
            writer.WriteStartObject("claim");
            writer.WriteString("kind", claim.Kind.ToString());
            writer.WriteString("location", claim.Location.Value);
            WriteIdValue(writer, "itemType", claim.ItemType);
            WriteActorValue(writer, "subject", claim.Subject);
            WriteNullableInt(writer, "quantity", claim.Quantity);
            writer.WriteEndObject();
        }

        private static void WriteShops(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartArray("shops");
            foreach (Shop shop in state.Shops.Shops)
            {
                writer.WriteStartObject();
                writer.WriteString("location", shop.Location.Value);
                writer.WriteString("owner", shop.Owner.Value);
                writer.WriteStartObject("stock");
                foreach (var pair in shop.Stock.Contents)
                    writer.WriteNumber(pair.Key.Value, pair.Value);
                writer.WriteEndObject();
                WriteLots(writer, shop.Stock);
                writer.WriteNumber("ownerCopper", shop.OwnerWallet.Balance);
                // Record whether the till is the owner's personal wallet (one shared
                // object) or a separate till: the loader must restore the same sharing,
                // since systems use ReferenceEquals to avoid double-counting.
                bool shared = state.Belongings.TryGet(ActorId.ForNpc(shop.Owner), out NpcBelongingsEntry entry)
                    && ReferenceEquals(shop.OwnerWallet, entry.Wallet);
                writer.WriteBoolean("ownerWalletShared", shared);
                writer.WriteStartObject("prices");
                foreach (var pair in shop.Prices)
                    writer.WriteNumber(pair.Key.Value, pair.Value);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static void WriteBelongings(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartArray("belongings");
            foreach (NpcBelongingsEntry entry in state.Belongings.Entries)
            {
                writer.WriteStartObject();
                WriteActor(writer, "owner", entry.Owner);
                writer.WriteStartObject("inventory");
                foreach (var pair in entry.Inventory.Contents)
                    writer.WriteNumber(pair.Key.Value, pair.Value);
                writer.WriteEndObject();
                WriteLots(writer, entry.Inventory);
                writer.WriteNumber("copper", entry.Wallet.Balance);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        /// <summary>
        /// Per-lot ages for spoilage (P2-09): each lot's item, quantity and age in days,
        /// in the inventory's deterministic lot order. Always written (possibly empty) so
        /// saves stay byte-identical for equal states.
        /// </summary>
        private static void WriteLots(Utf8JsonWriter writer, Inventory inventory)
        {
            writer.WriteStartArray("lots");
            foreach (StockLotRecord lot in inventory.CaptureLots())
            {
                writer.WriteStartObject();
                writer.WriteString("item", lot.Item.Value);
                writer.WriteNumber("quantity", lot.Quantity);
                writer.WriteNumber("ageDays", lot.AgeDays);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static void WriteProduction(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartObject("production");
            writer.WriteStartArray("completedIds");
            foreach (string id in state.Production.CompletedIds) writer.WriteStringValue(id);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteRestock(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartObject("restock");
            writer.WriteStartArray("triggeredIds");
            foreach (string id in state.Restock.TriggeredIds) writer.WriteStringValue(id);
            writer.WriteEndArray();
            writer.WriteStartArray("pendingOrders");
            foreach (PendingRestockOrder order in state.Restock.PendingOrders)
            {
                writer.WriteStartObject();
                writer.WriteString("configurationId", order.ConfigurationId);
                writer.WriteNumber("requestedAt", order.RequestedAt.TotalMinutes);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WritePrices(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartObject("prices");
            writer.WriteStartArray("progress");
            foreach (PriceAdjustmentProgress entry in state.Prices.Progress)
            {
                writer.WriteStartObject();
                writer.WriteString("configurationId", entry.ConfigurationId);
                writer.WriteNumber("lastProcessedEventId", entry.LastProcessedEventId);
                writer.WriteNumber("completedInterval", entry.CompletedInterval);
                writer.WriteBoolean("missedSalePending", entry.MissedSalePending);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteReputation(Utf8JsonWriter writer, WorldState state)
        {
            ReputationState reputation = state.Knowledge.Reputation;
            if (reputation == null)
            {
                writer.WriteNull("reputation");
                return;
            }
            writer.WriteStartObject("reputation");
            writer.WriteStartArray("standings");
            foreach (ReputationStanding standing in reputation.Standings)
            {
                writer.WriteStartObject();
                writer.WriteString("group", standing.Group.Value);
                writer.WriteNumber("value", standing.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteTravel(Utf8JsonWriter writer, WorldState state)
        {
            TravelState travel = state.Travel;
            if (travel == null)
            {
                writer.WriteNull("travel");
                return;
            }
            writer.WriteStartObject("travel");
            writer.WriteStartArray("npcs");
            foreach (NpcTravelState npc in travel.Npcs)
            {
                writer.WriteStartObject();
                writer.WriteString("id", npc.Id.Value);
                if (npc.CurrentLocation.HasValue)
                    writer.WriteString("currentLocation", npc.CurrentLocation.Value.Value);
                else
                    writer.WriteNull("currentLocation");
                TravelJourney journey = npc.Journey;
                if (journey == null)
                {
                    writer.WriteNull("journey");
                }
                else
                {
                    writer.WriteStartObject("journey");
                    writer.WriteString("origin", journey.Origin.Value);
                    writer.WriteString("destination", journey.Destination.Value);
                    writer.WriteNumber("arrival", journey.Arrival.TotalMinutes);
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteRelationships(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartObject("relationships");
            writer.WriteStartArray("pairs");
            foreach (Relationship pair in state.Knowledge.CaptureRelationships())
            {
                writer.WriteStartObject();
                writer.WriteString("from", pair.From.Value);
                writer.WriteString("to", pair.To.Value);
                writer.WriteNumber("trust", pair.Trust);
                writer.WriteNumber("affection", pair.Affection);
                writer.WriteString("reason", pair.Reason);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("baselines");
            foreach (RelationshipBaseline baseline in state.Knowledge.CaptureRelationshipBaselines())
            {
                writer.WriteStartObject();
                writer.WriteString("from", baseline.From.Value);
                writer.WriteString("to", baseline.To.Value);
                writer.WriteNumber("trust", baseline.Trust);
                writer.WriteNumber("affection", baseline.Affection);
                writer.WriteString("reason", baseline.Reason);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteNumber("dynamicsCursor", state.Knowledge.CaptureDynamicsCursor());
            writer.WriteNumber("dynamicsDay", state.Knowledge.CaptureDynamicsDay());
            writer.WriteNumber("recallCursor", state.Knowledge.CaptureRecallCursor());
            writer.WriteEndObject();
        }

        private static void WriteAttributedMemories(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartArray("attributedMemories");
            foreach (AttributedMemory record in state.Knowledge.CaptureAttributedMemories())
            {
                writer.WriteStartObject();
                writer.WriteString("owner", record.Owner.Value);
                WriteClaim(writer, record.Claim);
                writer.WriteNumber("originalTrustDelta", record.OriginalTrustDelta);
                writer.WriteNumber("originalAffectionDelta", record.OriginalAffectionDelta);
                writer.WriteNumber("recalledTrustDelta", record.RecalledTrustDelta);
                writer.WriteNumber("recalledAffectionDelta", record.RecalledAffectionDelta);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static void WriteSmithy(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartObject("smithy");
            writer.WriteBoolean("ironExhaustionOrdered", state.Smithy.IronExhaustionOrdered);
            writer.WriteEndObject();
        }

        private static void WriteMerchantSchedule(Utf8JsonWriter writer, WorldState state)
        {
            MerchantScheduleState schedule = state.MerchantSchedule;
            writer.WriteStartObject("merchantSchedule");
            writer.WriteBoolean("initialized", schedule.IsInitialized);
            writer.WriteNumber("nextVisitDay", schedule.NextVisitDay);
            writer.WriteEndObject();
        }

        private static void WriteTravelerSpend(Utf8JsonWriter writer, WorldState state)
        {
            TravelerSpendState spend = state.TravelerSpend;
            writer.WriteStartObject("travelerSpend");
            writer.WriteBoolean("initialized", spend.IsInitialized);
            writer.WriteNumber("lastPayoutDay", spend.LastPayoutDay);
            writer.WriteNumber("monthIndex", spend.MonthIndex);
            writer.WriteStartArray("paidThisMonth");
            foreach (int paid in spend.PaidThisMonth) writer.WriteNumberValue(paid);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteWolfBounty(Utf8JsonWriter writer, WorldState state)
        {
            WolfBountyState bounty = state.WolfBounty;
            writer.WriteStartObject("wolfBounty");
            writer.WriteBoolean("initialized", bounty.IsInitialized);
            writer.WriteNumber("winterYear", bounty.WinterYear);
            writer.WriteStartArray("bountyDays");
            foreach (long day in bounty.BountyDays) writer.WriteNumberValue(day);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteVillageFund(Utf8JsonWriter writer, WorldState state)
        {
            VillageFundState fund = state.VillageFund;
            writer.WriteStartObject("villageFund");
            writer.WriteBoolean("initialized", fund.IsInitialized);
            writer.WriteNumber("fundsCopper", fund.Funds.Balance);
            writer.WriteNumber("lastLevyDay", fund.LastLevyDay);
            writer.WriteNumber("lastWageDay", fund.LastWageDay);
            writer.WriteNumber("lastRetainerDay", fund.LastRetainerDay);
            writer.WriteEndObject();
        }

        private static void WriteHarvest(Utf8JsonWriter writer, WorldState state)
        {
            HarvestState harvest = state.Harvest;
            writer.WriteStartObject("harvest");
            writer.WriteBoolean("initialized", harvest.IsInitialized);
            writer.WriteNumber("lastWageDay", harvest.LastWageDay);
            writer.WriteEndObject();
        }

        private static void WriteTax(Utf8JsonWriter writer, WorldState state)
        {
            TaxState tax = state.Tax;
            writer.WriteStartObject("tax");
            writer.WriteBoolean("initialized", tax.IsInitialized);
            writer.WriteNumber("lastCollectionDay", tax.LastCollectionDay);
            writer.WriteEndObject();
        }

        private static void WriteCommunityFund(Utf8JsonWriter writer, WorldState state)
        {
            CommunityFundState fund = state.CommunityFund;
            writer.WriteStartObject("communityFund");
            writer.WriteBoolean("initialized", fund.IsInitialized);
            writer.WriteNumber("communityCopper", fund.CommunityPot.Balance);
            writer.WriteNumber("feastCopper", fund.FeastPot.Balance);
            writer.WriteNumber("lastMonthlyDay", fund.LastMonthlyDay);
            writer.WriteNumber("lastFeastYear", fund.LastFeastYear);
            writer.WriteEndObject();
        }

        private static void WriteEconomyBaseline(Utf8JsonWriter writer, WorldState state)
        {
            EconomyBaselineState baseline = state.EconomyBaseline;
            writer.WriteStartObject("economyBaseline");
            writer.WriteBoolean("initialized", baseline.IsInitialized);
            writer.WriteNumber("baselineCopper", baseline.BaselineCopper);
            writer.WriteEndObject();
        }

        private static void WriteSpoilage(Utf8JsonWriter writer, WorldState state)
        {
            SpoilageState spoilage = state.Spoilage;
            writer.WriteStartObject("spoilage");
            writer.WriteBoolean("initialized", spoilage.IsInitialized);
            writer.WriteNumber("lastAgedDay", spoilage.LastAgedDay);
            writer.WriteEndObject();
        }

        private static void WriteDebtLedger(Utf8JsonWriter writer, WorldState state)
        {
            DebtLedgerState ledger = state.DebtLedger;
            writer.WriteStartObject("debtLedger");
            writer.WriteBoolean("initialized", ledger.IsInitialized);
            writer.WriteNumber("lastFeastYear", ledger.LastFeastYear);
            writer.WriteStartArray("debts");
            foreach (DebtRecord debt in ledger.Debts)
            {
                writer.WriteStartObject();
                writer.WriteString("debtor", debt.Debtor.Value);
                writer.WriteString("creditor", debt.Creditor.Value);
                writer.WriteNumber("owedCopper", debt.OwedCopper);
                writer.WriteNumber("openedDay", debt.OpenedDay);
                writer.WriteNumber("lastPaymentDay", debt.LastPaymentDay);
                writer.WriteNumber("lastWeeklyDay", debt.LastWeeklyDay);
                writer.WriteBoolean("overdueDeclared", debt.OverdueDeclared);
                WriteDebtTerms(writer, debt.Terms);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteDebtTerms(Utf8JsonWriter writer, DebtTerms terms)
        {
            writer.WriteStartObject("terms");
            writer.WriteNumber("copperPerWeek", terms.CopperPerWeek);
            if (terms.ItemPerWeek.HasValue)
                writer.WriteString("itemPerWeek", terms.ItemPerWeek.Value.Value);
            else
                writer.WriteNull("itemPerWeek");
            writer.WriteNumber("itemsPerWeek", terms.ItemsPerWeek);
            writer.WriteNumber("itemCreditCopper", terms.ItemCreditCopper);
            writer.WriteNumber("payChancePercent", terms.PayChancePercent);
            writer.WriteNumber("overdueAfterDays", terms.OverdueAfterDays);
            writer.WriteEndObject();
        }

        /// <summary>
        /// Phase 3 skill state (formatVersion 3): every actor's captured skills, keyed
        /// by actor ("player" or "npc:&lt;id&gt;"). Actors with no skills are omitted;
        /// the loader restores them as empty stores. Skills inside each list are in
        /// ordinal SkillId order (SkillStore.Capture), so the section is deterministic.
        /// </summary>
        private static void WriteSkills(Utf8JsonWriter writer, WorldState state)
        {
            writer.WriteStartObject("skills");
            WriteSkillList(writer, "player", state.PlayerSkills.Capture());
            foreach (NpcState npc in state.Npcs.Npcs)
            {
                IReadOnlyList<SkillState> skills = npc.Skills.Capture();
                if (skills.Count == 0) continue;
                WriteSkillList(writer, "npc:" + npc.Definition.Id.Value, skills);
            }
            writer.WriteEndObject();
        }

        private static void WriteSkillList(Utf8JsonWriter writer, string name,
            IReadOnlyList<SkillState> skills)
        {
            if (skills.Count == 0) return;
            writer.WriteStartArray(name);
            foreach (SkillState skill in skills)
            {
                writer.WriteStartObject();
                writer.WriteString("skill", skill.Skill.Value);
                writer.WriteNumber("level", skill.Level);
                writer.WriteNumber("practicePoints", skill.PracticePoints);
                writer.WriteNumber("dailyPoints", skill.DailyPoints);
                writer.WriteNumber("lastPracticeDay", skill.LastPracticeDay);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        /// <summary>
        /// Phase 3 tavern popularity (formatVersion 3): the 0-100 renown plus the two
        /// day cursors the decay system needs to resume exactly where it left off.
        /// </summary>
        private static void WriteTavernPopularity(Utf8JsonWriter writer, WorldState state)
        {
            TavernPopularityState popularity = state.TavernPopularity;
            writer.WriteStartObject("tavernPopularity");
            writer.WriteNumber("popularity", popularity.Popularity);
            writer.WriteNumber("lastSkilledCookDay", popularity.LastSkilledCookDay);
            writer.WriteNumber("lastDecayDay", popularity.LastDecayDay);
            writer.WriteEndObject();
        }

        /// <summary>
        /// Phase 3 ingredient demand (formatVersion 3): the decay cursor plus every
        /// item with outstanding demand, in ordinal item order. Only positive demand
        /// is ever stored, so every written amount is at least 1.
        /// </summary>
        private static void WriteIngredientDemand(Utf8JsonWriter writer, WorldState state)
        {
            IngredientDemandState demand = state.IngredientDemand;
            writer.WriteStartObject("ingredientDemand");
            writer.WriteNumber("lastDecayDay", demand.LastDecayDay);
            writer.WriteStartObject("demand");
            foreach (KeyValuePair<ItemTypeId, int> pair in demand.All)
                writer.WriteNumber(pair.Key.Value, pair.Value);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static void WriteActor(Utf8JsonWriter writer, string name, ActorId actor)
        {
            writer.WriteString(name, EncodeActor(actor));
        }        private static void WriteActor(Utf8JsonWriter writer, ActorId actor)
        {
            writer.WriteStringValue(EncodeActor(actor));
        }

        private static void WriteActorValue(Utf8JsonWriter writer, string name, ActorId? actor)
        {
            if (actor.HasValue)
                writer.WriteString(name, EncodeActor(actor.Value));
            else
                writer.WriteNull(name);
        }

        /// <summary>Actors are encoded as "player" or "npc:&lt;content id&gt;".</summary>
        private static string EncodeActor(ActorId actor)
        {
            return actor.IsPlayer ? "player" : "npc:" + actor.Npc.Value.Value;
        }

        private static void WriteIdValue(Utf8JsonWriter writer, string name, ItemTypeId? id)
        {
            if (id.HasValue)
                writer.WriteString(name, id.Value.Value);
            else
                writer.WriteNull(name);
        }

        private static void WriteIdValue(Utf8JsonWriter writer, string name, ReputationGroupId? id)
        {
            if (id.HasValue)
                writer.WriteString(name, id.Value.Value);
            else
                writer.WriteNull(name);
        }

        private static void WriteNullableInt(Utf8JsonWriter writer, string name, int? value)
        {
            if (value.HasValue)
                writer.WriteNumber(name, value.Value);
            else
                writer.WriteNull(name);
        }
    }
}
