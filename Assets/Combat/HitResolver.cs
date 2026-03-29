using Unity.Netcode;
using UnityEngine;
using DCC.Core.Effects;
using DCC.Core.Entities;
using DCC.Core.Interactions;

namespace DCC.Combat
{
    /// <summary>
    /// Utility for melee/instant-hit attacks. Routes through InteractionEngine.
    ///
    /// Note on friendly fire: HitResolver applies effects to ALL valid targets in range,
    /// regardless of allegiance. The AllegianceMatrix is queried after the fact only
    /// to attribute XP/loot correctly. Damage is always applied.
    ///
    /// "Valid" means: has EntityAttributes, IsAlive, and effect's CanApplyTo passes.
    /// There is no allegiance gate on CanApplyTo.
    /// </summary>
    public static class HitResolver
    {
        /// <summary>Resolves a melee swing in a sphere around origin.</summary>
        public static void ResolveMelee(
            EffectDefinition[] effects,
            EffectContext context,
            Vector3 origin,
            float radius,
            int layerMask = Physics.DefaultRaycastLayers)
        {
            var cols = Physics.OverlapSphere(origin, radius, layerMask);
            foreach (var col in cols)
            {
                var attrs = col.GetComponentInParent<EntityAttributes>();
                if (attrs == null || !attrs.IsAlive) continue;
                if (attrs.NetworkObjectId == context.SourceNetworkObjectId) continue;

                InteractionEngine.Instance?.Resolve(effects, attrs, context);
            }
        }

        /// <summary>Resolves a hit against a single specific target.</summary>
        public static void ResolveSingleTarget(
            EffectDefinition[] effects,
            EffectContext context,
            EntityAttributes target)
        {
            if (target == null || !target.IsAlive) return;
            InteractionEngine.Instance?.Resolve(effects, target, context);
        }
    }
}
