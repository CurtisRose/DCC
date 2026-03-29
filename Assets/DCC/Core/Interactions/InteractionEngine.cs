using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using DCC.Core.Tags;
using DCC.Core.Effects;
using DCC.Core.Entities;

namespace DCC.Core.Interactions
{
    /// <summary>
    /// Top-level server-authoritative orchestrator for all emergent interactions.
    ///
    /// Step-by-step flow for any interaction:
    ///   1. Caller passes: list of effects, target entity, effect context
    ///   2. Engine builds composite tag mask from all effect GrantedTags
    ///   3. Engine resolves that mask (implies + suppresses)
    ///   4. Engine evaluates all InteractionRules against composite + target masks
    ///   5. Engine calls EffectComposer.ApplyAll (which applies individual effects)
    ///   6. Rules that matched fire their ResultEffects and tag mutations
    ///   7. Engine notifies DiscoverySystem if a novel combination was seen
    ///
    /// The engine does NOT know about specific item types. It only knows about tags.
    /// Adding a new item type (say, "Acid Flask") is purely a matter of authoring
    /// its TagDefinitions and EffectDefinitions; no engine code changes.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionEngine : NetworkBehaviour
    {
        public static InteractionEngine Instance { get; private set; }

        [SerializeField, Tooltip("All interaction rules evaluated each interaction. Sorted by Priority at startup.")]
        private InteractionRule[] _rules = new InteractionRule[0];

        // Per-(target, rule) cooldown tracking.
        private readonly Dictionary<(ulong, InteractionRule), float> _cooldowns = new();
        // Per-(target, rule) fired-once tracking.
        private readonly HashSet<(ulong, InteractionRule)> _firedOnce = new();

        private void Awake()
        {
            Instance = this;
            SortRules();
        }

        private void SortRules()
        {
            System.Array.Sort(_rules, (a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>
        /// Main entry point. Call this any time effects need to be applied to a target
        /// with the full emergent interaction evaluation.
        /// </summary>
        public void Resolve(
            IReadOnlyList<EffectDefinition> effects,
            EntityAttributes target,
            EffectContext context)
        {
            if (!IsServer) return;
            if (effects == null || effects.Count == 0 || target == null) return;

            // Build composite tag mask from all incoming effects.
            var compositeMask = BuildCompositeMask(effects);
            var resolvedComposite = compositeMask.Resolve();
            var targetMask = target.Tags.EffectiveMask;

            // Evaluate rules first (they may add effects or modify tags before application).
            EvaluateRules(resolvedComposite, target, context);

            // Apply effects via the composer (handles magnitude scaling, CanApplyTo, etc.).
            EffectComposer.ApplyAll(effects, target, context);

            // Notify discovery.
            if (UI.DiscoverySystem.Instance != null)
                UI.DiscoverySystem.Instance.RecordInteraction(resolvedComposite, context.OwnerClientId);
        }

        /// <summary>Convenience overload for a single effect.</summary>
        public void ResolveSingle(EffectDefinition effect, EntityAttributes target, EffectContext context)
        {
            Resolve(new[] { effect }, target, context);
        }

        // ── Private ────────────────────────────────────────────────────────

        private void EvaluateRules(TagMask compositeEffectMask, EntityAttributes target, EffectContext context)
        {
            var targetMask = target.Tags.EffectiveMask;
            ulong targetId = target.NetworkObjectId;
            float now = Time.time;

            foreach (var rule in _rules)
            {
                if (rule == null) continue;

                // Fire-once check.
                var key = (targetId, rule);
                if (rule.FireOnce && _firedOnce.Contains(key)) continue;

                // Cooldown check.
                if (rule.Cooldown > 0f)
                {
                    if (_cooldowns.TryGetValue(key, out float lastFired))
                        if (now - lastFired < rule.Cooldown) continue;
                }

                if (!rule.Matches(compositeEffectMask, targetMask)) continue;

                // Rule fires.
                FireRule(rule, target, context, key, now);
            }
        }

        private void FireRule(
            InteractionRule rule,
            EntityAttributes target,
            EffectContext context,
            (ulong, InteractionRule) key,
            float now)
        {
            // Track cooldown / fire-once.
            if (rule.Cooldown > 0f) _cooldowns[key] = now;
            if (rule.FireOnce) _firedOnce.Add(key);

            // Apply result effects.
            if (rule.ResultEffects != null && rule.ResultEffects.Length > 0)
                EffectComposer.ApplyAll(rule.ResultEffects, target, context);

            // Mutate target tags.
            if (rule.GrantedTargetTags != null)
                target.Tags.AddTags(rule.GrantedTargetTags);
            if (rule.RemovedTargetTags != null)
                target.Tags.RemoveTags(rule.RemovedTargetTags);

            // Spawn a prefab (e.g., explosion VFX, new zone).
            if (rule.SpawnPrefab != null)
            {
                var go = Instantiate(rule.SpawnPrefab, context.OriginPosition, Quaternion.identity);
                var netObj = go.GetComponent<NetworkObject>();
                netObj?.Spawn(destroyWithScene: true);
            }

            Debug.Log($"[InteractionEngine] Rule fired: {rule.RuleName} on {target.name}");
        }

        private static TagMask BuildCompositeMask(IReadOnlyList<EffectDefinition> effects)
        {
            var mask = new TagMask();
            foreach (var effect in effects)
            {
                if (effect?.GrantedTags == null) continue;
                foreach (var tag in effect.GrantedTags)
                    if (tag != null && tag.RuntimeId >= 0)
                        mask.Set(tag.RuntimeId);
            }
            return mask;
        }

        private void Update()
        {
            // Prune expired cooldown entries to prevent unbounded growth.
            if (Time.frameCount % 600 == 0) PruneCooldowns();
        }

        private void PruneCooldowns()
        {
            float now = Time.time;
            var toRemove = new List<(ulong, InteractionRule)>();
            foreach (var kvp in _cooldowns)
            {
                var rule = kvp.Key.Item2;
                if (now - kvp.Value > rule.Cooldown * 2f)
                    toRemove.Add(kvp.Key);
            }
            foreach (var k in toRemove) _cooldowns.Remove(k);
        }
    }
}
