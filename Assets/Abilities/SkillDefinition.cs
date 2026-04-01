using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;

namespace DCC.Abilities
{
    /// <summary>
    /// A skill in the DCC system. Skills are nonmagical talents that level up through use.
    ///
    /// Faithful to the books:
    ///   - Skills level from 1 to MaxLevel (default 15).
    ///   - The Primal race unlocks training to level 20 for ALL skills.
    ///   - Each use accumulates XP toward the next skill level.
    ///   - Higher skill levels improve the effects/abilities that reference this skill.
    ///   - Skills can be gained from gear, potions, race/class, guildhalls, or dungeon actions.
    ///
    /// Skills are NOT spells. Spells cost mana and are learned from tomes.
    /// Skills are physical/tactical talents trained by doing.
    ///
    /// Example skills from the books:
    ///
    ///   Unarmed Combat:
    ///     Description: "Hand-to-hand fighting. Higher levels unlock combos and critical hits."
    ///     LinkedStat: Strength
    ///     MaxLevel: 15 (20 with Primal)
    ///     XpPerUse: 1
    ///     GrantedTagsAtLevel: { 5: [MartialArtist], 10: [IronFist], 15: [Legendary_Unarmed] }
    ///
    ///   Dodge:
    ///     Description: "Instinctive evasion of incoming attacks."
    ///     LinkedStat: Dexterity
    ///     XpPerUse: 1 (gained when successfully dodging)
    ///
    ///   Explosives Handling:
    ///     Description: "Safely craft and deploy explosive devices. Reduces self-damage."
    ///     LinkedStat: Dexterity
    ///     RequiredLevel 5 unlocks Compensated Anarchist class
    ///
    ///   Regeneration:
    ///     Description: "Passive health recovery when out of combat."
    ///     LinkedStat: Constitution
    ///
    ///   Iron Punch:
    ///     Description: "A devastating single strike. Damage scales with skill level."
    ///     LinkedStat: Strength
    ///
    ///   Character Actor (Donut):
    ///     Description: "Assume a temporary class each floor. Quality scales with level."
    ///     LinkedStat: Charisma
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Skill", fileName = "Skill_New")]
    public class SkillDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Leveling")]
        [field: SerializeField, Tooltip("Normal max level is 15. Primal race can train to 20.")]
        public int MaxLevel { get; private set; } = 15;

        [field: SerializeField, Tooltip("XP gained per use of this skill.")]
        public int XpPerUse { get; private set; } = 1;

        [field: SerializeField, Tooltip("XP required = BaseXpToLevel * currentLevel. Scales linearly.")]
        public int BaseXpToLevel { get; private set; } = 10;

        [Header("Stat Link")]
        [field: SerializeField, Tooltip("The primary stat that affects this skill's effectiveness.")]
        public CrawlerStat LinkedStat { get; private set; } = CrawlerStat.Strength;

        [Header("Tags")]
        [field: SerializeField, Tooltip("Tags granted to the entity while this skill is known (any level).")]
        public TagDefinition[] GrantedWhileKnown { get; private set; }

        [field: SerializeField, Tooltip("Tag milestones: tags granted when the skill reaches specific levels.")]
        public SkillMilestone[] Milestones { get; private set; }

        /// <summary>XP needed to advance from the given level to the next.</summary>
        public int XpRequiredForLevel(int currentLevel) => BaseXpToLevel * currentLevel;
    }

    [System.Serializable]
    public struct SkillMilestone
    {
        [Tooltip("The skill level at which these tags are granted.")]
        public int RequiredLevel;

        [Tooltip("Tags granted to the entity when this milestone is reached.")]
        public TagDefinition[] GrantedTags;
    }
}
