using UnityEngine;
using DCC.Core.Entities;

namespace DCC.Core.Effects.Modifiers
{
    /// <summary>
    /// Restores health to the target.
    ///
    /// The "healing smoke" interaction emerges from this effect being applied to a Zone:
    ///   1. SmokeGrenade creates a Zone tagged [Gas, Smoke, Obscuring, Dispersible]
    ///   2. HealPotion (with GrantedTags: [Healing]) breaks inside the zone
    ///   3. ZoneTicker sees the zone now has effects: [SmokeEffect, HealEffect]
    ///   4. EffectComposer unions their tags → composite: [Gas, Smoke, Healing, ...]
    ///   5. Any Living entity inside receives OnApply every tick
    ///
    /// No special case. The healing cloud is an emergent consequence.
    ///
    /// Example: Healing Grenade (heals living, damages undead)
    ///   Create two effects on the same item:
    ///     HealEffect { RequiredTargetTags: [Living],  BaseMagnitude: 50 }
    ///     DamageEffect { RequiredTargetTags: [Undead], BaseMagnitude: 50,
    ///                    GrantedTags: [NecroticBurst] }
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Effect/Heal", fileName = "Effect_Heal")]
    public class HealEffect : EffectDefinition
    {
        [field: SerializeField, Tooltip("If true, healing can overheal above max health.")]
        public bool AllowOverheal { get; private set; }

        public override void OnApply(EffectInstance instance, EntityAttributes target)
        {
            // Negative magnitude means the effect was inverted by a ResistanceProfile
            // (e.g., undead entity has InvertsEffect on Healing tag).
            // A negative heal becomes damage automatically — no special case.
            if (instance.ResolvedMagnitude < 0f)
                target.AttributeSet.ApplyDamage(-instance.ResolvedMagnitude, instance.Context);
            else
                target.AttributeSet.ApplyHeal(instance.ResolvedMagnitude, AllowOverheal);
        }

        public override void OnTick(EffectInstance instance, EntityAttributes target)
        {
            // Ongoing heal-over-time (e.g., healing smoke cloud ticking each second).
            if (instance.ResolvedMagnitude < 0f)
                target.AttributeSet.ApplyDamage(-instance.ResolvedMagnitude, instance.Context);
            else
                target.AttributeSet.ApplyHeal(instance.ResolvedMagnitude, AllowOverheal);
        }
    }
}
