using System;
using UnityEngine;
using DCC.Core.Tags;

namespace DCC.Core.Effects
{
    /// <summary>
    /// A live, running instance of an EffectDefinition applied to a specific target.
    /// Pooled — returned to pool when duration expires or the effect is explicitly removed.
    ///
    /// The definition owns the logic; the instance owns the mutable runtime state
    /// (remaining duration, accumulated tick time, resolved magnitude).
    /// </summary>
    public class EffectInstance
    {
        public EffectDefinition Definition { get; private set; }
        public EffectContext Context { get; private set; }

        /// <summary>Resolved magnitude after source/target tag modifiers are applied.</summary>
        public float ResolvedMagnitude { get; internal set; }

        public float RemainingDuration { get; internal set; }
        public float TimeSinceLastTick { get; internal set; }

        /// <summary>True once the initial OnApply has been called.</summary>
        public bool Applied { get; internal set; }

        /// <summary>Set by EntityAttributes when the instance is removed.</summary>
        public bool IsExpired { get; internal set; }

        /// <summary>Tags this instance granted to its target (for cleanup on removal).</summary>
        public TagDefinition[] GrantedTags { get; internal set; }

        public static EffectInstance Create(EffectDefinition definition, EffectContext context, float resolvedMagnitude)
        {
            // Pooling would go here in production.
            return new EffectInstance
            {
                Definition = definition,
                Context = context,
                ResolvedMagnitude = resolvedMagnitude,
                RemainingDuration = definition.Duration,
                TimeSinceLastTick = 0f,
                Applied = false,
                IsExpired = false,
                GrantedTags = definition.GrantedTags
            };
        }

        public void Release()
        {
            // Return to pool.
            Definition = null;
            IsExpired = true;
        }
    }
}
