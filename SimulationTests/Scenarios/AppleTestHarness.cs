using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using LivingWorld.Simulation.Tests.Tools;

namespace LivingWorld.Simulation.Tests.Scenarios
{
    /// <summary>Approved fixed values that define the three-day Apple Test.</summary>
    public sealed class AppleTestConfiguration
    {
        public int StartingStock => 20;
        public int RetailCopper => 3;
        public int StolenQuantity => 6;
        public int LowStockThreshold => 10;
        public int DeliveryQuantity => 45;
        public int WholesaleCopper => 1;
        public int MaximumRetailCopper => 4;
    }

    /// <summary>Immutable observable outcome and caller-owned subsystem progress from one scenario run.</summary>
    public sealed class AppleTestResult
    {
        internal AppleTestResult(AppleTestConfiguration configuration, WorldState state, ScenarioVillage village,
            int initialApples, int initialCopper, ProductionState production, RestockState restock,
            PriceAdjustmentState prices, AppleScenarioState scenarioState, SuspicionResult suspicion, string report)
        {
            Configuration = configuration;
            EndTime = state.Clock;
            Events = state.Events.Query();
            InitialAppleTotal = initialApples;
            FinalAppleTotal = village.TotalApples();
            InitialCopperTotal = initialCopper;
            FinalCopperTotal = village.TotalCopper();
            PlayerApples = village.PlayerInventory.Count(ScenarioVillage.Apple);
            FinalShopStock = village.Shop.Stock.Count(ScenarioVillage.Apple);
            FinalRetailCopper = village.Shop.UnitPrice(ScenarioVillage.Apple);
            ProductionState = production;
            RestockState = restock;
            PriceState = prices;
            ScenarioState = scenarioState;
            GuardSuspicion = suspicion;
            Beliefs = CaptureBeliefs(state);
            WitnessObservedTheft = state.Knowledge.Get(ScenarioVillage.Witness).Query()
                .Any(belief => belief.Claim.Kind == BeliefClaimKind.TheftObserved);
            MiraMissingAppleQuantity = state.Knowledge.Get(ScenarioVillage.Mira)
                .Query(BeliefClaimKind.StockMissing, ScenarioVillage.Stall, ScenarioVillage.Apple)
                .Select(belief => belief.Claim.Quantity ?? 0).SingleOrDefault();
            Report = report;
            StoryReport = AppleStoryReportWriter.Write(this);
            Snapshot = BuildSnapshot();
        }

        public AppleTestConfiguration Configuration { get; }
        public GameTime EndTime { get; }
        public IReadOnlyList<WorldEvent> Events { get; }
        public int InitialAppleTotal { get; }
        public int FinalAppleTotal { get; }
        public int InitialCopperTotal { get; }
        public int FinalCopperTotal { get; }
        public int PlayerApples { get; }
        public int FinalShopStock { get; }
        public int FinalRetailCopper { get; }
        public int MiraMissingAppleQuantity { get; }
        public bool WitnessObservedTheft { get; }
        public SuspicionResult GuardSuspicion { get; }
        public ProductionState ProductionState { get; }
        public RestockState RestockState { get; }
        public PriceAdjustmentState PriceState { get; }
        public AppleScenarioState ScenarioState { get; }
        public IReadOnlyList<AppleBeliefView> Beliefs { get; }
        public string Report { get; }
        public string StoryReport { get; }
        public string Snapshot { get; }

        private string BuildSnapshot() => string.Join("|", new[] {
            EndTime.TotalMinutes.ToString(CultureInfo.InvariantCulture), InitialAppleTotal.ToString(CultureInfo.InvariantCulture),
            FinalAppleTotal.ToString(CultureInfo.InvariantCulture), InitialCopperTotal.ToString(CultureInfo.InvariantCulture),
            FinalCopperTotal.ToString(CultureInfo.InvariantCulture), PlayerApples.ToString(CultureInfo.InvariantCulture),
            FinalShopStock.ToString(CultureInfo.InvariantCulture), FinalRetailCopper.ToString(CultureInfo.InvariantCulture),
            MiraMissingAppleQuantity.ToString(CultureInfo.InvariantCulture), WitnessObservedTheft ? "1" : "0",
            GuardSuspicion.TotalEvidence.ToString(CultureInfo.InvariantCulture), GuardSuspicion.MayAct ? "1" : "0",
            string.Join(",", ProductionState.CompletedIds), string.Join(",", RestockState.TriggeredIds),
            string.Join(",", RestockState.PendingOrders.Select(order => order.ConfigurationId)),
            string.Join(",", PriceState.Progress.Select(progress => progress.ConfigurationId + ":" + progress.CompletedInterval)),
            string.Join(",", ScenarioState.CompletedBuyerIds), string.Join(",", ScenarioState.CompletedMeetingIds),
            ScenarioState.ExpectedShopStock.ToString(CultureInfo.InvariantCulture),
            ScenarioState.StockCounted ? "1" : "0", ScenarioState.TheftQueued ? "1" : "0",
            Report, StoryReport });

