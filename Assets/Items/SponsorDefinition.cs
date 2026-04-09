using UnityEngine;
using DCC.Core.Tags;

namespace DCC.Items
{
    /// <summary>
    /// Defines a sponsor that gifts items to crawlers who reach viewer milestones.
    ///
    /// In the DCC books:
    ///   - Sponsors are alien entities/corporations/governments watching the dungeon show.
    ///   - They sponsor crawlers whose viewer count reaches certain thresholds.
    ///   - Sponsors favor certain playstyles (aggressive, clever, charismatic, etc.).
    ///   - Gifts range from gold to rare items to loot boxes.
    ///   - Higher-tier sponsors require more viewers and give better rewards.
    ///   - Some sponsors have preferred tags (e.g., a sponsor that loves explosions
    ///     might target crawlers with [Explosives] or [Pyro] tags).
    ///
    /// Example sponsors:
    ///
    ///   "Bopca Industries":
    ///     Tier: 1
    ///     ViewerThreshold: 10,000
    ///     GoldGift: 100
    ///     GiftItems: [Good Healing Potion x3]
    ///     → Basic starter sponsor, everyone qualifies early.
    ///
    ///   "The Daughters of Nitara":
    ///     Tier: 3
    ///     ViewerThreshold: 500,000
    ///     PreferredTags: [Magical, Arcane]
    ///     GiftLootBox: Gold Adventurer Box
    ///     GoldGift: 1000
    ///     → High-tier sponsor favoring spellcasters.
    ///
    ///   "Skull Empire Entertainment":
    ///     Tier: 5
    ///     ViewerThreshold: 5,000,000
    ///     PreferredTags: [PvpKiller, Aggressive]
    ///     GiftLootBox: Platinum Savage Box
    ///     GoldGift: 10000
    ///     → Whale sponsor that loves violence and drama.
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Sponsor", fileName = "Sponsor_New")]
    public class SponsorDefinition : ScriptableObject
    {
        [field: SerializeField] public string SponsorName { get; private set; }
        [field: SerializeField] public Sprite Logo { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Requirements")]
        [field: SerializeField, Tooltip("Viewer count the crawler must reach to attract this sponsor.")]
        public long ViewerThreshold { get; private set; } = 10000;

        [field: SerializeField, Tooltip("Sponsor tier (higher = better rewards, checked in ascending order).")]
        public int Tier { get; private set; } = 1;

        [field: SerializeField, Tooltip("Tags the sponsor prefers. If set, crawler must have at least one to qualify.")]
        public TagDefinition[] PreferredTags { get; private set; }

        [Header("Gifts")]
        [field: SerializeField] public int GoldGift { get; private set; }
        [field: SerializeField] public ItemDefinition[] GiftItems { get; private set; }
        [field: SerializeField] public LootBoxDefinition GiftLootBox { get; private set; }
    }
}
