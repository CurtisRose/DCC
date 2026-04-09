using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;
using DCC.Core.Economy;

namespace DCC.Items
{
    /// <summary>
    /// Tracks viewer counts and dramatic moments. Server-authoritative singleton.
    ///
    /// In the DCC books:
    ///   - The dungeon is a reality TV show broadcast to trillions of alien viewers.
    ///   - Each crawler has a viewer count that fluctuates based on exciting events.
    ///   - High viewer counts attract sponsors who send gifts (items, gold, loot boxes).
    ///   - Dramatic moments (boss kills, PVP, clever combos, near-death escapes) spike viewers.
    ///   - Boring crawlers lose viewers over time.
    ///   - Carl's Charisma drives his massive viewer count ("The Butcher's Pets" show).
    ///   - Viewer milestones unlock sponsor tiers.
    ///
    /// ViewerSystem runs on the server and periodically syncs per-player viewer counts.
    /// Sponsors are evaluated when viewer thresholds are crossed.
    /// </summary>
    public class ViewerSystem : NetworkBehaviour
    {
        public static ViewerSystem Instance { get; private set; }

        [Header("Viewer Config")]
        [SerializeField, Tooltip("Base viewer gain per second (Charisma-scaled).")]
        private float _baseViewerGainPerSecond = 10f;

        [SerializeField, Tooltip("Viewer decay per second when nothing exciting happens.")]
        private float _boredDecayPerSecond = 5f;

        [SerializeField, Tooltip("Seconds after an event before decay resumes.")]
        private float _excitementCooldown = 30f;

        [Header("Sponsors")]
        [SerializeField, Tooltip("All available sponsors.")]
        private SponsorDefinition[] _sponsors;

        // Per-player viewer state (server only).
        private readonly Dictionary<ulong, ViewerState> _playerViewers = new();

        // Networked viewer counts for UI (synced periodically).
        private float _syncTimer;
        private const float SyncInterval = 2f;

        public event Action<ulong, long> OnViewerCountChanged;    // (clientId, newCount)
        public event Action<ulong, SponsorDefinition> OnSponsorGift;  // (clientId, sponsor)

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        // ── Player registration ────────────────────────────────────────────

        public void RegisterPlayer(ulong clientId, EntityAttributes attrs)
        {
            if (!IsServer) return;
            int charisma = attrs != null ? attrs.AttributeSet.Charisma : 4;

            _playerViewers[clientId] = new ViewerState
            {
                ViewerCount = charisma * 1000L, // Starting viewers based on Charisma.
                Charisma = charisma,
                ExcitementTimer = 0f,
                HighestViewerCount = charisma * 1000L,
                SponsorTierReached = 0
            };
        }

        public void UnregisterPlayer(ulong clientId)
        {
            _playerViewers.Remove(clientId);
        }

        // ── Queries ────────────────────────────────────────────────────────

        public long GetViewerCount(ulong clientId)
            => _playerViewers.TryGetValue(clientId, out var state) ? state.ViewerCount : 0;

        public long GetHighestViewerCount(ulong clientId)
            => _playerViewers.TryGetValue(clientId, out var state) ? state.HighestViewerCount : 0;

        // ── Dramatic events (called by other systems) ──────────────────────

        /// <summary>
        /// Report a dramatic event that spikes viewer count.
        /// Magnitude scales the boost (1.0 = normal, 2.0 = very exciting, etc.)
        /// </summary>
        public void ReportDramaticEvent(ulong clientId, DramaticEventType eventType, float magnitude = 1f)
        {
            if (!IsServer) return;
            if (!_playerViewers.TryGetValue(clientId, out var state)) return;

            long boost = CalculateViewerBoost(eventType, magnitude, state.Charisma);
            state.ViewerCount += boost;
            state.ExcitementTimer = _excitementCooldown;

            if (state.ViewerCount > state.HighestViewerCount)
                state.HighestViewerCount = state.ViewerCount;

            _playerViewers[clientId] = state;

            // Check sponsor thresholds.
            CheckSponsorThresholds(clientId, state);
        }

        private long CalculateViewerBoost(DramaticEventType eventType, float magnitude, int charisma)
        {
            float chaMultiplier = 1f + charisma * 0.1f;

            long baseBoost = eventType switch
            {
                DramaticEventType.MobKill => 100,
                DramaticEventType.BossKill => 5000,
                DramaticEventType.PvpKill => 10000,
                DramaticEventType.NearDeathEscape => 3000,
                DramaticEventType.CleverCombo => 2000,
                DramaticEventType.FirstDiscovery => 4000,
                DramaticEventType.AchievementEarned => 1500,
                DramaticEventType.FloorCleared => 8000,
                DramaticEventType.LegendaryLoot => 6000,
                DramaticEventType.PetDeath => 7000,       // Audience loves drama.
                DramaticEventType.SpeechOrTaunt => 1000,  // Cha-dependent showmanship.
                _ => 500
            };

            return (long)(baseBoost * magnitude * chaMultiplier);
        }

