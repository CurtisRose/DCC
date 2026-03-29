using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;

namespace DCC.Core.Effects.Modifiers
{
    /// <summary>
    /// Applies a set of tags to the target for the effect's duration.
    /// Used for crowd-control and environmental conditions: Burning, Frozen, Wet,
    /// Slowed, Stunned, Poisoned, Blessed, Cursed, Electrified, etc.
    ///
    /// The tags are removed automatically when the effect expires (OnRemove).
    /// Because these are real tags, they interact with the entire tag system:
    ///
    ///   Wet + Lightning → [Wet] tag is already on the target when the lightning
    ///   DamageEffect fires. Its AmplifiedByTargetTags sees [Wet] → damage × 2.5.
    ///   No explicit "wet + lightning = more damage" rule ever written.
    ///
    ///   Burning + Wet → WetEffect has SuppressedTags: [Burning] on the TagDefinition,
    ///   so applying Wet automatically extinguishes Burning. Designer-configured.
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Effect/Status", fileName = "Effect_Status")]
    public class StatusEffect : EffectDefinition
    {
        [field: SerializeField, Tooltip("Additional tags to apply beyond GrantedTags in the base class.")]
        public TagDefinition[] StatusTags { get; private set; }

        [field: SerializeField, Tooltip("Scale the target's move speed while this status is active. 1 = no change.")]
        public float MoveSpeedMultiplier { get; private set; } = 1f;

        public override void OnApply(EffectInstance instance, EntityAttributes target)
        {
            if (StatusTags != null)
                target.Tags.AddTags(StatusTags);

            if (!Mathf.Approximately(MoveSpeedMultiplier, 1f))
                target.AttributeSet.AddMoveSpeedMultiplier(MoveSpeedMultiplier);
        }

        public override void OnRemove(EffectInstance instance, EntityAttributes target)
        {
            if (StatusTags != null)
                target.Tags.RemoveTags(StatusTags);

            if (!Mathf.Approximately(MoveSpeedMultiplier, 1f))
                target.AttributeSet.RemoveMoveSpeedMultiplier(MoveSpeedMultiplier);
        }
    }
}