        private static IReadOnlyList<AppleBeliefView> CaptureBeliefs(WorldState state)
        {
            var result = new List<AppleBeliefView>();
            foreach (NpcId npc in new[] { ScenarioVillage.Mira, ScenarioVillage.Witness,
                ScenarioVillage.Contact, ScenarioVillage.Guard })
                foreach (Belief belief in state.Knowledge.Get(npc).Query())
                    result.Add(new AppleBeliefView(npc, belief));
            return result.AsReadOnly();
        }
    }

    /// <summary>Immutable acceptance/reporting view of one NPC belief and its provenance.</summary>
    public sealed class AppleBeliefView
    {
        internal AppleBeliefView(NpcId knower, Belief belief)
        {
            Knower = knower;
            Kind = belief.Claim.Kind;
            Subject = belief.Claim.Subject;
            Quantity = belief.Claim.Quantity;
            SourceKind = belief.Source.Kind;
            Speaker = belief.Source.Speaker;
            SourceChain = belief.Source.SourceChain.ToList().AsReadOnly();
            Confidence = belief.Confidence;
        }
        public NpcId Knower { get; }
        public BeliefClaimKind Kind { get; }
        public ActorId? Subject { get; }
        public int? Quantity { get; }
        public BeliefSourceKind SourceKind { get; }
        public NpcId? Speaker { get; }
        public IReadOnlyList<NpcId> SourceChain { get; }
        public int Confidence { get; }
    }

    /// <summary>Builds and runs the deterministic three-day scenario entirely through general systems.</summary>
    public static class AppleTestHarness
    {
        public static AppleTestResult Run(ulong seed)
        {
            var configuration = new AppleTestConfiguration();
            var village = new ScenarioVillage(configuration);
            var state = new WorldState(seed, new GameTime(5 * 60 + 59));
            foreach (NpcId npc in new[] { ScenarioVillage.Mira, ScenarioVillage.Witness,
                ScenarioVillage.Contact, ScenarioVillage.Guard }) state.Knowledge.Register(npc);

            var productionState = new ProductionState();
            var restockState = new RestockState();
            var priceState = new PriceAdjustmentState();
            var scenarioState = new AppleScenarioState(configuration.StartingStock);
            var world = new World(state);
            world.RegisterSystem(new CommandSystem());
            world.RegisterSystem(new TheftScheduleSystem(village, scenarioState, Time(1, 13, 59)));
            world.RegisterSystem(new BuyerActionSystem(village, scenarioState));
            world.RegisterSystem(new PerceptionSystem(new WitnessContext(), new WitnessTuning()));
            world.RegisterSystem(new MeetingSystem(scenarioState));
            world.RegisterSystem(new PriceAdjustmentSystem(new[] { new PriceAdjustmentConfiguration("apple-price",
                village.Shop, ScenarioVillage.Apple, Time(1, 18), 1440, 10, 100, 1, 3, 4,
                EventVisibility.Normal) }, priceState));
            world.RegisterSystem(new ProductionSystem(new[] { new ProductionConfiguration("farm-apples",
                ScenarioVillage.Farm, ScenarioVillage.Farmer, village.FarmerInventory, ScenarioVillage.Apple,
                configuration.DeliveryQuantity, Time(3, 8), EventVisibility.Normal) }, productionState));
            world.RegisterSystem(new RestockSystem(new[] { new RestockConfiguration("farm-to-stall", village.Shop,
                ScenarioVillage.Farm, ScenarioVillage.Farmer, village.FarmerInventory, village.FarmerWallet,
                ScenarioVillage.Apple, configuration.LowStockThreshold, configuration.DeliveryQuantity,
                configuration.WholesaleCopper, RestockFulfillmentPolicy.FullOnly,
                EventVisibility.Normal) }, restockState));
            using (Stream stream = File.OpenRead(Path.Combine(RepositoryRoot(), "Content/social/social.json")))
                world.RegisterSystem(new MemorySystem(MemoryRules.Load(stream)));

            int initialApples = village.TotalApples();
            int initialCopper = village.TotalCopper();
            while (state.Clock.TotalMinutes < Time(3, 23, 59).TotalMinutes) world.Tick();

            SuspicionResult suspicion = SuspicionEvaluator.Evaluate(state.Knowledge, ScenarioVillage.Guard,
                ActorId.Player, 70, new ApprovedEvidencePolicy());
            var writer = new StringWriter(CultureInfo.InvariantCulture);
            SimulationLogWriter.Write(writer, state.Events.Query(), ScenarioVillage.Names,
                ScenarioVillage.Locations, ScenarioVillage.Items);
            return new AppleTestResult(configuration, state, village, initialApples, initialCopper,
                productionState, restockState, priceState, scenarioState, suspicion, writer.ToString());
        }

