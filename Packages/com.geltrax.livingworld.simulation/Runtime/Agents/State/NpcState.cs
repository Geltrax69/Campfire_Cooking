using System;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>Holds one NPC's mutable simulation state separately from its definition.</summary>
    public sealed class NpcState
    {
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
        }

        /// <summary>
        /// Rebuilds exact mutable NPC state for save/load from an approved definition, exact needs
        /// (sixtieth remainders included), the sleeping flag and the current intention.
        /// Everything is validated before anything is built; a failed restore throws
        /// without mutating its inputs.
        /// </summary>
        internal static NpcState Restore(NpcDefinition definition, NeedState needs,
            bool isSleeping, NpcIntention intention)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (needs == null) throw new ArgumentNullException(nameof(needs));
            bool sleepsByIntention = intention != null && intention.Kind == ActivityKind.Sleep;
            if (isSleeping != sleepsByIntention)
                throw new ArgumentException(
                    "The sleeping flag must agree with the restored intention: sleeping requires " +
                    "a Sleep intention, being awake requires no intention or a non-Sleep intention.",
                    nameof(isSleeping));
            // Copy the exact sixtieths so the restored NPC owns its needs and later caller
            // changes to the snapshot cannot leak into the world.
            var exact = NeedState.FromSixtieths(needs.HungerSixtieths, needs.EnergySixtieths,
                needs.SocialSixtieths);
            return new NpcState(definition, exact, isSleeping, intention);
        }

        public NpcDefinition Definition { get; }
        public NeedState Needs { get; }
        public bool IsSleeping { get; set; }
        public NpcIntention CurrentIntention { get; private set; }

        public void AdvanceNeedsOneMinute()
        {
            if (!IsSleeping) Needs.AdvanceOneAwakeMinute(Definition.NeedRates);
        }

        internal void SetIntention(NpcIntention intention)
        {
            CurrentIntention = intention ?? throw new ArgumentNullException(nameof(intention));
            IsSleeping = intention.Kind == ActivityKind.Sleep;
        }
    }
}
