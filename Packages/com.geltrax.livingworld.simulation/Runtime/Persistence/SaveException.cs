using System;

namespace LivingWorld.Simulation.Persistence
{
    /// <summary>Thrown when a world state cannot be serialized (never a partial document).</summary>
    public sealed class SaveException : Exception
    {
        public SaveException(string message) : base(message) { }
        public SaveException(string message, Exception inner) : base(message, inner) { }
    }
}
