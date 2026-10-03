using System;

namespace LivingWorld.Simulation.Persistence
{
    /// <summary>Thrown when a save document cannot be loaded (never a half-built world).</summary>
    public sealed class LoadException : Exception
    {
        public LoadException(string message) : base(message) { }
        public LoadException(string message, Exception inner) : base(message, inner) { }
    }
}
