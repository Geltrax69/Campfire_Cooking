using System;

namespace LivingWorld.Simulation.Core
{
    /// <summary>A queued command with the game minute it becomes eligible, kept opaque for restore.</summary>
    /// <remarks>Core never inspects the concrete command type; persistence decodes and rehydrates it.</remarks>
    internal readonly struct PendingCommandEntry
    {
        internal PendingCommandEntry(IWorldCommand command, GameTime eligibleMinute)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            Command = command;
            EligibleMinute = eligibleMinute;
        }

        public IWorldCommand Command { get; }
        public GameTime EligibleMinute { get; }
    }
}
