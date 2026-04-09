using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects;
using DCC.Core.Allegiance;

namespace DCC.Core.Entities
{
    /// <summary>
    /// The central component on every living and interactable entity.
    /// Owns the AttributeSet (health, mana, crawler stats, armor, speed) and manages
    /// all active EffectInstances.
    ///
    /// Server-authoritative:
    ///   - Health and Mana are NetworkVariables so all clients see them.
    ///   - Crawler stats (Str/Con/Dex/Int/Cha) are server-authoritative.
    ///   - Effect application and ticking only run on the server.
    ///   - Health and mana regen tick server-side.
    ///
    /// Friendly fire is always on: this class applies damage regardless of allegiance.
    /// </summary>
    [RequireComponent(typeof(TagContainer))]
    [RequireComponent(typeof(AllegianceComponent))]
    public class EntityAttributes : NetworkBehaviour, IEffectable
    {
        [SerializeField] private EntityDefinition _definition;

        // Public accessors.
        public AttributeSet AttributeSet { get; } = new();
        public TagContainer Tags { get; private set; }
        public AllegianceComponent Allegiance { get; private set; }
        public bool IsAlive => AttributeSet.IsAlive;

        // Networked health so clients can show accurate health bars.
        private NetworkVariable<float> _networkHealth = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        private NetworkVariable<float> _networkMaxHealth = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        // Networked mana so clients can show mana bars.
        private NetworkVariable<float> _networkMana = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        private NetworkVariable<float> _networkMaxMana = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        // Networked level for UI display.
        private NetworkVariable<int> _networkLevel = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        public float NetworkHealth => _networkHealth.Value;
        public float NetworkMaxHealth => _networkMaxHealth.Value;
        public float NetworkMana => _networkMana.Value;
        public float NetworkMaxMana => _networkMaxMana.Value;
        public int NetworkLevel => _networkLevel.Value;

        // Server-only: active running effects.
        private readonly List<EffectInstance> _activeEffects = new();

        // Events (fired server-side, propagate UI via RPCs in caller).
        public event Action<EffectInstance> OnEffectApplied;
        public event Action<EffectInstance> OnEffectRemoved;
        public event Action<float, EffectContext> OnDamaged;
        public event Action<float> OnHealed;
        public event Action OnDied;

        /// <summary>
        /// Fired when this entity is killed. Passes the killer's NetworkObject and this entity's definition.
        /// External systems (GoldManager, ViewerSystem, SystemAI) subscribe to distribute rewards.
        /// </summary>
        public event Action<NetworkObject, EntityDefinition> OnKilled;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            Tags = GetComponent<TagContainer>();
            Allegiance = GetComponent<AllegianceComponent>();

            if (_definition != null)
                _definition.InitializeAttributes(AttributeSet);
            else
                AttributeSet.Initialize();

            AttributeSet.OnDamageTaken += HandleDamageTaken;
            AttributeSet.OnHealReceived += HandleHealReceived;
            AttributeSet.OnDeath += HandleDeath;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                SyncAllStats();
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            float dt = Time.deltaTime;
            TickActiveEffects(dt);
            TickRegen(dt);
        }

        // ── Regen ──────────────────────────────────────────────────────────

        private void TickRegen(float deltaTime)
        {
            if (!AttributeSet.IsAlive) return;

            // Health regen (Constitution-driven).
            if (AttributeSet.CurrentHealth < AttributeSet.MaxHealth)
            {
                float hpRegen = AttributeSet.HealthRegenPerSecond * deltaTime;
                if (hpRegen > 0f)
                {
                    AttributeSet.ApplyHeal(hpRegen);
                    SyncHealth();
                }
            }

            // Mana regen (Intelligence-driven).
            if (AttributeSet.CurrentMana < AttributeSet.MaxMana)
            {
                float mpRegen = AttributeSet.ManaRegenPerSecond * deltaTime;
                if (mpRegen > 0f)
                {
                    AttributeSet.RestoreMana(mpRegen);
                    SyncMana();
                }
            }
        }

        // ── Stat allocation (only in safe rooms in DCC) ────────────────────

        [SerializeField, Tooltip(
            "Tag required to allocate stat points (e.g., InSafeRoom). " +
            "Leave null to allow stat allocation anywhere (pre-Phase 5).")]
        private Tags.TagDefinition _safeRoomTag;

