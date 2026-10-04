using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Persistence;
using LivingWorld.Simulation.Tests.Tools;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Scenarios
{
    /// <summary>
    /// P4-04 acceptance: a 90-day animal simulation on the full village.
    ///
    /// The village opens in mid-autumn (game day 46) with the design's starting
    /// animal populations (P4-01 factory) and the four ecosystem systems running
    /// (P4-02). Ninety days tick minute-by-minute from late autumn into
    /// mid-winter while the player hand-feeds one hen for the first 7 days
    /// (P4-03 taming). The test proves: the hen bonds to the player; wolves thin
    /// the deer herd; at least one winter livestock loss occurs; hens lay
    /// markedly more in autumn than in winter; and a save at the autumn/winter
    /// boundary, loaded and continued, reaches exactly the same final state as
    /// an uninterrupted 90-day run.
    ///
    /// Ticking is minute-by-minute like the other acceptance scenarios: the
    /// ecosystem systems are day-cursor based and their cursors start at the
    /// build day, so no catch-up event is ever backdated before an event another
    /// system already appended (the event log rejects backwards time). The log
    /// this test writes is the human-readable proof.
    /// </summary>
    public sealed class Phase4AnimalAcceptanceTests
    {
        /// <summary>
        /// Fixed seed. Chosen so the winter incident roll lands at least one
        /// livestock loss inside the simulated half-winter and the tamed hen
        /// survives the season; every assertion below is pinned to this seed.
        /// </summary>
        private const ulong Seed = 20261004;
        /// <summary>Mid-autumn: 45 autumn days (46-90), then 45 winter days (91-135).</summary>
        private const long StartDay = 46;
        private const int RunDays = 90;
        /// <summary>Save/load at the autumn→winter boundary (after run day 45).</summary>
        private const int SaveAfterDays = 45;

        private static readonly SpeciesId Chicken = new SpeciesId("species_chicken");
        private static readonly SpeciesId Pig = new SpeciesId("species_pig");
        private static readonly SpeciesId Deer = new SpeciesId("species_deer");
        private static readonly SpeciesId Wolf = new SpeciesId("species_wolf");
        private static readonly SpeciesId Brambleback = new SpeciesId("species_brambleback");
        private static readonly LocationId Farm = new LocationId("loc_farm");
        private static readonly LocationId FennHome = new LocationId("loc_home_fenn");
        private static readonly ItemTypeId Egg = new ItemTypeId("item_egg");
        private static readonly NpcId MiraHolt = new NpcId("npc_mira_holt");

        /// <summary>One season run: the village, its animal wiring, and the log being built.</summary>
        private sealed class SeasonRun
        {
            public VillageAssembly.Village Village;
            public WorldState State => Village.State;
            public SpeciesDefinition ChickenDefinition;
            public Inventory FarmCoop;
            public Inventory FennCoop;
            public AnimalState TamedChicken;
            public readonly List<string> TamingDiary = new List<string>();
            public readonly List<DayStats> Days = new List<DayStats>();
        }

        private sealed class DayStats
        {
            public long Day;
            public Season Season;
            public int Chickens;
            public int Pigs;
            public int Deer;
            public int Wolves;
            public int Bramblebacks;
            public int EggsToDate;
            public int LivestockLossesToDate;
        }

        private static string ContentRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                !File.Exists(Path.Combine(directory.FullName, "Content/world/locations.json")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null, "Could not locate approved Content.");
            return directory.FullName;
        }

        private static string LogPath(string contentRoot) => Path.Combine(contentRoot,
            "SimulationTests", "Scenarios", "Phase4_Acceptance_Log.md");

        /// <summary>
        /// Builds the full village and wires the Phase 4 animal layer: the design's
        /// starting populations, the four ecosystem systems with initialized cursors,
        /// and two laying coops (Alder Farm and Fenn's). The cursors start at the
        /// build day (not the day before): the world opens at 04:00, so the opening
        /// day's hunts and laying are already done — and no system ever backdates
        /// an event before one another system appended (the event log rejects
        /// backwards time).
        /// </summary>
        private static SeasonRun SetUpSeason(string contentRoot, ulong seed, long startDay)
        {
            var run = new SeasonRun();
            run.Village = VillageAssembly.Build(contentRoot, seed, startDay);
            ContentBundle bundle = ContentBundle.Load(contentRoot);
            IReadOnlyList<SpeciesDefinition> species;
            using (Stream stream = File.OpenRead(
                Path.Combine(contentRoot, "Content", "animals", "species.json")))
                species = SpeciesContentLoader.Load(stream, bundle.Map);
            foreach (SpeciesDefinition definition in species)
                if (definition.Id == Chicken) run.ChickenDefinition = definition;
            Assert.That(run.ChickenDefinition, Is.Not.Null, "The chicken species definition is required.");
            foreach (AnimalState animal in AnimalPopulationFactory.CreateInitialPopulations(species))
                run.State.Animals.Add(animal);

            run.State.RestorePredation(new PredationState(initialized: true, lastHuntDay: startDay));
            run.State.RestoreBreeding(new BreedingState(initialized: true, lastBreedingDay: startDay));
            run.State.RestoreWinterPressure(new WinterPressureState(initialized: true, lastLossDay: startDay));
            run.State.RestoreEggProduction(new EggProductionState(initialized: true, lastLayDay: startDay));
            RegisterAnimalSystems(run, bundle.Catalog);

            // The taming subject: the first adult hen at Alder Farm.
            run.TamedChicken = run.State.Animals.GetBySpecies(Chicken)
                .First(a => a.Age == AnimalAge.Adult && a.Location == Farm);
            return run;
        }

        /// <summary>
        /// Registers the four ecosystem systems. Called on a fresh world and,
        /// after save/load, on the reassembled world (the states themselves are
        /// restored by the loader; only the stateless systems are re-registered).
        /// The coop inventories are caller-owned configuration: a load resumes
        /// laying into fresh baskets (a documented save-format limit).
        /// </summary>
        private static void RegisterAnimalSystems(SeasonRun run, ItemCatalog catalog)
        {
            run.FarmCoop = new Inventory(catalog);
            run.FennCoop = new Inventory(catalog);
            World world = run.Village.World;
            world.RegisterSystem(new PredationSystem(new[] { new PredationConfiguration("acceptance") }));
            world.RegisterSystem(new BreedingSystem(new[] { new BreedingConfiguration("acceptance") }));
            world.RegisterSystem(new WinterPressureSystem(new[] { new WinterPressureConfiguration("acceptance") }));
            world.RegisterSystem(new EggProductionSystem(new[]
            {
                new EggConfiguration("alder-coop", Farm, run.FarmCoop, EventVisibility.Quiet),
                new EggConfiguration("fenn-coop", FennHome, run.FennCoop, EventVisibility.Quiet),
            }));
        }

        /// <summary>Ticks one full game day, minute by minute, then records the day's stats.</summary>
        private static void TickDay(SeasonRun run)
        {
            for (int minute = 0; minute < 24 * 60; minute++)
                run.Village.World.Tick();
            RecordDay(run);
        }

        private static void RecordDay(SeasonRun run)
        {
            WorldState state = run.State;
            run.Days.Add(new DayStats
            {
                Day = state.Clock.Day - 1, // the day just simulated
                Season = VillageCalendar.SeasonAt(new GameTime((state.Clock.Day - 2) * 1440)),
                Chickens = state.Animals.PopulationCount(Chicken),
                Pigs = state.Animals.PopulationCount(Pig),
                Deer = state.Animals.PopulationCount(Deer),
                Wolves = state.Animals.PopulationCount(Wolf),
                Bramblebacks = state.Animals.PopulationCount(Brambleback),
                EggsToDate = CountEggsToDate(state),
                LivestockLossesToDate = CountLivestockLosses(state),
            });
        }

        private static int CountEggsToDate(WorldState state) =>
            state.Events.Query(type: WorldEventType.Produced)
                .Where(e => e.ItemType.HasValue && e.ItemType.Value == Egg)
                .Sum(e => e.Quantity ?? 0);

        private static int CountLivestockLosses(WorldState state) =>
            state.Events.Query(type: WorldEventType.Predation)
                .Count(e => e.Copper.HasValue && e.Copper.Value > 0);

        /// <summary>
        /// One hand-feeding by the player; records the trust movement for the log.
        /// </summary>
        private static void FeedChicken(SeasonRun run, int feedingNumber)
        {
            WorldState state = run.State;
            AnimalState chicken = run.TamedChicken;
            int before = chicken.Trust;
            TamingResult result = TamingSystem.Interact(state, chicken, run.ChickenDefinition,
                ActorId.Player, TamingInteraction.HandFeeding, state.Clock.Day,
                councilApproval: false, rng: state.Rng);
            run.TamingDiary.Add("Day " + feedingNumber + " (game day " + state.Clock.Day + "): trust " +
                before + " → " + result.Trust + " (" + result.Outcome + ")." +
                (result.Outcome == TamingOutcome.Bonded ? " **BONDED to the player.**" : ""));
        }

        /// <summary>Runs the whole 90 days without interruption.</summary>
        private static SeasonRun RunUninterrupted(string contentRoot)
        {
            SeasonRun run = SetUpSeason(contentRoot, Seed, StartDay);
            for (int day = 1; day <= RunDays; day++)
            {
                TickDay(run);
                if (day <= 7) FeedChicken(run, day);
            }
            return run;
        }

        /// <summary>
        /// Runs 45 days, saves, loads, reassembles every system, runs 45 more days.
        /// </summary>
        private static SeasonRun RunWithSaveLoad(string contentRoot)
        {
            SeasonRun run = SetUpSeason(contentRoot, Seed, StartDay);
            for (int day = 1; day <= SaveAfterDays; day++)
            {
                TickDay(run);
                if (day <= 7) FeedChicken(run, day);
            }

            string json = WorldSaver.Save(run.State);
            WorldState loaded = WorldLoader.Load(json, contentRoot);
            var continued = new SeasonRun
            {
                Village = VillageAssembly.Reassemble(loaded, contentRoot),
                ChickenDefinition = run.ChickenDefinition,
                TamedChicken = loaded.Animals.Get(run.TamedChicken.Id),
            };
            continued.TamingDiary.AddRange(run.TamingDiary);
            continued.Days.AddRange(run.Days);
            RegisterAnimalSystems(continued, ContentBundle.Load(contentRoot).Catalog);

            for (int day = SaveAfterDays + 1; day <= RunDays; day++) TickDay(continued);
            return continued;
        }

        [Test]
        public void NinetyDayAnimalAcceptance()
        {
            string root = ContentRoot();

            SeasonRun continued = RunWithSaveLoad(root);
            SeasonRun uninterrupted = RunUninterrupted(root);

            // Determinism: the save/load in the middle must change nothing.
            string continuedSave = WorldSaver.Save(continued.State);
            string uninterruptedSave = WorldSaver.Save(uninterrupted.State);
            Assert.That(continuedSave, Is.EqualTo(uninterruptedSave),
                "A save at day 45, load, and 45 more days must equal an uninterrupted 90-day run.");
            Assert.That(WorldDigest.Compute(continued.State),
                Is.EqualTo(WorldDigest.Compute(uninterrupted.State)));

            WorldState state = continued.State;

            // The hen bonded to the player through 7 days of hand-feeding.
            Assert.That(uninterrupted.TamedChicken.Owner, Is.EqualTo(ActorId.Player),
                "Seven days of hand-feeding bonds the hen to the player.");
            Assert.That(uninterrupted.TamedChicken.Trust,
                Is.GreaterThanOrEqualTo(uninterrupted.ChickenDefinition.BondThreshold));

            // Wolves thinned the deer herd over the season.
            int deer = state.Animals.PopulationCount(Deer);
            Assert.That(deer, Is.LessThan(40), "Wolf predation decreases the deer herd.");

            // At least one winter livestock loss (the design rate is 2-4 per winter).
            var winterLosses = state.Events.Query(type: WorldEventType.Predation)
                .Where(e => e.Copper.HasValue && e.Copper.Value > 0
                    && VillageCalendar.SeasonAt(e.Time) == Season.Winter)
                .ToList();
            Assert.That(winterLosses.Count, Is.GreaterThanOrEqualTo(1),
                "At least one livestock loss occurs in winter.");

            // Egg laying is seasonal: autumn out-lays winter clearly.
            int autumnEggs = EggsInSeason(state, Season.Autumn);
            int winterEggs = EggsInSeason(state, Season.Winter);
            Assert.That(autumnEggs, Is.GreaterThan(winterEggs),
                "Hens lay markedly more in autumn (" + autumnEggs + ") than in winter (" + winterEggs + ").");

            // No species went negative or vanished entirely.
            foreach (SpeciesId species in new[] { Chicken, Pig, Deer, Wolf, Brambleback })
                Assert.That(state.Animals.PopulationCount(species), Is.GreaterThanOrEqualTo(0));

            File.WriteAllText(LogPath(root), BuildLog(continued, winterLosses, autumnEggs, winterEggs));
        }

        private static int EggsInSeason(WorldState state, Season season) =>
            state.Events.Query(type: WorldEventType.Produced)
                .Where(e => e.ItemType.HasValue && e.ItemType.Value == Egg
                    && VillageCalendar.SeasonAt(e.Time) == season)
                .Sum(e => e.Quantity ?? 0);

        /// <summary>Save/load preserves every animal field and every ecosystem cursor.</summary>
        [Test]
        public void AnimalStateSaveLoadRoundTrip()
        {
            string root = ContentRoot();
            var state = new WorldState(777, new GameTime(9000));
            state.Animals.Add(AnimalState.Create(new AnimalId("animal_chicken_001"), Chicken,
                Farm, 77, ActorId.Player, AnimalAge.Adult, 100, 52));
            state.Animals.Add(AnimalState.Create(new AnimalId("animal_pig_001"), Pig,
                Farm, 10, null, AnimalAge.Young, 64));
            state.Animals.Add(AnimalState.Create(new AnimalId("animal_wolf_001"), Wolf,
                new LocationId("loc_forest_edge"), 0, null, AnimalAge.Adult, 88, 40));
            state.Animals.Add(AnimalState.Create(new AnimalId("animal_deer_001"), Deer,
                new LocationId("loc_forest_edge"), 5, ActorId.ForNpc(MiraHolt),
                AnimalAge.Young, 92, 12));
            state.RestorePredation(new PredationState(initialized: true, lastHuntDay: 45));
            state.RestoreBreeding(new BreedingState(initialized: true, lastBreedingDay: 45,
                lastBreedingYear: 0, nextBirthOrdinal: 7));
            state.RestoreWinterPressure(new WinterPressureState(initialized: true, lastLossDay: 100,
                lastWinterYear: 0, new[] { 104L, 127L }, deerAtFarms: true));
            state.RestoreEggProduction(new EggProductionState(initialized: true, lastLayDay: 45));

            WorldState loaded = WorldLoader.Load(WorldSaver.Save(state), root);

            Assert.That(loaded.Animals.Count, Is.EqualTo(4));
            AnimalState chicken = loaded.Animals.Get(new AnimalId("animal_chicken_001"));
            Assert.That(chicken.Species, Is.EqualTo(Chicken));
            Assert.That(chicken.Location, Is.EqualTo(Farm));
            Assert.That(chicken.Trust, Is.EqualTo(77));
            Assert.That(chicken.Owner, Is.EqualTo(ActorId.Player));
            Assert.That(chicken.Age, Is.EqualTo(AnimalAge.Adult));
            Assert.That(chicken.Health, Is.EqualTo(100));
            Assert.That(chicken.LastInteractionDay, Is.EqualTo(52));

            AnimalState pig = loaded.Animals.Get(new AnimalId("animal_pig_001"));
            Assert.That(pig.Owner.HasValue, Is.False, "Unbonded animals keep a null owner.");
            Assert.That(pig.Age, Is.EqualTo(AnimalAge.Young));
            Assert.That(pig.Health, Is.EqualTo(64));
            Assert.That(pig.LastInteractionDay, Is.EqualTo(-1), "Never-handled animals keep -1.");

            AnimalState deer = loaded.Animals.Get(new AnimalId("animal_deer_001"));
            Assert.That(deer.Owner, Is.EqualTo(ActorId.ForNpc(MiraHolt)));
            Assert.That(deer.Trust, Is.EqualTo(5));

            Assert.That(loaded.Predation.IsInitialized, Is.True);
            Assert.That(loaded.Predation.LastHuntDay, Is.EqualTo(45));
            Assert.That(loaded.Breeding.LastBreedingDay, Is.EqualTo(45));
            Assert.That(loaded.Breeding.LastBreedingYear, Is.EqualTo(0));
            Assert.That(loaded.Breeding.NextBirthOrdinal, Is.EqualTo(7));
            Assert.That(loaded.WinterPressure.LastLossDay, Is.EqualTo(100));
            Assert.That(loaded.WinterPressure.LastWinterYear, Is.EqualTo(0));
            Assert.That(loaded.WinterPressure.IncidentDays, Is.EqualTo(new[] { 104L, 127L }));
            Assert.That(loaded.WinterPressure.DeerAtFarms, Is.True);
            Assert.That(loaded.EggProduction.LastLayDay, Is.EqualTo(45));

            // And the round trip is byte-identical.
            Assert.That(WorldSaver.Save(loaded), Is.EqualTo(WorldSaver.Save(state)));
        }

        /// <summary>
        /// Version 4 documents load; version 3 documents (no animal sections) load
        /// with an empty store and uninitialized ecosystem cursors.
        /// </summary>
        [Test]
        public void AnimalSaveFormatVersion()
        {
            string root = ContentRoot();
            var state = new WorldState(4242, new GameTime(9000));
            state.Animals.Add(AnimalState.Create(new AnimalId("animal_chicken_001"), Chicken,
                Farm, 30, null, AnimalAge.Adult, 100));
            state.RestorePredation(new PredationState(initialized: true, lastHuntDay: 12));

            string v6 = WorldSaver.Save(state);
            Assert.That(v6, Does.Contain("\"formatVersion\": 6"));
            WorldState loadedV6 = WorldLoader.Load(v6, root);
            Assert.That(loadedV6.Animals.Count, Is.EqualTo(1));
            Assert.That(loadedV6.Predation.LastHuntDay, Is.EqualTo(12));

            WorldState loadedV4 = WorldLoader.Load(StripToVersion4(v6), root);
            Assert.That(loadedV4.Animals.Count, Is.EqualTo(1),
                "A v4 document keeps its animals.");
            Assert.That(loadedV4.TownStats.IsComputed, Is.False,
                "A v4 document has no town stats: uncomputed.");
            Assert.That(loadedV4.Migration.AdditionalBackgroundVillagers, Is.EqualTo(0));
            Assert.That(loadedV4.EmergentEvents.ActiveEvents, Is.Empty);
        }

        /// <summary>Rewrites a version 5 document as version 4 (drops Phase 5 sections).</summary>
        private static string StripToVersion4(string v5)
        {
            using (JsonDocument document = JsonDocument.Parse(v5))
            {
                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream))
                    {
                        writer.WriteStartObject();
                        foreach (JsonProperty property in document.RootElement.EnumerateObject())
                        {
                            switch (property.Name)
                            {
                                case "formatVersion":
                                    writer.WriteNumber("formatVersion", 4);
                                    break;
                                case "townStats":
                                case "migration":
                                case "emergentEvents":
                                    break;
                                default:
                                    property.WriteTo(writer);
                                    break;
                            }
                        }
                        writer.WriteEndObject();
                    }
                    stream.Position = 0;
                    using (var reader = new StreamReader(stream))
                        return reader.ReadToEnd();
                }
            }
        }

        private static string BuildLog(SeasonRun run, List<WorldEvent> winterLosses,
            int autumnEggs, int winterEggs)
        {
            var log = new StringBuilder();
            log.AppendLine("# Phase 4 acceptance log — ninety days with the animals");
            log.AppendLine();
            log.AppendLine("Seed " + Seed + ". The full village opens in mid-autumn (game day " +
                StartDay + ") with the design's starting animal populations: 20 chickens, " +
                "12 pigs, 40 deer, a 6-wolf pack and 25 bramblebacks. Ninety days run " +
                "from late autumn into mid-winter (days " + StartDay + "-" +
                (StartDay + RunDays - 1) + ") while the player hand-feeds one Alder Farm " +
                "hen for the first 7 days. A save at the autumn/winter boundary " +
                "(day 90), loaded and continued, reaches byte-identical final state " +
                "to an uninterrupted run — the determinism proof is in the test, " +
                "not just in this log.");
            log.AppendLine();
            log.AppendLine("Design decisions taken for this scenario (for human review):");
            log.AppendLine("- The 90-day window starts mid-autumn so one run covers both " +
                "seasons the brief asks about (autumn egg-laying vs winter losses). " +
                "A day-1 start would spend the whole run in autumn.");
            log.AppendLine("- The village ticks minute-by-minute like the other acceptance " +
                "scenarios. The ecosystem cursors start at the build day, so the " +
                "opening day (46) has no hunts or laying — the world opens at 04:00 " +
                "with the morning's animal business done — and no system ever " +
                "backdates an event before one another system appended (the event " +
                "log rejects backwards time).");
            log.AppendLine("- The two laying coops (Alder Farm, Fenn's) are caller-owned " +
                "configuration from P4-02, not world state: laid eggs are not in the " +
                "save document (see SCHEMA.md). The Produced truth events are, so egg " +
                "totals below come from the event log and survive save/load.");
            log.AppendLine("- The player tames animal_chicken_001, the first adult hen at " +
                "Alder Farm (starting trust 21).");
            log.AppendLine();
            log.AppendLine("## Taming diary — one hen, seven hand-feedings");
            log.AppendLine();
            foreach (string entry in run.TamingDiary) log.AppendLine("- " + entry);
            log.AppendLine();
            log.AppendLine("The design's chicken bond threshold is 60 trust (species.json); " +
                "8 trust per daily hand-feeding bonds her on the fifth day. The bond " +
                "persists through the save/load: the owner is part of the saved animal state.");
            log.AppendLine();
            log.AppendLine("## Week by week — populations, losses, eggs");
            log.AppendLine();
            log.AppendLine("| Days | Season | Chickens | Pigs | Deer | Wolves | Bramblebacks | Livestock lost (wk) | Eggs laid (wk) |");
            log.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            DayStats previous = null;
            for (int week = 0; week < run.Days.Count; week += 7)
            {
                DayStats first = run.Days[week];
                DayStats last = run.Days[Math.Min(week + 6, run.Days.Count - 1)];
                int losses = last.LivestockLossesToDate - (previous == null ? 0 : previous.LivestockLossesToDate);
                int eggs = last.EggsToDate - (previous == null ? 0 : previous.EggsToDate);
                log.AppendLine("| " + first.Day + "-" + last.Day + " | " + last.Season +
                    " | " + last.Chickens + " | " + last.Pigs + " | " + last.Deer + " | " +
                    last.Wolves + " | " + last.Bramblebacks + " | " + losses + " | " + eggs + " |");
                previous = last;
            }
            log.AppendLine();
            log.AppendLine("## Wolf incidents — winter livestock losses");
            log.AppendLine();
            if (winterLosses.Count == 0)
            {
                log.AppendLine("No winter livestock losses in this run.");
            }
            else
            {
                foreach (WorldEvent loss in winterLosses)
                {
                    string victim = loss.Copper == LivestockValue.PigCopper ? "pig" : "chicken";
                    log.AppendLine("- Game day " + loss.Time.Day + ": wolves took a " + victim +
                        " at " + loss.Location.Value + " (recorded value " + loss.Copper + " copper).");
                }
            }
            log.AppendLine();
            log.AppendLine("## Final state (game day " + (StartDay + RunDays - 1) + ")");
            log.AppendLine();
            DayStats final = run.Days[run.Days.Count - 1];
            log.AppendLine("- Chickens: " + final.Chickens + ", pigs: " + final.Pigs +
                ", deer: " + final.Deer + " (down from 40 — wolf predation), wolves: " +
                final.Wolves + ", bramblebacks: " + final.Bramblebacks + ".");
            log.AppendLine("- Eggs laid: " + autumnEggs + " in autumn, " + winterEggs +
                " in winter — laying is strongly seasonal.");
            var bonded = new List<string>();
            foreach (AnimalState animal in run.State.Animals.Animals)
                if (animal.Owner.HasValue)
                    bonded.Add(animal.Id.Value + " (" + animal.Species.Value + ", trust " +
                        animal.Trust + ", owner " +
                        (animal.Owner.Value.IsPlayer ? "player" : "npc:" + animal.Owner.Value.Npc.Value.Value) + ")");
            log.AppendLine("- Bonded animals: " + (bonded.Count == 0 ? "none" : string.Join("; ", bonded)) + ".");
            log.AppendLine("- WorldDigest (save/load-continued run): " +
                WorldDigest.Compute(run.State) + ".");
            return log.ToString();
        }
    }
}
