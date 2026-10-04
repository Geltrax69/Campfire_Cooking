using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
namespace LivingWorld.Game.Bridge
{
    /// <summary>Owns the content-backed Apple Test world and translates narrow player inputs.</summary>
    public sealed class AppleSlice
    {
        internal static readonly ItemTypeId Apple = new ItemTypeId("item_apple");
        internal static readonly LocationId Stall = new LocationId("loc_apple_stall");
        internal static readonly NpcId Mira = new NpcId("npc_mira_holt");
        private readonly World _world;
        private readonly Shop _shop;
        private readonly Inventory _player;
        private readonly Wallet _wallet;
        private readonly SliceActivity _activity;
        private readonly NpcRow[] _npcs;
        public bool IsFaulted => _world.IsFaulted;
        public AppleSlice(string contentDirectory, ulong seed = 42)
        {
            var economy = Read<EconomyFile>(Path.Combine(contentDirectory, "economy/economy.json"));
            var start = Read<PlayerFile>(Path.Combine(contentDirectory, "player/start.json"));
            _npcs = Read<NpcFile>(Path.Combine(contentDirectory, "npcs/npcs.json")).Npcs;
            ItemRow apple = Read<ItemFile>(Path.Combine(contentDirectory, "items/items.json")).Items.Single(x => x.Id == Apple.Value);
            var catalog = new ItemCatalog(new[] { new ItemDefinition(Apple, apple.Name, apple.Category, apple.BaseValue, apple.Phase) });
            var state = new WorldState(seed, new GameTime(6 * 60 + 30));
            _world = new World(state);
            var locations = Read<LocationFile>(Path.Combine(contentDirectory, "world/locations.json"));
            var map = new LocationMap(locations.Locations.Select(x => new LocationDefinition(new LocationId(x.Id), x.Name, x.Type, x.Position.X, x.Position.Y, x.Owner == "village" ? (NpcId?)null : new NpcId(x.Owner))),
                locations.Links.Select(x => new TravelLink(new LocationId(x.From), new LocationId(x.To), x.Minutes)));
            using (var stream = File.OpenRead(Path.Combine(contentDirectory, "npcs/npcs.json")))
                foreach (NpcDefinition definition in NpcContentLoader.Load(stream, map))
                    // Same bounded initial need inputs as SimulationTests/Tools/VillageAssembly.
                    state.Npcs.Register(new NpcState(definition, 30, 80, 50));
            using (var stream = File.OpenRead(Path.Combine(contentDirectory, "npcs/npcs.json")))
                FamilySetup.AssignInitialFamilies(state, FamilyContentLoader.Load(stream));
            var stock = new Inventory(catalog); stock.Add(Apple, economy.AppleTest.StartStock);
            _shop = new Shop(Stall, Mira, stock, new Wallet(_npcs.Single(x => x.Id == Mira.Value).Money), new[] { new KeyValuePair<ItemTypeId, int>(Apple, economy.AppleTest.BasePrice) });
            state.Shops.Register(_shop);
            _player = new Inventory(catalog); _wallet = new Wallet(start.Inventory.Money);
            state.Belongings.Register(ActorId.Player, _player, _wallet);
            foreach (NpcRow npc in _npcs)
            {
                var id = new NpcId(npc.Id);
                state.Knowledge.Register(id);
                state.Belongings.Register(ActorId.ForNpc(id), new Inventory(catalog), id == Mira ? _shop.OwnerWallet : new Wallet(npc.Money));
            }
            _activity = new SliceActivity(_shop, economy.AppleTest.StartStock);
            _world.RegisterSystem(new CommandSystem());
            _world.RegisterSystem(_activity);
            _world.RegisterSystem(new PerceptionSystem(new SliceWitness(), new SliceWitness()));
            _world.RegisterSystem(new PriceAdjustmentSystem(new[] { new PriceAdjustmentConfiguration("apple-price", _shop, Apple, new GameTime(18 * 60), 1440, 10, 100, 1, 3, 4, EventVisibility.Normal) }, state));
            NpcId farmer = new NpcId("npc_corvin_alder");
            var farm = state.Belongings[ActorId.ForNpc(farmer)];
            var farmLocation = new LocationId("loc_farm");
            _world.RegisterSystem(new ProductionSystem(new[] { new ProductionConfiguration("farm-apples", farmLocation, farmer, farm.Inventory, Apple, 45, new GameTime(2 * 1440 + 8 * 60), EventVisibility.Normal) }, state));
            _world.RegisterSystem(new RestockSystem(new[] { new RestockConfiguration("farm-to-stall", _shop, farmLocation, farmer, farm.Inventory, farm.Wallet, Apple, 10, 45, 1, RestockFulfillmentPolicy.FullOnly, EventVisibility.Normal) }, state));
        }
        public void Tick() => _world.Tick();
        public string TalkToNpc(string npcId, ConversationTopic topic)
        {
            if (string.IsNullOrWhiteSpace(npcId) || !_npcs.Any(x => x.Id == npcId)) return "That villager is not available.";
            DialogueIntent intent;
            switch (topic)
            {
                case ConversationTopic.ShopStock: return Capture().Npcs.Single(x => x.Id == npcId).Dialogue;
                case ConversationTopic.Greeting: intent = DialogueIntent.Greeting; break;
                case ConversationTopic.Smalltalk: intent = DialogueIntent.Smalltalk; break;
                case ConversationTopic.AboutPlayer: intent = DialogueIntent.AskAboutPlayer; break;
                case ConversationTopic.News: intent = DialogueIntent.ShareNews; break;
                case ConversationTopic.Family: intent = DialogueIntent.AskAboutFamily; break;
                case ConversationTopic.Farewell: intent = DialogueIntent.Farewell; break;
                default: return "That conversation topic is not available.";
            }
            var sheet = FactSheetBuilder.Build(_world.State, new NpcId(npcId));
            return new DialogueSession(sheet, new TemplatePhrasingEngine(42)).Say(intent);
        }
        public void QueueBuyApples(int quantity)
        { _world.State.EnqueueCommand(new SlicePurchase(_shop, new PurchaseRequest(ActorId.Player, _player, _wallet, Apple, quantity, false), _activity)); }
        public void QueueStealApples(int quantity)
        { _world.State.EnqueueCommand(new TheftCommand(Stall, ActorId.Player, ActorId.ForNpc(Mira), _shop.Stock, _player, Apple, quantity, EventVisibility.Normal)); }
        public DisplaySnapshot Capture()
        {
            var displays = new List<NpcDisplay>();
            foreach (NpcRow npc in _npcs)
            {
                var beliefs = _world.State.Knowledge.Get(new NpcId(npc.Id)).Query();
                string dialogue = "I haven't heard any news about the apple stall.";
                foreach (Belief belief in beliefs)
                {
                    if (belief.Claim.Kind == BeliefClaimKind.StockMissing) dialogue = "The count suggests " + belief.Claim.Quantity + " apples are missing. I don't know who took them.";
                    if (belief.Claim.Kind == BeliefClaimKind.TheftObserved) dialogue = "I saw someone taking apples from the stall.";
                }
                displays.Add(new NpcDisplay(npc.Id, npc.Name, npc.Id == Mira.Value ? Stall.Value : npc.Workplace, "Workplace preview", dialogue, npc.Model, _world.State.Npcs[new NpcId(npc.Id)].Definition.Occupation));
            }
            return new DisplaySnapshot(_world.State.Clock.TotalMinutes, _shop.Stock.Count(Apple), _shop.UnitPrice(Apple), _player.Count(Apple), _wallet.Balance, displays);
        }
        private static T Read<T>(string path)
        { using (var stream = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
        [DataContract] private sealed class EconomyFile { [DataMember(Name="appleTest")] public AppleRow AppleTest; }
        [DataContract] private sealed class AppleRow { [DataMember(Name="startStock")] public int StartStock; [DataMember(Name="basePrice")] public int BasePrice; }
        [DataContract] private sealed class PlayerFile { [DataMember(Name="inventory")] public PlayerRow Inventory; }
        [DataContract] private sealed class PlayerRow { [DataMember(Name="money")] public int Money; }
        [DataContract] private sealed class ItemFile { [DataMember(Name="items")] public ItemRow[] Items; }
        [DataContract] private sealed class ItemRow { [DataMember(Name="id")] public string Id; [DataMember(Name="name")] public string Name; [DataMember(Name="category")] public string Category; [DataMember(Name="baseValue")] public int BaseValue; [DataMember(Name="phase")] public int Phase; }
        [DataContract] private sealed class LocationFile { [DataMember(Name="locations")] public LocationRow[] Locations; [DataMember(Name="travelMinutes")] public LinkRow[] Links; }
        [DataContract] private sealed class LocationRow { [DataMember(Name="id")] public string Id; [DataMember(Name="name")] public string Name; [DataMember(Name="type")] public string Type; [DataMember(Name="owner")] public string Owner; [DataMember(Name="position")] public PositionRow Position; }
        [DataContract] private sealed class PositionRow { [DataMember(Name="x")] public int X; [DataMember(Name="y")] public int Y; }
        [DataContract] private sealed class LinkRow { [DataMember(Name="from")] public string From; [DataMember(Name="to")] public string To; [DataMember(Name="minutes")] public int Minutes; }
        [DataContract] private sealed class NpcFile { [DataMember(Name="npcs")] public NpcRow[] Npcs; }
        [DataContract] private sealed class NpcRow { [DataMember(Name="id")] public string Id; [DataMember(Name="name")] public string Name; [DataMember(Name="money")] public int Money; [DataMember(Name="workplace")] public string Workplace; [DataMember(Name="model")] public string Model; }
    }
    internal sealed class SlicePurchase : IWorldCommand
    {
        private readonly Shop _shop; private readonly PurchaseRequest _request; private readonly SliceActivity _activity;
        internal SlicePurchase(Shop shop, PurchaseRequest request, SliceActivity activity) { _shop = shop; _request = request; _activity = activity; }
        public void Execute(WorldState state) { _activity.RecordSale(_shop.Purchase(state, _request).ActualQuantity); }
    }
    internal sealed class SliceActivity : IWorldSystem
    {
        private readonly Shop _shop; private int _expected; private int _next; private bool _counted;
        // Fixed buyer inputs match SimulationTests/Scenarios/AppleTestHarness, not general NPC AI.
        private readonly long[] _times = { 480, 600, 900, 1980, 2040 };
        private readonly int[] _quantities = { 2, 3, 5, 7, 2 };
        private readonly string[] _buyers = { "npc_tansy_alder", "npc_bram_stone", "npc_bessa_marlowe", "npc_bram_stone", "npc_bessa_marlowe" };
        internal SliceActivity(Shop shop, int expected) { _shop = shop; _expected = expected; }
        internal void RecordSale(int quantity) { _expected -= quantity; }
        public string Id => "slice.buyers";
        public SimulationPhase Phase => SimulationPhase.Actions;
        public void Tick(WorldState state)
        {
            while (_next < _times.Length && state.Clock.TotalMinutes >= _times[_next])
            {
                ActorId buyer = ActorId.ForNpc(new NpcId(_buyers[_next])); var belongings = state.Belongings[buyer];
                RecordSale(_shop.Purchase(state, new PurchaseRequest(buyer, belongings.Inventory, belongings.Wallet, AppleSlice.Apple, _quantities[_next], _next != 4)).ActualQuantity); _next++;
            }
            if (!_counted && state.Clock.TotalMinutes >= 19 * 60)
            { StockCountInference.Record(state, new StockCountSnapshot(AppleSlice.Mira, AppleSlice.Stall, AppleSlice.Apple, _expected, _shop.Stock.Count(AppleSlice.Apple)), 90); _counted = true; }
        }
    }
    internal sealed class SliceWitness : IPerceptionContext, IPerceptionTuning
    {
        private static readonly NpcId Witness = new NpcId("npc_lida_alder");
        public IEnumerable<NpcId> Candidates(WorldEvent e) => e.Type == WorldEventType.Theft ? new[] { Witness } : Array.Empty<NpcId>();
        public bool IsPresentAt(NpcId npc, LocationId location) => npc == Witness && location == AppleSlice.Stall;
        public bool IsAwake(NpcId npc) => true;
        public int NoticeChancePercent(NpcId npc, WorldEvent e) => 50;
        public int Confidence(NpcId npc, WorldEvent e) => 70;
    }
}
