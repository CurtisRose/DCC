using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects.Modifiers;
using DCC.Abilities;

namespace DCC.Core.Entities
{
    /// <summary>
    /// A subclass specialization, chosen on Floor 6.
    /// Extends the parent class with additional skills, benefits, and tags.
    ///
    /// Example Subclasses from the DCC books:
    ///
    ///   Agent Provocateur (Compensated Anarchist subclass — Carl chose this):
    ///     Description: "Bomb-focused. Enhanced explosive crafting and deployment."
    ///     AdditionalSkills: [Advanced Explosives, Incendiary Device Handling]
    ///     AdditionalTags: [Provocateur, Demolitions]
    ///     AdditionalBenefits: [Blast Radius+ (explosions deal 20% more damage)]
    ///
    ///   Revolutionary (Compensated Anarchist subclass):
    ///     Description: "Melee-focused. Enhanced unarmed combat and rally abilities."
    ///     AdditionalSkills: [Iron Punch, Rally]
    ///     AdditionalTags: [Revolutionary, Inspiring]
    ///
    ///   Guerilla (Compensated Anarchist subclass):
    ///     Description: "Trap and ranged-focused. Enhanced trap crafting and ambush tactics."
    ///     AdditionalSkills: [Advanced Traps, Ambush]
    ///     AdditionalTags: [Guerilla, Stealthy]
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Subclass", fileName = "Subclass_New")]
    public class SubclassDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Parent")]
        [field: SerializeField, Tooltip("The class this subclass specializes.")]
        public ClassDefinition ParentClass { get; private set; }

        [Header("Requirements")]
        [field: SerializeField, Tooltip("Additional stat requirements beyond the parent class.")]
        public StatRequirement[] AdditionalStatRequirements { get; private set; }

        [Header("Granted on Selection")]
        [field: SerializeField]
        public StatModifier[] StatBonuses { get; private set; }

        [field: SerializeField]
        public SkillDefinition[] AdditionalSkills { get; private set; }

        [field: SerializeField]
        public AbilityDefinition[] AdditionalAbilities { get; private set; }

        [field: SerializeField]
        public BenefitDefinition[] AdditionalBenefits { get; private set; }

        [field: SerializeField]
        public TagDefinition[] AdditionalTags { get; private set; }
    }
}
