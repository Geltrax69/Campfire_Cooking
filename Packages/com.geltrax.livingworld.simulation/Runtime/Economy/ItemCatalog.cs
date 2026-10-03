using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>Immutable gameplay metadata for one aggregate item type.</summary>
    public sealed class ItemDefinition
    {
        public ItemTypeId Id { get; }
        public string Name { get; }
        public string Category { get; }
        public int BaseValue { get; }
        public int Phase { get; }
        public int? HungerEffect { get; }
        public int? HealthEffect { get; }
        public int? SocialEffect { get; }

        public ItemDefinition(
            ItemTypeId id,
            string name,
            string category,
            int baseValue,
            int phase,
            int? hungerEffect = null,
            int? healthEffect = null,
            int? socialEffect = null)
        {
            if (!id.IsValid) throw new ArgumentException("An item type ID must be valid.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("A category is required.", nameof(category));
            if (baseValue < 0) throw new ArgumentOutOfRangeException(nameof(baseValue));
            if (phase <= 0) throw new ArgumentOutOfRangeException(nameof(phase));

            Id = id;
            Name = name;
            Category = category;
            BaseValue = baseValue;
            Phase = phase;
            HungerEffect = hungerEffect;
            HealthEffect = healthEffect;
            SocialEffect = socialEffect;
        }
    }

    /// <summary>Validated item definitions exposed in deterministic ordinal ID order.</summary>
    public sealed class ItemCatalog
    {
        private readonly Dictionary<ItemTypeId, ItemDefinition> _byId;

        public ItemCatalog(IEnumerable<ItemDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            _byId = new Dictionary<ItemTypeId, ItemDefinition>();
            foreach (ItemDefinition definition in definitions)
            {
                if (definition == null)
                    throw new ArgumentException("Catalog definitions must not contain null.", nameof(definitions));
                if (!_byId.TryAdd(definition.Id, definition))
                    throw new ArgumentException("Catalog item IDs must be unique.", nameof(definitions));
            }

            ItemDefinition[] ordered = _byId.Values.OrderBy(item => item.Id).ToArray();
            Items = new ReadOnlyCollection<ItemDefinition>(ordered);
        }

        public IReadOnlyList<ItemDefinition> Items { get; }

        public ItemDefinition this[ItemTypeId id]
        {
            get
            {
                if (!id.IsValid) throw new ArgumentException("An item type ID must be valid.", nameof(id));
                if (!_byId.TryGetValue(id, out ItemDefinition definition))
                    throw new ArgumentException("The item type is not present in this catalog.", nameof(id));
                return definition;
            }
        }

        internal void Require(ItemTypeId id)
        {
            _ = this[id];
        }
    }
}
