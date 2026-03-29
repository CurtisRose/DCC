using UnityEngine;
using DCC.Core.Entities;
using DCC.Core.Tags;

namespace DCC.Core.Effects.Modifiers
{
    /// <summary>
    /// Deals damage to the target's health pool.
    ///
    /// Example configurations (all via ScriptableObject assets, no code changes):
    ///
    ///   Fire Damage:
    ///     GrantedTags: [Burning, Hot]
    ///     AmplifiedByTargetTags: [Flammable] × 3.0, [Frozen] × 0.5
    ///     RequiredTargetTags: (none — applies to everything)
    ///
    ///   Holy Damage:
    ///     GrantedTags: [Radiant]
    ///     AmplifiedByTargetTags: [Undead] × 3.0, [Blessed] × 0.0 (immune)
    ///
    ///   Necrotic Damage (heals undead instead — handled by setting negative magnitude):
    ///     Use a separate HealEffect with RequiredTargetTags: [Undead]
    ///     and a DamageEffect with RequiredTargetTags: [Living]
    ///     Pair them together on the same item/ability.
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Effect/Damage", fileName = "Effect_Damage")]
    public class DamageEffect : EffectDefinition
    {
        [field: SerializeField, Tooltip("If true, ignores armor/resistance calculations.")]
        public bool IgnoresArmor { get; private set; }

        public override void OnApply(EffectInstance instance, EntityAttributes target)
        {
            float damage = instance.ResolvedMagnitude;

            if (!IgnoresArmor)
                damage = target.AttributeSet.CalculateDamageAfterArmor(damage);

            target.AttributeSet.ApplyDamage(damage, instance.Context);
        }
    }
}
