using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;

namespace DCC.Core.Effects.Modifiers
{
    /// <summary>
    /// Applies a set of tags to the target for the effect's duration.
    /// Used for crowd-control, environmental conditions, buffs, and debuffs.
    ///
    /// The tags are removed automatically when the effect expires (OnRemove).
    /// Because these are real tags, they interact with the entire tag system:
    ///
    ///   Wet + Lightning → [Wet] tag is already on the target when the lightning
    ///   DamageEffect fires. Its AmplifiedByTargetTags sees [Wet] → damage × 2.5.
    ///   No explicit "wet + lightning = more damage" rule ever written.
    ///
    /// DCC-specific status effects (configure as assets):
    ///
    ///   Poisoned:
    ///     StatusType: DamageOverTime
    ///     DamagePerTick: 3
    ///     StatusTags: [Poisoned]
    ///     ImmuneToHealing: true (faithful to books — Poison can't be healed, needs antidote)
    ///     MoveSpeedMultiplier: 0.85
    ///     Duration: 30, TickInterval: 2
    ///
    ///   Sepsis:
    ///     StatusType: DamageOverTime
    ///     DamagePerTick: 8
    ///     StatusTags: [Sepsis, Staggered]
    ///     ImmuneToHealing: false (CAN be healed, unlike Poison)
    ///     MoveSpeedMultiplier: 0.5
    ///     Duration: 15, TickInterval: 1
    ///
    ///   Bleed:
    ///     StatusType: DamageOverTime
    ///     DamagePerTick: 5
    ///     StatusTags: [Bleeding]
    ///     Duration: 10, TickInterval: 2
    ///
    ///   Stunned:
    ///     StatusType: CrowdControl
    ///     PreventsActions: true
    ///     PreventsMovement: true
    ///     StatusTags: [Stunned, Incapacitated]
    ///     Duration: 3
    ///
    ///   Paralyzed:
    ///     StatusType: CrowdControl
    ///     PreventsActions: false
    ///     PreventsMovement: true
    ///     StatusTags: [Paralyzed]
    ///     Duration: 5
    ///
    ///   Slowed:
    ///     StatusType: Debuff
    ///     MoveSpeedMultiplier: 0.5
    ///     StatusTags: [Slowed]
    ///     Duration: 8
    ///
    ///   Frosted:
    ///     StatusType: Debuff
    ///     MoveSpeedMultiplier: 0.6
    ///     StatusTags: [Frosted, Cold]
    ///     Duration: 10
    ///     AmplifiedByTargetTags: [Wet] × 1.5 (being wet makes frost worse)
    ///
    ///   Fear/Terror:
    ///     StatusType: CrowdControl
    ///     PreventsActions: true
    ///     StatusTags: [Feared, Fleeing]
    ///     Duration: 4
    ///     Note: In the books, Fear denies team XP for feared mobs.
    ///
    ///   Peace-Bonded:
    ///     StatusType: CrowdControl
    ///     PreventsActions: true (prevents accessing Inventory)
    ///     StatusTags: [PeaceBonded]
    ///     Duration: -1 (permanent until removed by safe room or spell)
    ///
    ///   Burning:
    ///     StatusType: DamageOverTime
    ///     DamagePerTick: 4
    ///     StatusTags: [Burning, Hot]
    ///     Duration: 6, TickInterval: 1
    ///     SuppressedTags on Wet TagDef → applying Wet extinguishes Burning
    ///
    ///   Regeneration (buff):
    ///     StatusType: HealOverTime
    ///     HealPerTick: 5
    ///     StatusTags: [Regenerating]
    ///     Duration: 20, TickInterval: 2
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Effect/Status", fileName = "Effect_Status")]
    public class StatusEffect : EffectDefinition
    {
        public enum StatusType
        {
            Debuff,             // applies tags and speed change only
            DamageOverTime,     // periodic damage ticks
            HealOverTime,       // periodic healing ticks
            CrowdControl        // prevents actions/movement
        }

        [field: SerializeField, Tooltip("Additional tags to apply beyond GrantedTags in the base class.")]
        public TagDefinition[] StatusTags { get; private set; }

        [field: SerializeField]
        public StatusType Type { get; private set; } = StatusType.Debuff;

        [Header("Movement & Action")]
        [field: SerializeField, Tooltip("Scale the target's move speed while this status is active. 1 = no change.")]
        public float MoveSpeedMultiplier { get; private set; } = 1f;

        [field: SerializeField, Tooltip("If true, the target cannot use abilities or items (Stun, Peace-Bonded).")]
        public bool PreventsActions { get; private set; }

        [field: SerializeField, Tooltip("If true, the target cannot move (Stun, Paralysis).")]
        public bool PreventsMovement { get; private set; }

        [Header("Damage Over Time")]
        [field: SerializeField, Tooltip("Damage dealt per tick (for DamageOverTime type). Scaled by BaseMagnitude modifiers.")]
        public float DamagePerTick { get; private set; } = 0f;

        [field: SerializeField, Tooltip(
            "If true, this DOT cannot be removed by healing effects (e.g., Poison in DCC). " +
            "Requires a specific antidote item/effect with the right tags to remove.")]
        public bool ImmuneToHealing { get; private set; }

        [Header("Heal Over Time")]
        [field: SerializeField, Tooltip("Health restored per tick (for HealOverTime type).")]
        public float HealPerTick { get; private set; } = 0f;

        public override void OnApply(EffectInstance instance, EntityAttributes target)
        {
            if (StatusTags != null)
                target.Tags.AddTags(StatusTags);

            if (!Mathf.Approximately(MoveSpeedMultiplier, 1f))
                target.AttributeSet.AddMoveSpeedMultiplier(MoveSpeedMultiplier);

            // For CC, apply a 0 speed multiplier to freeze movement.
            if (PreventsMovement)
                target.AttributeSet.AddMoveSpeedMultiplier(0f);
        }

        public override void OnTick(EffectInstance instance, EntityAttributes target)
        {
            switch (Type)
            {
                case StatusType.DamageOverTime:
                    if (DamagePerTick > 0f)
                    {
                        float damage = DamagePerTick * (instance.ResolvedMagnitude / BaseMagnitude);
                        target.AttributeSet.ApplyDamage(damage, instance.Context);
                    }
                    break;

                case StatusType.HealOverTime:
                    if (HealPerTick > 0f)
                    {
                        float heal = HealPerTick * (instance.ResolvedMagnitude / Mathf.Max(0.01f, BaseMagnitude));
                        target.AttributeSet.ApplyHeal(heal);
                    }
                    break;
            }
        }

        public override void OnRemove(EffectInstance instance, EntityAttributes target)
        {
            if (StatusTags != null)
                target.Tags.RemoveTags(StatusTags);

            if (!Mathf.Approximately(MoveSpeedMultiplier, 1f))
                target.AttributeSet.RemoveMoveSpeedMultiplier(MoveSpeedMultiplier);

            if (PreventsMovement)
                target.AttributeSet.RemoveMoveSpeedMultiplier(0f);
        }
    }
}
