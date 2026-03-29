using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Effects;
using DCC.Core.Entities;
using DCC.Core.Interactions;

namespace DCC.Combat
{
    /// <summary>
    /// A server-authoritative flying projectile that carries a payload of effects.
    ///
    /// On collision with a valid target, it routes through InteractionEngine so all
    /// tag composition, rule evaluation, and discovery logging happen automatically.
    ///
    /// Projectiles themselves have a TagContainer — this means:
    ///   - A "fire arrow" can be tagged [Fire, Projectile, Piercing]
    ///   - Flying through a [Wet] zone gains [Wet] tag (via rule or manual infusion)
    ///   - Now it's a "wet fire arrow" — fire is suppressed, hits deal reduced burn
    ///   - That emergence came from the environment, not item design
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Core.Tags.TagContainer))]
    public class Projectile : NetworkBehaviour
    {
        private IReadOnlyList<EffectDefinition> _effects;
        private EffectContext _context;
        private float _speed;
        private float _maxRange;
        private Vector3 _origin;

        private Rigidbody _rb;
        private bool _hit;

        // ── Initialization ─────────────────────────────────────────────────

        public void Initialize(
            IReadOnlyList<EffectDefinition> effects,
            EffectContext context,
            float speed,
            float maxRange)
        {
            _effects = effects;
            _context = context;
            _speed = speed;
            _maxRange = maxRange;
            _origin = transform.position;

            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.velocity = transform.forward * _speed;
        }

        private void Update()
        {
            if (!IsServer || _hit) return;

            // Self-destruct if over max range.
            if (Vector3.Distance(_origin, transform.position) >= _maxRange)
                NetworkObject.Despawn(true);
        }

        // ── Collision ──────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || _hit) return;

            var entity = other.GetComponentInParent<EntityAttributes>();
            if (entity == null) return;

            // Ignore source entity for the first frame (avoid self-hit on spawn).
            if (entity.NetworkObjectId == _context.SourceNetworkObjectId) return;

            _hit = true;

            // Update origin to impact point for AoE sub-effects.
            _context.OriginPosition = transform.position;

            InteractionEngine.Instance?.Resolve(_effects, entity, _context);

            NetworkObject.Despawn(true);
        }
    }
}
