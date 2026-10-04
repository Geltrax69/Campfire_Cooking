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
    /// The owner is a player-or-NPC actor because taming is the player's skill as
    /// much as any NPC's (P4-03).
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
            int trust, ActorId? owner, AnimalAge age, int health, long lastInteractionDay)
        {
            Id = id;
            Species = species;
            Location = location;
            Trust = Clamp(trust, MinTrust, MaxTrust);
            Owner = owner;
            Age = age;
            Health = Clamp(health, MinHealth, MaxHealth);
            LastInteractionDay = lastInteractionDay;
        }

        /// <summary>
        /// Creates a live animal, clamping trust and health into range. Wild
        /// animals start at trust 0; domestic ones at 20-30 (see
        /// <see cref="AnimalPopulationFactory"/>). The last interaction day
        /// starts at -1 (never handled).
        /// </summary>
        public static AnimalState Create(AnimalId id, SpeciesId species, LocationId location,
            int trust, ActorId? owner, AnimalAge age, int health, long lastInteractionDay = -1)
        {
            Validate(id, species, location, owner, lastInteractionDay);
            return new AnimalState(id, species, location, trust, owner, age, health, lastInteractionDay);
        }

        /// <summary>
        /// Rebuilds exact animal state for save/load. Trust and health outside
        /// 0-100 mean corrupt save data and throw; everything is validated
        /// before anything is built, so a failed restore has no side effects.
        /// </summary>
        public static AnimalState Restore(AnimalId id, SpeciesId species, LocationId location,
            int trust, ActorId? owner, AnimalAge age, int health, long lastInteractionDay = -1)
        {
            Validate(id, species, location, owner, lastInteractionDay);
            if (trust < MinTrust || trust > MaxTrust)
                throw new ArgumentOutOfRangeException(nameof(trust), "Saved trust must be 0-100.");
            if (health < MinHealth || health > MaxHealth)
                throw new ArgumentOutOfRangeException(nameof(health), "Saved health must be 0-100.");
            return new AnimalState(id, species, location, trust, owner, age, health, lastInteractionDay);
        }

        private static void Validate(AnimalId id, SpeciesId species, LocationId location,
            ActorId? owner, long lastInteractionDay)
        {
            if (!id.IsValid) throw new ArgumentException("An animal needs a valid ID.", nameof(id));
            if (!species.IsValid) throw new ArgumentException("An animal needs a valid species.", nameof(species));
            if (!location.IsValid) throw new ArgumentException("An animal needs a valid location.", nameof(location));
            if (owner.HasValue && !owner.Value.IsValid)
                throw new ArgumentException("A bonded owner must be a valid actor.", nameof(owner));
            if (lastInteractionDay < -1)
                throw new ArgumentOutOfRangeException(nameof(lastInteractionDay),
                    "The last interaction day is -1 before any interaction.");
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : value > max ? max : value;

        public AnimalId Id { get; }
        public SpeciesId Species { get; }
        /// <summary>Where the animal lives/ranges. Mutable: winter pressure moves herds.</summary>
        public LocationId Location { get; private set; }
        /// <summary>Taming trust 0-100; clamped on every change.</summary>
        public int Trust { get; private set; }
        /// <summary>Bonded owner (player or NPC); null = wild/unbonded.</summary>
        public ActorId? Owner { get; private set; }
        public AnimalAge Age { get; }
        /// <summary>0-100; clamped on every change.</summary>
        public int Health { get; private set; }
        /// <summary>
        /// Game day of the last taming interaction with this animal; -1 before
        /// any. The taming system uses it to enforce one meaningful trust gain
        /// per animal per day (docs/design/ANIMALS.md).
        /// </summary>
        public long LastInteractionDay { get; private set; }

        /// <summary>
        /// Moves trust by a delta; clamps at 0 and 100. (One meaningful gain per
        /// animal per day is enforced by the calling system, not here.)
        /// </summary>
        public void AdjustTrust(int delta) => Trust = Clamp(Trust + delta, MinTrust, MaxTrust);

        /// <summary>Moves health by a delta (predation/injury); clamps at 0 and 100.</summary>
        public void AdjustHealth(int delta) => Health = Clamp(Health + delta, MinHealth, MaxHealth);

        /// <summary>Bonds (or un-bonds, with null) the animal to a player or NPC.</summary>
        public void SetOwner(ActorId? owner)
        {
            if (owner.HasValue && !owner.Value.IsValid)
                throw new ArgumentException("A bonded owner must be a valid actor.", nameof(owner));
            Owner = owner;
        }

        /// <summary>
        /// Moves the animal to a new location (seasonal ranging). The herd keeps its
        /// identity: ID, species, trust, owner, age and health are untouched.
        /// </summary>
        public void MoveTo(LocationId location)
        {
            if (!location.IsValid)
                throw new ArgumentException("An animal needs a valid location.", nameof(location));
            Location = location;
        }

        /// <summary>
        /// Records that a taming interaction happened on the given game day.
        /// </summary>
        public void RecordInteraction(long day)
        {
            if (day < 0)
                throw new ArgumentOutOfRangeException(nameof(day), "Game day must not be negative.");
            LastInteractionDay = day;
        }
    }
}
