using System;
using System.Collections.Generic;
using System.Text.Json;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;

namespace LivingWorld.Simulation.Persistence
{
    /// <summary>
    /// Rebuilds a complete <see cref="WorldState"/> from a <see cref="WorldSaver"/> JSON
    /// document. Immutable definitions are reloaded from approved Content/ by ID first;
    /// every saved reference is validated against them. Validation is atomic: a corrupt,
    /// unknown-version, unknown-ID or contradictory document throws <see cref="LoadException"/>
    /// before any world state is built, so a failed load never leaves a half-built world.
    /// </summary>
    /// <remarks>
    /// Build order: fresh world (saved clock + RNG state) → event log → shops →
    /// belongings → pending commands (inventories resolve against the restored shops and
    /// belongings) → NPCs → beliefs/memories → perception cursor → production, restock
    /// and price progress → reputation → travel.
    /// </remarks>
    public static class WorldLoader
    {
        /// <summary>Loads a fully restored world. Throws <see cref="LoadException"/> on any problem.</summary>
        /// <param name="json">A <see cref="WorldSaver"/> document.</param>
        /// <param name="contentRoot">Directory containing the approved Content/ folder.</param>
        public static WorldState Load(string json, string contentRoot)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (contentRoot == null) throw new ArgumentNullException(nameof(contentRoot));
            ContentBundle bundle = ContentBundle.Load(contentRoot);
            SavePlan plan = SavePlan.Parse(json, bundle);
            try
            {
                return plan.Build(bundle);
            }
            catch (LoadException)
            {
                throw;
            }
            catch (Exception failure)
            {
                // The plan was fully validated, so this is unreachable for honest documents;
                // never hand back a partially restored world.
                throw new LoadException("Failed to rebuild the world from the save document.", failure);
            }
        }

        /// <summary>Fully validated save contents: every ID resolved, every value checked.</summary>
        private sealed class SavePlan
        {
            private SavePlan(ulong rngState, GameTime clock) { RngState = rngState; Clock = clock; }

            private ulong RngState { get; }
            private GameTime Clock { get; }
            private EventLogState EventLog { get; set; }
            private List<PendingCommandPlan> Commands { get; } = new List<PendingCommandPlan>();
            private List<NpcPlan> Npcs { get; } = new List<NpcPlan>();
            private SortedDictionary<NpcId, List<Belief>> Beliefs { get; } = new SortedDictionary<NpcId, List<Belief>>();
            private SortedDictionary<NpcId, List<Memory>> Memories { get; } = new SortedDictionary<NpcId, List<Memory>>();
            private long PerceptionCursor { get; set; }
            private List<ShopPlan> Shops { get; } = new List<ShopPlan>();
            private List<BelongingsPlan> Belongings { get; } = new List<BelongingsPlan>();
            private ProductionState Production { get; set; }
            private RestockState Restock { get; set; }
            private PriceAdjustmentState Prices { get; set; }
            private ReputationState Reputation { get; set; }
            private bool TravelInitialized { get; set; }
            private List<TravelPlan> Travel { get; } = new List<TravelPlan>();
            // Phase 2 (formatVersion 2) sections; null when loading a version 1 document.
            private List<Relationship> Relationships { get; } = new List<Relationship>();
            private List<RelationshipBaseline> RelationshipBaselines { get; } = new List<RelationshipBaseline>();
            private long DynamicsCursor { get; set; }
            private long DynamicsDay { get; set; }
            private long RecallCursor { get; set; }
            private List<AttributedMemory> AttributedMemories { get; } = new List<AttributedMemory>();
            private bool SmithyIronExhaustionOrdered { get; set; }
            private MerchantScheduleState MerchantSchedule { get; set; }
            private TravelerSpendState TravelerSpend { get; set; }
            private WolfBountyState WolfBounty { get; set; }
            private VillageFundState VillageFund { get; set; }
            private HarvestState Harvest { get; set; }
            private TaxState Tax { get; set; }
            private CommunityFundState CommunityFund { get; set; }
            private EconomyBaselineState EconomyBaseline { get; set; }
            private SpoilageState Spoilage { get; set; }
            private DebtLedgerState DebtLedger { get; set; }

            public static SavePlan Parse(string json, ContentBundle bundle)
            {
                JsonDocument document;
                try { document = JsonDocument.Parse(json); }
                catch (Exception failure) { throw new LoadException("The save document is not valid JSON.", failure); }
                using (document)
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                        throw new LoadException("The save document must be a JSON object.");
                    var root = new Reader(document.RootElement, "save root");
                    int version = root.Property("formatVersion").Int32(0, int.MaxValue);
                    if (version < 1 || version > WorldSaver.FormatVersion)
                        throw new LoadException("Unsupported save format version " + version +
                            "; this loader reads versions 1 through " + WorldSaver.FormatVersion + ".");
                    var plan = new SavePlan(root.Property("rngState").UInt64(),
                        new GameTime(root.Property("clock").Int64()));
                    plan.ParseEventLog(root, bundle);
                    plan.ParsePendingCommands(root, bundle);
                    plan.ParseNpcs(root, bundle);
                    plan.ParseBeliefs(root, bundle);
                    plan.ParseMemories(root, bundle);
                    plan.PerceptionCursor = root.Property("perceptionCursor").Int64();
                    plan.ParseShops(root, bundle);
                    plan.ParseBelongings(root, bundle);
                    plan.ParseProduction(root);
                    plan.ParseRestock(root);
                    plan.ParsePrices(root);
                    plan.ParseReputation(root, bundle);
                    plan.ParseTravel(root, bundle);
                    if (version >= 2)
                    {
                        plan.ParseRelationships(root, bundle);
                        plan.ParseAttributedMemories(root, bundle);
                        plan.ParseSmithy(root);
                        plan.ParseMerchantSchedule(root);
                        plan.ParseTravelerSpend(root);
                        plan.ParseWolfBounty(root);
                        plan.ParseVillageFund(root);
                        plan.ParseHarvest(root);
                        plan.ParseTax(root);
                        plan.ParseCommunityFund(root);
                        plan.ParseEconomyBaseline(root);
                        plan.ParseSpoilage(root);
                        plan.ParseDebtLedger(root, bundle);
                    }
                    plan.CrossCheckCommands();
                    plan.CrossCheckWallets();
                    plan.CrossCheckLots();
                    return plan;
                }
            }

