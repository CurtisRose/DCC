using System;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;

namespace DCC.Core.Effects
{
    /// <summary>
    /// The definition (archetype) of an effect. Lives as a ScriptableObject asset.
    /// EffectDefinitions are the "verbs" of the emergent system — they describe
    /// what happens, not who triggered it or where it came from.
    ///
    /// Subclass this for specific effect types (DamageEffect, HealEffect, etc.).
    /// Each subclass overrides OnApply / OnTick / OnRemove with its logic.
    ///
    /// Effects compose through the tag system:
    ///   - GrantedTags: tags this effect adds to the zone or entity it is applied to
    ///   - RequiredTargetTags: gate — effect only fires if target has all these tags
    ///   - AmplifiedByTargetTags: if target has these, magnitude multiplies
    ///   - DiminishedByTargetTags: if target has these, magnitude shrinks
    ///
    /// IMPORTANT: No effect should ever reference another specific effect by name.
    /// All cross-effect interactions happen via tags.
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Effect/Base (do not use directly)", fileName = "Effect_New")]
    public abstract class EffectDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Tag Behaviour")]
        /// <summary>Tags added to the target when this effect is applied. Removed when effect ends.</summary>
        [field: SerializeField] public TagDefinition[] GrantedTags { get; private set; }

        /// <summary>Effect only applies if target's effective mask includes ALL of these.</summary>
        [field: SerializeField] public TagDefinition[] RequiredTargetTags { get; private set; }

        /// <summary>Effects that share any of these tags cannot be applied simultaneously with this one.</summary>
        [field: SerializeField] public TagDefinition[] BlockedConcurrentTags { get; private set; }

        [Header("Magnitude Modifiers (by target tags)")]
        [field: SerializeField] public TaggedModifier[] AmplifiedByTargetTags { get; private set; }
        [field: SerializeField] public TaggedModifier[] DiminishedByTargetTags { get; private set; }

        [Header("Magnitude Modifiers (by source tags)")]
        [field: SerializeField] public TaggedModifier[] AmplifiedBySourceTags { get; private set; }
        [field: SerializeField] public TaggedModifier[] DiminishedBySourceTags { get; private set; }

        [Header("Timing")]
        [field: SerializeField, Tooltip("Seconds. 0 = instant. -1 = permanent until explicitly removed.")]
        public float Duration { get; private set; } = 0f;

        [field: SerializeField, Tooltip("Seconds between OnTick calls. 0 = tick every frame.")]
        public float TickInterval { get; private set; } = 1f;

        [Header("Base Value")]
        [field: SerializeField, Tooltip("Base magnitude (damage, heal amount, etc). Scaled by tag modifiers.")]
        public float BaseMagnitude { get; private set; } = 10f;

        [Header("Zone Behaviour")]
        [field: SerializeField, Tooltip("If set, this effect can infuse itself into a Zone, creating an area effect.")]
        public bool CanInfuseZone { get; private set; } = true;

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether this effect can be applied to the given target.
        /// Override for custom logic (e.g., line-of-sight requirements).
        /// </summary>
        public virtual bool CanApplyTo(TagContainer target, EffectContext context)
        {
            if (RequiredTargetTags == null || RequiredTargetTags.Length == 0) return true;
            foreach (var tag in RequiredTargetTags)
                if (tag != null && !target.HasTag(tag)) return false;
            return true;
        }

        /// <summary>
        /// Resolves the final magnitude for this effect given source and target tags.
        /// Applies amplification and diminishment multipliers from designer data.
        /// </summary>
        public float ResolveMagnitude(TagContainer target, EffectContext context)
        {
            float value = context.ValueOverride >= 0f ? context.ValueOverride : BaseMagnitude;

            if (target != null)
            {
                if (AmplifiedByTargetTags != null)
                    foreach (var mod in AmplifiedByTargetTags)
                        if (mod.Tag != null && target.HasTag(mod.Tag))
                            value = value * mod.Multiplier + mod.FlatBonus;

                if (DiminishedByTargetTags != null)
                    foreach (var mod in DiminishedByTargetTags)
                        if (mod.Tag != null && target.HasTag(mod.Tag))
                            value = value * mod.Multiplier + mod.FlatBonus;
            }

            if (AmplifiedBySourceTags != null)
                foreach (var mod in AmplifiedBySourceTags)
                    if (mod.Tag != null && context.SourceTags.HasTag(mod.Tag))
                        value = value * mod.Multiplier + mod.FlatBonus;

            if (DiminishedBySourceTags != null)
                foreach (var mod in DiminishedBySourceTags)
                    if (mod.Tag != null && context.SourceTags.HasTag(mod.Tag))
                        value = value * mod.Multiplier + mod.FlatBonus;

            return value;
        }

        // ── Overrideable lifecycle ─────────────────────────────────────────

        /// <summary>Called once when the effect first lands on a target (or each tick for instant effects).</summary>
        public abstract void OnApply(EffectInstance instance, EntityAttributes target);

        /// <summary>Called each tick interval while the effect is active.</summary>
        public virtual void OnTick(EffectInstance instance, EntityAttributes target) { }

        /// <summary>Called when the effect expires or is manually removed.</summary>
        public virtual void OnRemove(EffectInstance instance, EntityAttributes target) { }

        [Header("Inversion (for ResistanceProfile.InvertsEffect)")]
        [field: SerializeField, Tooltip(
            "The effect to swap to when a ResistanceProfile inverts this effect.\n" +
            "Example: HealEffect.InvertedEffect = DamageEffect of the same magnitude.\n" +
            "Leave null to simply negate the magnitude (heal 50 → damage 50 as damage).")]
        public EffectDefinition InvertedEffect { get; private set; }

        /// <summary>
        /// Called by EntityAttributes when a ResistanceProfile.InvertsEffect is true.
        /// If InvertedEffect is assigned, redirects the instance to that definition.
        /// Otherwise negates magnitude (works for most heal/damage pairs).
        /// </summary>
        public void InvertEffect(EffectInstance instance)
        {
            if (InvertedEffect != null)
            {
                instance.SwapDefinition(InvertedEffect);
            }
            else
            {
                // Negate: a positive heal becomes a negative heal = damage, handled
                // by the effect's OnApply reading the sign of ResolvedMagnitude.
                instance.ResolvedMagnitude = -instance.ResolvedMagnitude;
            }
        }
    }

    // ── Supporting types ───────────────────────────────────────────────────────

    [Serializable]
    public struct TaggedModifier
    {
        [Tooltip("If the target/source has this tag, apply the modifier.")]
        public TagDefinition Tag;

        [Tooltip("Multiplicative scale. 2.0 = double, 0.5 = half.")]
        public float Multiplier;

        [Tooltip("Flat bonus/penalty added after multiplication.")]
        public float FlatBonus;

        public TaggedModifier(TagDefinition tag, float multiplier, float flatBonus = 0f)
        {
            Tag = tag;
            Multiplier = multiplier;
            FlatBonus = flatBonus;
        }
    }
}
