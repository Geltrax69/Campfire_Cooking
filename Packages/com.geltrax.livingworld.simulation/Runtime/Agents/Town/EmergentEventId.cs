using System;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Agents
{
    /// <summary>
    /// Strongly-typed ID for the 10 emergent events (P5-03, TOWN.md section 3).
    /// Events fire only when world conditions are met, never scripted.
    /// </summary>
    public readonly struct EmergentEventId : IEquatable<EmergentEventId>, IComparable<EmergentEventId>
    {
        public static readonly EmergentEventId FoodShortage = new EmergentEventId("event_food_shortage");
        public static readonly EmergentEventId WolfAttack = new EmergentEventId("event_wolf_attack");
        public static readonly EmergentEventId Festival = new EmergentEventId("event_festival");
        public static readonly EmergentEventId Fire = new EmergentEventId("event_fire");
        public static readonly EmergentEventId TheftWave = new EmergentEventId("event_theft_wave");
        public static readonly EmergentEventId MerchantArrival = new EmergentEventId("event_merchant_arrival");
        public static readonly EmergentEventId Fever = new EmergentEventId("event_fever");
        public static readonly EmergentEventId Drought = new EmergentEventId("event_drought");
        public static readonly EmergentEventId WheelFailure = new EmergentEventId("event_wheel_failure");
        public static readonly EmergentEventId BridgeProject = new EmergentEventId("event_bridge_project");

        private readonly string _value;

        public EmergentEventId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Event ID must not be blank.", nameof(value));
            _value = value;
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(_value);
        public string Value => _value ?? string.Empty;

        public bool Equals(EmergentEventId other) =>
            string.Equals(_value, other._value, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is EmergentEventId other && Equals(other);

        public override int GetHashCode() =>
            _value != null ? _value.GetHashCode() : 0;

        public int CompareTo(EmergentEventId other) =>
            string.CompareOrdinal(_value, other._value);

        public static bool operator ==(EmergentEventId left, EmergentEventId right) => left.Equals(right);
        public static bool operator !=(EmergentEventId left, EmergentEventId right) => !left.Equals(right);

        public override string ToString() => Value;
    }
}
