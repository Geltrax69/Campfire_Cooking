using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Age category for breeding: young animals are not yet breeding adults.
    /// (P4-02 will use this for breeding eligibility.)
    /// </summary>
    public enum AnimalAge
    {
        Young,
        Adult
    }

    /// <summary>
    /// One animal's mutable state (docs/design/ANIMALS.md): its species, where it
    /// lives/ranges, taming trust 0-100, bonded owner (null = wild/unbonded), age
    /// category and health 0-100. Trust and health are clamped to their range —
    /// systems hand in raw deltas and the state keeps the invariant instead of
    /// throwing, while <see cref="Restore"/> rejects out-of-range save data.
    /// </summary>
    public sealed class AnimalState
    {
        /// <summary>Trust below this means no bond; never goes negative.</summary>
        public const int MinTrust = 0;
        /// <summary>A fully-bonded animal trusts its person completely.</summary>
        public const int MaxTrust = 100;
        /// <summary>Dead / dying.</summary>
        public const int MinHealth = 0;
        /// <summary>Full health.</summary>
        public const int MaxHealth = 100;

        private AnimalState(AnimalId id, SpeciesId species, LocationId location,
            int trust, NpcId? owner, AnimalAge age, int health)
        {
            Id = id;
            Species = species;
            Location = location;
            Trust = Clamp(trust, MinTrust, MaxTrust);
            Owner = owner;
            Age = age;
            Health = Clamp(health, MinHealth, MaxHealth);
        }

        /// <summary>
        /// Creates a live animal, clamping trust and health into range. Wild
        /// animals start at trust 0; domestic ones at 20-30 (see
        /// <see cref="AnimalPopulationFactory"/>).
        /// </summary>
        public static AnimalState Create(AnimalId id, SpeciesId species, LocationId location,
            int trust, NpcId? owner, AnimalAge age, int health)
        {
            Validate(id, species, location);
            return new AnimalState(id, species, location, trust, owner, age, health);
        }

        /// <summary>
        /// Rebuilds exact animal state for save/load. Trust and health outside
        /// 0-100 mean corrupt save data and throw; everything is validated
        /// before anything is built, so a failed restore has no side effects.
        /// </summary>
        public static AnimalState Restore(AnimalId id, SpeciesId species, LocationId location,
            int trust, NpcId? owner, AnimalAge age, int health)
        {
            Validate(id, species, location);
            if (trust < MinTrust || trust > MaxTrust)
                throw new ArgumentOutOfRangeException(nameof(trust), "Saved trust must be 0-100.");
            if (health < MinHealth || health > MaxHealth)
                throw new ArgumentOutOfRangeException(nameof(health), "Saved health must be 0-100.");
            return new AnimalState(id, species, location, trust, owner, age, health);
        }

        private static void Validate(AnimalId id, SpeciesId species, LocationId location)
        {
            if (!id.IsValid) throw new ArgumentException("An animal needs a valid ID.", nameof(id));
            if (!species.IsValid) throw new ArgumentException("An animal needs a valid species.", nameof(species));
            if (!location.IsValid) throw new ArgumentException("An animal needs a valid location.", nameof(location));
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : value > max ? max : value;

        public AnimalId Id { get; }
        public SpeciesId Species { get; }
        /// <summary>Where the animal lives/ranges.</summary>
        public LocationId Location { get; }
        /// <summary>Taming trust 0-100; clamped on every change.</summary>
        public int Trust { get; private set; }
        /// <summary>Bonded owner; null = wild/unbonded.</summary>
        public NpcId? Owner { get; private set; }
        public AnimalAge Age { get; }
        /// <summary>0-100; clamped on every change.</summary>
        public int Health { get; private set; }

        /// <summary>
        /// Moves trust by a delta; clamps at 0 and 100. (One meaningful gain per
        /// animal per day is enforced by the calling system, not here.)
        /// </summary>
        public void AdjustTrust(int delta) => Trust = Clamp(Trust + delta, MinTrust, MaxTrust);

        /// <summary>Moves health by a delta (predation/injury); clamps at 0 and 100.</summary>
        public void AdjustHealth(int delta) => Health = Clamp(Health + delta, MinHealth, MaxHealth);

        /// <summary>Bonds (or un-bonds, with null) the animal to an NPC.</summary>
        public void SetOwner(NpcId? owner) => Owner = owner;
    }
}
