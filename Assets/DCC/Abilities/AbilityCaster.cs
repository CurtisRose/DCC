using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Effects;
using DCC.Core.Entities;
using DCC.Core.Interactions;
using DCC.Combat;

namespace DCC.Abilities
{
    /// <summary>
    /// Server-authoritative ability execution. Attached alongside EntityAttributes.
    /// PlayerNetworkController.UseAbilityServerRpc calls CastAbility here.
    ///
    /// Cooldowns are stored per-slot and validated on the server.
    /// Clients send the request; the server decides whether it fires.
    /// </summary>
    [RequireComponent(typeof(EntityAttributes))]
    public class AbilityCaster : NetworkBehaviour
    {
        [SerializeField] private AbilityDefinition[] _equippedAbilities = new AbilityDefinition[4];

        private EntityAttributes _caster;
        private readonly Dictionary<int, float> _lastUsedTime = new();

        private void Awake() => _caster = GetComponent<EntityAttributes>();

        /// <summary>Called server-side only. Validates and executes the ability.</summary>
        public void CastAbility(int slot, Vector3 targetPos, ulong callerClientId)
        {
            if (!IsServer) return;
            if (slot < 0 || slot >= _equippedAbilities.Length) return;

            var ability = _equippedAbilities[slot];
            if (ability == null) return;
            if (!_caster.IsAlive) return;

            // Cooldown check.
            if (_lastUsedTime.TryGetValue(slot, out float last) &&
                Time.time - last < ability.Cooldown) return;

            // Caster tag requirements.
            if (ability.RequiredCasterTags != null)
                foreach (var tag in ability.RequiredCasterTags)
                    if (tag != null && !_caster.Tags.HasTag(tag)) return;

            _lastUsedTime[slot] = Time.time;

            var ctx = EffectContext.FromNetworkObject(NetworkObject);
            ctx.OriginPosition = targetPos;

            switch (ability.Mode)
            {
                case AbilityDefinition.CastMode.Instant:
                    ExecuteInstant(ability, targetPos, ctx);
                    break;
                case AbilityDefinition.CastMode.Projectile:
                    LaunchProjectile(ability, targetPos, ctx);
                    break;
                case AbilityDefinition.CastMode.SpawnZone:
                    SpawnZone(ability, targetPos, ctx);
                    break;
                case AbilityDefinition.CastMode.Channeled:
                    StartCoroutine(ChannelAbility(ability, targetPos, ctx));
                    break;
            }
        }

        // ── Execution modes ────────────────────────────────────────────────

        private void ExecuteInstant(AbilityDefinition ability, Vector3 targetPos, EffectContext ctx)
        {
            if (ability.AoERadius > 0f)
            {
                var cols = Physics.OverlapSphere(targetPos, ability.AoERadius);
                foreach (var col in cols)
                {
                    var attrs = col.GetComponentInParent<EntityAttributes>();
                    if (attrs != null && attrs.IsAlive)
                        InteractionEngine.Instance?.Resolve(ability.Effects, attrs, ctx);
                }
            }
            else
            {
                // Single target: ray against target.
                var cols = Physics.OverlapSphere(targetPos, 1f);
                foreach (var col in cols)
                {
                    var attrs = col.GetComponentInParent<EntityAttributes>();
                    if (attrs != null && attrs.IsAlive)
                    {
                        InteractionEngine.Instance?.Resolve(ability.Effects, attrs, ctx);
                        break;
                    }
                }
            }
        }

        private void LaunchProjectile(AbilityDefinition ability, Vector3 targetPos, EffectContext ctx)
        {
            if (ability.ProjectilePrefab == null) return;
            var dir = (targetPos - transform.position).normalized;
            var go = Instantiate(ability.ProjectilePrefab, transform.position + dir, Quaternion.LookRotation(dir));
            var proj = go.GetComponent<Projectile>();
            if (proj != null)
                proj.Initialize(ability.Effects, ctx, ability.ProjectileSpeed, ability.CastRange);
            go.GetComponent<NetworkObject>()?.Spawn(destroyWithScene: true);
        }

        private void SpawnZone(AbilityDefinition ability, Vector3 targetPos, EffectContext ctx)
        {
            if (ability.ZoneToSpawn?.VisualPrefab == null) return;
            var go = Instantiate(ability.ZoneToSpawn.VisualPrefab, targetPos, Quaternion.identity);
            go.GetComponent<NetworkObject>()?.Spawn(destroyWithScene: true);
        }

        private System.Collections.IEnumerator ChannelAbility(
            AbilityDefinition ability, Vector3 targetPos, EffectContext ctx)
        {
            float elapsed = 0f;
            while (elapsed < ability.ChannelDuration && _caster.IsAlive)
            {
                ExecuteInstant(ability, targetPos, ctx);
                yield return new WaitForSeconds(ability.ChannelTickInterval);
                elapsed += ability.ChannelTickInterval;
            }
        }
    }
}
