using System.Collections.Generic;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;

namespace DCC.Core.Effects
{
    /// <summary>
    /// The heart of the emergent system.
    ///
    /// Given a set of effects (from a zone, a projectile impact, overlapping items, etc.)
    /// and a target, the EffectComposer:
    ///   1. Unions all effect tag sets into a composite tag mask
    ///   2. Resolves that mask (implies + suppresses)
    ///   3. For each effect, checks CanApplyTo against the COMPOSITE mask (not just raw target)
    ///   4. Resolves magnitude using the composite context
    ///   5. Logs novel tag combinations to the DiscoverySystem
    ///
    /// This is where "smoke + healing" becomes "healing cloud" without any code that
    /// knows those two things exist: both effects write their tags into the zone's
    /// TagContainer, the composer unions them, and any entity inside inherits the result.
    /// </summary>
    public static class EffectComposer
    {
        /// <summary>
        /// Apply a collection of effects to a target, merging their tag contexts first.
        /// Call this from ZoneTicker (zone → entity) or HitResolver (projectile → entity).
        /// </summary>
        public static void ApplyAll(
            IReadOnlyList<EffectDefinition> effects,
            EntityAttributes target,
            EffectContext context)
        {
            if (effects == null || effects.Count == 0) return;
            if (target == null) return;

            // Build the composite tag mask: union of all effect granted-tag sets.
            var compositeTags = new TagMask();
            foreach (var effect in effects)
            {
                if (effect?.GrantedTags == null) continue;
                foreach (var tag in effect.GrantedTags)
                    if (tag != null && tag.RuntimeId >= 0)
                        compositeTags.Set(tag.RuntimeId);
            }

            // Resolve: expand implications, remove suppressions.
            var resolved = compositeTags.Resolve();

            // Build a synthetic composite context enriched with the merged tags.
            var compositeContext = new EffectContext
            {
                SourceNetworkObjectId = context.SourceNetworkObjectId,
                OwnerClientId = context.OwnerClientId,
                OriginPosition = context.OriginPosition,
                SourceTags = context.SourceTags.Union(resolved),
                ValueOverride = context.ValueOverride
            };

            // Apply each effect if it can land on this target given the composite context.
            foreach (var effect in effects)
            {
                if (effect == null) continue;
                if (!effect.CanApplyTo(target.Tags, compositeContext)) continue;

                float magnitude = effect.ResolveMagnitude(target.Tags, compositeContext);
                var instance = EffectInstance.Create(effect, compositeContext, magnitude);
                target.ApplyEffect(instance);
            }

            // Notify the discovery system about this tag combination.
            if (UI.DiscoverySystem.Instance != null)
                UI.DiscoverySystem.Instance.RecordInteraction(resolved, context.OwnerClientId);
        }

        /// <summary>
        /// Apply a single effect to a target, without composite merging.
        /// Use this for direct, isolated effects (e.g., a basic sword swing).
        /// </summary>
        public static void ApplySingle(
            EffectDefinition effect,
            EntityAttributes target,
            EffectContext context)
        {
            if (effect == null || target == null) return;
            if (!effect.CanApplyTo(target.Tags, context)) return;

            float magnitude = effect.ResolveMagnitude(target.Tags, context);
            var instance = EffectInstance.Create(effect, context, magnitude);
            target.ApplyEffect(instance);
        }
    }
}
