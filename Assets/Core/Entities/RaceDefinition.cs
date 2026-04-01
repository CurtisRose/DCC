using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects.Modifiers;
using DCC.Abilities;

namespace DCC.Core.Entities
{
    /// <summary>
    /// Defines a playable race. Chosen before entering the dungeon (or on Floor 3 in the books).
    ///
    /// Each race provides:
    ///   - Bonus stat points allocated on creation
    ///   - Permanent stat modifiers
    ///   - Tags that identify the race (for tag-based interactions)
    ///   - Skills granted at level 1
    ///   - Benefits (passive traits)
    ///   - Special flags (e.g., Primal unlocks all skills to level 20)
    ///
    /// Example Races from the DCC books:
    ///
    ///   Human:
    ///     BonusStatPoints: 10
    ///     GrantedTags: [Human, Adaptable]
    ///     GrantedBenefits: [Adaptability]
    ///     IsPrimal: false
    ///     → Best for generalist builds. Extra 10 points = flexibility.
    ///     → Most crawlers are human since Earth's population enters the dungeon.
    ///
    ///   Primal:
    ///     BonusStatPoints: -5 (costs 5 points relative to human)
    ///     GrantedTags: [Primal, Beastkin]
    ///     GrantedBenefits: [Primal Instinct]
    ///     IsPrimal: true
    ///     → ALL skills can train to level 20 instead of 15.
    ///     → Sacrifices early stats for massive late-game skill ceiling.
    ///     → Must plan build carefully due to point deficit.
    ///
    ///   Half-Orc (example non-book race for gameplay variety):
    ///     BonusStatPoints: 5
    ///     StatBonuses: [Str+2, Con+1]
    ///     GrantedTags: [HalfOrc, Corporeal]
    ///     GrantedBenefits: [Thick Skin (+5 armor)]
    ///     IsPrimal: false
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Race", fileName = "Race_New")]
    public class RaceDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Stat Points")]
        [field: SerializeField, Tooltip(
            "Extra stat points granted on creation. Can be negative (Primal costs 5 points). " +
            "Human gets +10. These are added to UnspentStatPoints for the player to allocate.")]
        public int BonusStatPoints { get; private set; } = 0;

        [Header("Permanent Stat Modifiers")]
        [field: SerializeField, Tooltip("Stat bonuses applied permanently when this race is selected.")]
        public StatModifier[] StatBonuses { get; private set; }

        [Header("Tags")]
        [field: SerializeField, Tooltip("Tags added to the crawler permanently.")]
        public TagDefinition[] GrantedTags { get; private set; }

        [Header("Skills")]
        [field: SerializeField, Tooltip("Skills granted at level 1 when this race is chosen.")]
        public SkillDefinition[] GrantedSkills { get; private set; }

        [Header("Benefits")]
        [field: SerializeField, Tooltip("Passive benefits granted by this race.")]
        public BenefitDefinition[] GrantedBenefits { get; private set; }

        [Header("Special")]
        [field: SerializeField, Tooltip(
            "If true, this race allows ALL skills to be trained to level 20 instead of 15. " +
            "Faithful to the Primal race in the DCC books.")]
        public bool IsPrimal { get; private set; }
    }
}
