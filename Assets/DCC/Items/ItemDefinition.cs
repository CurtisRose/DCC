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
    ///   Smoke Grenade:
    ///     UseMode: SpawnZone(SmokZoneDef)
    ///     ItemTags: [Throwable, Grenade, Gas, Smoke]
    ///     ZoneEffects: [SmokeEffect]           → obscures vision
    ///
    ///   Healing Potion:
    ///     UseMode: ApplyEffectAtCursor
    ///     ItemTags: [Consumable, Magical, Healing]
    ///     OnUseEffects: [HealEffect(50)]
    ///     Can be thrown into a SmokeZone → infuses HealEffect into zone
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
    ///     → Teleports anyone/anything Corporeal that steps on it
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

        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [field: SerializeField] public UseMode Mode { get; private set; }

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
}
