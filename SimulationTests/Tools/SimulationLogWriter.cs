using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Tests.Tools
{
    /// <summary>Writes deterministic human-readable reports from world-truth events.</summary>
    public static class SimulationLogWriter
    {
        private const string Header = "WORLD TRUTH REPORT — facts below do not imply NPC knowledge\n";

        public static void Write(
            TextWriter writer,
            IEnumerable<WorldEvent> events,
            IReadOnlyDictionary<NpcId, string> npcNames = null,
            IReadOnlyDictionary<LocationId, string> locationNames = null,
            IReadOnlyDictionary<ItemTypeId, string> itemNames = null)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (events == null) throw new ArgumentNullException(nameof(events));
            ValidateNames(npcNames, id => id.IsValid, nameof(npcNames));
            ValidateNames(locationNames, id => id.IsValid, nameof(locationNames));
            ValidateNames(itemNames, id => id.IsValid, nameof(itemNames));

            var ordered = new List<WorldEvent>();
            foreach (WorldEvent entry in events)
            {
                if (entry == null) throw new ArgumentException("Events must not contain null.", nameof(events));
                ordered.Add(entry);
            }
            ordered.Sort(CompareEvents);

            writer.Write(Header);
            WriteSummary(writer, ordered);
            writer.Write("\n");
            foreach (WorldEvent entry in ordered) WriteEvent(writer, entry, npcNames, locationNames, itemNames);
        }

        private static void WriteSummary(TextWriter writer, IReadOnlyList<WorldEvent> events)
        {
            if (events.Count == 0)
            {
                writer.Write("Summary: 0 events; no game days covered.\n");
                return;
            }

            var counts = new int[Enum.GetValues(typeof(WorldEventType)).Length];
            foreach (WorldEvent entry in events) counts[(int)entry.Type]++;
            long firstDay = events[0].Time.Day;
            long lastDay = events[events.Count - 1].Time.Day;
            writer.Write("Summary: ");
            writer.Write(events.Count.ToString(CultureInfo.InvariantCulture));
            writer.Write(events.Count == 1 ? " event across " : " events across ");
            writer.Write(DayRange(firstDay, lastDay));
            writer.Write("; ");
            bool wroteCount = false;
            foreach (WorldEventType type in Enum.GetValues(typeof(WorldEventType)))
            {
                int count = counts[(int)type];
                if (count == 0) continue;
                if (wroteCount) writer.Write(", ");
                writer.Write(type.ToString());
                writer.Write(" ");
                writer.Write(count.ToString(CultureInfo.InvariantCulture));
                wroteCount = true;
            }
            writer.Write(".\n");
        }

        private static void WriteEvent(TextWriter writer, WorldEvent entry,
            IReadOnlyDictionary<NpcId, string> npcNames,
            IReadOnlyDictionary<LocationId, string> locationNames,
            IReadOnlyDictionary<ItemTypeId, string> itemNames)
        {
            var line = new StringBuilder();
            line.Append("Day ").Append(entry.Time.Day.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(entry.Time.Hour.ToString("00", CultureInfo.InvariantCulture)).Append(':')
                .Append(entry.Time.Minute.ToString("00", CultureInfo.InvariantCulture)).Append(" — ")
                .Append(entry.Type.ToString());
            if (entry.Actor.HasValue) AddField(line, "actor", ActorName(entry.Actor.Value, npcNames));
            AddField(line, "location", Name(entry.Location, locationNames));
            if (entry.Targets.Count > 0)
                AddField(line, "targets", string.Join(", ", entry.Targets.Select(target => ActorName(target, npcNames))));
            if (entry.ItemType.HasValue) AddField(line, "item", Name(entry.ItemType.Value, itemNames));
            if (entry.Quantity.HasValue) AddField(line, "quantity", entry.Quantity.Value.ToString(CultureInfo.InvariantCulture));
            if (entry.Copper.HasValue) AddField(line, "copper", entry.Copper.Value.ToString(CultureInfo.InvariantCulture));
            AddField(line, "visibility", entry.Visibility.ToString());
            line.Append('\n');
            writer.Write(line.ToString());
        }

        private static void AddField(StringBuilder line, string label, string value)
        {
            line.Append(" | ").Append(label).Append(": ").Append(value);
        }

        private static string ActorName(ActorId actor, IReadOnlyDictionary<NpcId, string> names)
        {
            return actor.IsPlayer ? "Player" : Name(actor.Npc.Value, names);
        }

        private static string Name<T>(T id, IReadOnlyDictionary<T, string> names)
        {
            if (names != null && names.TryGetValue(id, out string displayName)) return Clean(displayName);
            return Clean(id.ToString());
        }

        private static void ValidateNames<T>(IReadOnlyDictionary<T, string> names, Func<T, bool> validId, string parameter)
        {
            if (names == null) return;
            foreach (KeyValuePair<T, string> pair in names)
                if (!validId(pair.Key) || pair.Value == null || Clean(pair.Value).Length == 0)
                    throw new ArgumentException("Display-name entries require valid IDs and nonblank names.", parameter);
        }

        private static string Clean(string value)
        {
            var result = new StringBuilder(value.Length);
            bool pendingSpace = false;
            foreach (char character in value)
            {
                if (char.IsWhiteSpace(character) || char.IsControl(character))
                {
                    pendingSpace = result.Length > 0;
                    continue;
                }
                if (pendingSpace) result.Append(' ');
                result.Append(character);
                pendingSpace = false;
            }
            return result.ToString();
        }

        private static string DayRange(long first, long last)
        {
            string start = "Day " + first.ToString(CultureInfo.InvariantCulture);
            return first == last ? start : start + "–Day " + last.ToString(CultureInfo.InvariantCulture);
        }

        private static int CompareEvents(WorldEvent left, WorldEvent right)
        {
            int time = left.Time.CompareTo(right.Time);
            return time != 0 ? time : left.Id.CompareTo(right.Id);
        }
    }
}