            private void ParseEventLog(Reader root, ContentBundle bundle)
            {
                Reader log = root.Property("eventLog");
                long lastIssuedId = log.Property("lastIssuedId").Int64();
                var lastIssuedTime = new GameTime(log.Property("lastIssuedTime").Int64());
                var events = new List<WorldEvent>();
                long previousId = 0;
                var previousTime = new GameTime(0);
                foreach (Reader entry in log.Items("events"))
                {
                    string path = entry.Path;
                    long id = entry.Property("id").Int64(1);
                    if (id <= previousId)
                        throw new LoadException("Event IDs must be strictly increasing in " + path + ".");
                    var time = new GameTime(entry.Property("time").Int64());
                    if (time < previousTime)
                        throw new LoadException("Event times cannot go backwards in " + path + ".");
                    var location = new LocationId(entry.Property("location").Text());
                    RequireLocation(bundle, location, path);
                    WorldEventType type = entry.Property("type").EnumValue<WorldEventType>();
                    ActorId? actor = ParseOptionalActor(entry.Property("actor"), bundle, path);
                    var targets = new List<ActorId>();
                    foreach (Reader target in entry.Items("targets"))
                        targets.Add(ParseActor(target.Text(), bundle, target.Path));
                    EventVisibility visibility = entry.Property("visibility").EnumValue<EventVisibility>();
                    ItemTypeId? itemType = ParseOptionalItem(entry.Property("itemType"), bundle, path);
                    int? quantity = ParseOptionalInt(entry.Property("quantity"));
                    int? copper = ParseOptionalInt(entry.Property("copper"));
                    ReputationGroupId? group = ParseOptionalGroup(entry.Property("reputationGroup"), bundle, path);
                    int? delta = ParseOptionalSignedInt(entry.Property("reputationDelta"));
                    CheckReputationFacts(type, group, delta, path);
                    try
                    {
                        events.Add(new WorldEvent(new WorldEventId(id), time, location, type,
                            actor, targets, visibility, itemType, quantity, copper, group, delta));
                    }
                    catch (Exception failure) { throw new LoadException("Invalid event in " + path + ".", failure); }
                    previousId = id;
                    previousTime = time;
                }
                try { EventLog = new EventLogState(events, lastIssuedId, lastIssuedTime); }
                catch (Exception failure) { throw new LoadException("Invalid event log section.", failure); }
            }

            private void ParsePendingCommands(Reader root, ContentBundle bundle)
            {
                var previousEligible = new GameTime(0);
                bool first = true;
                foreach (Reader entry in root.Items("pendingCommands"))
                {
                    string path = entry.Path;
                    var eligible = new GameTime(entry.Property("eligibleMinute").Int64());
                    if (!first && eligible < previousEligible)
                        throw new LoadException("Pending command eligible minutes cannot go backwards in " + path + ".");
                    Reader command = entry.Property("command");
                    string type = command.Property("type").Text();
                    if (type != "TheftCommand")
                        throw new LoadException("Unsupported pending command type '" + type + "' in " + path +
                            "; only TheftCommand can be restored.");
                    Reader payload = command.Property("payload");
                    string payloadPath = payload.Path;
                    var location = new LocationId(payload.Property("location").Text());
                    RequireLocation(bundle, location, payloadPath);
                    ActorId thief = ParseActor(payload.Property("thief").Text(), bundle, payloadPath);
                    ActorId sourceOwner = ParseActor(payload.Property("sourceOwner").Text(), bundle, payloadPath);
                    InventoryRef source = ParseInventoryRef(payload.Property("source"), bundle, payloadPath);
                    InventoryRef destination = ParseInventoryRef(payload.Property("destination"), bundle, payloadPath);
                    var item = new ItemTypeId(payload.Property("item").Text());
                    bundle.RequireItem(item, payloadPath);
                    int quantity = payload.Property("quantity").Int32(1, int.MaxValue);
                    EventVisibility visibility = payload.Property("visibility").EnumValue<EventVisibility>();
                    Commands.Add(new PendingCommandPlan(eligible, location, thief, sourceOwner,
                        source, destination, item, quantity, visibility));
                    previousEligible = eligible;
                    first = false;
                }
            }

            private void ParseNpcs(Reader root, ContentBundle bundle)
            {
                var seen = new HashSet<NpcId>();
                foreach (Reader entry in root.Items("npcs"))
                {
                    string path = entry.Path;
                    NpcId id = ParseKnownNpc(entry.Property("definitionId").Text(), bundle, path);
                    if (!seen.Add(id))
                        throw new LoadException("Duplicate NPC '" + id.Value + "' in " + path + ".");
                    NpcDefinition definition = bundle.NpcDefinitions[id];
                    Reader needs = entry.Property("needs");
                    NeedState needState;
                    try
                    {
                        needState = NeedState.FromSixtieths(
                            needs.Property("hungerSixtieths").Int32(),
                            needs.Property("energySixtieths").Int32(),
                            needs.Property("socialSixtieths").Int32());
                    }
                    catch (Exception failure) { throw new LoadException("Invalid needs in " + path + ".", failure); }
                    bool isSleeping = entry.Property("isSleeping").Flag();
                    NpcIntention intention = null;
                    Reader intentReader = entry.Property("intention");
                    if (!intentReader.IsNull)
                    {
                        ActivityKind kind = intentReader.Property("kind").EnumValue<ActivityKind>();
                        var destination = new LocationId(intentReader.Property("destination").Text());
                        RequireLocation(bundle, destination, intentReader.Path);
                        intention = new NpcIntention(kind, destination,
                            new GameTime(intentReader.Property("chosenAt").Int64()));
                    }
                    // NpcState.Restore enforces this again; reject here so the document is
                    // fully validated before anything is built.
                    bool sleepsByIntention = intention != null && intention.Kind == ActivityKind.Sleep;
                    if (isSleeping != sleepsByIntention)
                        throw new LoadException("Contradictory sleeping flag and intention in " + path +
                            ": sleeping requires a Sleep intention.");
                    Npcs.Add(new NpcPlan(definition, needState, isSleeping, intention));
                }
            }

            private void ParseBeliefs(Reader root, ContentBundle bundle)
            {
                foreach (KeyValuePair<string, Reader> store in root.Property("beliefs").Properties())
                {
                    NpcId owner = ParseKnownNpc(store.Key, bundle, store.Value.Path);
                    var beliefs = new List<Belief>();
                    foreach (Reader entry in store.Value.Items())
                    {
                        string path = entry.Path;
                        BeliefClaim claim = ParseClaim(entry.Property("claim"), bundle, path);
                        beliefs.Add(new Belief(claim, ParseSource(entry.Property("source"), bundle, path),
                            entry.Property("confidence").Int32(0, 100),
                            new GameTime(entry.Property("learnedAt").Int64())));
                    }
                    Beliefs.Add(owner, beliefs);
                }
            }

            private void ParseMemories(Reader root, ContentBundle bundle)
            {
                foreach (KeyValuePair<string, Reader> store in root.Property("memories").Properties())
                {
                    NpcId owner = ParseKnownNpc(store.Key, bundle, store.Value.Path);
                    var memories = new List<Memory>();
                    foreach (Reader entry in store.Value.Items())
                    {
                        string path = entry.Path;
                        BeliefClaim claim = ParseClaim(entry.Property("claim"), bundle, path);
                        int importance = entry.Property("importance").Int32(0, 100);
                        var formedAt = new GameTime(entry.Property("formedAt").Int64());
                        var lastReinforcedAt = new GameTime(entry.Property("lastReinforcedAt").Int64());
                        if (lastReinforcedAt < formedAt)
                            throw new LoadException("Memory reinforcement predates formation in " + path + ".");
                        int strength = entry.Property("strength").Int32(0, importance);
                        WorldEventId? origin = ParseOptionalEventId(entry.Property("originEventId"), path);
                        try { memories.Add(new Memory(claim, importance, formedAt, lastReinforcedAt, strength, origin)); }
                        catch (Exception failure) { throw new LoadException("Invalid memory in " + path + ".", failure); }
                    }
                    Memories.Add(owner, memories);
                }
            }

