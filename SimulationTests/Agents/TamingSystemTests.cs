using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Agents
{
    /// <summary>
    /// Verifies taming as a relationship (P4-03, docs/design/ANIMALS.md): trust
    /// builds 0-100 through repeated calm interactions with one meaningful gain
    /// per animal per day, bonds form at species thresholds, cruelty and neglect
    /// break bonds, wild boars and stags can never be tamed, wolf pups need
    /// council approval, and the taming skill multiplies trust gains.
    /// </summary>
    public sealed class TamingSystemTests
    {
        private static readonly SkillId TamingSkill = new SkillId("skill_taming");
        private static readonly LocationId Farm = new LocationId("loc_farm");
        private static readonly LocationId ForestEdge = new LocationId("loc_forest_edge");

        // Bond thresholds mirror Content/animals/species.json (approved data).
        private static SpeciesDefinition ChickenDef() => TestSpecies(
            SpeciesContentLoader.Chicken, "Chicken", bondThreshold: 60, daysToBond: 7);
        private static SpeciesDefinition PigDef() => TestSpecies(
            SpeciesContentLoader.Pig, "Pig", bondThreshold: 70, daysToBond: 12);
        private static SpeciesDefinition DeerDef() => TestSpecies(
            SpeciesContentLoader.Deer, "Deer", bondThreshold: 80, daysToBond: 25);
        private static SpeciesDefinition WolfDef() => TestSpecies(
            SpeciesContentLoader.Wolf, "Wolf", bondThreshold: 85, daysToBond: 60, wild: true);
        private static SpeciesDefinition BramblebackDef() => TestSpecies(
            SpeciesContentLoader.Brambleback, "Brambleback", bondThreshold: 50, daysToBond: 5, wild: true);

        private static SpeciesDefinition TestSpecies(SpeciesId id, string name,
            int bondThreshold, int daysToBond, bool wild = false)
        {
            return new SpeciesDefinition(id, name,
                new[] { Farm, ForestEdge }, new[] { "item_grain" }, "placid",
                wild, tameable: true, bondThreshold, daysToBond, populationCount: 4,
                new[] { "hand_feeding_+8_per_day" }, new[] { "struck_-25" },
                tamingRequirement: null);
        }

        private static AnimalState MakeAnimal(string id, SpeciesDefinition species,
            AnimalAge age, int trust, ActorId? owner = null)
        {
            return AnimalState.Create(new AnimalId(id), species.Id, Farm,
                trust, owner, age, AnimalState.MaxHealth);
        }

        private static WorldState TestWorld() => new WorldState(7);

        private static NpcId RegisterNpc(WorldState state, string id)
        {
            var npcId = new NpcId(id);
            var definition = new NpcDefinition(npcId, id, 30, "test", "tester",
                Farm, Farm, 0,
                new Dictionary<string, int> { ["honest"] = 50 },
                new NeedRates(0, 0, 0),
                new NpcSchedule(Array.Empty<ScheduleEntry>(), Array.Empty<ScheduleEntry>()));
            state.Npcs.Register(new NpcState(definition, 50, 80, 50));
            return npcId;
        }

        private static void SetTamingLevel(WorldState state, NpcId npc, int level)
        {
            state.Npcs[npc].Skills.Restore(new[] { new SkillState(TamingSkill, level) });
        }

        private static TamingResult Interact(WorldState state, AnimalState animal,
            SpeciesDefinition species, ActorId actor, TamingInteraction interaction,
            long day, bool councilApproval = false)
        {
            return TamingSystem.Interact(state, animal, species, actor,
                interaction, day, councilApproval, state.Rng);
        }

        [Test]
        public void ChickenBondsInSevenDays()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = ChickenDef();
            NpcId lida = RegisterNpc(state, "npc_lida");
            var lidaActor = ActorId.ForNpc(lida);
            // Domestic chickens start at trust 20-30 (AnimalPopulationFactory).
            AnimalState hen = MakeAnimal("animal_chicken_001", species, AnimalAge.Adult, trust: 20);

            TamingResult[] days = new TamingResult[7];
            for (long day = 1; day <= 7; day++)
                days[day - 1] = Interact(state, hen, species, lidaActor, TamingInteraction.HandFeeding, day);

            Assert.That(hen.Trust, Is.GreaterThanOrEqualTo(60),
                "20 + 7 days x +8 hand-feeding = 76, past the 60 bond threshold.");
            Assert.That(hen.Owner, Is.EqualTo((ActorId?)lidaActor));
            Assert.That(days[4].Outcome, Is.EqualTo(TamingOutcome.Bonded),
                "20 + 5 x 8 = 60: the hen bonds on day five.");
            Assert.That(days[6].Outcome, Is.EqualTo(TamingOutcome.TrustChanged),
                "Already bonded: later kindness only deepens trust.");
        }

        [Test]
        public void BramblebackBondsInFiveDays()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = BramblebackDef();
            var player = ActorId.Player;
            AnimalState back = MakeAnimal("animal_brambleback_001", species, AnimalAge.Adult, trust: 0);

            TamingResult last = null;
            for (long day = 1; day <= 5; day++)
                last = Interact(state, back, species, player, TamingInteraction.Porridge, day);

            Assert.That(back.Trust, Is.GreaterThanOrEqualTo(50),
                "5 days x +12 porridge = 60, past the 50 bond threshold.");
            Assert.That(back.Owner, Is.EqualTo((ActorId?)player),
                "The player can bond an animal: ownership records player-or-NPC actors.");
            Assert.That(last.Outcome, Is.EqualTo(TamingOutcome.Bonded));
        }

        [Test]
        public void CrueltyBreaksTrust()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = PigDef();
            NpcId jory = RegisterNpc(state, "npc_jory");
            var joryActor = ActorId.ForNpc(jory);
            AnimalState piglet = MakeAnimal("animal_pig_001", species, AnimalAge.Young,
                trust: 75, owner: joryActor);

            TamingResult first = Interact(state, piglet, species, joryActor,
                TamingInteraction.Struck, day: 1);
            Assert.That(first.Outcome, Is.EqualTo(TamingOutcome.TrustChanged));
            Assert.That(piglet.Trust, Is.EqualTo(50), "75 - 25 for being struck.");
            Assert.That(piglet.Owner, Is.EqualTo((ActorId?)joryActor),
                "Still bonded: 50 is above the pig's 40 break threshold.");

            // Cruelty is never blunted by the one-gain-per-day rule: losses always apply fully.
            TamingResult second = Interact(state, piglet, species, joryActor,
                TamingInteraction.Struck, day: 1);
            Assert.That(second.Outcome, Is.EqualTo(TamingOutcome.BondBroken));
            Assert.That(piglet.Owner, Is.Null, "Trust fell below 40: the pig reverts.");
            Assert.That(piglet.Trust, Is.EqualTo(10), "A broken bond resets trust to 10.");
        }

        [Test]
        public void WolfPupTakesSixtyDays()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = WolfDef();
            var player = ActorId.Player;
            AnimalState pup = MakeAnimal("animal_wolf_001", species, AnimalAge.Young, trust: 0);

            for (long day = 1; day <= 60; day++)
            {
                var interaction = day % 2 == 0
                    ? TamingInteraction.FeedingMeat
                    : TamingInteraction.PlayAndTraining;
                TamingResult result = Interact(state, pup, species, player,
                    interaction, day, councilApproval: true);
                Assert.That(result.Outcome, Is.Not.EqualTo(TamingOutcome.RejectedNeedsApproval));
            }

            Assert.That(pup.Trust, Is.GreaterThanOrEqualTo(85));
            Assert.That(pup.Owner, Is.EqualTo((ActorId?)player));
        }

        [Test]
        public void WolfNeedsCouncilApproval()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = WolfDef();
            AnimalState pup = MakeAnimal("animal_wolf_001", species, AnimalAge.Young, trust: 0);

            TamingResult result = Interact(state, pup, species, ActorId.Player,
                TamingInteraction.FeedingMeat, day: 1, councilApproval: false);

            Assert.That(result.Outcome, Is.EqualTo(TamingOutcome.RejectedNeedsApproval));
            Assert.That(pup.Trust, Is.EqualTo(0), "Rejected interactions change nothing.");
            Assert.That(pup.Owner, Is.Null);
            Assert.That(pup.LastInteractionDay, Is.EqualTo(-1),
                "Rejected interactions do not count as the day's interaction.");
        }

        [Test]
        public void WolfCrueltyDoesNotNeedApproval()
        {
            // Council approval gates *taming* (trust-building), not cruelty: striking
            // a wolf without a permit is still cruelty, and still breaks trust.
            WorldState state = TestWorld();
            SpeciesDefinition species = WolfDef();
            AnimalState pup = MakeAnimal("animal_wolf_001", species, AnimalAge.Young, trust: 20);

            TamingResult result = Interact(state, pup, species, ActorId.Player,
                TamingInteraction.Struck, day: 1, councilApproval: false);

            Assert.That(result.Outcome, Is.EqualTo(TamingOutcome.TrustChanged));
            Assert.That(pup.Trust, Is.EqualTo(0), "20 - 50, clamped at 0.");
        }

        [Test]
        public void WildBoarCannotBeTamed()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = PigDef();
            NpcId ralf = RegisterNpc(state, "npc_ralf");
            // Adult pigs are sows and wild boars: only piglets (young) can be tamed.
            AnimalState boar = MakeAnimal("animal_pig_007", species, AnimalAge.Adult, trust: 0);

            TamingResult result = Interact(state, boar, species, ActorId.ForNpc(ralf),
                TamingInteraction.Feeding, day: 1);

            Assert.That(result.Outcome, Is.EqualTo(TamingOutcome.RejectedUntameable));
            Assert.That(boar.Trust, Is.EqualTo(0));
        }

        [Test]
        public void StagCannotBeTamed()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = DeerDef();
            // Only fawns (young) can be tamed; stags (adults) never.
            AnimalState stag = MakeAnimal("animal_deer_001", species, AnimalAge.Adult, trust: 0);

            TamingResult result = Interact(state, stag, species, ActorId.Player,
                TamingInteraction.WinterFeeding, day: 1);

            Assert.That(result.Outcome, Is.EqualTo(TamingOutcome.RejectedUntameable));
            Assert.That(stag.Trust, Is.EqualTo(0));
        }

        [Test]
        public void AdultWolfCannotBeTamed()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = WolfDef();
            // Only orphaned pups (young) with council approval; adult wolves never.
            AnimalState adult = MakeAnimal("animal_wolf_002", species, AnimalAge.Adult, trust: 0);

            TamingResult result = Interact(state, adult, species, ActorId.Player,
                TamingInteraction.FeedingMeat, day: 1, councilApproval: true);

            Assert.That(result.Outcome, Is.EqualTo(TamingOutcome.RejectedUntameable));
        }

        [Test]
        public void OneGainPerDay()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = ChickenDef();
            NpcId lida = RegisterNpc(state, "npc_lida");
            var lidaActor = ActorId.ForNpc(lida);
            AnimalState hen = MakeAnimal("animal_chicken_001", species, AnimalAge.Adult, trust: 20);

            TamingResult first = Interact(state, hen, species, lidaActor,
                TamingInteraction.HandFeeding, day: 1);
            Assert.That(first.TrustDelta, Is.EqualTo(8));
            Assert.That(hen.LastInteractionDay, Is.EqualTo(1));

            // Extra positive interactions the same day give only a tiny nibble.
            TamingResult second = Interact(state, hen, species, lidaActor,
                TamingInteraction.HandFeeding, day: 1);
            Assert.That(second.TrustDelta, Is.EqualTo(1));
            TamingResult third = Interact(state, hen, species, lidaActor,
                TamingInteraction.GentleHandling, day: 1);
            Assert.That(third.TrustDelta, Is.EqualTo(1));
            Assert.That(hen.Trust, Is.EqualTo(30), "20 + 8 + 1 + 1.");

            // A new day restores the full gain.
            TamingResult nextDay = Interact(state, hen, species, lidaActor,
                TamingInteraction.HandFeeding, day: 2);
            Assert.That(nextDay.TrustDelta, Is.EqualTo(8));
            Assert.That(hen.Trust, Is.EqualTo(38));
        }

        [Test]
        public void TamingSkillMultipliesGains()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = ChickenDef();
            NpcId novice = RegisterNpc(state, "npc_novice");
            NpcId master = RegisterNpc(state, "npc_master");
            SetTamingLevel(state, novice, 1);
            SetTamingLevel(state, master, 3);
            AnimalState henA = MakeAnimal("animal_chicken_001", species, AnimalAge.Adult, trust: 20);
            AnimalState henB = MakeAnimal("animal_chicken_002", species, AnimalAge.Adult, trust: 20);

            TamingResult noviceResult = Interact(state, henA, species,
                ActorId.ForNpc(novice), TamingInteraction.HandFeeding, day: 1);
            TamingResult masterResult = Interact(state, henB, species,
                ActorId.ForNpc(master), TamingInteraction.HandFeeding, day: 1);

            Assert.That(noviceResult.TrustDelta, Is.EqualTo(8), "Level 1: base rate.");
            Assert.That(masterResult.TrustDelta, Is.EqualTo(16), "Level 3: x2 trust gain.");
        }

        [Test]
        public void TamingSkillLevelTwoGivesOneAndAHalf()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = ChickenDef();
            NpcId tamer = RegisterNpc(state, "npc_tamer");
            SetTamingLevel(state, tamer, 2);
            AnimalState hen = MakeAnimal("animal_chicken_001", species, AnimalAge.Adult, trust: 20);

            TamingResult result = Interact(state, hen, species, ActorId.ForNpc(tamer),
                TamingInteraction.HandFeeding, day: 1);

            Assert.That(result.TrustDelta, Is.EqualTo(12), "Level 2: 8 x 1.5 = 12.");
        }

        [Test]
        public void CrueltyIsNotSoftenedBySkill()
        {
            // The skill multiplies trust *gains*; cruelty always lands in full.
            WorldState state = TestWorld();
            SpeciesDefinition species = PigDef();
            NpcId master = RegisterNpc(state, "npc_master");
            SetTamingLevel(state, master, 3);
            AnimalState piglet = MakeAnimal("animal_pig_001", species, AnimalAge.Young, trust: 75);

            TamingResult result = Interact(state, piglet, species, ActorId.ForNpc(master),
                TamingInteraction.Struck, day: 1);

            Assert.That(result.TrustDelta, Is.EqualTo(-25), "Losses are never multiplied.");
            Assert.That(piglet.Trust, Is.EqualTo(50));
        }

        [Test]
        public void StruckBondedWolfMayBreak()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = WolfDef();
            var player = ActorId.Player;
            AnimalState wolf = MakeAnimal("animal_wolf_001", species, AnimalAge.Young,
                trust: 90, owner: player);

            TamingResult first = Interact(state, wolf, species, player,
                TamingInteraction.Struck, day: 1, councilApproval: true);
            Assert.That(first.Outcome, Is.EqualTo(TamingOutcome.TrustChanged));
            Assert.That(wolf.Trust, Is.EqualTo(40), "90 - 50 for being struck.");
            Assert.That(wolf.Owner, Is.EqualTo((ActorId?)player),
                "Still bonded: 40 is above the wolf's 30 break threshold.");

            TamingResult second = Interact(state, wolf, species, player,
                TamingInteraction.Struck, day: 2, councilApproval: true);
            Assert.That(second.Outcome, Is.EqualTo(TamingOutcome.BondBroken),
                "Trust broke below 30 through cruelty: the wolf leaves. (Whether it turns is future work.)");
            Assert.That(wolf.Owner, Is.Null);
            Assert.That(wolf.Trust, Is.EqualTo(10));
        }

        [Test]
        public void StarvedWolfBreaksInstantly()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = WolfDef();
            var player = ActorId.Player;
            AnimalState wolf = MakeAnimal("animal_wolf_001", species, AnimalAge.Young,
                trust: 95, owner: player);

            TamingResult result = Interact(state, wolf, species, player,
                TamingInteraction.Starved, day: 1, councilApproval: true);

            Assert.That(result.Outcome, Is.EqualTo(TamingOutcome.StarvedBreak));
            Assert.That(wolf.Owner, Is.Null, "A starved wolf breaks instantly, whatever the trust was.");
            Assert.That(wolf.Trust, Is.EqualTo(0), "Starving a bonded animal zeroes trust.");
        }

        [Test]
        public void BondedAnimalHasAService()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = ChickenDef();
            NpcId lida = RegisterNpc(state, "npc_lida");
            var lidaActor = ActorId.ForNpc(lida);
            AnimalState hen = MakeAnimal("animal_chicken_001", species, AnimalAge.Adult, trust: 20);

            for (long day = 1; day <= 7; day++)
                Interact(state, hen, species, lidaActor, TamingInteraction.HandFeeding, day);

            Assert.That(hen.Owner, Is.Not.Null);
            string service = TamingSystem.BondServiceFor(species.Id);
            Assert.That(service, Does.Contain("comes when called"),
                "The bond records the animal's service; the mechanics are future work.");
        }

        [Test]
        public void UnknownInteractionForSpeciesThrows()
        {
            WorldState state = TestWorld();
            SpeciesDefinition species = PigDef();
            AnimalState piglet = MakeAnimal("animal_pig_001", species, AnimalAge.Young, trust: 30);

            // Hand-feeding is a chicken interaction; offering it to a pig is a caller bug.
            Assert.Throws<ArgumentException>(() => Interact(state, piglet, species,
                ActorId.Player, TamingInteraction.HandFeeding, day: 1));
        }

        [Test]
        public void BondStaysWithFirstOwner()
        {
            // Trust is per-animal, not per-actor: once bonded, another actor's
            // kindness raises trust but does not steal the bond.
            WorldState state = TestWorld();
            SpeciesDefinition species = ChickenDef();
            NpcId lida = RegisterNpc(state, "npc_lida");
            NpcId tansy = RegisterNpc(state, "npc_tansy");
            AnimalState hen = MakeAnimal("animal_chicken_001", species, AnimalAge.Adult,
                trust: 65, owner: ActorId.ForNpc(lida));

            TamingResult result = Interact(state, hen, species, ActorId.ForNpc(tansy),
                TamingInteraction.HandFeeding, day: 1);

            Assert.That(result.Outcome, Is.EqualTo(TamingOutcome.TrustChanged));
            Assert.That(hen.Owner, Is.EqualTo((ActorId?)ActorId.ForNpc(lida)));
        }
    }
}
