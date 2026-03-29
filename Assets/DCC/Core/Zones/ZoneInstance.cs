using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects;
using DCC.Core.Entities;

namespace DCC.Core.Zones
{
    /// <summary>
    /// A live, networked zone in the world. This is the spatial surface where
    /// emergent combinations are most visible.
    ///
    /// A zone owns a TagContainer. When an effect is "infused" into a zone:
    ///   1. The effect's GrantedTags are added to the zone's TagContainer
    ///   2. The effect is added to the zone's active effect list
    ///   3. ZoneTicker begins applying the combined effect set to all overlapping entities
    ///
    /// This means the zone itself becomes the composite — the "healing smoke cloud"
    /// IS the zone, with tags [Gas, Smoke, Healing] and effects [SmokeEffect, HealEffect].
    ///
    /// Server-authoritative. The trigger overlap runs on server only.
    /// Visual feedback (particles, glow) is handled client-side by ZoneVisuals.
    /// </summary>
    [RequireComponent(typeof(TagContainer))]
    public class ZoneInstance : NetworkBehaviour, IZone
    {
        [SerializeField] private ZoneDefinition _definition;

        public TagContainer Tags { get; private set; }
        public ulong OwnerClientId => OwnerClientId;

        private readonly List<EffectDefinition> _activeEffects = new();
        private float _remainingLifetime;
        private float _tickAccumulator;

        // Overlap detection.
        private readonly Collider[] _overlapBuffer = new Collider[32];
        private SphereCollider _trigger;

        public IReadOnlyList<EffectDefinition> ActiveEffects => _activeEffects;

        // ── Lifecycle ──────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Tags = GetComponent<TagContainer>();

            if (!IsServer) return;

            _remainingLifetime = _definition != null ? _definition.Lifetime : 10f;

            // Apply initial tags from definition.
            if (_definition?.InitialTags != null)
                Tags.AddTags(_definition.InitialTags);

            // Register initial effects.
            if (_definition?.InitialEffects != null)
                foreach (var e in _definition.InitialEffects)
                    if (e != null) InfuseEffect(e, EffectContext.Environment(transform.position));

            SetupTrigger();
        }

        private void SetupTrigger()
        {
            _trigger = GetComponent<SphereCollider>();
            if (_trigger == null) _trigger = gameObject.AddComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _trigger.radius = _definition?.Radius ?? 3f;
        }

        private void Update()
        {
            if (!IsServer) return;

            // Lifetime countdown.
            if (_remainingLifetime >= 0f)
            {
                _remainingLifetime -= Time.deltaTime;
                if (_remainingLifetime <= 0f)
                {
                    NetworkObject.Despawn(true);
                    return;
                }
            }

            // Tick all overlapping entities.
            _tickAccumulator += Time.deltaTime;
            float interval = _definition?.TickInterval ?? 1f;
            if (_tickAccumulator >= interval)
            {
                _tickAccumulator -= interval;
                TickOverlappingEntities();
            }
        }

        // ── Infusion ───────────────────────────────────────────────────────

        /// <summary>
        /// Add an effect to this zone, merging its tags into the zone's TagContainer.
        /// This is how "healing potion lands in smoke zone" creates healing smoke.
        /// </summary>
        public void InfuseEffect(EffectDefinition effect, EffectContext context)
        {
            if (!IsServer) return;
            if (effect == null) return;
            if (_definition != null && !_definition.AcceptsInfusion) return;
            if (_definition != null && _definition.MaxInfusedEffects > 0 &&
                _activeEffects.Count >= _definition.MaxInfusedEffects) return;

            _activeEffects.Add(effect);

            // Merge the effect's tags into this zone — this is the emergence moment.
            if (effect.GrantedTags != null)
                Tags.AddTags(effect.GrantedTags);

            Debug.Log($"[Zone:{name}] Infused effect: {effect.DisplayName}. " +
                      $"Zone tags now: {Tags.EffectiveMask}");
        }

        public void RemoveEffect(EffectDefinition effect)
        {
            if (!IsServer || effect == null) return;
            if (!_activeEffects.Remove(effect)) return;
            if (effect.GrantedTags != null)
                Tags.RemoveTags(effect.GrantedTags);
        }

        // ── Overlap ticking ────────────────────────────────────────────────

        private void TickOverlappingEntities()
        {
            if (_activeEffects.Count == 0) return;

            var layerMask = _definition?.AffectedLayers ?? Physics.DefaultRaycastLayers;
            float radius = _trigger != null ? _trigger.radius : 3f;

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, radius, _overlapBuffer, layerMask);

            var ctx = new EffectContext
            {
                SourceNetworkObjectId = NetworkObjectId,
                OwnerClientId = NetworkObject.OwnerClientId,
                OriginPosition = transform.position,
                SourceTags = Tags.EffectiveMask
            };

            for (int i = 0; i < count; i++)
            {
                var entity = _overlapBuffer[i].GetComponentInParent<EntityAttributes>();
                if (entity == null || !entity.IsAlive) continue;

                // Route through InteractionEngine for rule evaluation + discovery.
                if (Interactions.InteractionEngine.Instance != null)
                    Interactions.InteractionEngine.Instance.Resolve(_activeEffects, entity, ctx);
                else
                    EffectComposer.ApplyAll(_activeEffects, entity, ctx);
            }
        }

        // ── IZone ──────────────────────────────────────────────────────────
        TagContainer ITagged.Tags => Tags;
        IReadOnlyList<EffectDefinition> IZone.ActiveEffectDefinitions => _activeEffects;
        void IZone.AddEffect(EffectDefinition effect, EffectContext context) => InfuseEffect(effect, context);
    }

    // ── Interface ──────────────────────────────────────────────────────────────

    public interface IZone : ITagged
    {
        IReadOnlyList<EffectDefinition> ActiveEffectDefinitions { get; }
        ulong OwnerClientId { get; }
        void AddEffect(EffectDefinition effect, EffectContext context);
    }
}