            private void ParseShops(Reader root, ContentBundle bundle)
            {
                var seen = new HashSet<LocationId>();
                foreach (Reader entry in root.Items("shops"))
                {
                    string path = entry.Path;
                    var location = new LocationId(entry.Property("location").Text());
                    RequireLocation(bundle, location, path);
                    if (!seen.Add(location))
                        throw new LoadException("Duplicate shop '" + location.Value + "' in " + path + ".");
                    NpcId owner = ParseKnownNpc(entry.Property("owner").Text(), bundle, path);
                    var stock = ParseItemCounts(entry.Property("stock"), bundle, path);
                    var lots = ParseLots(entry, bundle, path);
                    int ownerCopper = entry.Property("ownerCopper").Int32();
                    var prices = new List<KeyValuePair<ItemTypeId, int>>();
                    foreach (KeyValuePair<string, Reader> price in entry.Property("prices").Properties())
                    {
                        var item = new ItemTypeId(price.Key);
                        bundle.RequireItem(item, price.Value.Path);
                        prices.Add(new KeyValuePair<ItemTypeId, int>(item, price.Value.Int32(1, int.MaxValue)));
                    }
                    Shops.Add(new ShopPlan(location, owner, stock, lots, ownerCopper, prices));
                }
            }

            private void ParseBelongings(Reader root, ContentBundle bundle)
            {
                var seen = new HashSet<ActorId>();
                foreach (Reader entry in root.Items("belongings"))
                {
                    string path = entry.Path;
                    ActorId owner = ParseActor(entry.Property("owner").Text(), bundle, path);
                    if (!seen.Add(owner))
                        throw new LoadException("Duplicate belongings owner in " + path + ".");
                    var inventory = ParseItemCounts(entry.Property("inventory"), bundle, path);
                    var lots = ParseLots(entry, bundle, path);
                    int copper = entry.Property("copper").Int32();
                    Belongings.Add(new BelongingsPlan(owner, inventory, lots, copper));
                }
            }

            /// <summary>
            /// Parses the optional per-lot ages (formatVersion 2). Returns null when the
            /// "lots" property is absent (version 1 documents): the caller then builds
            /// age-0 lots from the counts, preserving the old behavior exactly.
            /// </summary>
            private static List<StockLotRecord> ParseLots(Reader entry, ContentBundle bundle, string path)
            {
                Reader lotsReader;
                if (!entry.TryProperty("lots", out lotsReader)) return null;
                var lots = new List<StockLotRecord>();
                foreach (Reader lot in lotsReader.Items())
                {
                    string lotPath = lot.Path;
                    var item = new ItemTypeId(lot.Property("item").Text());
                    bundle.RequireItem(item, lotPath);
                    int quantity = lot.Property("quantity").Int32(1);
                    int ageDays = lot.Property("ageDays").Int32(0);
                    try { lots.Add(new StockLotRecord(item, quantity, ageDays)); }
                    catch (Exception failure) { throw new LoadException("Invalid stock lot in " + lotPath + ".", failure); }
                }
                return lots;
            }

            private void ParseProduction(Reader root)
            {
                var ids = ParseUniqueIds(root.Property("production").Items("completedIds"));
                try { Production = new ProductionState(ids); }
                catch (Exception failure) { throw new LoadException("Invalid production section.", failure); }
            }

            private void ParseRestock(Reader root)
            {
                Reader restock = root.Property("restock");
                var triggered = ParseUniqueIds(restock.Items("triggeredIds"));
                var orders = new List<PendingRestockOrder>();
                var seenConfigs = new HashSet<string>(StringComparer.Ordinal);
                foreach (Reader order in restock.Items("pendingOrders"))
                {
                    string configurationId = order.Property("configurationId").Text();
                    if (string.IsNullOrWhiteSpace(configurationId))
                        throw new LoadException("Restock orders need a configuration ID in " + order.Path + ".");
                    if (!seenConfigs.Add(configurationId))
                        throw new LoadException("Duplicate restock order '" + configurationId + "' in " + order.Path + ".");
                    orders.Add(new PendingRestockOrder(configurationId,
                        new GameTime(order.Property("requestedAt").Int64())));
                }
                try { Restock = new RestockState(triggered, orders); }
                catch (Exception failure) { throw new LoadException("Invalid restock section.", failure); }
            }

            private void ParsePrices(Reader root)
            {
                var progress = new List<PriceAdjustmentProgress>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (Reader entry in root.Property("prices").Items("progress"))
                {
                    string configurationId = entry.Property("configurationId").Text();
                    if (string.IsNullOrWhiteSpace(configurationId))
                        throw new LoadException("Price progress needs a configuration ID in " + entry.Path + ".");
                    if (!seen.Add(configurationId))
                        throw new LoadException("Duplicate price progress '" + configurationId + "' in " + entry.Path + ".");
                    progress.Add(new PriceAdjustmentProgress(configurationId,
                        entry.Property("lastProcessedEventId").Int64(),
                        entry.Property("completedInterval").Int64(),
                        entry.Property("missedSalePending").Flag()));
                }
                try { Prices = new PriceAdjustmentState(progress); }
                catch (Exception failure) { throw new LoadException("Invalid prices section.", failure); }
            }

            private void ParseReputation(Reader root, ContentBundle bundle)
            {
                Reader reputation = root.Property("reputation");
                if (reputation.IsNull) return;
                var standings = new List<ReputationStanding>();
                var seen = new HashSet<ReputationGroupId>();
                foreach (Reader entry in reputation.Items("standings"))
                {
                    var group = new ReputationGroupId(entry.Property("group").Text());
                    if (!group.IsValid || !bundle.ReputationGroups.ContainsKey(group))
                        throw new LoadException("Unknown reputation group '" + entry.Property("group").Text() +
                            "' in " + entry.Path + ".");
                    if (!seen.Add(group))
                        throw new LoadException("Duplicate reputation group '" + group.Value + "' in " + entry.Path + ".");
                    int value = entry.Property("value").Int32(0, 100);
                    standings.Add(new ReputationStanding(group, value));
                }
                try { Reputation = new ReputationState(standings); }
                catch (Exception failure) { throw new LoadException("Invalid reputation section.", failure); }
            }

            private void ParseTravel(Reader root, ContentBundle bundle)
            {
                Reader travel = root.Property("travel");
                if (travel.IsNull) return;
                TravelInitialized = true;
                var seen = new HashSet<NpcId>();
                foreach (Reader entry in travel.Items("npcs"))
                {
                    string path = entry.Path;
                    NpcId id = ParseKnownNpc(entry.Property("id").Text(), bundle, path);
                    if (!seen.Add(id))
                        throw new LoadException("Duplicate travel NPC '" + id.Value + "' in " + path + ".");
                    Reader locationReader = entry.Property("currentLocation");
                    Reader journeyReader = entry.Property("journey");
                    bool travelling = !journeyReader.IsNull;
                    if (!travelling && locationReader.IsNull)
                        throw new LoadException("A stationary travel NPC needs a current location in " + path + ".");
                    if (travelling && !locationReader.IsNull)
                        throw new LoadException("A travelling NPC must not have a current location in " + path + ".");
                    LocationId station;
                    TravelJourney journey = null;
                    if (travelling)
                    {
                        var origin = new LocationId(journeyReader.Property("origin").Text());
                        RequireLocation(bundle, origin, journeyReader.Path);
                        var destination = new LocationId(journeyReader.Property("destination").Text());
                        RequireLocation(bundle, destination, journeyReader.Path);
                        if (origin == destination)
                            throw new LoadException("A journey needs distinct origin and destination in " + journeyReader.Path + ".");
                        long arrival = journeyReader.Property("arrival").Int64();
                        if (arrival <= Clock.TotalMinutes)
                            throw new LoadException("A journey arrival must be after the saved clock in " + journeyReader.Path + ".");
                        if (!bundle.Map.GetTravelMinutes(origin, destination).HasValue)
                            throw new LoadException("Unreachable journey destination in " + journeyReader.Path + ".");
                        journey = new TravelJourney(origin, destination, new GameTime(arrival));
                        station = origin;
                    }
                    else
                    {
                        station = new LocationId(locationReader.Text());
                        RequireLocation(bundle, station, path);
                    }
                    Travel.Add(new TravelPlan(id, station, journey));
                }
            }

