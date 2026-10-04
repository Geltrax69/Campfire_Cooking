namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// What a <see cref="TamingSystem.Interact"/> call did. Rejections leave the
    /// animal untouched; every other outcome applied a trust change.
    /// </summary>
    public enum TamingOutcome
    {
        /// <summary>Trust changed; no bond formed or broken.</summary>
        TrustChanged,
        /// <summary>Trust reached the species' bond threshold; the animal is now bonded to the actor.</summary>
        Bonded,
        /// <summary>
        /// Trust fell below the species' break threshold (wolf: 30, pig: 40):
        /// the bond is broken, the owner cleared and trust reset to 10. When a
        /// wolf breaks through cruelty it may turn — that reckoning is future work.
        /// </summary>
        BondBroken,
        /// <summary>
        /// A wolf pup was starved: instant break whatever the trust was — the
        /// owner is cleared and trust zeroed (docs/design/ANIMALS.md).
        /// </summary>
        StarvedBreak,
        /// <summary>
        /// Rejected: this animal can never be tamed — wild boars, stags, adult
        /// wolves, sows, and species with no taming rules. Nothing changed.
        /// </summary>
        RejectedUntameable,
        /// <summary>
        /// Rejected: building trust with a wolf pup needs the council's
        /// approval (a public-safety decision; real voting is Phase 5 town work).
        /// Nothing changed.
        /// </summary>
        RejectedNeedsApproval,
    }
}
