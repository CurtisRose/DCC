using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects;
using DCC.Core.Effects.Modifiers;

namespace DCC.Core.Entities
{
    /// <summary>
    /// A passive permanent trait that cannot be trained or leveled.
    /// Distinct from Skills (which level through use) and Spells (which cost mana).
    ///
    /// Benefits are granted by Race, Class, Achievements, or special dungeon events.
    /// Once gained, they persist permanently (unless explicitly revoked).
    ///
    /// Each Benefit can:
    ///   - Grant permanent tags (integrates with the entire tag/effect system)
    ///   - Apply permanent stat bonuses (via StatModifier[])
    ///   - Apply permanent effects (Duration: -1, e.g., passive auras)
    ///
    /// Example Benefits from the DCC books:
    ///
    ///   Adaptability (Human race benefit):
    ///     Description: "Humans adapt. +10 stat points on creation."
    ///     GrantedTags: [Adaptable]
    ///     StatBonuses: [] (the +10 points are on the RaceDefinition, not the Benefit)
    ///     Note: The tag [Adaptable] can be checked by InteractionRules for human-only interactions.
    ///
    ///   Manager Benefit:
    ///     Description: "Your Game Guide becomes a permanent Manager."
    ///     GrantedTags: [HasManager]
    ///     PassiveEffects: [] (Manager functionality is gameplay logic, not an effect)
    ///
    ///   Iron Stomach:
    ///     Description: "Potion cooldown reduced by 25%."
    ///     GrantedTags: [IronStomach]
    ///     Note: PotionCooldownTracker checks for this tag to apply a reduction.
    ///
    ///   Primal Instinct (Primal race benefit):
    ///     Description: "All skills can be trained to level 20."
    ///     GrantedTags: [Primal]
    ///     Note: SkillTracker checks IsPrimal flag, set by CrawlerIdentity.
    ///
    ///   Thick Skin:
    ///     Description: "Natural armor. +5 base armor."
    ///     StatBonuses: [] (armor is handled separately via BonusArmor)
    ///     BonusArmor: 5
    ///
    ///   Second Wind:
    ///     Description: "When health drops below 20%, gain a burst of regeneration."
    ///     GrantedTags: [SecondWind]
    ///     PassiveEffects: [HealOverTime triggered by InteractionRule on [SecondWind, LowHealth]]
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Benefit", fileName = "Benefit_New")]
    public class BenefitDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Tags")]
        [field: SerializeField, Tooltip("Tags granted permanently while this benefit is active.")]
        public TagDefinition[] GrantedTags { get; private set; }

        [Header("Stat Bonuses")]
        [field: SerializeField, Tooltip("Permanent stat bonuses applied when this benefit is gained.")]
        public StatModifier[] StatBonuses { get; private set; }

        [field: SerializeField, Tooltip("Permanent bonus armor.")]
        public float BonusArmor { get; private set; }

        [Header("Passive Effects")]
        [field: SerializeField, Tooltip(
            "Permanent effects applied when this benefit is gained (use Duration: -1). " +
            "Example: a passive regen aura, a damage reflection effect, etc.")]
        public EffectDefinition[] PassiveEffects { get; private set; }
    }
}
