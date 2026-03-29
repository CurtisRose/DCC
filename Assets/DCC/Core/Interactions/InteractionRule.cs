using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects;

namespace DCC.Core.Interactions
{
    /// <summary>
    /// A single conditional rule in the interaction engine.
    /// Rules are ScriptableObject assets — designers author them, not programmers.
    ///
    /// A rule fires when ALL of RequiredEffectTags are present in the composite
    /// tag mask of the active effects, AND ALL of RequiredTargetTags are on the target.
    /// When fired, it spawns ResultEffects and/or adds ResultTags to the target or zone.
    ///
    /// Rules are evaluated AFTER EffectComposer has already merged and resolved the
    /// composite effect mask. They are an additional layer that allows designers to
    /// define "if these emergent conditions coexist, something extra happens."
    ///
    /// Most emergence doesn't need rules — it flows naturally from tag composition.
    /// Rules are for cases that need a qualitative state change:
    ///   e.g., "if [Gas] + [Burning] exist together → spawn an Explosion effect"
    ///   because an explosion is a fundamentally different event, not just
    ///   a magnitude change.
    ///
    /// Evaluation priority: lower Priority value = evaluated first.
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Interaction Rule", fileName = "Rule_New")]
    public class InteractionRule : ScriptableObject
    {
        [field: SerializeField] public string RuleName { get; private set; }

        [Header("Conditions")]
        [field: SerializeField, Tooltip("All of these must be in the composite effect tag mask.")]
        public TagDefinition[] RequiredEffectTags { get; private set; }

        [field: SerializeField, Tooltip("All of these must be on the TARGET entity/zone.")]
        public TagDefinition[] RequiredTargetTags { get; private set; }

        [field: SerializeField, Tooltip("The rule will NOT fire if any of these are present.")]
        public TagDefinition[] BlockingTags { get; private set; }

        [Header("Results")]
        [field: SerializeField, Tooltip("These effects are applied to the target when the rule fires.")]
        public EffectDefinition[] ResultEffects { get; private set; }

        [field: SerializeField, Tooltip("These tags are added to the target when the rule fires.")]
        public TagDefinition[] GrantedTargetTags { get; private set; }

        [field: SerializeField, Tooltip("These tags are removed from the target when the rule fires.")]
        public TagDefinition[] RemovedTargetTags { get; private set; }

        [field: SerializeField, Tooltip("Optional: spawn this prefab at the effect origin.")]
        public GameObject SpawnPrefab { get; private set; }

        [Header("Misc")]
        [field: SerializeField, Tooltip("Lower = evaluated first. Use to control rule ordering.")]
        public int Priority { get; private set; } = 0;

        [field: SerializeField, Tooltip("Seconds before this rule can fire again on the same target. 0 = always.")]
        public float Cooldown { get; private set; } = 0f;

        [field: SerializeField, Tooltip("Once fired, this rule cannot fire again on this target.")]
        public bool FireOnce { get; private set; } = false;

        /// <summary>
        /// Check whether this rule's conditions are met.
        /// compositeEffectMask: the resolved union of all active effect tags.
        /// targetMask: the target entity/zone's resolved effective tag mask.
        /// </summary>
        public bool Matches(TagMask compositeEffectMask, TagMask targetMask)
        {
            if (RequiredEffectTags != null)
                foreach (var tag in RequiredEffectTags)
                    if (tag != null && !compositeEffectMask.HasTag(tag)) return false;

            if (RequiredTargetTags != null)
                foreach (var tag in RequiredTargetTags)
                    if (tag != null && !targetMask.HasTag(tag)) return false;

            if (BlockingTags != null)
                foreach (var tag in BlockingTags)
                    if (tag != null && (compositeEffectMask.HasTag(tag) || targetMask.HasTag(tag)))
                        return false;

            return true;
        }
    }
}