        private static GameTime Time(int day, int hour, int minute = 0) =>
            new GameTime((day - 1L) * 1440 + hour * 60 + minute);

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Content/social/social.json")))
                directory = directory.Parent;
            if (directory == null) throw new DirectoryNotFoundException("Could not locate approved Content.");
            return directory.FullName;
        }
    }

    internal sealed class ScenarioVillage
    {
        internal static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        internal static readonly LocationId Stall = new LocationId("loc_apple_stall");
        internal static readonly LocationId Farm = new LocationId("loc_farm");
        internal static readonly LocationId Tavern = new LocationId("loc_tavern");
        internal static readonly NpcId Mira = new NpcId("npc_mira_holt");
        internal static readonly NpcId Farmer = new NpcId("npc_corvin_alder");
        internal static readonly NpcId Witness = new NpcId("npc_lida_alder");
        internal static readonly NpcId Contact = new NpcId("npc_bessa_marlowe");
        internal static readonly NpcId Guard = new NpcId("npc_bram_stone");
        internal static readonly IReadOnlyDictionary<NpcId, string> Names = new Dictionary<NpcId, string> {
            [Mira] = "Mira", [Farmer] = "Corvin", [Witness] = "Lida", [Contact] = "Bessa", [Guard] = "Bram" };
        internal static readonly IReadOnlyDictionary<LocationId, string> Locations = new Dictionary<LocationId, string> {
            [Stall] = "Apple stall", [Farm] = "Alder Farm", [Tavern] = "Hearthside" };
        internal static readonly IReadOnlyDictionary<ItemTypeId, string> Items = new Dictionary<ItemTypeId, string> {
            [Apple] = "apple" };

        private readonly List<Inventory> _inventories = new List<Inventory>();
        private readonly List<Wallet> _wallets = new List<Wallet>();
        internal ScenarioVillage(AppleTestConfiguration configuration)
        {
            var catalog = new ItemCatalog(new[] { new ItemDefinition(Apple, "Apple", "food", 3, 1) });
            Inventory shopStock = Inventory(catalog, configuration.StartingStock);
            FarmerInventory = Inventory(catalog, 0);
            PlayerInventory = Inventory(catalog, 0);
            var miraWallet = Wallet(850);
            FarmerWallet = Wallet(1200);
            Shop = new Shop(Stall, Mira, shopStock, miraWallet,
                new[] { new KeyValuePair<ItemTypeId, int>(Apple, configuration.RetailCopper) });
            StolenQuantity = configuration.StolenQuantity;
            Buyers = new[] {
                new BuyerPlan("morning-a", new NpcId("npc_tansy_alder"), Time(1, 8), 80, 50, 2, true, Inventory(catalog, 0), Wallet(100)),
                new BuyerPlan("morning-b", Guard, Time(1, 10), 80, 50, 3, true, Inventory(catalog, 0), Wallet(100)),
                new BuyerPlan("afternoon", Contact, Time(1, 15), 80, 50, 5, true, Inventory(catalog, 0), Wallet(100)),
                new BuyerPlan("day2-partial", Guard, Time(2, 9), 80, 50, 7, true, Inventory(catalog, 0), Wallet(100)),
                new BuyerPlan("day2-failed", Contact, Time(2, 10), 80, 50, 2, false, Inventory(catalog, 0), Wallet(100)) };
        }

        internal Shop Shop { get; }
        internal Inventory FarmerInventory { get; }
        internal Inventory PlayerInventory { get; }
        internal Wallet FarmerWallet { get; }
        internal int StolenQuantity { get; }
        internal IReadOnlyList<BuyerPlan> Buyers { get; }
        internal int TotalApples() => _inventories.Sum(inventory => inventory.Count(Apple));
        internal int TotalCopper() => _wallets.Sum(wallet => wallet.Balance);
        private Inventory Inventory(ItemCatalog catalog, int apples)
        {
            var inventory = new Inventory(catalog);
            if (apples > 0) inventory.Add(Apple, apples);
            _inventories.Add(inventory);
            return inventory;
        }
        private Wallet Wallet(int copper) { var wallet = new Wallet(copper); _wallets.Add(wallet); return wallet; }
        private static GameTime Time(int day, int hour) => new GameTime((day - 1L) * 1440 + hour * 60);
    }

    internal sealed class BuyerPlan
    {
        internal BuyerPlan(string id, NpcId buyer, GameTime at, int hunger, int minimumHunger, int quantity,
            bool allowPartial, Inventory inventory, Wallet wallet)
        { Id = id; Buyer = buyer; At = at; Hunger = hunger; MinimumHunger = minimumHunger; Quantity = quantity;
            AllowPartial = allowPartial; Inventory = inventory; Wallet = wallet; }
        internal string Id { get; }
        internal NpcId Buyer { get; }
        internal GameTime At { get; }
        internal int Hunger { get; }
        internal int MinimumHunger { get; }
        internal int Quantity { get; }
        internal bool AllowPartial { get; }
        internal Inventory Inventory { get; }
        internal Wallet Wallet { get; }
    }

    /// <summary>Caller-owned progress sufficient to reconstruct test-only scheduling systems.</summary>
    public sealed class AppleScenarioState
    {
        private readonly SortedSet<string> _buyers;
        private readonly SortedSet<string> _meetings;
        public AppleScenarioState(int expectedShopStock, IEnumerable<string> completedBuyerIds = null,
            bool stockCounted = false, IEnumerable<string> completedMeetingIds = null, bool theftQueued = false)
        {
            if (expectedShopStock < 0) throw new ArgumentOutOfRangeException(nameof(expectedShopStock));
            _buyers = Copy(completedBuyerIds, nameof(completedBuyerIds));
            _meetings = Copy(completedMeetingIds, nameof(completedMeetingIds));
            ExpectedShopStock = expectedShopStock;
            StockCounted = stockCounted;
            TheftQueued = theftQueued;
        }
        public IReadOnlyList<string> CompletedBuyerIds => _buyers.ToList().AsReadOnly();
        public IReadOnlyList<string> CompletedMeetingIds => _meetings.ToList().AsReadOnly();
        public int ExpectedShopStock { get; internal set; }
        public bool StockCounted { get; internal set; }
        public bool TheftQueued { get; internal set; }
        internal bool HasBuyer(string id) => _buyers.Contains(id);
        internal void CompleteBuyer(string id) => _buyers.Add(id);
        internal bool HasMeeting(string id) => _meetings.Contains(id);
        internal void CompleteMeeting(string id) => _meetings.Add(id);
        private static SortedSet<string> Copy(IEnumerable<string> values, string parameter)
        {
            var result = new SortedSet<string>(StringComparer.Ordinal);
            if (values == null) return result;
            foreach (string value in values)
                if (string.IsNullOrWhiteSpace(value) || !result.Add(value))
                    throw new ArgumentException("Scenario progress IDs must be unique and nonblank.", parameter);
            return result;
        }
    }

    internal sealed class TheftScheduleSystem : IWorldSystem
    {
        private readonly ScenarioVillage _village;
        private readonly AppleScenarioState _progress;
        private readonly GameTime _queueAt;
        internal TheftScheduleSystem(ScenarioVillage village, AppleScenarioState progress, GameTime queueAt)
        { _village = village; _progress = progress; _queueAt = queueAt; }
        public string Id => "scenario.theft-schedule";
        public SimulationPhase Phase => SimulationPhase.Commands;
        public void Tick(WorldState state)
        {
            if (_progress.TheftQueued || state.Clock < _queueAt) return;
            state.EnqueueCommand(new TheftCommand(ScenarioVillage.Stall, ActorId.Player,
                ActorId.ForNpc(ScenarioVillage.Mira), _village.Shop.Stock, _village.PlayerInventory,
                ScenarioVillage.Apple, _village.StolenQuantity, EventVisibility.Normal));
            _progress.TheftQueued = true;
        }
    }

    internal sealed class BuyerActionSystem : IWorldSystem
    {
        private readonly ScenarioVillage _village;
        private readonly AppleScenarioState _progress;
        internal BuyerActionSystem(ScenarioVillage village, AppleScenarioState progress)
        { _village = village; _progress = progress; }
        public string Id => "scenario.buyer-actions";
        public SimulationPhase Phase => SimulationPhase.Actions;
        public void Tick(WorldState state)
        {
            foreach (BuyerPlan plan in _village.Buyers)
                if (!_progress.HasBuyer(plan.Id) && state.Clock >= plan.At && plan.Hunger >= plan.MinimumHunger)
                {
                    PurchaseResult result = _village.Shop.Purchase(state, new PurchaseRequest(ActorId.ForNpc(plan.Buyer),
                        plan.Inventory, plan.Wallet, ScenarioVillage.Apple, plan.Quantity, plan.AllowPartial));
                    _progress.ExpectedShopStock -= result.ActualQuantity;
                    _progress.CompleteBuyer(plan.Id);
                }
            if (!_progress.StockCounted && state.Clock >= AppleTime(1, 19))
            {
                StockCountInference.Record(state, new StockCountSnapshot(ScenarioVillage.Mira, ScenarioVillage.Stall,
                    ScenarioVillage.Apple, _progress.ExpectedShopStock,
                    _village.Shop.Stock.Count(ScenarioVillage.Apple)), 90);
                _progress.StockCounted = true;
            }
        }
        private static GameTime AppleTime(int day, int hour) => new GameTime((day - 1L) * 1440 + hour * 60);
    }

    internal sealed class WitnessContext : IPerceptionContext
    {
        public IEnumerable<NpcId> Candidates(WorldEvent worldEvent) =>
            worldEvent.Type == WorldEventType.Theft ? new[] { ScenarioVillage.Witness } : Array.Empty<NpcId>();
        public bool IsPresentAt(NpcId npc, LocationId location) => npc == ScenarioVillage.Witness && location == ScenarioVillage.Stall;
        public bool IsAwake(NpcId npc) => true;
    }

    internal sealed class WitnessTuning : IPerceptionTuning
    {
        public int NoticeChancePercent(NpcId npc, WorldEvent worldEvent) => 50;
        public int Confidence(NpcId npc, WorldEvent worldEvent) => 70;
    }

    internal sealed class MeetingSystem : IWorldSystem
    {
        private readonly AppleScenarioState _progress;
        private readonly IRumorSelectionPolicy _selection = new TheftSelectionPolicy();
        private readonly IRumorDistortionPolicy _distortion = new NoDistortionPolicy();
        internal MeetingSystem(AppleScenarioState progress) { _progress = progress; }
        public string Id => "scenario.meetings";
        public SimulationPhase Phase => SimulationPhase.Social;
        public void Tick(WorldState state)
        {
            Meet(state, "lida-bessa", 1, ScenarioVillage.Witness, ScenarioVillage.Contact, 90);
            Meet(state, "bessa-bram", 2, ScenarioVillage.Contact, ScenarioVillage.Guard, 90);
            Meet(state, "mira-bram", 3, ScenarioVillage.Mira, ScenarioVillage.Guard, 100);
        }
        private void Meet(WorldState state, string id, int day, NpcId speaker, NpcId listener, int trust)
        {
            if (_progress.HasMeeting(id) || state.Clock < new GameTime((day - 1L) * 1440 + 20 * 60 + 30)) return;
            RumorExchange.Share(state, new ConversationContext(speaker, listener, ScenarioVillage.Tavern,
                trust, 100, 0, 0), _selection, _distortion);
            _progress.CompleteMeeting(id);
        }
    }

    internal sealed class TheftSelectionPolicy : IRumorSelectionPolicy
    {
        public bool IsEligible(Belief belief, ConversationContext context) =>
            belief.Claim.Kind == BeliefClaimKind.TheftObserved || belief.Claim.Kind == BeliefClaimKind.StockMissing;
        public int Salience(Belief belief, ConversationContext context) =>
            (belief.Claim.Kind == BeliefClaimKind.TheftObserved ? 100 : 0) + belief.Confidence;
    }
    internal sealed class NoDistortionPolicy : IRumorDistortionPolicy
    { public BeliefClaim Distort(BeliefClaim claim, ConversationContext context) => claim; }
    internal sealed class ApprovedEvidencePolicy : IEvidencePolicy
    {
        private int _circumstantial;
        public int Score(Belief belief, ActorId suspect)
        {
            if (belief.Claim.Kind == BeliefClaimKind.TheftObserved && belief.Claim.Subject == suspect
                && belief.Source.SourceChain.Contains(ScenarioVillage.Witness)) return 40;
            if (belief.Claim.Kind != BeliefClaimKind.StockMissing || _circumstantial >= 30) return 0;
            int score = Math.Min(10, 30 - _circumstantial);
            _circumstantial += score;
            return score;
        }
    }
}
