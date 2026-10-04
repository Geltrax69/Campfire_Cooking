namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// One meaningful taming interaction with an animal (P4-03,
    /// docs/design/ANIMALS.md section 1 and Content/animals/species.json
    /// "taming" builds/breaks). Each species only accepts its own interactions —
    /// offering a species an interaction it has no rule for is a caller bug and
    /// throws. Positive interactions build trust (one meaningful gain per animal
    /// per day); negative ones are cruelty, fright or neglect and always apply
    /// in full.
    /// </summary>
    public enum TamingInteraction
    {
        /// <summary>Chicken: hand-feeding corn, +8 trust per day.</summary>
        HandFeeding,
        /// <summary>Chicken: gentle handling, +5 trust per day.</summary>
        GentleHandling,
        /// <summary>Chicken: rough handling, -20 trust.</summary>
        RoughHandling,
        /// <summary>Chicken: chased by dogs, -30 trust.</summary>
        ChasedByDogs,
        /// <summary>Piglet: regular feeding and calm presence, +6 trust per day.</summary>
        Feeding,
        /// <summary>Piglet: scratching favorite spots, +10 trust per day.</summary>
        Scratching,
        /// <summary>Piglet (-25) and wolf pup (-50): struck.</summary>
        Struck,
        /// <summary>Piglet: starved, -40 trust. Wolf pup: instant break (see StarvedBreak).</summary>
        Starved,
        /// <summary>Fawn: winter feeding at a fixed station, +4 trust per day.</summary>
        WinterFeeding,
        /// <summary>Fawn: calm presence, +2 trust per day.</summary>
        CalmPresence,
        /// <summary>Fawn: sudden chase or loud noise, -30 trust.</summary>
        ChaseOrNoise,
        /// <summary>Wolf pup: feeding meat, +5 trust per day.</summary>
        FeedingMeat,
        /// <summary>Wolf pup: play and training, +5 trust per day.</summary>
        PlayAndTraining,
        /// <summary>Wolf pup: showing calm strength (standing ground without fear), +8 trust.</summary>
        CalmStrength,
        /// <summary>Brambleback: porridge or oats left at the burrow, +12 trust per day.</summary>
        Porridge,
        /// <summary>Brambleback: sitting quietly nearby, +6 trust per day.</summary>
        SittingQuietly,
        /// <summary>Brambleback: loud disturbance, -15 trust.</summary>
        LoudDisturbance,
    }
}
