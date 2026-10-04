using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Taming as a relationship, not a button (P4-03, docs/design/ANIMALS.md
    /// section 1-2, docs/design/SKILLS.md section 3): trust builds 0-100 through
    /// repeated calm interactions with one meaningful gain per animal per day,
    /// bonds form at the species' threshold, and cruelty or neglect breaks them.
    ///
    /// Rules, all from the approved design and Content/animals/species.json:
    /// <list type="bullet">
    /// <item>Only tameable animals accept trust: chickens and bramblebacks at any
    /// age; piglets, fawns and wolf pups (young) only. Wild boars, stags, sows
    /// and adult wolves can never be tamed — rejection, never an exception.</item>
    /// <item>Wolf pups additionally need council approval before trust can be
    /// built (a public-safety decision; real council voting is Phase 5 town
    /// work). Approval is a call parameter for now. Cruelty to a wolf is never
    /// gated by approval — a permit to tame is not a permit to be cruel.</item>
    /// <item>Gains are scaled by the actor's skill_taming level (x1 at 0-1, x1.5
    /// at 2, x2 at 3+); losses always land in full.</item>
    /// <item>A bond forms only while the animal is unbonded; the first bond wins
    /// and later kindness from others does not steal it.</item>
    /// <item>A wolf whose trust falls below 30 leaves; a pig whose trust falls
    /// below 40 reverts (owner cleared, trust reset to 10). A starved wolf pup
    /// breaks instantly with trust zeroed.</item>
    /// </list>
    ///
    /// Truth record: the bond itself (AnimalState.Owner) and the trust score are
    /// world truth. A dedicated Bonded/BondBroken entry in the world event log
    /// still needs new WorldEventType values in Core (orchestrator follow-up);
    /// until then the state change is the truth record, and the "may turn"
    /// reckoning for a wolf broken through cruelty is future work.
    ///
    /// Fully deterministic: the RNG is accepted for future stochastic touches
    /// but no draws are consumed; gains are fixed per-interaction values.
    /// </summary>
    public static class TamingSystem
    {
        /// <summary>Trust regained from a repeat positive interaction on the same day.</summary>
        public const int SameDayNibble = 1;
        /// <summary>A wolf whose trust falls below this leaves for the wild.</summary>
        public const int WolfBreakThreshold = 30;
        /// <summary>A pig whose trust falls below this reverts to an ordinary animal.</summary>
        public const int PigBreakThreshold = 40;
        /// <summary>Trust a broken bond resets to (starvation zeroes it instead).</summary>
        public const int BrokenBondTrust = 10;

        private static readonly SkillId TamingSkill = new SkillId("skill_taming");

        private sealed class Effect
        {
            public Effect(int delta, bool instantBreak = false)
            {
                Delta = delta;
                InstantBreak = instantBreak;
            }

            public int Delta { get; }
            public bool IsGain => Delta > 0;
            /// <summary>Starving a wolf pup: breaks the bond at once, whatever trust was.</summary>
            public bool InstantBreak { get; }
        }

        /// <summary>
        /// One taming interaction between an actor (player or NPC) and an animal:
        /// applies the trust change for the interaction, enforces one meaningful
        /// gain per animal per day, forms and breaks bonds, and returns what happened.
        /// </summary>
        /// <param name="state">The world (for the actor's taming skill).</param>
        /// <param name="animal">The animal being handled; mutated in place.</param>
        /// <param name="species">The animal's species definition (bond threshold); must match the animal.</param>
        /// <param name="actor">The player or NPC doing the interacting.</param>
        /// <param name="interaction">The kind of interaction.</param>
        /// <param name="day">Current game day (state.Clock.Day).</param>
        /// <param name="councilApproval">Whether the actor may tame wolves (Phase 5 placeholder).</param>
        /// <param name="rng">Accepted for future use; no draws are consumed.</param>
        public static TamingResult Interact(WorldState state, AnimalState animal,
            SpeciesDefinition species, ActorId actor, TamingInteraction interaction,
            long day, bool councilApproval, SimRng rng)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (animal == null) throw new ArgumentNullException(nameof(animal));
            if (species == null) throw new ArgumentNullException(nameof(species));
            if (species.Id != animal.Species)
                throw new ArgumentException(
                    "The species definition does not match the animal's species.", nameof(species));
            if (!actor.IsValid)
                throw new ArgumentException("A taming interaction needs a valid actor.", nameof(actor));
            if (day < 0)
                throw new ArgumentOutOfRangeException(nameof(day), "Game day must not be negative.");
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            if (!IsTameable(animal))
                return new TamingResult(TamingOutcome.RejectedUntameable, 0, animal.Trust);

            Effect effect = LookupEffect(animal.Species, interaction);
            bool isWolf = animal.Species == SpeciesContentLoader.Wolf;
            if (isWolf && effect.IsGain && !councilApproval)
                return new TamingResult(TamingOutcome.RejectedNeedsApproval, 0, animal.Trust);

            long previousDay = animal.LastInteractionDay;
            animal.RecordInteraction(day);

            if (effect.InstantBreak)
            {
                // A starved wolf pup breaks instantly: trust zeroed, bond cleared.
                int before = animal.Trust;
                animal.AdjustTrust(-before);
                animal.SetOwner(null);
                return new TamingResult(TamingOutcome.StarvedBreak, animal.Trust - before, animal.Trust);
            }

            int delta = effect.Delta;
            if (effect.IsGain)
            {
                delta = previousDay == day
                    ? SameDayNibble
                    : ScaleGain(delta, TamingLevel(state, actor));
            }

            int trustBefore = animal.Trust;
            animal.AdjustTrust(delta);

            TamingOutcome outcome;
            if (animal.Owner == null && animal.Trust >= species.BondThreshold)
            {
                animal.SetOwner(actor);
                outcome = TamingOutcome.Bonded;
            }
            else if (animal.Owner != null && isWolf && animal.Trust < WolfBreakThreshold)
            {
                BreakBond(animal);
                outcome = TamingOutcome.BondBroken;
            }
            else if (animal.Owner != null && animal.Species == SpeciesContentLoader.Pig
                && animal.Trust < PigBreakThreshold)
            {
                BreakBond(animal);
                outcome = TamingOutcome.BondBroken;
            }
            else
            {
                outcome = TamingOutcome.TrustChanged;
            }

            return new TamingResult(outcome, animal.Trust - trustBefore, animal.Trust);
        }

        private static void BreakBond(AnimalState animal)
        {
            animal.SetOwner(null);
            animal.AdjustTrust(BrokenBondTrust - animal.Trust);
        }

        /// <summary>
        /// Whether this individual animal can ever be tamed. Chickens and
        /// bramblebacks at any age; pigs (piglets), deer (fawns) and wolves
        /// (orphaned pups) only while young. Adult pigs are sows and wild boars,
        /// adult deer are stags, adult wolves are pack wolves — never tameable,
        /// the design says so plainly. Unknown species default to untameable.
        /// </summary>
        private static bool IsTameable(AnimalState animal)
        {
            if (animal.Species == SpeciesContentLoader.Chicken) return true;
            if (animal.Species == SpeciesContentLoader.Brambleback) return true;
            if (animal.Species == SpeciesContentLoader.Pig) return animal.Age == AnimalAge.Young;
            if (animal.Species == SpeciesContentLoader.Deer) return animal.Age == AnimalAge.Young;
            if (animal.Species == SpeciesContentLoader.Wolf) return animal.Age == AnimalAge.Young;
            return false;
        }

        /// <summary>
        /// The trust effect of one interaction for a species, mirroring
        /// Content/animals/species.json "taming" builds/breaks. An interaction
        /// with no rule for the species is a caller bug and throws.
        /// </summary>
        private static Effect LookupEffect(SpeciesId species, TamingInteraction interaction)
        {
            if (species == SpeciesContentLoader.Chicken)
            {
                switch (interaction)
                {
                    case TamingInteraction.HandFeeding: return new Effect(8);
                    case TamingInteraction.GentleHandling: return new Effect(5);
                    case TamingInteraction.RoughHandling: return new Effect(-20);
                    case TamingInteraction.ChasedByDogs: return new Effect(-30);
                }
            }
            if (species == SpeciesContentLoader.Pig)
            {
                switch (interaction)
                {
                    case TamingInteraction.Feeding: return new Effect(6);
                    case TamingInteraction.Scratching: return new Effect(10);
                    case TamingInteraction.Struck: return new Effect(-25);
                    case TamingInteraction.Starved: return new Effect(-40);
                }
            }
            if (species == SpeciesContentLoader.Deer)
            {
                switch (interaction)
                {
                    case TamingInteraction.WinterFeeding: return new Effect(4);
                    case TamingInteraction.CalmPresence: return new Effect(2);
                    case TamingInteraction.ChaseOrNoise: return new Effect(-30);
                }
            }
            if (species == SpeciesContentLoader.Wolf)
            {
                switch (interaction)
                {
                    case TamingInteraction.FeedingMeat: return new Effect(5);
                    case TamingInteraction.PlayAndTraining: return new Effect(5);
                    case TamingInteraction.CalmStrength: return new Effect(8);
                    case TamingInteraction.Struck: return new Effect(-50);
                    case TamingInteraction.Starved: return new Effect(0, instantBreak: true);
                }
            }
            if (species == SpeciesContentLoader.Brambleback)
            {
                switch (interaction)
                {
                    case TamingInteraction.Porridge: return new Effect(12);
                    case TamingInteraction.SittingQuietly: return new Effect(6);
                    case TamingInteraction.LoudDisturbance: return new Effect(-15);
                }
            }
            throw new ArgumentException(
                "Species '" + species + "' has no taming rule for '" + interaction + "'.",
                nameof(interaction));
        }

        /// <summary>
        /// Scales a trust gain by the actor's skill_taming level (SKILLS.md
        /// section 3): base rate at levels 0-1 (no skill is required to start),
        /// x1.5 at level 2, x2 at level 3 and above (levels 4-5 unlock new
        /// capabilities, not faster gains).
        /// </summary>
        private static int ScaleGain(int baseGain, int level)
        {
            double multiplier = level >= 3 ? 2.0 : level == 2 ? 1.5 : 1.0;
            return (int)Math.Round(baseGain * multiplier, MidpointRounding.AwayFromZero);
        }

        private static int TamingLevel(WorldState state, ActorId actor)
        {
            // The player keeps skills on the world; NPCs keep them on their state.
            SkillStore store = actor.IsPlayer
                ? state.PlayerSkills
                : state.Npcs[actor.Npc.Value].Skills;
            return store.GetLevel(TamingSkill);
        }

        /// <summary>
        /// What a bonded animal of this species does for its owner — the service
        /// the bond records as truth (docs/design/ANIMALS.md). The mechanics
        /// behind each service are P4-04-or-later work.
        /// </summary>
        public static string BondServiceFor(SpeciesId species)
        {
            if (!species.IsValid)
                throw new ArgumentException("A species lookup needs a valid ID.", nameof(species));
            if (species == SpeciesContentLoader.Chicken)
                return "comes when called and lays in a chosen nest box";
            if (species == SpeciesContentLoader.Pig)
                return "follows its person and finds mushrooms in autumn";
            if (species == SpeciesContentLoader.Deer)
                return "carries light packs (10 kg) and alarm-snorts at wolves";
            if (species == SpeciesContentLoader.Wolf)
                return "guards its person's home, hunts alongside them and tracks by scent";
            if (species == SpeciesContentLoader.Brambleback)
                return "nests in the barn and keeps livestock nearly tick-free";
            throw new ArgumentException("Unknown species '" + species + "'.", nameof(species));
        }
    }
}
