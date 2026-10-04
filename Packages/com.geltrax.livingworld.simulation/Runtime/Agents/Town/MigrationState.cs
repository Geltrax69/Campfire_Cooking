using System;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Immutable snapshot of migration state for save/load (P5-02).
    /// </summary>
    public sealed class MigrationSnapshot
    {
        public MigrationSnapshot(int additionalVillagers, int additionalHouseholds,
            int additionalSoundRoofs, long lastInMigrationSeason, long lastOutMigrationYear)
        {
            if (additionalVillagers < 0) throw new ArgumentOutOfRangeException(nameof(additionalVillagers));
            if (additionalHouseholds < 0) throw new ArgumentOutOfRangeException(nameof(additionalHouseholds));
            if (additionalSoundRoofs < 0 || additionalSoundRoofs > additionalHouseholds)
                throw new ArgumentException("Sound roofs cannot exceed households.", nameof(additionalSoundRoofs));
            AdditionalVillagers = additionalVillagers;
            AdditionalHouseholds = additionalHouseholds;
            AdditionalSoundRoofs = additionalSoundRoofs;
            LastInMigrationSeason = lastInMigrationSeason;
            LastOutMigrationYear = lastOutMigrationYear;
        }

        public int AdditionalVillagers { get; }
        public int AdditionalHouseholds { get; }
        public int AdditionalSoundRoofs { get; }
        public long LastInMigrationSeason { get; }
        public long LastOutMigrationYear { get; }
    }

    /// <summary>
    /// Tracks village growth and decline from migration (P5-02, TOWN.md section 2).
    /// Newcomers are background villagers (the 20 simulated NPCs stay fixed per the
    /// prototype); the town-stats calculator adds these counts to population and
    /// housing. At most one in-migration per season; out-migration is checked once
    /// per spring. All changes are validated; Capture/Restore support save/load.
    /// </summary>
    public sealed class MigrationState
    {
        public MigrationState()
        {
        }

        /// <summary>Background villagers added by in-migration (beyond the P5-01 base of 100).</summary>
        public int AdditionalBackgroundVillagers { get; private set; }

        /// <summary>Background households added by in-migration.</summary>
        public int AdditionalBackgroundHouseholds { get; private set; }

        /// <summary>Sound roofs among the added households (newcomers arrive housed).</summary>
        public int AdditionalBackgroundSoundRoofs { get; private set; }

        /// <summary>Absolute season index of the last in-migration; -1 if never.</summary>
        public long LastInMigrationSeason { get; private set; } = -1;

        /// <summary>Absolute year index of the last spring out-migration check; -1 if never.</summary>
        public long LastOutMigrationYear { get; private set; } = -1;

        /// <summary>Records one arriving household (3-5 people, housed).</summary>
        public void RecordInMigration(int villagers, int households, long seasonIndex)
        {
            if (villagers <= 0) throw new ArgumentOutOfRangeException(nameof(villagers));
            if (households <= 0) throw new ArgumentOutOfRangeException(nameof(households));
            if (seasonIndex < 0) throw new ArgumentOutOfRangeException(nameof(seasonIndex));
            AdditionalBackgroundVillagers += villagers;
            AdditionalBackgroundHouseholds += households;
            // Newcomers only arrive when housing has slack (the stat gate), so they
            // arrive into sound roofs.
            AdditionalBackgroundSoundRoofs += households;
            LastInMigrationSeason = seasonIndex;
        }

        /// <summary>
        /// Marks a season's in-migration check as done without any arrival, so a
        /// failed gate does not retry every tick for the rest of the season.
        /// </summary>
        public void NoteSeasonChecked(long seasonIndex)
        {
            if (seasonIndex < 0) throw new ArgumentOutOfRangeException(nameof(seasonIndex));
            if (seasonIndex > LastInMigrationSeason)
                LastInMigrationSeason = seasonIndex;
        }

        /// <summary>Adds villagers to an already-recorded season (used by the system after the gate passes).</summary>
        public void AddHousehold(int villagers, long seasonIndex)
        {
            if (villagers <= 0) throw new ArgumentOutOfRangeException(nameof(villagers));
            if (seasonIndex < 0) throw new ArgumentOutOfRangeException(nameof(seasonIndex));
            AdditionalBackgroundVillagers += villagers;
            AdditionalBackgroundHouseholds += 1;
            AdditionalBackgroundSoundRoofs += 1;
            LastInMigrationSeason = seasonIndex;
        }

        /// <summary>Marks the spring out-migration check done for this year.</summary>
        public void RecordOutMigrationCheck(long yearIndex)
        {
            if (yearIndex < 0) throw new ArgumentOutOfRangeException(nameof(yearIndex));
            LastOutMigrationYear = yearIndex;
        }

        public MigrationSnapshot Capture() => new MigrationSnapshot(
            AdditionalBackgroundVillagers, AdditionalBackgroundHouseholds,
            AdditionalBackgroundSoundRoofs, LastInMigrationSeason, LastOutMigrationYear);

        public void Restore(MigrationSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            AdditionalBackgroundVillagers = snapshot.AdditionalVillagers;
            AdditionalBackgroundHouseholds = snapshot.AdditionalHouseholds;
            AdditionalBackgroundSoundRoofs = snapshot.AdditionalSoundRoofs;
            LastInMigrationSeason = snapshot.LastInMigrationSeason;
            LastOutMigrationYear = snapshot.LastOutMigrationYear;
        }
    }
}
