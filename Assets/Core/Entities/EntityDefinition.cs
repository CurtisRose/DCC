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
    /// Crawler stats use the DCC scale: 0 = unconscious, 3 = low average, 4 = average,
    /// 6 = above average, 9–10 = peak human. Mobs can exceed human limits.
    ///
    /// Example entity setups:
    ///
    ///   Average Crawler (entering the dungeon):
    ///     Str: 4, Con: 4, Dex: 4, Int: 4, Cha: 4
    ///     → 150 HP, 4 MP, slow mana regen
    ///
    ///   Carl (mid-game, Compensated Anarchist):
    ///     Str: 8, Con: 12, Dex: 14, Int: 10, Cha: 28
    ///     → High Cha for the show, good Dex for bombs/acrobatics
    ///
    ///   Goblin (Floor 1 mob):
    ///     Str: 3, Con: 2, Dex: 5, Int: 1, Cha: 1
    ///     → Low HP, fast, dumb
    ///
    ///   Skeleton Warrior:
    ///     Base Tags: [Undead, Corporeal, NotLiving]
    ///     → Heals from necrotic, takes extra from holy/radiant
    ///
    ///   Fire Elemental:
    ///     Base Tags: [Elemental, Incorporeal, Burning, Hot]
    ///     → Immune to fire, Wet suppressed instantly
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Entity Definition", fileName = "EntityDef_New")]
    public class EntityDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }

        [Header("Crawler Stats (DCC scale: 0=unconscious, 4=average, 10=peak human)")]
        [field: SerializeField] public int BaseStrength { get; private set; } = 4;
        [field: SerializeField] public int BaseConstitution { get; private set; } = 4;
        [field: SerializeField] public int BaseDexterity { get; private set; } = 4;
        [field: SerializeField] public int BaseIntelligence { get; private set; } = 4;
        [field: SerializeField] public int BaseCharisma { get; private set; } = 4;

        [Header("Base Stats")]
        [field: SerializeField] public float BaseArmor { get; private set; } = 0f;
        [field: SerializeField] public float BaseMoveSpeed { get; private set; } = 5f;

        [Header("Resistances")]
        [field: SerializeField, Tooltip(
            "Resistance profiles applied to all incoming effects. " +
            "Evaluated in order; multipliers stack, InvertsEffect toggles.\n\n" +
            "Example: assign Resistance_Undead to make ALL healing damage this entity.")]
        public ResistanceProfile[] ResistanceProfiles { get; private set; }

        [Header("Loot & Economy")]
        [field: SerializeField] public int BaseXP { get; private set; } = 10;
        [field: SerializeField] public float LootMultiplier { get; private set; } = 1f;

        public void InitializeAttributes(AttributeSet attrs)
        {
            attrs.BaseStrength = BaseStrength;
            attrs.BaseConstitution = BaseConstitution;
            attrs.BaseDexterity = BaseDexterity;
            attrs.BaseIntelligence = BaseIntelligence;
            attrs.BaseCharisma = BaseCharisma;
            attrs.BaseArmor = BaseArmor;
            attrs.BaseMoveSpeed = BaseMoveSpeed;
            attrs.Initialize();
        }
    }
}
