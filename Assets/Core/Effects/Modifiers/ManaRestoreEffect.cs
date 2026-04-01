using UnityEngine;
using DCC.Core.Entities;

namespace DCC.Core.Effects.Modifiers
{
    /// <summary>
    /// Restores mana to the target. Instant or over time (like HealEffect but for MP).
    ///
    /// In the DCC books, Mana Potions fully restore MP instantly.
    /// Mana Toast and other items provide partial mana restoration.
    ///
    /// Example configurations:
    ///
    ///   Mana Potion (full restore):
    ///     BaseMagnitude: 9999 (effectively restores all MP, clamped to MaxMana)
    ///     Duration: 0 (instant)
    ///
    ///   Mana Toast:
    ///     BaseMagnitude: 10
    ///     Duration: 0 (instant)
    ///
    ///   Mana Regeneration Aura:
    ///     BaseMagnitude: 1
    ///     Duration: 30, TickInterval: 2
    ///     → Restores 1 MP every 2 seconds for 30 seconds
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Effect/Mana Restore", fileName = "Effect_ManaRestore")]
    public class ManaRestoreEffect : EffectDefinition
    {
        public override void OnApply(EffectInstance instance, EntityAttributes target)
        {
            if (instance.ResolvedMagnitude > 0f)
                target.AttributeSet.RestoreMana(instance.ResolvedMagnitude);
        }

        public override void OnTick(EffectInstance instance, EntityAttributes target)
        {
            if (instance.ResolvedMagnitude > 0f)
                target.AttributeSet.RestoreMana(instance.ResolvedMagnitude);
        }
    }
}
