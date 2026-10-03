using System;
using System.Collections.Generic;
using System.IO;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Knowledge;
using LivingWorld.Simulation.Persistence;

namespace LivingWorld.Simulation.Tests.Tools
{
    /// <summary>
    /// Builds the full Phase 2 village: every NPC from approved Content with starting
    /// needs, knowledge stores and relationships, every Phase 2 setup in dependency
    /// order, friend pricing on the shops, and every system registered in phase order
    /// (the world runs systems in registration order, so this is the phase order).
    ///
    /// Assembly order and why:
    /// 1. NPCs, knowledge stores, relationships — people before businesses.
    /// 2. GeneralStore, Smithy, BakeryChain — the shops and their stock.
    /// 3. MoneySources — registers personal wallets (reuses shop tills where they exist).
    /// 4. MoneySinks — needs the sources handle; sets the prosperity baseline.
    /// 5. Debts — documented: repayments need the wallets MoneySources registered.
    /// 6. Starting pantries — 2 days of rye bread per NPC (a world-open grant, not a
    ///    purchase). NPCs without Content money (children) get a 0-copper wallet.
    /// 7. Friend pricing on the three shops, from each owner's live relationships.
    /// 8. Systems, in phase order. (The World sorts registered systems by phase,
    ///    then by system ID within each phase, so the IDs are chosen to sort into
    ///    the intended order: recall before dynamics, meetings before dynamics.)
    ///
    /// Deliberately not wired: DecisionSystem (needs a context provider — intentions
    /// and sleep are not driven yet), PerceptionSystem and TravelSystem (need tuning
    /// implementations; nothing in the acceptance criteria requires witnessing or
    /// physical movement — meetings use schedule locations).
    /// </summary>
    public static class VillageAssembly
    {
        /// <summary>World-open hunger/energy/social: villagers start the day peckish but rested.</summary>
        public const int StartingHunger = 30;
        public const int StartingEnergy = 80;
        public const int StartingSocial = 50;

        /// <summary>Two days of rye bread per NPC at world-open (a grant, not a purchase).</summary>
        public const int StartingBreadLoaves = 8;

        private static readonly ItemTypeId RyeBread = new ItemTypeId("item_bread_rye");

        /// <summary>The assembled village: state, world, catalog and the shop handles.</summary>
        public sealed class Village
        {
            public Village(WorldState state, World world, ItemCatalog catalog,
                Shop generalStore, Shop smithy, Shop bakery)
            {
                State = state;
                World = world;
                Catalog = catalog;
                GeneralStore = generalStore;
                Smithy = smithy;
                Bakery = bakery;
            }

            public WorldState State { get; }
            public World World { get; }
            public ItemCatalog Catalog { get; }
            public Shop GeneralStore { get; }
            public Shop Smithy { get; }
            public Shop Bakery { get; }
        }

        /// <summary>
        /// Builds the village. Day 1 is Thirdday in early autumn (the approved start);
        /// the world opens at 04:00 so the bakery's 05:00 bake fires on day one.
        /// </summary>
        public static Village Build(string contentRoot, ulong seed, long startDay = 1)
        {
            if (string.IsNullOrWhiteSpace(contentRoot))
                throw new ArgumentException("A content root is required.", nameof(contentRoot));
            if (startDay < 1) throw new ArgumentOutOfRangeException(nameof(startDay));

            ContentBundle bundle = ContentBundle.Load(contentRoot);
            ItemCatalog catalog = bundle.Catalog;
            var state = new WorldState(seed, new GameTime((startDay - 1) * 1440 + 4 * 60));
            var world = new World(state);

            List<NpcId> npcIds = RegisterNpcs(state, bundle);
            RegisterKnowledge(state, npcIds, contentRoot);
            Shop store = GeneralStoreSetup.Stock(state, catalog);
            SmithySetup.SmithyHandle smithyHandle = SmithySetup.Stock(state, catalog);
            Shop smithy = smithyHandle.SmithyShop;
            BakeryChainSetup.BakeryChain bakeryChain = BakeryChainSetup.Stock(state, catalog);
            MoneySourcesSetup.MoneySourcesHandle money =
                MoneySourcesSetup.Stock(state, catalog, store, smithy, bakeryChain.BakeryShop);
            MoneySinksSetup.MoneySinksHandle sinks = MoneySinksSetup.Stock(state, catalog, money);
            DebtConfiguration debts = DebtSetup.OpenLedger(state, startDay);
            StockPantries(state, catalog, npcIds);
            WireFriendPricing(state, store, smithy, bakeryChain.BakeryShop);
            RegisterSystems(world, state, catalog, contentRoot, bakeryChain, smithyHandle,
                money, sinks, debts);

            return new Village(state, world, catalog, store, smithy, bakeryChain.BakeryShop);
        }