            private void ParseRelationships(Reader root, ContentBundle bundle)
            {
                Reader section = root.Property("relationships");
                var seenPairs = new HashSet<string>(StringComparer.Ordinal);
                foreach (Reader entry in section.Items("pairs"))
                {
                    string path = entry.Path;
                    NpcId from = ParseKnownNpc(entry.Property("from").Text(), bundle, path);
                    NpcId to = ParseKnownNpc(entry.Property("to").Text(), bundle, path);
                    string key = from.Value + "→" + to.Value;
                    if (!seenPairs.Add(key))
                        throw new LoadException("Duplicate relationship pair '" + key + "' in " + path + ".");
                    int trust = entry.Property("trust").Int32(0, 100);
                    int affection = entry.Property("affection").Int32(0, 100);
                    string reason = entry.Property("reason").Text();
                    try { Relationships.Add(new Relationship(from, to, trust, affection, reason)); }
                    catch (Exception failure) { throw new LoadException("Invalid relationship in " + path + ".", failure); }
                }
                var seenBaselines = new HashSet<string>(StringComparer.Ordinal);
                foreach (Reader entry in section.Items("baselines"))
                {
                    string path = entry.Path;
                    NpcId from = ParseKnownNpc(entry.Property("from").Text(), bundle, path);
                    NpcId to = ParseKnownNpc(entry.Property("to").Text(), bundle, path);
                    string key = from.Value + "→" + to.Value;
                    if (!seenBaselines.Add(key))
                        throw new LoadException("Duplicate relationship baseline '" + key + "' in " + path + ".");
                    int trust = entry.Property("trust").Int32(0, 100);
                    int affection = entry.Property("affection").Int32(0, 100);
                    string reason = entry.Property("reason").Text();
                    try { RelationshipBaselines.Add(new RelationshipBaseline(from, to, trust, affection, reason)); }
                    catch (Exception failure) { throw new LoadException("Invalid relationship baseline in " + path + ".", failure); }
                }
                DynamicsCursor = section.Property("dynamicsCursor").Int64();
                DynamicsDay = section.Property("dynamicsDay").Int64();
                RecallCursor = section.Property("recallCursor").Int64();
            }

            private void ParseAttributedMemories(Reader root, ContentBundle bundle)
            {
                foreach (Reader entry in root.Items("attributedMemories"))
                {
                    string path = entry.Path;
                    NpcId owner = ParseKnownNpc(entry.Property("owner").Text(), bundle, path);
                    BeliefClaim claim = ParseClaim(entry.Property("claim"), bundle, path);
                    int originalTrust = entry.Property("originalTrustDelta").Int32(int.MinValue, int.MaxValue);
                    int originalAffection = entry.Property("originalAffectionDelta").Int32(int.MinValue, int.MaxValue);
                    int recalledTrust = entry.Property("recalledTrustDelta").Int32(int.MinValue, int.MaxValue);
                    int recalledAffection = entry.Property("recalledAffectionDelta").Int32(int.MinValue, int.MaxValue);
                    AttributedMemory record;
                    try { record = new AttributedMemory(owner, claim, originalTrust, originalAffection); }
                    catch (Exception failure) { throw new LoadException("Invalid attributed memory in " + path + ".", failure); }
                    // The recall bound (|recalled| <= |original| per axis) is a runtime
                    // invariant, not a constructor rule; still, a save that violates it is
                    // corrupt, so reject it here rather than installing a lie.
                    if (Math.Abs((long)recalledTrust) > Math.Abs((long)originalTrust) ||
                        Math.Abs((long)recalledAffection) > Math.Abs((long)originalAffection))
                        throw new LoadException("Attributed memory recall exceeds its bound in " + path + ".");
                    record.AddRecall(recalledTrust, recalledAffection);
                    AttributedMemories.Add(record);
                }
            }

            private void ParseSmithy(Reader root)
            {
                Reader section = root.Property("smithy");
                SmithyIronExhaustionOrdered = section.Property("ironExhaustionOrdered").Flag();
            }

            private void ParseMerchantSchedule(Reader root)
            {
                Reader section = root.Property("merchantSchedule");
                bool initialized = section.Property("initialized").Flag();
                long nextVisitDay = section.Property("nextVisitDay").Int64(-1);
                try { MerchantSchedule = new MerchantScheduleState(initialized, nextVisitDay); }
                catch (Exception failure) { throw new LoadException("Invalid merchant schedule.", failure); }
            }

            private void ParseTravelerSpend(Reader root)
            {
                Reader section = root.Property("travelerSpend");
                bool initialized = section.Property("initialized").Flag();
                long lastPayoutDay = section.Property("lastPayoutDay").Int64();
                int monthIndex = section.Property("monthIndex").Int32(0);
                var paid = new List<int>();
                foreach (Reader amount in section.Items("paidThisMonth"))
                    paid.Add(amount.Int32(0));
                try { TravelerSpend = new TravelerSpendState(initialized, lastPayoutDay, monthIndex, paid); }
                catch (Exception failure) { throw new LoadException("Invalid traveler spend.", failure); }
            }

            private void ParseWolfBounty(Reader root)
            {
                Reader section = root.Property("wolfBounty");
                bool initialized = section.Property("initialized").Flag();
                long winterYear = section.Property("winterYear").Int64(-1);
                var days = new List<long>();
                foreach (Reader day in section.Items("bountyDays"))
                    days.Add(day.Int64(1));
                try { WolfBounty = new WolfBountyState(initialized, winterYear, days); }
                catch (Exception failure) { throw new LoadException("Invalid wolf bounty.", failure); }
            }

            private void ParseVillageFund(Reader root)
            {
                Reader section = root.Property("villageFund");
                bool initialized = section.Property("initialized").Flag();
                int fundsCopper = section.Property("fundsCopper").Int32(0);
                long lastLevyDay = section.Property("lastLevyDay").Int64();
                long lastWageDay = section.Property("lastWageDay").Int64();
                long lastRetainerDay = section.Property("lastRetainerDay").Int64();
                var state = new VillageFundState(initialized, fundsCopper);
                state.LastLevyDay = lastLevyDay;
                state.LastWageDay = lastWageDay;
                state.LastRetainerDay = lastRetainerDay;
                VillageFund = state;
            }

            private void ParseHarvest(Reader root)
            {
                Reader section = root.Property("harvest");
                bool initialized = section.Property("initialized").Flag();
                long lastWageDay = section.Property("lastWageDay").Int64();
                try { Harvest = new HarvestState(initialized, lastWageDay); }
                catch (Exception failure) { throw new LoadException("Invalid harvest.", failure); }
            }

            private void ParseTax(Reader root)
            {
                Reader section = root.Property("tax");
                bool initialized = section.Property("initialized").Flag();
                long lastCollectionDay = section.Property("lastCollectionDay").Int64();
                try { Tax = new TaxState(initialized, lastCollectionDay); }
                catch (Exception failure) { throw new LoadException("Invalid tax.", failure); }
            }

