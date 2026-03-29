using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using DCC.Core.Entities;
using DCC.Core.Tags;

namespace DCC.Core.Effects.Modifiers
{
    /// <summary>
    /// Area-of-effect burst that applies a set of child effects to all valid targets
    /// within a radius. The AoE itself is not a zone — it's a one-frame overlap scan.
    ///
    /// For persistent zones (smoke, fire puddle), use ZoneInstance instead.
    ///
    /// Emergent example — Gas Cloud Explosion:
    ///   A zone with [Gas, Flammable] tags exists.
    ///   A FireDamageEffect lands in the zone (its GrantedTags include [Fire, Hot]).
    ///   ZoneTicker evaluates: zone's composite tags include [Flammable].
    ///   The Gas zone's TagDefinition has: "if composite includes [Burning], amplify × 3 and add [Explosive]".
    ///   No "gas + fire = explosion" rule is needed; the tag modifiers cause it naturally.
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Effect/Explosion", fileName = "Effect_Explosion")]
    public class ExplosionEffect : EffectDefinition
    {
        [field: SerializeField] public float Radius { get; private set; } = 4f;
        [field: SerializeField] public LayerMask TargetLayers { get; private set; }

        [field: SerializeField, Tooltip("These effects are applied to every entity in the blast radius.")]
        public EffectDefinition[] ChainedEffects { get; private set; }

        [field: SerializeField, Tooltip("Apply knockback force at this magnitude. 0 = no knockback.")]
        public float KnockbackForce { get; private set; } = 5f;

        private readonly Collider[] _overlapBuffer = new Collider[32];

        public override void OnApply(EffectInstance instance, EntityAttributes source)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            Vector3 origin = instance.Context.OriginPosition;
            int count = Physics.OverlapSphereNonAlloc(origin, Radius, _overlapBuffer, TargetLayers);

            for (int i = 0; i < count; i++)
            {
                var attrs = _overlapBuffer[i].GetComponentInParent<EntityAttributes>();
                if (attrs == null || !attrs.IsAlive) continue;

                // Apply each chained effect via the composer (handles tag merging).
                EffectComposer.ApplyAll(ChainedEffects, attrs, instance.Context);

                // Knockback (server-side physics).
                if (KnockbackForce > 0f)
                {
                    var rb = attrs.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 dir = (attrs.transform.position - origin).normalized;
                        float falloff = 1f - (Vector3.Distance(origin, attrs.transform.position) / Radius);
                        rb.AddForce(dir * KnockbackForce * falloff, ForceMode.Impulse);
                    }
                }
            }
        }
    }
}
