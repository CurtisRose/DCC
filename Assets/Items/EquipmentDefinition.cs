using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects;
using DCC.Core.Effects.Modifiers;
using DCC.Core.Entities;
using DCC.Abilities;

namespace DCC.Items
{
    /// <summary>
    /// Defines a piece of equippable gear. Equipment is distinct from consumable items:
    /// it occupies a body slot, applies bonuses while worn, and is removed on unequip.
    ///
    /// Equipment can:
    ///   - Modify crawler stats (via StatModifier[])
    ///   - Grant bonus armor
    ///   - Grant skills (active while equipped, removed on unequip)
    ///   - Add tags (integrates with the entire tag/effect system)
    ///   - Apply enchantment effects (permanent Duration:-1 effects active while worn)
    ///   - Require specific stats, classes, or skills to equip
    ///
    /// Example equipment from the DCC books:
    ///
    ///   Enchanted Pedicure Kit of the Sylph:
    ///     Slot: Feet
    ///     Rarity: Legendary
    ///     StatBonuses: [Dex+3]
    ///     GrantedSkills: [Unarmed Combat]
    ///     GrantedTags: [Enchanted, Sylph_Blessed, WellGroomed]
    ///     Description: "Maintains Carl's feet — his primary weapon."
    ///     → Dex bonus improves dodge and move speed.
    ///     → Unarmed Combat skill enhances kick damage.
    ///     → [WellGroomed] tag could trigger NPC interaction bonuses.
    ///
    ///   Gauntlet of the Blood-Soaked Path:
    ///     Slot: Hands
    ///     Rarity: Artifact
    ///     StatBonuses: [Str+5, Con-2, Cha-1]
    ///     GrantedTags: [Cursed, BloodSoaked, Explosive_Enhanced]
    ///     Enchantments: [DamageEffect(5, GrantedTags:[Bleed], Duration:-1, TickInterval:10)]
    ///     Description: "Enhances explosive capabilities at the cost of sanity and health."
    ///     → [Explosive_Enhanced] tag interacts with Explosives Handling skill.
    ///     → [Cursed] tag could block [Blessed] effects via tag suppression.
    ///     → The bleed enchantment ticks damage to the WEARER (self-harm for power).
    ///
    ///   Enchanted War Gauntlet of the Exalted Grull:
    ///     Slot: Hands
    ///     Rarity: Rare
    ///     StatBonuses: [Str+3]
    ///     BonusArmor: 5
    ///     GrantedTags: [Enchanted, Grull_Blessed]
    ///
    ///   Iron Helmet (common loot):
    ///     Slot: Head
    ///     Rarity: Common
    ///     BonusArmor: 8
    ///     GrantedTags: [Armored]
    ///
    ///   Ring of Mana Regeneration:
    ///     Slot: Ring1 or Ring2
    ///     Rarity: Rare
    ///     StatBonuses: [Int+2]
    ///     Enchantments: [ManaRestoreEffect(1, Duration:-1, TickInterval:5)]
    ///     GrantedTags: [Enchanted, Magical]
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Equipment", fileName = "Equip_New")]
    public class EquipmentDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Slot & Rarity")]
        [field: SerializeField] public EquipmentSlot Slot { get; private set; }
        [field: SerializeField] public ItemRarity Rarity { get; private set; } = ItemRarity.Common;

        [Header("Stat Bonuses (while equipped)")]
        [field: SerializeField, Tooltip("Stat modifiers applied while this gear is worn. Removed on unequip.")]
        public StatModifier[] StatBonuses { get; private set; }

        [field: SerializeField, Tooltip("Bonus armor while equipped.")]
        public float BonusArmor { get; private set; }

        [Header("Skills (while equipped)")]
        [field: SerializeField, Tooltip("Skills granted while this gear is worn. Removed on unequip.")]
        public SkillDefinition[] GrantedSkills { get; private set; }

        [Header("Tags (while equipped)")]
        [field: SerializeField, Tooltip("Tags added while this gear is worn. Removed on unequip.")]
        public TagDefinition[] GrantedTags { get; private set; }

        [Header("Enchantments (while equipped)")]
        [field: SerializeField, Tooltip(
            "Effects applied as permanent (Duration:-1) while equipped. " +
            "Use for passive auras, regen, self-damage curses, etc. " +
            "All removed on unequip.")]
        public EffectDefinition[] Enchantments { get; private set; }

        [Header("Requirements")]
        [field: SerializeField, Tooltip("Minimum stats required to equip this gear.")]
        public StatRequirement[] StatRequirements { get; private set; }

        [field: SerializeField, Tooltip("Classes allowed to equip this. Empty = any class.")]
        public ClassDefinition[] RequiredClasses { get; private set; }

        [field: SerializeField, Tooltip("Skill required to equip (e.g., weapon proficiency). Null = no requirement.")]
        public SkillDefinition RequiredSkill { get; private set; }

        [field: SerializeField, Tooltip("Minimum level of the required skill.")]
        public int RequiredSkillLevel { get; private set; }

        [Header("Economy")]
        [field: SerializeField] public int GoldValue { get; private set; } = 10;
    }
}