            private void ParseCommunityFund(Reader root)
            {
                Reader section = root.Property("communityFund");
                bool initialized = section.Property("initialized").Flag();
                int communityCopper = section.Property("communityCopper").Int32(0);
                int feastCopper = section.Property("feastCopper").Int32(0);
                long lastMonthlyDay = section.Property("lastMonthlyDay").Int64();
                long lastFeastYear = section.Property("lastFeastYear").Int64();
                var state = new CommunityFundState(initialized, lastMonthlyDay, lastFeastYear);
                if (communityCopper > 0) state.CommunityPot.Credit(communityCopper);
                if (feastCopper > 0) state.FeastPot.Credit(feastCopper);
                CommunityFund = state;
            }

            private void ParseEconomyBaseline(Reader root)
            {
                Reader section = root.Property("economyBaseline");
                bool initialized = section.Property("initialized").Flag();
                long baselineCopper = section.Property("baselineCopper").Int64();
                try { EconomyBaseline = new EconomyBaselineState(initialized, baselineCopper); }
                catch (Exception failure) { throw new LoadException("Invalid economy baseline.", failure); }
            }

            private void ParseSpoilage(Reader root)
            {
                Reader section = root.Property("spoilage");
                bool initialized = section.Property("initialized").Flag();
                long lastAgedDay = section.Property("lastAgedDay").Int64();
                try { Spoilage = new SpoilageState(initialized, lastAgedDay); }
                catch (Exception failure) { throw new LoadException("Invalid spoilage.", failure); }
            }

            private void ParseDebtLedger(Reader root, ContentBundle bundle)
            {
                Reader section = root.Property("debtLedger");
                bool initialized = section.Property("initialized").Flag();
                long lastFeastYear = section.Property("lastFeastYear").Int64();
                var debts = new List<DebtRecord>();
                var seenPairs = new HashSet<string>(StringComparer.Ordinal);
                foreach (Reader entry in section.Items("debts"))
                {
                    string path = entry.Path;
                    NpcId debtor = ParseKnownNpc(entry.Property("debtor").Text(), bundle, path);
                    NpcId creditor = ParseKnownNpc(entry.Property("creditor").Text(), bundle, path);
                    string key = debtor.Value + "→" + creditor.Value;
                    if (!seenPairs.Add(key))
                        throw new LoadException("Duplicate debt '" + key + "' in " + path + ".");
                    int owedCopper = entry.Property("owedCopper").Int32(1);
                    long openedDay = entry.Property("openedDay").Int64(1);
                    DebtTerms terms = ParseDebtTerms(entry.Property("terms"), bundle, path);
                    DebtRecord debt;
                    try { debt = new DebtRecord(debtor, creditor, owedCopper, terms, openedDay); }
                    catch (Exception failure) { throw new LoadException("Invalid debt in " + path + ".", failure); }
                    debt.LastPaymentDay = entry.Property("lastPaymentDay").Int64();
                    debt.LastWeeklyDay = entry.Property("lastWeeklyDay").Int64();
                    debt.OverdueDeclared = entry.Property("overdueDeclared").Flag();
                    debts.Add(debt);
                }
                var ledger = new DebtLedgerState(initialized);
                ledger.LastFeastYear = lastFeastYear;
                try { ledger.ReplaceAll(debts); }
                catch (Exception failure) { throw new LoadException("Invalid debt ledger.", failure); }
                DebtLedger = ledger;
            }

            private static DebtTerms ParseDebtTerms(Reader terms, ContentBundle bundle, string path)
            {
                int copperPerWeek = terms.Property("copperPerWeek").Int32(0);
                ItemTypeId? itemPerWeek = null;
                Reader itemReader = terms.Property("itemPerWeek");
                if (!itemReader.IsNull)
                {
                    var item = new ItemTypeId(itemReader.Text());
                    bundle.RequireItem(item, path);
                    itemPerWeek = item;
                }
                int itemsPerWeek = terms.Property("itemsPerWeek").Int32(0);
                int itemCreditCopper = terms.Property("itemCreditCopper").Int32(0);
                int payChancePercent = terms.Property("payChancePercent").Int32(0, 100);
                int overdueAfterDays = terms.Property("overdueAfterDays").Int32(1);
                try
                {
                    return new DebtTerms(copperPerWeek, itemPerWeek, itemsPerWeek,
                        itemCreditCopper, payChancePercent, overdueAfterDays);
                }
                catch (Exception failure) { throw new LoadException("Invalid debt terms in " + path + ".", failure); }
            }

            private void CrossCheckCommands()
            {
                var shopLocations = new HashSet<LocationId>();
                foreach (ShopPlan shop in Shops) shopLocations.Add(shop.Location);
                var owners = new HashSet<ActorId>();
                foreach (BelongingsPlan belongings in Belongings) owners.Add(belongings.Owner);
                foreach (PendingCommandPlan command in Commands)
                {
                    CheckInventoryRef(command.Source, shopLocations, owners, "source");
                    CheckInventoryRef(command.Destination, shopLocations, owners, "destination");
                }
            }

            private static void CheckInventoryRef(InventoryRef reference, HashSet<LocationId> shops,
                HashSet<ActorId> owners, string role)
            {
                if (reference.IsShopStock)
                {
                    if (!shops.Contains(reference.Shop))
                        throw new LoadException("Pending command " + role + " references unknown shop '" +
                            reference.Shop.Value + "'.");
                }
                else if (!owners.Contains(reference.Owner))
                {
                    throw new LoadException("Pending command " + role + " references unknown belongings owner.");
                }
            }

            /// <summary>
            /// A shop owner's wallet is their personal wallet (one object, registered once):
            /// when the save holds both, the two copper values must agree and the loader
            /// restores a single shared Wallet. Disagreement is contradictory.
            /// </summary>
            private void CrossCheckWallets()
            {
                var copper = new Dictionary<ActorId, int>();
                foreach (BelongingsPlan belongings in Belongings) copper.Add(belongings.Owner, belongings.Copper);
                foreach (ShopPlan shop in Shops)
                {
                    ActorId owner = ActorId.ForNpc(shop.Owner);
                    int personal;
                    if (copper.TryGetValue(owner, out personal) && personal != shop.OwnerCopper)
                        throw new LoadException("Contradictory wallet balances for '" + shop.Owner.Value +
                            "': shop owner copper is " + shop.OwnerCopper + " but belongings copper is " +
                            personal + ".");
                }
            }

            /// <summary>
            /// Lot quantities must sum to the saved counts: a document that claims 9 apples
            /// in "stock" but lots totaling 7 is corrupt. Version 1 documents have no lots
            /// and skip this check.
            /// </summary>
            private void CrossCheckLots()
            {
                foreach (ShopPlan shop in Shops) CheckLots(shop.Stock, shop.Lots, "shop '" + shop.Location.Value + "'");
                foreach (BelongingsPlan belongings in Belongings)
                    CheckLots(belongings.Inventory, belongings.Lots, "belongings of '" + belongings.Owner + "'");
            }

            private static void CheckLots(List<KeyValuePair<ItemTypeId, int>> counts,
                List<StockLotRecord> lots, string where)
            {
                if (lots == null) return;
                var totals = new Dictionary<ItemTypeId, int>();
                foreach (StockLotRecord lot in lots)
                {
                    int total;
                    totals.TryGetValue(lot.Item, out total);
                    totals[lot.Item] = checked(total + lot.Quantity);
                }
                foreach (KeyValuePair<ItemTypeId, int> pair in counts)
                {
                    int total;
                    if (!totals.TryGetValue(pair.Key, out total) || total != pair.Value)
                        throw new LoadException("Lot quantities do not sum to the saved counts for " + where + ".");
                }
                foreach (KeyValuePair<ItemTypeId, int> total in totals)
                {
                    bool found = false;
                    foreach (KeyValuePair<ItemTypeId, int> pair in counts)
                        if (pair.Key == total.Key) { found = true; break; }
                    if (!found)
                        throw new LoadException("Lots name an item with no saved count for " + where + ".");
                }
            }

