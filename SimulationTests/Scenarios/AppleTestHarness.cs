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

    /// <summary>Immutable observable outcome and world-owned subsystem progress from one scenario run.</summary>
    public sealed class AppleTestResult
    {
        internal AppleTestResult(AppleTestConfiguration configuration, WorldState state, ScenarioVillage village,
            int initialApples, int initialCopper, AppleScenarioState scenarioState, SuspicionResult suspicion,
            string report)
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
            ProductionState = state.Production;
            RestockState = state.Restock;
            PriceState = state.Prices;
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
            var state = new WorldState(seed, new GameTime(5 * 60 + 59));
            var village = new ScenarioVillage(configuration, state);
            var scenarioState = new AppleScenarioState(configuration.StartingStock);
            var world = new World(state);
            RegisterAppleSystems(state, world, configuration, scenarioState);

            int initialApples = village.TotalApples();
            int initialCopper = village.TotalCopper();
            while (state.Clock.TotalMinutes < Time(3, 23, 59).TotalMinutes) world.Tick();

            SuspicionResult suspicion = SuspicionEvaluator.Evaluate(state.Knowledge, ScenarioVillage.Guard,
                ActorId.Player, 70, new ApprovedEvidencePolicy());
            var writer = new StringWriter(CultureInfo.InvariantCulture);
            SimulationLogWriter.Write(writer, state.Events.Query(), ScenarioVillage.Names,
                ScenarioVillage.Locations, ScenarioVillage.Items);
            return new AppleTestResult(configuration, state, village, initialApples, initialCopper,
                scenarioState, suspicion, writer.ToString());
        }

        private static GameTime Time(int day, int hour, int minute = 0) =>
            new GameTime((day - 1L) * 1440 + hour * 60 + minute);

        /// <summary>
        /// Registers the full Apple Test system set against a world whose shops, belongings and
        /// knowledge were built through WorldState APIs. Shared with the world-completeness test
        /// so only the village assembly differs between the two paths.
        /// </summary>
        internal static void RegisterAppleSystems(WorldState state, World world,
            AppleTestConfiguration configuration, AppleScenarioState scenarioState)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (scenarioState == null) throw new ArgumentNullException(nameof(scenarioState));
            world.RegisterSystem(new CommandSystem());
            world.RegisterSystem(new TheftScheduleSystem(scenarioState, Time(1, 13, 59),
                configuration.StolenQuantity));
            world.RegisterSystem(new BuyerActionSystem(ScenarioVillage.BuyerPlans(configuration), scenarioState));
            world.RegisterSystem(new PerceptionSystem(new WitnessContext(), new WitnessTuning()));
            world.RegisterSystem(new MeetingSystem(scenarioState));
            world.RegisterSystem(new PriceAdjustmentSystem(new[] { new PriceAdjustmentConfiguration("apple-price",
                state.Shops[ScenarioVillage.Stall], ScenarioVillage.Apple, Time(1, 18), 1440, 10, 100, 1, 3, 4,
                EventVisibility.Normal) }, state));
            NpcBelongingsEntry farmer = state.Belongings[ActorId.ForNpc(ScenarioVillage.Farmer)];
            world.RegisterSystem(new ProductionSystem(new[] { new ProductionConfiguration("farm-apples",
                ScenarioVillage.Farm, ScenarioVillage.Farmer, farmer.Inventory, ScenarioVillage.Apple,
                configuration.DeliveryQuantity, Time(3, 8), EventVisibility.Normal) }, state));
            world.RegisterSystem(new RestockSystem(new[] { new RestockConfiguration("farm-to-stall",
                state.Shops[ScenarioVillage.Stall], ScenarioVillage.Farm, ScenarioVillage.Farmer,
                farmer.Inventory, farmer.Wallet, ScenarioVillage.Apple, configuration.LowStockThreshold,
                configuration.DeliveryQuantity, configuration.WholesaleCopper,
                RestockFulfillmentPolicy.FullOnly, EventVisibility.Normal) }, state));
            using (Stream stream = File.OpenRead(Path.Combine(RepositoryRoot(), "Content/social/social.json")))
                world.RegisterSystem(new MemorySystem(MemoryRules.Load(stream)));
        }

        internal static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Content/social/social.json")))
                directory = directory.Parent;
            if (directory == null) throw new DirectoryNotFoundException("Could not locate approved Content.");
            return directory.FullName;
        }
    }

    /// <summary>
    /// Builds the Apple Test village solely through WorldState registration APIs and reads it
    /// back the same way, so no scenario game state lives outside the world.
    /// </summary>
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

        private readonly WorldState _state;

        internal ScenarioVillage(AppleTestConfiguration configuration, WorldState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            var catalog = new ItemCatalog(new[] { new ItemDefinition(Apple, "Apple", "food", 3, 1) });

            var shopStock = new Inventory(catalog);
            shopStock.Add(Apple, configuration.StartingStock);
            var miraWallet = new Wallet(850);
            _state.Shops.Register(new Shop(Stall, Mira, shopStock, miraWallet,
                new[] { new KeyValuePair<ItemTypeId, int>(Apple, configuration.RetailCopper) }));
            // Mira's personal wallet is the shop's owner wallet: one object, registered once.
            _state.Belongings.Register(ActorId.ForNpc(Mira), new Inventory(catalog), miraWallet);

            var farmerStock = new Inventory(catalog);
            var farmerWallet = new Wallet(1200);
            _state.Belongings.Register(ActorId.ForNpc(Farmer), farmerStock, farmerWallet);
            _state.Belongings.Register(ActorId.Player, new Inventory(catalog), new Wallet());
            // Several buyer plans share an NPC: each owner registers belongings exactly once.
            var registeredBuyers = new HashSet<NpcId>();
            foreach (BuyerPlan plan in BuyerPlans(configuration))
                if (registeredBuyers.Add(plan.Buyer))
                    _state.Belongings.Register(ActorId.ForNpc(plan.Buyer), new Inventory(catalog),
                        new Wallet(100));

            foreach (NpcId npc in new[] { Mira, Witness, Contact, Guard }) _state.Knowledge.Register(npc);
        }

        internal static IReadOnlyList<BuyerPlan> BuyerPlans(AppleTestConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            return new[]
            {
                new BuyerPlan("morning-a", new NpcId("npc_tansy_alder"), Time(1, 8), 80, 50, 2, true),
                new BuyerPlan("morning-b", Guard, Time(1, 10), 80, 50, 3, true),
                new BuyerPlan("afternoon", Contact, Time(1, 15), 80, 50, 5, true),
                new BuyerPlan("day2-partial", Guard, Time(2, 9), 80, 50, 7, true),
                new BuyerPlan("day2-failed", Contact, Time(2, 10), 80, 50, 2, false)
            };
        }

        internal Shop Shop => _state.Shops[Stall];
        internal Inventory FarmerInventory => _state.Belongings[ActorId.ForNpc(Farmer)].Inventory;
        internal Inventory PlayerInventory => _state.Belongings[ActorId.Player].Inventory;
        internal Wallet FarmerWallet => _state.Belongings[ActorId.ForNpc(Farmer)].Wallet;
        internal int TotalApples() =>
            _state.Belongings.Entries.Sum(entry => entry.Inventory.Count(Apple))
            + _state.Shops.Shops.Sum(shop => shop.Stock.Count(Apple));
        internal int TotalCopper() =>
            // The shop owner wallet is Mira's registered wallet: counted once via belongings.
            _state.Belongings.Entries.Sum(entry => entry.Wallet.Balance);

        private static GameTime Time(int day, int hour) => new GameTime((day - 1L) * 1440 + hour * 60);
    }

    /// <summary>Immutable scenario input: who buys how many apples, and when.</summary>
    internal sealed class BuyerPlan
    {
        internal BuyerPlan(string id, NpcId buyer, GameTime at, int hunger, int minimumHunger, int quantity,
            bool allowPartial)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A buyer plan needs a stable ID.", nameof(id));
            if (!buyer.IsValid) throw new ArgumentException("A buyer plan needs a valid buyer.", nameof(buyer));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Id = id; Buyer = buyer; At = at; Hunger = hunger; MinimumHunger = minimumHunger;
            Quantity = quantity; AllowPartial = allowPartial;
        }
        internal string Id { get; }
        internal NpcId Buyer { get; }
        internal GameTime At { get; }
        internal int Hunger { get; }
        internal int MinimumHunger { get; }
        internal int Quantity { get; }
        internal bool AllowPartial { get; }
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
        private readonly AppleScenarioState _progress;
        private readonly GameTime _queueAt;
        private readonly int _stolenQuantity;
        internal TheftScheduleSystem(AppleScenarioState progress, GameTime queueAt, int stolenQuantity)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _queueAt = queueAt;
            if (stolenQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(stolenQuantity));
            _stolenQuantity = stolenQuantity;
        }
        public string Id => "scenario.theft-schedule";
        public SimulationPhase Phase => SimulationPhase.Commands;
        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (_progress.TheftQueued || state.Clock < _queueAt) return;
            Shop shop = state.Shops[ScenarioVillage.Stall];
            Inventory playerInventory = state.Belongings[ActorId.Player].Inventory;
            state.EnqueueCommand(new TheftCommand(ScenarioVillage.Stall, ActorId.Player,
                ActorId.ForNpc(ScenarioVillage.Mira), shop.Stock, playerInventory,
                ScenarioVillage.Apple, _stolenQuantity, EventVisibility.Normal));
            _progress.TheftQueued = true;
        }
    }

    internal sealed class BuyerActionSystem : IWorldSystem
    {
        private readonly IReadOnlyList<BuyerPlan> _plans;
        private readonly AppleScenarioState _progress;
        internal BuyerActionSystem(IReadOnlyList<BuyerPlan> plans, AppleScenarioState progress)
        {
            if (plans == null) throw new ArgumentNullException(nameof(plans));
            _plans = plans;
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }
        public string Id => "scenario.buyer-actions";
        public SimulationPhase Phase => SimulationPhase.Actions;
        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            Shop shop = state.Shops[ScenarioVillage.Stall];
            foreach (BuyerPlan plan in _plans)
                if (!_progress.HasBuyer(plan.Id) && state.Clock >= plan.At && plan.Hunger >= plan.MinimumHunger)
                {
                    NpcBelongingsEntry belongings = state.Belongings[ActorId.ForNpc(plan.Buyer)];
                    PurchaseResult result = shop.Purchase(state, new PurchaseRequest(ActorId.ForNpc(plan.Buyer),
                        belongings.Inventory, belongings.Wallet, ScenarioVillage.Apple, plan.Quantity,
                        plan.AllowPartial));
                    _progress.ExpectedShopStock -= result.ActualQuantity;
                    _progress.CompleteBuyer(plan.Id);
                }
            if (!_progress.StockCounted && state.Clock >= AppleTime(1, 19))
            {
                StockCountInference.Record(state, new StockCountSnapshot(ScenarioVillage.Mira, ScenarioVillage.Stall,
                    ScenarioVillage.Apple, _progress.ExpectedShopStock,
                    state.Shops[ScenarioVillage.Stall].Stock.Count(ScenarioVillage.Apple)), 90);
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
