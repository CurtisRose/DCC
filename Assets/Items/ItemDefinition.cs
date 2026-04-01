using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects;

namespace DCC.Items
{
    /// <summary>
    /// Archetype for a usable item. Items are data — no item-specific code needed.
    ///
    /// An item's emergent power comes entirely from:
    ///   1. The effects it carries (EffectDefinitions)
    ///   2. The tags those effects grant
    ///   3. How those tags compose with whatever else is in the environment
    ///
    /// Example items (all configured in assets, no code):
    ///
    ///   Good Healing Potion:
    ///     UseMode: ApplyEffectAtSelf
    ///     IsPotion: true, PotionTier: Good
    ///     ItemTags: [Consumable, Magical, Healing, Potion]
    ///     OnUseEffects: [HealEffect(50)]
    ///     → Triggers potion cooldown. Chugging another before cooldown = Poisoned.
    ///
    ///   Great Mana Potion:
    ///     UseMode: ApplyEffectAtSelf
    ///     IsPotion: true, PotionTier: Great
    ///     ItemTags: [Consumable, Magical, Mana, Potion]
    ///     OnUseEffects: [ManaRestoreEffect]
    ///
    ///   Superb Strength Potion:
    ///     UseMode: ApplyEffectAtSelf
    ///     IsPotion: true, PotionTier: Superb
    ///     ItemTags: [Consumable, Magical, Potion]
    ///     OnUseEffects: [StatModifierEffect(Str+5, Duration:600)]
    ///
    ///   Poison Antidote:
    ///     UseMode: ApplyEffectAtSelf
    ///     IsPotion: true, PotionTier: Good
    ///     ItemTags: [Consumable, Antidote, Potion]
    ///     OnUseEffects: [] (removes Poisoned tag — configured via BlockedConcurrentTags)
    ///
    ///   Smoke Grenade:
    ///     UseMode: SpawnZone(SmokeZoneDef)
    ///     ItemTags: [Throwable, Grenade, Gas, Smoke]
    ///
    ///   Holy Water Flask:
    ///     UseMode: SpawnZone(WaterZoneDef)
    ///     ItemTags: [Throwable, Liquid, Holy, Radiant]
    ///     ZoneEffects: [HealEffect(RequiredTargetTags:[Living]),
    ///                   DamageEffect(RequiredTargetTags:[Undead], GrantedTags:[Radiant])]
    ///
    ///   Teleport Trap Kit:
    ///     UseMode: PlaceTrap
    ///     ItemTags: [Trap, Magical, Teleport]
    ///     TrapEffects: [TeleportEffect(RequiredTargetTags:[Corporeal])]
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Item", fileName = "Item_New")]
    public class ItemDefinition : ScriptableObject
    {
        public enum UseMode
        {
            ApplyEffectAtSelf,       // heal self
            ApplyEffectAtTarget,     // throw at a specific entity
            ApplyEffectAtCursor,     // AOE burst at target position
            SpawnZone,               // creates a persistent zone
            PlaceTrap,               // places a trap object
            InfuseZone               // infuses effects into an existing zone at target pos
        }

        /// <summary>
        /// Potion quality tiers from the DCC books. Higher tiers have stronger effects.
        /// Potion tier does NOT affect cooldown — any potion triggers the shared cooldown.
        /// </summary>
        public enum PotionTier
        {
            None,       // not a potion
            Good,       // standard quality
            Great,      // enhanced
            Superb,     // rare, powerful
            Cosmic      // legendary rarity
        }

        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [field: SerializeField] public UseMode Mode { get; private set; }

        [Header("Potion")]
        [field: SerializeField, Tooltip(
            "If true, consuming this item triggers the potion cooldown. " +
            "Drinking another potion before cooldown expires inflicts Poisoned (faithful to DCC books).")]
        public bool IsPotion { get; private set; }

        [field: SerializeField, Tooltip("Potion quality tier. Higher tiers have stronger effects in the books.")]
        public PotionTier Tier { get; private set; } = PotionTier.None;

        [Header("Item Rarity")]
        [field: SerializeField]
        public ItemRarity Rarity { get; private set; } = ItemRarity.Common;

        [Header("Tags")]
        [field: SerializeField, Tooltip("Tags this item has when it exists as a world object.")]
        public TagDefinition[] ItemTags { get; private set; }

        [Header("Effects (applied on use)")]
        [field: SerializeField] public EffectDefinition[] OnUseEffects { get; private set; }

        [Header("Zone Spawn (if Mode == SpawnZone)")]
        [field: SerializeField] public Zones.ZoneDefinition ZoneToSpawn { get; private set; }

        [Header("Trap (if Mode == PlaceTrap)")]
        [field: SerializeField] public GameObject TrapPrefab { get; private set; }

        [Header("Economy")]
        [field: SerializeField] public int GoldValue { get; private set; } = 10;
        [field: SerializeField] public int MaxStackSize { get; private set; } = 1;

        [field: SerializeField] public float Cooldown { get; private set; } = 0f;
    }

    /// <summary>
    /// Item rarity tiers from the DCC books (ascending).
    /// Loot boxes follow a similar but separate tier system.
    /// </summary>
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Legendary,
        Artifact,
        Celestial
    }
}
