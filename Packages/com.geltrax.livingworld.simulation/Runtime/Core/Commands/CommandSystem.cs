using System;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Executes eligible commands in FIFO order; register explicitly before the first tick.</summary>
    public sealed class CommandSystem : IWorldSystem
    {
        public string Id => "core.commands";
        public SimulationPhase Phase => SimulationPhase.Commands;

        public void Tick(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.IsProcessingCommands) throw new InvalidOperationException("Command processing cannot reenter.");
            state.IsProcessingCommands = true;
            try
            {
                // Dequeue before invoking caller code: a partial failure must never be retried.
                while (state.TryDequeueEligibleCommand(out IWorldCommand command)) command.Execute(state);
            }
            finally
            {
                state.IsProcessingCommands = false;
            }
        }
    }
}
