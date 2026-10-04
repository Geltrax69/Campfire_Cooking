using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace LivingWorld.Game.Bridge
{
    /// <summary>Detached display data; no simulation objects escape the bridge.</summary>
    public sealed class DisplaySnapshot
    {
        public long Minute { get; }
        public int ShopApples { get; }
        public int ApplePriceCopper { get; }
        public int PlayerApples { get; }
        public int PlayerCopper { get; }
        public IReadOnlyList<NpcDisplay> Npcs { get; }
        internal DisplaySnapshot(long minute, int stock, int price, int apples, int copper, List<NpcDisplay> npcs)
        { Minute = minute; ShopApples = stock; ApplePriceCopper = price; PlayerApples = apples; PlayerCopper = copper; Npcs = new ReadOnlyCollection<NpcDisplay>(npcs.ToArray()); }
    }
    /// <summary>Immutable NPC identity and knowledge phrasing for presentation.</summary>
    public sealed class NpcDisplay
    {
        public string Id { get; }
        public string Name { get; }
        public string LocationId { get; }
        public string Activity { get; }
        public string Dialogue { get; }
        internal NpcDisplay(string id, string name, string location, string activity, string dialogue)
        { Id = id; Name = name; LocationId = location; Activity = activity; Dialogue = dialogue; }
    }
}