            public WorldState Build(ContentBundle bundle)
            {
                // Every value below was validated during Parse; the restore contracts
                // validate once more, and any failure discards this world (never returned).
                var state = new WorldState(RngState, Clock);
                state.Events.RestoreState(EventLog);
                List<NpcBelongingsEntry> belongings = BuildBelongings(bundle);
                state.Shops.Restore(BuildShops(bundle, belongings));
                state.Belongings.Restore(belongings);
                state.RestorePendingCommands(new PendingCommandsState(BuildCommands(state)));
                foreach (NpcPlan npc in Npcs)
                    state.Npcs.Register(NpcState.Restore(npc.Definition, npc.Needs, npc.IsSleeping, npc.Intention));
                var known = new SortedSet<NpcId>();
                foreach (KeyValuePair<NpcId, List<Belief>> pair in Beliefs) known.Add(pair.Key);
                foreach (KeyValuePair<NpcId, List<Memory>> pair in Memories) known.Add(pair.Key);
                foreach (NpcId npc in known) state.Knowledge.Register(npc);
                foreach (KeyValuePair<NpcId, List<Belief>> pair in Beliefs)
                {
                    BeliefStore store = state.Knowledge.Get(pair.Key);
                    foreach (Belief belief in pair.Value) store.Set(belief);
                }
                foreach (KeyValuePair<NpcId, List<Memory>> pair in Memories)
                    state.Knowledge.GetMemories(pair.Key).Restore(pair.Value);
                state.Knowledge.RestorePerceptionCursor(PerceptionCursor);
                state.RestoreProduction(Production);
                state.RestoreRestock(Restock);
                state.RestorePrices(Prices);
                if (Reputation != null) state.Knowledge.RestoreReputation(Reputation);
                if (TravelInitialized)
                {
                    state.InitializeTravel(bundle.Map);
                    foreach (TravelPlan plan in Travel)
                    {
                        state.Travel.RegisterNpc(plan.Id, plan.Station);
                        if (plan.Journey != null)
                        {
                            NpcTravelState entry = state.Travel[plan.Id];
                            entry.CurrentLocation = null;
                            entry.Journey = plan.Journey;
                        }
                    }
                }
                // Phase 2 state (formatVersion 2); a version 1 document leaves the
                // defaults in place, matching a freshly built world.
                state.Knowledge.RestoreRelationships(Relationships);
                state.Knowledge.RestoreRelationshipBaselines(RelationshipBaselines);
                state.Knowledge.RestoreDynamicsCursor(DynamicsCursor);
                state.Knowledge.RestoreDynamicsDay(DynamicsDay);
                state.Knowledge.RestoreRecallCursor(RecallCursor);
                state.Knowledge.RestoreAttributedMemories(AttributedMemories);
                state.RestoreSmithy(new SmithyState(SmithyIronExhaustionOrdered));
                if (MerchantSchedule != null) state.RestoreMerchantSchedule(MerchantSchedule);
                if (TravelerSpend != null) state.RestoreTravelerSpend(TravelerSpend);
                if (WolfBounty != null) state.RestoreWolfBounty(WolfBounty);
                if (VillageFund != null) state.RestoreVillageFund(VillageFund);
                if (Harvest != null) state.RestoreHarvest(Harvest);
                if (Tax != null) state.RestoreTax(Tax);
                if (CommunityFund != null) state.RestoreCommunityFund(CommunityFund);
                if (EconomyBaseline != null) state.RestoreEconomyBaseline(EconomyBaseline);
                if (Spoilage != null) state.RestoreSpoilage(Spoilage);
                if (DebtLedger != null) state.RestoreDebtLedger(DebtLedger);
                return state;
            }

            private List<Shop> BuildShops(ContentBundle bundle, List<NpcBelongingsEntry> belongings)
            {
                var wallets = new Dictionary<ActorId, Wallet>();
                foreach (NpcBelongingsEntry entry in belongings) wallets.Add(entry.Owner, entry.Wallet);
                var shops = new List<Shop>();
                foreach (ShopPlan plan in Shops)
                {
                    var stock = new Inventory(bundle.Catalog);
                    if (plan.Lots != null)
                        stock.RestoreLots(plan.Lots);
                    else
                        foreach (KeyValuePair<ItemTypeId, int> pair in plan.Stock) stock.Add(pair.Key, pair.Value);
                    // The owner's personal wallet is the shop's wallet: share the object so
                    // later transfers through either view stay identical (CrossCheckWallets
                    // already proved the saved balances agree).
                    Wallet ownerWallet;
                    ActorId owner = ActorId.ForNpc(plan.Owner);
                    if (!wallets.TryGetValue(owner, out ownerWallet))
                        ownerWallet = new Wallet(plan.OwnerCopper);
                    shops.Add(new Shop(plan.Location, plan.Owner, stock, ownerWallet, plan.Prices));
                }
                return shops;
            }

            private List<NpcBelongingsEntry> BuildBelongings(ContentBundle bundle)
            {
                var entries = new List<NpcBelongingsEntry>();
                foreach (BelongingsPlan plan in Belongings)
                {
                    var inventory = new Inventory(bundle.Catalog);
                    if (plan.Lots != null)
                        inventory.RestoreLots(plan.Lots);
                    else
                        foreach (KeyValuePair<ItemTypeId, int> pair in plan.Inventory)
                            inventory.Add(pair.Key, pair.Value);
                    entries.Add(new NpcBelongingsEntry(plan.Owner, inventory, new Wallet(plan.Copper)));
                }
                return entries;
            }

            private List<PendingCommandEntry> BuildCommands(WorldState state)
            {
                var entries = new List<PendingCommandEntry>();
                foreach (PendingCommandPlan plan in Commands)
                {
                    Inventory source = ResolveInventory(state, plan.Source, "source");
                    Inventory destination = ResolveInventory(state, plan.Destination, "destination");
                    entries.Add(new PendingCommandEntry(new TheftCommand(plan.Location, plan.Thief,
                        plan.SourceOwner, source, destination, plan.Item, plan.Quantity,
                        plan.Visibility), plan.EligibleMinute));
                }
                return entries;
            }

