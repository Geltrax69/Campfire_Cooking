namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// The result of one <see cref="TamingSystem.Interact"/> call: what happened,
    /// the trust actually gained or lost (after skill scaling and the 0-100
    /// clamp), and the animal's trust afterwards.
    /// </summary>
    public sealed class TamingResult
    {
        public TamingResult(TamingOutcome outcome, int trustDelta, int trust)
        {
            Outcome = outcome;
            TrustDelta = trustDelta;
            Trust = trust;
        }

        public TamingOutcome Outcome { get; }
        /// <summary>Signed trust actually applied (post-clamp); 0 on rejections.</summary>
        public int TrustDelta { get; }
        /// <summary>The animal's trust after the interaction.</summary>
        public int Trust { get; }
    }
}
