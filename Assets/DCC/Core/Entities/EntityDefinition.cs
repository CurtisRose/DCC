using UnityEngine;

namespace DCC.Core.Entities
{
    /// <summary>
    /// Archetype data for an entity type. Configures starting stats, base tags,
    /// and prefab reference. Think of this as the "class" of an entity.
    ///
    /// Shared across all instances of that entity type (e.g., all Skeleton Warriors
    /// share one SkeletonWarrior EntityDefinition asset).
    ///
    /// Interesting tag setups to try in the editor:
    ///
    ///   Skeleton Warrior:
    ///     Base Tags: [Undead, Corporeal, NotLiving]
    ///     → Heals from necrotic, takes extra from holy/radiant
    ///     → Teleport traps configured for [Corporeal] catch them
    ///
    ///   Ghost:
    ///     Base Tags: [Undead, Incorporeal, NotLiving]
    ///     → Teleport traps for [Corporeal] MISS them (no Corporeal tag)
    ///     → Physical DamageEffect has RequiredTargetTags: [Corporeal] → misses
    ///     → Need a magic/spectral damage type with no Corporeal requirement
    ///
    ///   Player:
    ///     Base Tags: [Living, Corporeal, Player]
    ///     → Healing effects apply, teleport traps catch them
    ///
    ///   Fire Elemental:
    ///     Base Tags: [Elemental, Incorporeal, Burning, Hot]
    ///     → Immune to fire (Burning already present, same tag = no-op stacking)
    ///     → Wet status suppressed instantly (Hot implies drying)
    ///     → Water deals damage via InteractionRule: [Wet] + [Hot] → steam explosion
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Entity Definition", fileName = "EntityDef_New")]
    public class EntityDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }

        [Header("Base Stats")]
        [field: SerializeField] public float MaxHealth { get; private set; } = 100f;
        [field: SerializeField] public float BaseArmor { get; private set; } = 0f;
        [field: SerializeField] public float BaseMoveSpeed { get; private set; } = 5f;

        [Header("Loot & Economy")]
        [field: SerializeField] public int BaseXP { get; private set; } = 10;
        [field: SerializeField] public float LootMultiplier { get; private set; } = 1f;

        public void InitializeAttributes(AttributeSet attrs)
        {
            attrs.MaxHealth = MaxHealth;
            attrs.BaseArmor = BaseArmor;
            attrs.BaseMoveSpeed = BaseMoveSpeed;
            attrs.Initialize();
        }
    }
}