        // ── Tick ───────────────────────────────────────────────────────────

        private void Update()
        {
            if (!IsServer) return;

            float dt = Time.deltaTime;

            // Tick each player's viewers.
            var keys = new List<ulong>(_playerViewers.Keys);
            foreach (ulong clientId in keys)
            {
                var state = _playerViewers[clientId];

                // Passive gain from Charisma (being entertaining just by existing).
                float chaGain = _baseViewerGainPerSecond * (1f + state.Charisma * 0.05f);
                state.ViewerCount += (long)(chaGain * dt);

                // Decay when not exciting.
                state.ExcitementTimer -= dt;
                if (state.ExcitementTimer <= 0f)
                {
                    long decay = (long)(_boredDecayPerSecond * dt);
                    state.ViewerCount = Mathf.Max(0, (int)(state.ViewerCount - decay));
                }

                _playerViewers[clientId] = state;
            }

            // Periodic sync to clients.
            _syncTimer -= dt;
            if (_syncTimer <= 0f)
            {
                _syncTimer = SyncInterval;
                SyncViewerCounts();
            }
        }

        private void SyncViewerCounts()
        {
            foreach (var kvp in _playerViewers)
            {
                NotifyViewerCountClientRpc(kvp.Key, kvp.Value.ViewerCount);
                OnViewerCountChanged?.Invoke(kvp.Key, kvp.Value.ViewerCount);
            }
        }

        // ── Sponsors ───────────────────────────────────────────────────────

        private void CheckSponsorThresholds(ulong clientId, ViewerState state)
        {
            if (_sponsors == null) return;

            foreach (var sponsor in _sponsors)
            {
                if (sponsor == null) continue;
                if (state.ViewerCount < sponsor.ViewerThreshold) continue;
                if (state.SponsorTierReached >= sponsor.Tier) continue;

                // New sponsor tier reached!
                state.SponsorTierReached = sponsor.Tier;
                _playerViewers[clientId] = state;

                OnSponsorGift?.Invoke(clientId, sponsor);
                AwardSponsorGift(clientId, sponsor);
            }
        }

        private void AwardSponsorGift(ulong clientId, SponsorDefinition sponsor)
        {
            // Find the player's NetworkObject to deliver rewards.
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var playerObj = client.PlayerObject;
            if (playerObj == null) return;

            // Gold gift.
            if (sponsor.GoldGift > 0)
            {
                var gold = playerObj.GetComponent<GoldManager>();
                if (gold != null) gold.AddGold(sponsor.GoldGift);
            }

            // Item gifts.
            if (sponsor.GiftItems != null)
            {
                var inventory = playerObj.GetComponent<Inventory>();
                if (inventory != null)
                {
                    foreach (var item in sponsor.GiftItems)
                        if (item != null) inventory.AddItem(item);
                }
            }

            // Loot box gift.
            if (sponsor.GiftLootBox != null)
            {
                var lootBoxes = playerObj.GetComponent<LootBoxInventory>();
                if (lootBoxes != null) lootBoxes.AddBox(sponsor.GiftLootBox);
            }

            NotifySponsorGiftClientRpc(clientId, sponsor.SponsorName);
        }

        // ── Network ────────────────────────────────────────────────────────

        [ClientRpc]
        private void NotifyViewerCountClientRpc(ulong clientId, long viewerCount)
        {
            // Client updates viewer count UI.
            OnViewerCountChanged?.Invoke(clientId, viewerCount);
        }

        [ClientRpc]
        private void NotifySponsorGiftClientRpc(ulong clientId, string sponsorName)
        {
            // Client shows "Sponsor Gift from {sponsorName}!" notification.
        }

        // ── Internal state ─────────────────────────────────────────────────

        private struct ViewerState
        {
            public long ViewerCount;
            public int Charisma;
            public float ExcitementTimer;
            public long HighestViewerCount;
            public int SponsorTierReached;
        }
    }

    public enum DramaticEventType
    {
        MobKill,
        BossKill,
        PvpKill,
        NearDeathEscape,
        CleverCombo,
        FirstDiscovery,
        AchievementEarned,
        FloorCleared,
        LegendaryLoot,
        PetDeath,
        SpeechOrTaunt,
        Custom
    }
}
