using System;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Identifies a recipe by its ordinal content key (e.g. "recipe_campfire_stew"); default is invalid.</summary>
    public readonly struct RecipeId : IEquatable<RecipeId>, IComparable<RecipeId>
    {
        public RecipeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A recipe ID must not be null, empty or whitespace.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => Value != null;
        public bool Equals(RecipeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is RecipeId other && Equals(other);
        public override int GetHashCode()
        {
            if (Value == null) return 0;
            // FNV-1a over UTF-16 code units, matching the Core content-ID hash.
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in Value) hash = (hash ^ character) * 16777619;
                return (int)hash;
            }
        }
        public int CompareTo(RecipeId other) => string.CompareOrdinal(Value, other.Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(RecipeId left, RecipeId right) => left.Equals(right);
        public static bool operator !=(RecipeId left, RecipeId right) => !left.Equals(right);
        public static bool operator <(RecipeId left, RecipeId right) => left.CompareTo(right) < 0;
        public static bool operator >(RecipeId left, RecipeId right) => left.CompareTo(right) > 0;
        public static bool operator <=(RecipeId left, RecipeId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(RecipeId left, RecipeId right) => left.CompareTo(right) >= 0;
    }
}