        [ServerRpc(RequireOwnership = false)]
        public void AllocateStatPointServerRpc(CrawlerStat stat, ServerRpcParams rpcParams = default)
        {
            // In the DCC books, stat points can only be allocated in safe rooms.
            if (_safeRoomTag != null && !Tags.HasTag(_safeRoomTag)) return;
            if (!AttributeSet.AllocateStatPoint(stat)) return;
            SyncAllStats();
        }

        /// <summary>
        /// Grants XP and levels up if threshold is reached.
        /// Each level grants 3 stat points (faithful to books).
        /// </summary>
        public void GrantXP(int amount)
        {
            if (!IsServer) return;
            // Simple leveling: 100 XP per level, scaling by current level.
            // (Tune this formula as needed for game feel.)
            _xpAccumulated += amount;
            int xpRequired = AttributeSet.Level * 100;
            while (_xpAccumulated >= xpRequired)
            {
                _xpAccumulated -= xpRequired;
                AttributeSet.GainLevel();
                xpRequired = AttributeSet.Level * 100;
                NotifyLevelUpClientRpc(AttributeSet.Level);
            }
            SyncAllStats();
        }

        private int _xpAccumulated;

        [ClientRpc]
        private void NotifyLevelUpClientRpc(int newLevel)
        {
            // Clients play level-up VFX, sound, show stat allocation UI.
        }

        // ── Effect application ─────────────────────────────────────────────

        public void ApplyEffect(EffectInstance instance)
        {
            if (!IsServer) return;
            if (instance == null || instance.Definition == null) return;

            // Check if a blocking tag conflict exists.
            if (IsBlockedByConcurrentEffect(instance)) return;

            // Grant tags to this entity for the duration of the effect.
            if (instance.GrantedTags != null)
                Tags.AddTags(instance.GrantedTags);

            _activeEffects.Add(instance);
            OnEffectApplied?.Invoke(instance);

            // Apply resistance profiles: scale magnitude and handle inversion.
            ApplyResistances(instance);

            // Instant effects apply once immediately.
            if (instance.Definition.Duration == 0f)
            {
                instance.Definition.OnApply(instance, this);
                RemoveEffect(instance);
            }
            else
            {
                instance.Applied = true;
                instance.Definition.OnApply(instance, this);
            }

            SyncHealth();
        }

        public void RemoveEffect(EffectInstance instance)
        {
            if (!IsServer || instance == null) return;
            if (!_activeEffects.Remove(instance)) return;

            instance.IsExpired = true;
            instance.Definition?.OnRemove(instance, this);

            if (instance.GrantedTags != null)
                Tags.RemoveTags(instance.GrantedTags);

            OnEffectRemoved?.Invoke(instance);
            instance.Release();
        }

        // ── Ticking ───────────────────────────────────────────────────────

        private void TickActiveEffects(float deltaTime)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var inst = _activeEffects[i];
                if (inst.IsExpired) { _activeEffects.RemoveAt(i); continue; }

                // Tick interval.
                inst.TimeSinceLastTick += deltaTime;
                if (inst.Definition.TickInterval > 0f &&
                    inst.TimeSinceLastTick >= inst.Definition.TickInterval)
                {
                    inst.TimeSinceLastTick -= inst.Definition.TickInterval;
                    inst.Definition.OnTick(inst, this);
                    SyncHealth();
                }