            private static Inventory ResolveInventory(WorldState state, InventoryRef reference, string role)
            {
                if (reference.IsShopStock)
                {
                    Shop shop;
                    if (!state.Shops.TryGet(reference.Shop, out shop))
                        throw new LoadException("Pending command " + role + " references unknown shop '" +
                            reference.Shop.Value + "'.");
                    return shop.Stock;
                }
                NpcBelongingsEntry entry;
                if (!state.Belongings.TryGet(reference.Owner, out entry))
                    throw new LoadException("Pending command " + role + " references unknown belongings owner.");
                return entry.Inventory;
            }
        }

        private static void RequireLocation(ContentBundle bundle, LocationId location, string path)
        {
            try { _ = bundle.Map[location]; }
            catch (Exception failure)
            {
                throw new LoadException("Unknown location '" + location.Value + "' in " + path + ".", failure);
            }
        }

        private static NpcId ParseKnownNpc(string text, ContentBundle bundle, string path)
        {
            var npc = new NpcId(text ?? string.Empty);
            if (!npc.IsValid || !bundle.NpcDefinitions.ContainsKey(npc))
                throw new LoadException("Unknown NPC '" + text + "' in " + path + ".");
            return npc;
        }

        private static ActorId ParseActor(string text, ContentBundle bundle, string path)
        {
            if (text == "player") return ActorId.Player;
            if (text != null && text.StartsWith("npc:", StringComparison.Ordinal))
                return ActorId.ForNpc(ParseKnownNpc(text.Substring(4), bundle, path));
            throw new LoadException("Invalid actor '" + text + "' in " + path + "; expected 'player' or 'npc:<id>'.");
        }

        private static ActorId? ParseOptionalActor(Reader reader, ContentBundle bundle, string path)
        {
            if (reader.IsNull) return null;
            return ParseActor(reader.Text(), bundle, path);
        }

        private static ItemTypeId? ParseOptionalItem(Reader reader, ContentBundle bundle, string path)
        {
            if (reader.IsNull) return null;
            var item = new ItemTypeId(reader.Text());
            bundle.RequireItem(item, path);
            return item;
        }

        private static int? ParseOptionalInt(Reader reader)
        {
            if (reader.IsNull) return null;
            return reader.Int32();
        }

        /// <summary>Reputation deltas are signed (-100..100); the range is checked later.</summary>
        private static int? ParseOptionalSignedInt(Reader reader)
        {
            if (reader.IsNull) return null;
            return reader.Int32(int.MinValue, int.MaxValue);
        }

        private static WorldEventId? ParseOptionalEventId(Reader reader, string path)
        {
            if (reader.IsNull) return null;
            return new WorldEventId(reader.Int64(1));
        }

        private static ReputationGroupId? ParseOptionalGroup(Reader reader, ContentBundle bundle, string path)
        {
            if (reader.IsNull) return null;
            string text = reader.Text();
            var group = new ReputationGroupId(text);
            if (!group.IsValid || !bundle.ReputationGroups.ContainsKey(group))
                throw new LoadException("Unknown reputation group '" + text + "' in " + path + ".");
            return group;
        }

        private static void CheckReputationFacts(WorldEventType type, ReputationGroupId? group, int? delta, string path)
        {
            if (type == WorldEventType.ReputationChanged)
            {
                if (!group.HasValue) throw new LoadException("A reputation-change event requires a group in " + path + ".");
                if (!delta.HasValue) throw new LoadException("A reputation-change event requires a delta in " + path + ".");
                if (delta.Value == 0 || delta.Value < -100 || delta.Value > 100)
                    throw new LoadException("Reputation delta out of range in " + path + ".");
            }
            else if (group.HasValue || delta.HasValue)
            {
                throw new LoadException("Reputation facts require a reputation-change event in " + path + ".");
            }
        }

        private static InventoryRef ParseInventoryRef(Reader reader, ContentBundle bundle, string path)
        {
            string kind = reader.Property("kind").Text();
            if (kind == "shopStock")
            {
                var shop = new LocationId(reader.Property("shop").Text());
                RequireLocation(bundle, shop, path);
                return InventoryRef.ForShop(shop);
            }
            if (kind == "belongings")
                return InventoryRef.ForOwner(ParseActor(reader.Property("owner").Text(), bundle, path));
            throw new LoadException("Unknown inventory reference kind '" + kind + "' in " + path + ".");
        }

        private static BeliefClaim ParseClaim(Reader claim, ContentBundle bundle, string path)
        {
            BeliefClaimKind kind = claim.Property("kind").EnumValue<BeliefClaimKind>();
            var location = new LocationId(claim.Property("location").Text());
            RequireLocation(bundle, location, path);
            ItemTypeId? itemType = ParseOptionalItem(claim.Property("itemType"), bundle, path);
            ActorId? subject = ParseOptionalActor(claim.Property("subject"), bundle, path);
            int? quantity = ParseOptionalInt(claim.Property("quantity"));
            try { return new BeliefClaim(kind, location, itemType, subject, quantity); }
            catch (Exception failure) { throw new LoadException("Contradictory belief claim in " + path + ".", failure); }
        }

        private static BeliefSource ParseSource(Reader source, ContentBundle bundle, string path)
        {
            BeliefSourceKind kind = source.Property("kind").EnumValue<BeliefSourceKind>();
            NpcId? speaker = null;
            Reader speakerReader = source.Property("speaker");
            if (!speakerReader.IsNull)
            {
                string text = speakerReader.Text();
                if (!text.StartsWith("npc:", StringComparison.Ordinal))
                    throw new LoadException("A belief speaker must be an NPC in " + path + ".");
                speaker = ParseKnownNpc(text.Substring(4), bundle, path);
            }
            if (kind == BeliefSourceKind.ToldBy && !speaker.HasValue)
                throw new LoadException("ToldBy provenance requires a speaker in " + path + ".");
            if (kind != BeliefSourceKind.ToldBy && speaker.HasValue)
                throw new LoadException("Only ToldBy provenance has a speaker in " + path + ".");
            WorldEventId? origin = ParseOptionalEventId(source.Property("originEventId"), path);
            var chain = new List<NpcId>();
            foreach (Reader link in source.Items("sourceChain"))
            {
                NpcId npc = ParseKnownNpc(link.Text(), bundle, link.Path);
                if (chain.Contains(npc))
                    throw new LoadException("A rumor source chain cannot repeat an NPC in " + path + ".");
                chain.Add(npc);
            }
            try { return new BeliefSource(kind, speaker, origin, chain); }
            catch (Exception failure) { throw new LoadException("Invalid belief source in " + path + ".", failure); }
        }

        private static List<KeyValuePair<ItemTypeId, int>> ParseItemCounts(Reader map, ContentBundle bundle, string path)
        {
            var counts = new List<KeyValuePair<ItemTypeId, int>>();
            foreach (KeyValuePair<string, Reader> pair in map.Properties())
            {
                var item = new ItemTypeId(pair.Key);
                bundle.RequireItem(item, pair.Value.Path);
                int count = pair.Value.Int32();
                // Inventories never hold zero counts (they are removed), so a zero is
                // redundant rather than contradictory; skipping it keeps the round trip byte-identical.
                if (count > 0) counts.Add(new KeyValuePair<ItemTypeId, int>(item, count));
            }
            return counts;
        }

        private static List<string> ParseUniqueIds(List<Reader> items)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Reader item in items)
            {
                string id = item.Text();
                if (string.IsNullOrWhiteSpace(id))
                    throw new LoadException("IDs must be non-blank in " + item.Path + ".");
                if (!seen.Add(id))
                    throw new LoadException("Duplicate ID '" + id + "' in " + item.Path + ".");
                ids.Add(id);
            }
            return ids;
        }

        private sealed class PendingCommandPlan
        {
            public PendingCommandPlan(GameTime eligibleMinute, LocationId location, ActorId thief,
                ActorId sourceOwner, InventoryRef source, InventoryRef destination,
                ItemTypeId item, int quantity, EventVisibility visibility)
            {
                EligibleMinute = eligibleMinute; Location = location; Thief = thief;
                SourceOwner = sourceOwner; Source = source; Destination = destination;
                Item = item; Quantity = quantity; Visibility = visibility;
            }
            public GameTime EligibleMinute { get; }
            public LocationId Location { get; }
            public ActorId Thief { get; }
            public ActorId SourceOwner { get; }
            public InventoryRef Source { get; }
            public InventoryRef Destination { get; }
            public ItemTypeId Item { get; }
            public int Quantity { get; }
            public EventVisibility Visibility { get; }
        }

        private sealed class InventoryRef
        {
            private InventoryRef(LocationId shop, ActorId owner, bool isShopStock)
            {
                Shop = shop; Owner = owner; IsShopStock = isShopStock;
            }
            public static InventoryRef ForShop(LocationId shop) =>
                new InventoryRef(shop, default(ActorId), true);
            public static InventoryRef ForOwner(ActorId owner) =>
                new InventoryRef(default(LocationId), owner, false);
            public bool IsShopStock { get; }
            public LocationId Shop { get; }
            public ActorId Owner { get; }
        }

        private sealed class NpcPlan
        {
            public NpcPlan(NpcDefinition definition, NeedState needs, bool isSleeping, NpcIntention intention)
            {
                Definition = definition; Needs = needs; IsSleeping = isSleeping; Intention = intention;
            }
            public NpcDefinition Definition { get; }
            public NeedState Needs { get; }
            public bool IsSleeping { get; }
            public NpcIntention Intention { get; }
        }

        private sealed class ShopPlan
        {
            public ShopPlan(LocationId location, NpcId owner, List<KeyValuePair<ItemTypeId, int>> stock,
                List<StockLotRecord> lots, int ownerCopper, List<KeyValuePair<ItemTypeId, int>> prices)
            {
                Location = location; Owner = owner; Stock = stock; Lots = lots;
                OwnerCopper = ownerCopper; Prices = prices;
            }
            public LocationId Location { get; }
            public NpcId Owner { get; }
            public List<KeyValuePair<ItemTypeId, int>> Stock { get; }
            /// <summary>Per-lot ages (formatVersion 2); null for version 1 documents.</summary>
            public List<StockLotRecord> Lots { get; }
            public int OwnerCopper { get; }
            public List<KeyValuePair<ItemTypeId, int>> Prices { get; }
        }

        private sealed class BelongingsPlan
        {
            public BelongingsPlan(ActorId owner, List<KeyValuePair<ItemTypeId, int>> inventory,
                List<StockLotRecord> lots, int copper)
            {
                Owner = owner; Inventory = inventory; Lots = lots; Copper = copper;
            }
            public ActorId Owner { get; }
            public List<KeyValuePair<ItemTypeId, int>> Inventory { get; }
            /// <summary>Per-lot ages (formatVersion 2); null for version 1 documents.</summary>
            public List<StockLotRecord> Lots { get; }
            public int Copper { get; }
        }

        private sealed class TravelPlan
        {
            public TravelPlan(NpcId id, LocationId station, TravelJourney journey)
            {
                Id = id; Station = station; Journey = journey;
            }
            public NpcId Id { get; }
            public LocationId Station { get; }
            public TravelJourney Journey { get; }
        }

        /// <summary>Strict JSON reader: duplicate properties rejected, every access path-tagged for errors.</summary>
        private sealed class Reader
        {
            private readonly JsonElement _element;
            private readonly Dictionary<string, JsonElement> _properties;

            public Reader(JsonElement element, string path)
            {
                _element = element;
                Path = path;
                if (element.ValueKind == JsonValueKind.Object)
                {
                    _properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        if (_properties.ContainsKey(property.Name))
                            throw new LoadException("Duplicate property '" + property.Name + "' in " + path + ".");
                        _properties.Add(property.Name, property.Value);
                    }
                }
            }

            public string Path { get; }
            public bool IsNull => _element.ValueKind == JsonValueKind.Null;

            public Reader Property(string name)
            {
                JsonElement value;
                if (_properties == null || !_properties.TryGetValue(name, out value))
                    throw new LoadException("Missing '" + name + "' in " + Path + ".");
                return new Reader(value, Path + " → " + name);
            }

            public bool TryProperty(string name, out Reader value)
            {
                JsonElement element;
                if (_properties != null && _properties.TryGetValue(name, out element))
                {
                    value = new Reader(element, Path + " → " + name);
                    return true;
                }
                value = null;
                return false;
            }

            public List<Reader> Items(string name)
            {
                Reader array = Property(name);
                return array.Items();
            }

            public List<Reader> Items()
            {
                if (_element.ValueKind != JsonValueKind.Array)
                    throw new LoadException("Expected an array in " + Path + ".");
                var items = new List<Reader>();
                int index = 0;
                foreach (JsonElement element in _element.EnumerateArray())
                    items.Add(new Reader(element, Path + "[" + index++ + "]"));
                return items;
            }

            /// <summary>Enumerates object properties for string-keyed maps (beliefs, stock, prices).</summary>
            public List<KeyValuePair<string, Reader>> Properties()
            {
                if (_properties == null)
                    throw new LoadException("Expected an object in " + Path + ".");
                var properties = new List<KeyValuePair<string, Reader>>();
                foreach (KeyValuePair<string, JsonElement> pair in _properties)
                    properties.Add(new KeyValuePair<string, Reader>(pair.Key,
                        new Reader(pair.Value, Path + " → " + pair.Key)));
                return properties;
            }

            public string Text()
            {
                if (_element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(_element.GetString()))
                    throw new LoadException("Expected a non-empty string in " + Path + ".");
                return _element.GetString();
            }

            public bool Flag()
            {
                if (_element.ValueKind != JsonValueKind.True && _element.ValueKind != JsonValueKind.False)
                    throw new LoadException("Expected a boolean in " + Path + ".");
                return _element.GetBoolean();
            }

            public long Int64(long min = 0)
            {
                long value;
                if (!TryGetInt64(out value))
                    throw new LoadException("Expected an integer in " + Path + ".");
                if (value < min)
                    throw new LoadException("Value " + value + " below minimum " + min + " in " + Path + ".");
                return value;
            }

            public int Int32(int min = 0, int max = int.MaxValue)
            {
                long value = Int64(min);
                if (value > max)
                    throw new LoadException("Value " + value + " above maximum " + max + " in " + Path + ".");
                return (int)value;
            }

            public ulong UInt64()
            {
                if (_element.ValueKind != JsonValueKind.Number)
                    throw new LoadException("Expected an integer in " + Path + ".");
                try { return _element.GetUInt64(); }
                catch (Exception failure) { throw new LoadException("Expected an integer in " + Path + ".", failure); }
            }

            public T EnumValue<T>()
            {
                if (_element.ValueKind != JsonValueKind.String)
                    throw new LoadException("Expected an enum name in " + Path + ".");
                string text = _element.GetString();
                object parsed;
                try { parsed = Enum.Parse(typeof(T), text, false); }
                catch (Exception failure)
                {
                    throw new LoadException("Unknown " + typeof(T).Name + " '" + text + "' in " + Path + ".", failure);
                }
                // Enum.Parse also accepts numeric strings; only exact names round-trip.
                if (!Enum.IsDefined(typeof(T), parsed) || !parsed.ToString().Equals(text, StringComparison.Ordinal))
                    throw new LoadException("Unknown " + typeof(T).Name + " '" + text + "' in " + Path + ".");
                return (T)parsed;
            }

            private bool TryGetInt64(out long value)
            {
                value = 0;
                if (_element.ValueKind != JsonValueKind.Number) return false;
                try { value = _element.GetInt64(); return true; }
                catch (FormatException) { return false; }
                catch (OverflowException) { return false; }
            }
        }
    }
}
