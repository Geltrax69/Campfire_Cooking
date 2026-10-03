using System;
using System.Collections.Generic;

namespace LivingWorld.Simulation.Core
{
    /// <summary>Runs single-threaded, fixed-order ticks and permanently stops after a failed tick.</summary>
    public sealed class World
    {
        private readonly List<Registration> _systems = new List<Registration>();
        private bool _started;
        private bool _ticking;
        private bool _registering;

        public WorldState State { get; }
        public bool IsFaulted { get; private set; }

        public World(WorldState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void RegisterSystem(IWorldSystem system)
        {
            if (_started || _registering)
                throw new InvalidOperationException("Registration is closed or already in progress.");
            if (system == null) throw new ArgumentNullException(nameof(system));

            // Property getters are caller code: prevent them from reentering registration or Tick.
            _registering = true;
            try
            {
                string id = system.Id;
                if (string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException("A system ID must be nonempty and non-whitespace.", nameof(system));
                SimulationPhase phase = system.Phase;
                if (phase < SimulationPhase.Commands || phase > SimulationPhase.Memory)
                    throw new ArgumentOutOfRangeException(nameof(system), "Unknown simulation phase.");

                int insertionIndex = _systems.Count;
                for (int i = 0; i < _systems.Count; i++)
                {
                    Registration existing = _systems[i];
                    int idOrder = string.CompareOrdinal(id, existing.Id);
                    if (idOrder == 0)
                        throw new ArgumentException("System IDs must be unique across phases.", nameof(system));
                    if (insertionIndex == _systems.Count &&
                        (phase < existing.Phase || (phase == existing.Phase && idOrder < 0)))
                        insertionIndex = i;
                }
                // Capture keys and order during setup; ticks never sort, allocate or reread keys.
                _systems.Insert(insertionIndex, new Registration(system, id, phase));
            }
            finally
            {
                _registering = false;
            }
        }

        public void Tick()
        {
            if (IsFaulted) throw new InvalidOperationException("A faulted world cannot continue ticking.");
            if (_ticking || _registering)
                throw new InvalidOperationException("Cannot tick during another tick or registration.");

            _started = true;
            _ticking = true;
            try
            {
                // Checked advancement happens before any system can observe or modify state.
                State.Clock = State.Clock.Advance(1);
                for (int i = 0; i < _systems.Count; i++) _systems[i].System.Tick(State);
            }
            catch
            {
                // Earlier systems may already have changed state; resuming would hide a partial tick.
                IsFaulted = true;
                throw;
            }
            finally
            {
                _ticking = false;
            }
        }

        private readonly struct Registration
        {
            public IWorldSystem System { get; }
            public string Id { get; }
            public SimulationPhase Phase { get; }
            public Registration(IWorldSystem system, string id, SimulationPhase phase)
            {
                System = system;
                Id = id;
                Phase = phase;
            }
        }
    }
}