        private static List<NpcId> RegisterNpcs(WorldState state, ContentBundle bundle)
        {
            var ids = new List<NpcId>(bundle.NpcDefinitions.Keys);
            ids.Sort((left, right) => string.Compare(left.Value, right.Value, StringComparison.Ordinal));
            foreach (NpcId id in ids)
                state.Npcs.Register(new NpcState(bundle.NpcDefinitions[id],
                    StartingHunger, StartingEnergy, StartingSocial));
            return ids;
        }

        private static void RegisterKnowledge(WorldState state, List<NpcId> npcIds, string contentRoot)
        {
            foreach (NpcId id in npcIds)
                state.Knowledge.Register(id);
            string npcsPath = Path.Combine(contentRoot, "Content", "npcs", "npcs.json");
            using (Stream stream = File.OpenRead(npcsPath))
            {
                foreach (Relationship relationship in RelationshipContentLoader.Load(stream, npcIds))
                    state.Knowledge.Relationships.Set(relationship);
            }
        }

        private static void StockPantries(WorldState state, ItemCatalog catalog, List<NpcId> npcIds)
        {
            foreach (NpcId id in npcIds)
            {
                var owner = ActorId.ForNpc(id);
                if (!state.Belongings.TryGet(owner, out NpcBelongingsEntry belongings))
                {
                    // Children and other NPCs without Content money: a 0-copper wallet.
                    // The fiction covers them through family; mechanically they start
                    // with their pantry like everyone else.
                    belongings = new NpcBelongingsEntry(owner, new Inventory(catalog), new Wallet(0));
                    state.Belongings.Register(owner, belongings.Inventory, belongings.Wallet);
                }
                belongings.Inventory.Add(RyeBread, StartingBreadLoaves);
            }
        }

        private static void WireFriendPricing(WorldState state, Shop store, Shop smithy, Shop bakery)
        {
            store.DiscountPolicy = FriendPricing.ForShopkeeper(state, GeneralStoreSetup.Owner);
            smithy.DiscountPolicy = FriendPricing.ForShopkeeper(state, SmithySetup.Smith);
            bakery.DiscountPolicy = FriendPricing.ForShopkeeper(state, BakeryChainSetup.Baker);
        }

        private static void RegisterSystems(World world, WorldState state, ItemCatalog catalog,
            string contentRoot, BakeryChainSetup.BakeryChain bakeryChain,
            SmithySetup.SmithyHandle smithyHandle,
            MoneySourcesSetup.MoneySourcesHandle money, MoneySinksSetup.MoneySinksHandle sinks,
            DebtConfiguration debts)
        {
            // Commands.
            world.RegisterSystem(new CommandSystem());
            // Needs.
            world.RegisterSystem(new NeedsSystem());
            // Actions: eat before shop (a full pantry shops less); farm meals after
            // shopping so the farm household's home-grown lunch tops up the day.
            world.RegisterSystem(new EatSystem(catalog));
            world.RegisterSystem(new ShoppingSystem(catalog));
            world.RegisterSystem(new FarmMealSystem());
            // Social: the World sorts by ID within the phase, so these run as
            // meetings, recall, dynamics. Recall only recalls earlier ticks'
            // memories, so the order is safe; dynamics sees tonight's tavern talk.
            world.RegisterSystem(new MemoryRecallSystem());
            world.RegisterSystem(new MeetingSystem());
            world.RegisterSystem(new RelationshipDynamicsSystem());
            // Economy: production first, then money in, then money out.
            world.RegisterSystem(new MillingSystem(new[] { bakeryChain.Milling() }));
            world.RegisterSystem(new BakingSystem(new[] { bakeryChain.Baking() }));
            world.RegisterSystem(new SmithySystem(new[] { smithyHandle.Forging() }));
            world.RegisterSystem(new RestockSystem(new[] { bakeryChain.FlourRestock() }, state));
            world.RegisterSystem(new MerchantSystem(new[] { sinks.MerchantVisitsAdaptive() }));
            world.RegisterSystem(new TravelerSpendSystem(new[] { money.TravelerSpend() }));
            world.RegisterSystem(new WolfBountySystem(new[] { money.WolfBounties() }));
            world.RegisterSystem(new VillageFundSystem(new[] { money.VillageFund() }));
            world.RegisterSystem(new HarvestSystem(new[] { money.Harvest() }));
            world.RegisterSystem(new TaxSystem(new[] { sinks.Taxes() }));
            world.RegisterSystem(new CommunityFundSystem(new[] { sinks.CommunityFund() }));
            world.RegisterSystem(new SpoilageSystem(new[] { sinks.Spoilage() }));
            world.RegisterSystem(new DebtSystem(new[] { debts }));
            // Memory.
            using (Stream stream = File.OpenRead(Path.Combine(contentRoot, "Content", "social", "social.json")))
                world.RegisterSystem(new MemorySystem(MemoryRules.Load(stream)));
        }
    }
}