                // Duration countdown (-1 = permanent).
                if (inst.RemainingDuration >= 0f)
                {
                    inst.RemainingDuration -= deltaTime;
                    if (inst.RemainingDuration <= 0f)
                        RemoveEffect(inst);
                }
            }
        }

        // ── Internals ──────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates all ResistanceProfiles on this entity against the effect's tag set.
        /// Scales magnitude and, if InvertsEffect is flagged, swaps the effect definition
        /// for its inverse (Heal→Damage or Damage→Heal) using the InvertedEffect asset ref.
        ///
        /// This is the point where "Healing damages Undead" happens — not in the item,
        /// not in the ability, but here when the effect lands on an entity whose
        /// ResistanceProfile says { Tag: Healing, InvertsEffect: true }.
        /// </summary>
        private void ApplyResistances(EffectInstance instance)
        {
            if (_definition?.ResistanceProfiles == null) return;

            var effectTags = Tags.EffectiveMask;
            // Also include the effect's own granted tags in the tag set to check against.
            if (instance.GrantedTags != null)
                foreach (var t in instance.GrantedTags)
                    if (t != null && t.RuntimeId >= 0) effectTags.Set(t.RuntimeId);

            float totalMultiplier = 1f;
            bool inverted = false;

            foreach (var profile in _definition.ResistanceProfiles)
            {
                if (profile == null) continue;
                profile.Evaluate(effectTags, out float mult, out bool inv);
                totalMultiplier *= mult;
                if (inv) inverted = !inverted;
            }

            instance.ResolvedMagnitude *= totalMultiplier;

            if (inverted)
                instance.Definition.InvertEffect(instance);
        }

        private bool IsBlockedByConcurrentEffect(EffectInstance incoming)
        {
            var blocked = incoming.Definition.BlockedConcurrentTags;
            if (blocked == null || blocked.Length == 0) return false;
            foreach (var inst in _activeEffects)
            {
                if (inst.GrantedTags == null) continue;
                foreach (var grantedTag in inst.GrantedTags)
                    foreach (var blockedTag in blocked)
                        if (grantedTag == blockedTag) return true;
            }
            return false;
        }

        private void SyncHealth()
        {
            if (!IsServer) return;
            _networkHealth.Value = AttributeSet.CurrentHealth;
            _networkMaxHealth.Value = AttributeSet.MaxHealth;
        }

        private void SyncMana()
        {
            if (!IsServer) return;
            _networkMana.Value = AttributeSet.CurrentMana;
            _networkMaxMana.Value = AttributeSet.MaxMana;
        }

        private void SyncAllStats()
        {
            if (!IsServer) return;
            _networkHealth.Value = AttributeSet.CurrentHealth;
            _networkMaxHealth.Value = AttributeSet.MaxHealth;
            _networkMana.Value = AttributeSet.CurrentMana;
            _networkMaxMana.Value = AttributeSet.MaxMana;
            _networkLevel.Value = AttributeSet.Level;
        }

        // Track last damage source for kill attribution (gold, XP, achievements).
        private EffectContext _lastDamageContext;

        private void HandleDamageTaken(float raw, float final, EffectContext ctx)
        {
            _lastDamageContext = ctx;
            SyncHealth();
            OnDamaged?.Invoke(final, ctx);
            NotifyDamageClientRpc(final, ctx.SourceNetworkObjectId);
        }

        private void HandleHealReceived(float amount)
        {
            SyncHealth();
            OnHealed?.Invoke(amount);
        }

        private void HandleDeath()
        {
            AwardKillRewards();
            OnDied?.Invoke();
            NotifyDeathClientRpc();
        }

        /// <summary>
        /// Resolves the killer and fires OnKilled so external systems can award gold, XP, achievements, etc.
        /// Also awards XP directly (XP is part of the core progression loop).
        /// </summary>
        private void AwardKillRewards()
        {
            if (!IsServer) return;
            if (_lastDamageContext.SourceNetworkObjectId == 0) return;

            // Find the killer's NetworkObject.
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    _lastDamageContext.SourceNetworkObjectId, out var killerObj))
                return;

            // Award XP (core progression, stays here).
            int xp = _definition != null ? _definition.BaseXP : 10;
            var killerAttrs = killerObj.GetComponent<EntityAttributes>();
            if (killerAttrs != null)
                killerAttrs.GrantXP(xp);

            // Fire event for external systems (GoldManager, ViewerSystem, SystemAI).
            OnKilled?.Invoke(killerObj, _definition);
        }

        [ClientRpc]
        private void NotifyDamageClientRpc(float amount, ulong sourceId)
        {
            // Clients hook into this for floating damage numbers, hit sounds, etc.
        }

        [ClientRpc]
        private void NotifyDeathClientRpc()
        {
            // Clients play death animation, spawn death VFX, etc.
        }

        // ── IEffectable ────────────────────────────────────────────────────
        TagContainer ITagged.Tags => Tags;
    }

    // ── Interfaces ─────────────────────────────────────────────────────────────

    public interface IEffectable : ITagged
    {
        void ApplyEffect(EffectInstance instance);
        EntityAttributes AttributeSet { get; }
    }

    public interface IInteractable : IEffectable
    {
        AllegianceComponent Allegiance { get; }
        bool IsAlive { get; }
    }
}
