using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;
using DCC.Items;

namespace DCC.Core.Zones
{
    /// <summary>
    /// Defines a single floor of the dungeon. Pure data — no logic.
    ///
    /// In the DCC books:
    ///   - 18 floors total, from Earth's surface down to its core.
    ///   - Floor 1 is largest; Floor 18 is smallest.
    ///   - Floors 1–2 are tutorial levels.
    ///   - Floor 3 (and every 3rd floor) are part of the overarching storyline.
    ///   - Each floor has a countdown timer (multiple real-time days in the books,
    ///     tuned shorter for gameplay).
    ///   - Stairwells to next floor: each floor has half (rounded up) the stairwells
    ///     of the previous floor.
    ///   - Entering a stairwell 6+ hours early puts you in stasis until the next
    ///     floor starts.
    ///   - Failing to reach a stairwell before the timer expires = death (floor collapses).
    ///
    /// Example floors:
    ///
    ///   Floor 1 — The Staircase (Tutorial):
    ///     CountdownDuration: 600 (10 min for gameplay, days in books)
    ///     StairwellCount: 64
    ///     FloorTags: [Tutorial, Well_Lit]
    ///     MaxLootTier: Bronze
    ///     MobDefs: [Goblin, Rat]
    ///
    ///   Floor 3 — The Tutorial Guild Hall:
    ///     CountdownDuration: 900
    ///     StairwellCount: 16
    ///     FloorTags: [Story, Guild_Hall]
    ///     IsClassSelectionFloor: true
    ///     → Race and Class chosen here
    ///
    ///   Floor 6 — Subclass Selection:
    ///     StairwellCount: 8
    ///     FloorTags: [Story, Subclass]
    ///     IsSubclassSelectionFloor: true
    ///
    ///   Floor 9 — The Hunting Grounds:
    ///     CountdownDuration: 1200
    ///     StairwellCount: 4
    ///     FloorTags: [Dark, Dangerous, Hunting]
    ///     MaxLootTier: Gold
    ///     MobDefs: [Shade, Nightmare_Hound, Cave_Troll]
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Floor", fileName = "Floor_New")]
    public class FloorDefinition : ScriptableObject
    {
        [field: SerializeField] public int FloorNumber { get; private set; } = 1;
        [field: SerializeField] public string FloorName { get; private set; }
        [field: SerializeField, TextArea] public string ThemeDescription { get; private set; }

        [Header("Timing")]
        [field: SerializeField, Tooltip("Seconds until the floor collapses. In the books this is days — tune for gameplay.")]
        public float CountdownDuration { get; private set; } = 600f;

        [field: SerializeField, Tooltip(
            "Seconds before countdown expires where entering a stairwell puts you in stasis. " +
            "In the books this is 6 hours. 0 = no early entry stasis.")]
        public float EarlyEntryStasisThreshold { get; private set; } = 60f;

        [Header("Stairwells")]
        [field: SerializeField, Tooltip("Number of stairwells on this floor. Each floor has half the previous (rounded up).")]
        public int StairwellCount { get; private set; } = 32;

        [Header("Environment")]
        [field: SerializeField, Tooltip("Tags active on this floor as environmental conditions.")]
        public TagDefinition[] FloorTags { get; private set; }

        [field: SerializeField, Tooltip("Zone definition for stairwells on this floor.")]
        public ZoneDefinition StairwellZoneDef { get; private set; }

        [Header("Mobs")]
        [field: SerializeField, Tooltip("Entity definitions for mobs that spawn on this floor.")]
        public FloorMobEntry[] MobTable { get; private set; }

        [Header("Loot")]
        [field: SerializeField, Tooltip("Highest tier loot box that can drop on this floor.")]
        public LootBoxTier MaxLootTier { get; private set; } = LootBoxTier.Bronze;

        [Header("Progression")]
        [field: SerializeField] public FloorDefinition NextFloor { get; private set; }

        [field: SerializeField, Tooltip("If true, crawlers select Race and Class on this floor (Floor 3).")]
        public bool IsClassSelectionFloor { get; private set; }

        [field: SerializeField, Tooltip("If true, crawlers choose a Subclass on this floor (Floor 6).")]
        public bool IsSubclassSelectionFloor { get; private set; }
    }

    [System.Serializable]
    public struct FloorMobEntry
    {
        public EntityDefinition MobDefinition;

        [Tooltip("Spawn weight. Higher = more frequent.")]
        public float Weight;

        [Tooltip("Maximum concurrent instances of this mob type on the floor.")]
        public int MaxCount;
    }
}
