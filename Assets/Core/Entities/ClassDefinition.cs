using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects.Modifiers;
using DCC.Abilities;

namespace DCC.Core.Entities
{
    /// <summary>
    /// Defines a crawler class. Chosen on Floor 3 at the Tutorial Guild Hall.
    ///
    /// Classes grant:
    ///   - Skills and spells
    ///   - Passive Benefits
    ///   - Tags identifying the class (for tag-based gating and interactions)
    ///   - Stat requirements to select
    ///   - Available subclasses (chosen on Floor 6)
    ///
    /// Over 40,000 different classes were selected in the current season (per the books).
    ///
    /// Example Classes from the DCC books:
    ///
    ///   Compensated Anarchist (Carl's class):
    ///     Description: "Monk/Rogue hybrid. Trapmaking, bomb-making, social-media dynamo."
    ///     StatRequirements: [{ Cha >= 25 }, { Dex >= 10 }]
    ///     SkillRequirements: [Explosives Handling level 5]
    ///     GrantedSkills: [Unarmed Combat (if not already known)]
    ///     GrantedTags: [Anarchist, Pugilist, Trapmaker]
    ///     AvailableSubclasses: [Agent Provocateur, Revolutionary, Guerilla]
    ///     → Expert in hand-to-hand and dirty tactics; weaker with traditional weapons.
    ///
    ///   Former Child Actor (Donut's class):
    ///     Description: "Each floor, pick a new temporary class and get random abilities."
    ///     GrantedSkills: [Character Actor]
    ///     GrantedTags: [Actor, Performer]
    ///     → Character Actor skill: each floor, pick a temp class, get random selection of
    ///       that class's benefits/abilities. Quality scales with skill level.
    ///     → Can permanently retain abilities from temp class if she gains a level in it.
    ///
    ///   Boring Ol' Fighter:
    ///     Description: "The basic melee class. Reliable, straightforward."
    ///     StatBonuses: [Str+2]
    ///     GrantedSkills: [Dodge, basic weapon proficiencies]
    ///     GrantedTags: [Fighter, Martial]
    ///
    ///   Bard:
    ///     Description: "Musical magic. Charisma-driven spells and buffs."
    ///     StatRequirements: [{ Cha >= 8 }]
    ///     GrantedAbilities: [Bard Song (channeled buff spell)]
    ///     GrantedTags: [Bard, Performer, Musical]
    ///
    ///   Necromancer:
    ///     Description: "Command the dead. Intelligence-driven."
    ///     StatRequirements: [{ Int >= 8 }]
    ///     GrantedAbilities: [Raise Dead, Bone Shield]
    ///     GrantedTags: [Necromancer, DarkMagic]
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Class", fileName = "Class_New")]
    public class ClassDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Requirements")]
        [field: SerializeField, Tooltip("Minimum stats required to select this class.")]
        public StatRequirement[] StatRequirements { get; private set; }

        [field: SerializeField, Tooltip("Skills required at specific levels to select this class.")]
        public SkillRequirement[] SkillRequirements { get; private set; }

        [Header("Granted on Selection")]
        [field: SerializeField, Tooltip("Permanent stat bonuses applied when this class is selected.")]
        public StatModifier[] StatBonuses { get; private set; }

        [field: SerializeField, Tooltip("Skills granted at level 1.")]
        public SkillDefinition[] GrantedSkills { get; private set; }

        [field: SerializeField, Tooltip("Spells/abilities granted immediately.")]
        public AbilityDefinition[] GrantedAbilities { get; private set; }

        [field: SerializeField, Tooltip("Passive benefits granted by this class.")]
        public BenefitDefinition[] GrantedBenefits { get; private set; }

        [field: SerializeField, Tooltip("Tags added permanently.")]
        public TagDefinition[] GrantedTags { get; private set; }

        [Header("Subclasses (Floor 6)")]
        [field: SerializeField, Tooltip("Available subclass specializations, unlocked on Floor 6.")]
        public SubclassDefinition[] AvailableSubclasses { get; private set; }
    }

    [System.Serializable]
    public struct StatRequirement
    {
        public CrawlerStat Stat;
        [Tooltip("Minimum value required (checks effective stat = base + bonus).")]
        public int MinValue;
    }

    [System.Serializable]
    public struct SkillRequirement
    {
        public SkillDefinition Skill;
        [Tooltip("Minimum skill level required.")]
        public int MinLevel;
    }
}
