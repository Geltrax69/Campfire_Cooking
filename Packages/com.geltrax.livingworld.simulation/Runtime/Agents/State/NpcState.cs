using System;
using System.Collections.Generic;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Holds one NPC's mutable simulation state separately from its definition.</summary>
    public sealed class NpcState
    {
        private readonly List<NpcId> _children = new List<NpcId>();

        public NpcState(NpcDefinition definition, int initialHunger, int initialEnergy, int initialSocial)
            : this(definition ?? throw new ArgumentNullException(nameof(definition)),
                new NeedState(initialHunger, initialEnergy, initialSocial), false, null)
        {
        }

        private NpcState(NpcDefinition definition, NeedState needs, bool isSleeping, NpcIntention intention)
        {
            Definition = definition;
            Needs = needs;
            IsSleeping = isSleeping;
            CurrentIntention = intention;
            Skills = new SkillStore();
            Happiness = NeutralHappiness;
            Age = definition.Age;
            IsDeceased = false;
        }

        /// <summary>
        /// Rebuilds exact mutable NPC state for save/load from an approved definition, exact needs
        /// (sixtieth remainders included), the sleeping flag and the current intention.
        /// Everything is validated before anything is built; a failed restore throws
        /// without mutating its inputs.
        /// </summary>
        /// <remarks>
        /// The sleeping flag and the intention are restored independently, without
        /// requiring them to agree: the running world sets IsSleeping from the
        /// NPC's schedule every tick (NeedsSystem) while the intention driver is
        /// still unwired, so a night-time save honestly carries IsSleeping=true
        /// with no Sleep intention. IsSleeping is recomputed from the schedule on
        /// the next tick, so the disagreement is transient and self-healing.
        /// </remarks>
        internal static NpcState Restore(NpcDefinition definition, NeedState needs,
            bool isSleeping, NpcIntention intention)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return Restore(definition, needs, isSleeping, intention, definition.Age, false);
        }

        /// <summary>
        /// Rebuilds NPC state with an exact age and deceased flag (P7-01). The
        /// four-argument overload keeps older callers (including Persistence)
        /// compiling: it restores the Content age with the NPC alive.
        /// </summary>
        internal static NpcState Restore(NpcDefinition definition, NeedState needs,
            bool isSleeping, NpcIntention intention, int age, bool isDeceased)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (needs == null) throw new ArgumentNullException(nameof(needs));
            if (age < 0) throw new ArgumentOutOfRangeException(nameof(age), "Age must not be negative.");
            // Copy the exact sixtieths so the restored NPC owns its needs and later caller
            // changes to the snapshot cannot leak into the world.
            var exact = NeedState.FromSixtieths(needs.HungerSixtieths, needs.EnergySixtieths,
                needs.SocialSixtieths);
            var npc = new NpcState(definition, exact, isSleeping, intention);
            npc.Age = age;
            npc.IsDeceased = isDeceased;
            return npc;
        }

        public NpcDefinition Definition { get; }
        public NeedState Needs { get; }
        public bool IsSleeping { get; set; }
        public NpcIntention CurrentIntention { get; private set; }
        /// <summary>This NPC's skill states (levels earned through practice and teaching).</summary>
        public SkillStore Skills { get; }
        /// <summary>
        /// Age in years (P7-01). Starts at the Content definition's age and advances
        /// one year per birthday via the AgingSystem. Mutable world truth, unlike
        /// the immutable approved definition.
        /// </summary>
        public int Age { get; private set; }
        /// <summary>
        /// True once the NPC has died of old age (P7-01). The body stays in the
        /// registry so inheritance (P7-03) can find it; systems must skip the dead.
        /// </summary>
        public bool IsDeceased { get; private set; }
        /// <summary>Child (0-14), Adult (15-59), or Elder (60+), computed from age.</summary>
        public LifeStage LifeStage =>
            Age < 15 ? LifeStage.Child : Age < 60 ? LifeStage.Adult : LifeStage.Elder;
        /// <summary>
        /// The NPC's mother and father (P7-02). Set at birth (or from approved
        /// Content by FamilySetup) and never changed to different parents; either
        /// may be absent when the Content names only one.
        /// </summary>
        public NpcId? MotherId { get; private set; }
        public NpcId? FatherId { get; private set; }
        /// <summary>The NPC's spouse, if any (P7-02, from husband/wife Content).</summary>
        public NpcId? PartnerId { get; private set; }
        /// <summary>The NPC's children, in the order the links were made.</summary>
        public IReadOnlyList<NpcId> ChildrenIds => _children.AsReadOnly();
        /// <summary>The household the NPC lives in (P7-02); null until assigned.</summary>
        public HouseholdId? HouseholdId { get; private set; }
        /// <summary>
        /// The NPC named in the will to receive the whole estate (P7-03).
        /// Overrides the normal heir priority when set and the named heir is
        /// alive; a real will can name anyone, not just family. Persistence
        /// note: like the other P7 family fields, the JSON saver does not yet
        /// write this — wire it when the saver learns the P7 fields.
        /// </summary>
        public NpcId? DesignatedHeirId { get; private set; }

        /// <summary>
        /// Links parents (P7-02). Either side may be null to fill in one parent at
        /// a time; changing an already-set parent to a different NPC throws,
        /// because parents are set at birth and never change.
        /// </summary>
        internal void SetParents(NpcId? motherId, NpcId? fatherId)
        {
            if (motherId.HasValue)
            {
                ValidateParent(motherId.Value, nameof(motherId));
                if (MotherId.HasValue && MotherId.Value != motherId.Value)
                    throw new InvalidOperationException("Parents are set at birth and never change.");
                MotherId = motherId;
            }
            if (fatherId.HasValue)
            {
                ValidateParent(fatherId.Value, nameof(fatherId));
                if (FatherId.HasValue && FatherId.Value != fatherId.Value)
                    throw new InvalidOperationException("Parents are set at birth and never change.");
                FatherId = fatherId;
            }
            if (MotherId.HasValue && FatherId.HasValue && MotherId.Value == FatherId.Value)
                throw new ArgumentException("Mother and father must be different NPCs.");
        }

        private void ValidateParent(NpcId parentId, string parameterName)
        {
            if (!parentId.IsValid) throw new ArgumentException("A parent ID must be valid.", parameterName);
            if (parentId == Definition.Id)
                throw new ArgumentException("An NPC cannot be its own parent.", parameterName);
        }

        /// <summary>Sets (or clears, with null) the spouse link (P7-02).</summary>
        internal void SetPartner(NpcId? partnerId)
        {
            if (partnerId.HasValue)
            {
                if (!partnerId.Value.IsValid)
                    throw new ArgumentException("A partner ID must be valid.", nameof(partnerId));
                if (partnerId.Value == Definition.Id)
                    throw new ArgumentException("An NPC cannot partner itself.", nameof(partnerId));
            }
            PartnerId = partnerId;
        }

        /// <summary>Adds a child link; adding the same child twice is a no-op.</summary>
        internal void AddChild(NpcId childId)
        {
            if (!childId.IsValid) throw new ArgumentException("A child ID is required.", nameof(childId));
            if (childId == Definition.Id)
                throw new ArgumentException("An NPC cannot be its own child.", nameof(childId));
            if (!_children.Contains(childId)) _children.Add(childId);
        }

        /// <summary>Moves the NPC into a household (P7-02); null clears the link.</summary>
        internal void SetHousehold(HouseholdId? householdId)
        {
            if (householdId.HasValue && !householdId.Value.IsValid)
                throw new ArgumentException("A household ID must be valid.", nameof(householdId));
            HouseholdId = householdId;
        }

        /// <summary>
        /// Names (or clears, with null) the designated heir for the will (P7-03).
        /// An NPC cannot name itself; naming a deceased or unknown NPC is allowed
        /// at will-writing time — distribution skips dead heirs (falling through
        /// to the normal priority) and rejects unknown IDs.
        /// </summary>
        internal void SetDesignatedHeir(NpcId? heirId)
        {
            if (heirId.HasValue)
            {
                if (!heirId.Value.IsValid)
                    throw new ArgumentException("A designated heir must be valid.", nameof(heirId));
                if (heirId.Value == Definition.Id)
                    throw new ArgumentException("An NPC cannot designate itself as heir.", nameof(heirId));
            }
            DesignatedHeirId = heirId;
        }
        /// <summary>
        /// Mood on a 0-100 scale, 50 neutral. Good meals nudge it up, bad ones down
        /// (P3-03, SKILLS.md: a small daily happiness that compounds). Clamped 0-100.
        /// </summary>
        public int Happiness { get; private set; }

        /// <summary>The neutral midpoint of the happiness scale; new NPCs start here.</summary>
        public const int NeutralHappiness = 50;

        /// <summary>
        /// Shifts happiness by a signed amount, clamped to 0-100. Persistence note:
        /// happiness is new in Phase 3 and not yet in the save format — the JSON
        /// saver must write it (see RestoreHappiness) or save/load will reset moods.
        /// </summary>
        internal void AdjustHappiness(int delta)
        {
            int shifted = Happiness + delta;
            if (shifted < 0) shifted = 0;
            if (shifted > 100) shifted = 100;
            Happiness = shifted;
        }

        /// <summary>
        /// Restores an exact happiness value for Persistence (validated 0-100).
        /// </summary>
        internal void RestoreHappiness(int happiness)
        {
            if (happiness < 0 || happiness > 100)
                throw new ArgumentOutOfRangeException(nameof(happiness), "Happiness is 0-100.");
            Happiness = happiness;
        }

        public void AdvanceNeedsOneMinute()
        {
            if (!IsSleeping) Needs.AdvanceOneAwakeMinute(Definition.NeedRates);
        }

        internal void SetIntention(NpcIntention intention)
        {
            CurrentIntention = intention ?? throw new ArgumentNullException(nameof(intention));
            IsSleeping = intention.Kind == ActivityKind.Sleep;
        }

        /// <summary>Turns one year older on a birthday (P7-01, AgingSystem).</summary>
        internal void AdvanceAge()
        {
            if (IsDeceased) throw new InvalidOperationException("The deceased do not age.");
            Age++;
        }

        /// <summary>Marks the NPC deceased from old age (P7-01, AgingSystem).</summary>
        internal void MarkDeceased()
        {
            if (IsDeceased) throw new InvalidOperationException("The NPC is already deceased.");
            IsDeceased = true;
        }
    }
}
