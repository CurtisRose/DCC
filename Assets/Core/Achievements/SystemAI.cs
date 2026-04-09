using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects;
using DCC.Core.Entities;
using DCC.Core.Zones;
using DCC.UI;
using DCC.Abilities;

namespace DCC.Core.Achievements
{
    /// <summary>
    /// The System AI — server-authoritative singleton that monitors game events,
    /// evaluates achievements, and broadcasts snarky narrator commentary.
    ///
    /// In the DCC books, the System AI is:
    ///   - The narrator of the dungeon. It controls everything.
    ///   - Petulant, snarky, passive-aggressive, and occasionally impressed.
    ///   - Responsible for all achievement notifications.
    ///   - Broadcasts countdown warnings, floor collapses, and crawler deaths.
    ///   - Its commentary ranges from backhanded compliments to genuine warnings.
    ///
    /// SystemAI doesn't own game logic — it observes events from other systems
    /// (EntityAttributes, SkillTracker, FloorManager, DiscoverySystem) and reacts
    /// by awarding achievements and broadcasting commentary.
    /// </summary>
    public class SystemAI : NetworkBehaviour
    {
        public static SystemAI Instance { get; private set; }

        [Header("Achievements")]
        [SerializeField, Tooltip("All achievements the System AI can award.")]
        private AchievementDefinition[] _achievements;

        [Header("Commentary")]
        [SerializeField, Tooltip("Commentary templates grouped by trigger type.")]
        private SystemAICommentary[] _commentary;

        [Header("Event Hooks")]
        [SerializeField, Tooltip("Tag required for first-kill achievements (optional).")]
        private TagDefinition _playerTag;

        // Lookup: trigger → commentary pool (built on Awake).
        private readonly Dictionary<CommentaryTrigger, List<SystemAICommentary>> _commentaryLookup = new();

        // Lookup: trigger → achievements (built on Awake).
        private readonly Dictionary<AchievementTrigger, List<AchievementDefinition>> _achievementLookup = new();

        // Tracks global-first achievements already awarded to any crawler.
        private readonly HashSet<AchievementDefinition> _globalFirstsAwarded = new();

        // All registered player trackers (added on spawn).
        private readonly Dictionary<ulong, AchievementTracker> _trackers = new();

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            BuildCommentaryLookup();
            BuildAchievementLookup();
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeAll();
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            SubscribeToGlobalEvents();
        }

        // ── Player registration ────────────────────────────────────────────

        /// <summary>
        /// Called by NetworkGameManager (or spawn logic) when a player entity spawns.
        /// Subscribes to that player's events for achievement evaluation.
        /// </summary>
        public void RegisterPlayer(NetworkObject playerObj)
        {
            if (!IsServer || playerObj == null) return;

            ulong clientId = playerObj.OwnerClientId;
            var tracker = playerObj.GetComponent<AchievementTracker>();
            if (tracker == null) return;

            _trackers[clientId] = tracker;

            // Subscribe to player-specific events.
            var attrs = playerObj.GetComponent<EntityAttributes>();
            var skills = playerObj.GetComponent<SkillTracker>();

            if (attrs != null)
            {
                attrs.OnDied += () => HandlePlayerDeath(clientId, playerObj);
                attrs.OnDamaged += (dmg, ctx) => HandlePlayerDamaged(clientId, playerObj, dmg, ctx);
            }

            if (skills != null)
            {
                skills.OnSkillLevelUp += (skill, level) => HandleSkillLevelUp(clientId, playerObj, skill, level);
            }

            // Evaluate any achievements that might already be satisfied (e.g., race-granted tags).
            EvaluateAllForPlayer(tracker);
        }

        /// <summary>
        /// Called when a player entity despawns (disconnect, death with removal, etc.).
        /// </summary>
        public void UnregisterPlayer(ulong clientId)
        {
            _trackers.Remove(clientId);
            // Event unsubscription happens naturally when the entity is destroyed.
        }

        // ── Global event subscriptions ─────────────────────────────────────

        private void SubscribeToGlobalEvents()
        {
            var discovery = DiscoverySystem.Instance;
            if (discovery != null)
                discovery.OnNewDiscovery += HandleNewDiscovery;

            var floor = FloorManager.Instance;
            if (floor != null)
            {
                floor.OnFloorStarted += HandleFloorStarted;
                floor.OnFloorCollapsed += HandleFloorCollapsed;
                floor.OnFloorTransition += HandleFloorTransition;
            }
        }

        private void UnsubscribeAll()
        {
            var discovery = DiscoverySystem.Instance;
            if (discovery != null)
                discovery.OnNewDiscovery -= HandleNewDiscovery;

            var floor = FloorManager.Instance;
            if (floor != null)
            {
                floor.OnFloorStarted -= HandleFloorStarted;
                floor.OnFloorCollapsed -= HandleFloorCollapsed;
                floor.OnFloorTransition -= HandleFloorTransition;
            }
        }

        // ── Event handlers ─────────────────────────────────────────────────

        private void HandlePlayerDeath(ulong clientId, NetworkObject playerObj)
        {
            if (!IsServer) return;

            string playerName = GetPlayerName(playerObj);
            int floor = GetCurrentFloor();

            Broadcast(CommentaryTrigger.CrawlerDeath, new CommentaryContext
            {
                PlayerName = playerName,
                FloorNumber = floor
            });
        }

        private void HandlePlayerDamaged(ulong clientId, NetworkObject playerObj, float damage, EffectContext ctx)
        {
            // Could trigger "first blood" or damage-milestone achievements here.
            // For now, check kill-based achievements when a mob dies from this player's damage.
        }

        private void HandleSkillLevelUp(ulong clientId, NetworkObject playerObj, SkillDefinition skill, int newLevel)
        {
            if (!IsServer) return;

            string playerName = GetPlayerName(playerObj);

            // Check skill-maxed achievements.
            if (_trackers.TryGetValue(clientId, out var tracker))
            {
                EvaluateByTrigger(tracker, AchievementTrigger.SkillMaxed);
            }

            // Commentary for skill max.
            var skillTracker = playerObj.GetComponent<SkillTracker>();
            if (skillTracker != null)
            {
                int max = skillTracker.IsPrimal ? 20 : skill.MaxLevel;
                if (newLevel >= max)
                {
                    Broadcast(CommentaryTrigger.SkillMaxed, new CommentaryContext
                    {
                        PlayerName = playerName,
                        SkillName = skill.DisplayName,
                        Value = newLevel,
                        FloorNumber = GetCurrentFloor()
                    });
                }
            }
        }

        private void HandleNewDiscovery(TagMask mask, string discoveryName, ulong clientId)
        {
            if (!IsServer) return;

            // Check tag-combination achievements for this player.
            if (_trackers.TryGetValue(clientId, out var tracker))
            {
                EvaluateByTrigger(tracker, AchievementTrigger.TagCombination);
                EvaluateByTrigger(tracker, AchievementTrigger.FirstTagCombination);
            }

            // Global first discovery commentary.
            NetworkObject playerObj = GetPlayerObject(clientId);
            string playerName = playerObj != null ? GetPlayerName(playerObj) : $"Crawler {clientId}";

            Broadcast(CommentaryTrigger.GlobalFirstDiscovery, new CommentaryContext
            {
                PlayerName = playerName,
                ItemName = discoveryName,
                FloorNumber = GetCurrentFloor()
            });
        }

        private void HandleFloorStarted(FloorDefinition floor)
        {
            if (!IsServer) return;

            // Evaluate floor-reached achievements for all players.
            foreach (var kvp in _trackers)
                EvaluateByTrigger(kvp.Value, AchievementTrigger.FloorReached);
        }

        private void HandleFloorCollapsed(int floorNumber)
        {
            if (!IsServer) return;

            Broadcast(CommentaryTrigger.FloorCollapse, new CommentaryContext
            {
                FloorNumber = floorNumber
            });
        }

        private void HandleFloorTransition(FloorDefinition nextFloor)
        {
            // Floor transition commentary could go here (e.g., "Welcome to Floor X").
        }

        // ── Kill tracking (called externally) ──────────────────────────────

        /// <summary>
        /// Called when an entity kills a mob. Evaluates first-kill achievements
        /// and broadcasts commentary.
        /// </summary>
        public void NotifyKill(ulong killerClientId, NetworkObject victim)
        {
            if (!IsServer) return;

            if (_trackers.TryGetValue(killerClientId, out var tracker))
                EvaluateByTrigger(tracker, AchievementTrigger.FirstKill);

            // Check if victim was a player (PVP kill).
            if (victim != null && victim.GetComponent<AchievementTracker>() != null)
            {
                if (_trackers.TryGetValue(killerClientId, out var pvpTracker))
                    EvaluateByTrigger(pvpTracker, AchievementTrigger.FirstPvpKill);

                NetworkObject killerObj = GetPlayerObject(killerClientId);
                string killerName = killerObj != null ? GetPlayerName(killerObj) : $"Crawler {killerClientId}";
                string victimName = GetPlayerName(victim);

                Broadcast(CommentaryTrigger.PvpKill, new CommentaryContext
                {
                    PlayerName = killerName,
                    MobName = victimName,
                    FloorNumber = GetCurrentFloor()
                });
            }
        }

        /// <summary>
        /// Trigger a custom achievement by name. Used for one-off scripted events.
        /// </summary>
        public void TriggerCustom(ulong clientId, string achievementName)
        {
            if (!IsServer) return;
            if (!_trackers.TryGetValue(clientId, out var tracker)) return;

            if (_achievementLookup.TryGetValue(AchievementTrigger.Custom, out var customs))
            {
                foreach (var achievement in customs)
                {
                    if (achievement.DisplayName == achievementName)
                        tracker.TryAward(achievement);
                }
            }
        }

        /// <summary>
        /// Evaluate all stat-milestone achievements for a specific player.
        /// Called after stat allocation or level-up.
        /// </summary>
        public void CheckStatMilestones(ulong clientId)
        {
            if (!IsServer) return;
            if (_trackers.TryGetValue(clientId, out var tracker))
                EvaluateByTrigger(tracker, AchievementTrigger.StatMilestone);
        }

        // ── Achievement evaluation ─────────────────────────────────────────

        private void EvaluateAllForPlayer(AchievementTracker tracker)
        {
            if (_achievements == null) return;

            foreach (var achievement in _achievements)
            {
                if (achievement == null) continue;
                if (achievement.IsGlobalFirst && _globalFirstsAwarded.Contains(achievement)) continue;

                if (tracker.TryAward(achievement) && achievement.IsGlobalFirst)
                    _globalFirstsAwarded.Add(achievement);
            }
        }

        private void EvaluateByTrigger(AchievementTracker tracker, AchievementTrigger trigger)
        {
            if (!_achievementLookup.TryGetValue(trigger, out var achievements)) return;

            foreach (var achievement in achievements)
            {
                if (achievement == null) continue;
                if (achievement.IsGlobalFirst && _globalFirstsAwarded.Contains(achievement)) continue;

                if (tracker.TryAward(achievement) && achievement.IsGlobalFirst)
                    _globalFirstsAwarded.Add(achievement);
            }
        }

        // ── Commentary broadcast ───────────────────────────────────────────

        private void Broadcast(CommentaryTrigger trigger, CommentaryContext ctx)
        {
            if (!_commentaryLookup.TryGetValue(trigger, out var pool)) return;
            if (pool.Count == 0) return;

            // Pick highest priority, then random among ties.
            int bestPriority = int.MinValue;
            List<SystemAICommentary> candidates = null;

            foreach (var entry in pool)
            {
                if (entry.Priority > bestPriority)
                {
                    bestPriority = entry.Priority;
                    candidates = new List<SystemAICommentary> { entry };
                }
                else if (entry.Priority == bestPriority)
                {
                    candidates?.Add(entry);
                }
            }

            if (candidates == null || candidates.Count == 0) return;

            var chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            string message = chosen.Format(ctx);

            if (string.IsNullOrEmpty(message)) return;

            if (chosen.IsBroadcast)
                BroadcastCommentaryClientRpc(message);
            else if (!string.IsNullOrEmpty(ctx.PlayerName))
                // Targeted message — send to all and let client filter by name.
                BroadcastCommentaryClientRpc(message);
        }

        [ClientRpc]
        private void BroadcastCommentaryClientRpc(string message)
        {
            // Clients display this in the System AI chat/notification area.
            // UI hooks into this for floating text, chat log, toast popups, etc.
            OnCommentaryReceived?.Invoke(message);
        }

        /// <summary>
        /// Client-side event for UI to subscribe to.
        /// </summary>
        public event Action<string> OnCommentaryReceived;

        // ── Helpers ────────────────────────────────────────────────────────

        private void BuildCommentaryLookup()
        {
            _commentaryLookup.Clear();
            if (_commentary == null) return;

            foreach (var entry in _commentary)
            {
                if (entry == null) continue;
                if (!_commentaryLookup.TryGetValue(entry.Trigger, out var list))
                {
                    list = new List<SystemAICommentary>();
                    _commentaryLookup[entry.Trigger] = list;
                }
                list.Add(entry);
            }
        }

        private void BuildAchievementLookup()
        {
            _achievementLookup.Clear();
            if (_achievements == null) return;

            foreach (var achievement in _achievements)
            {
                if (achievement == null) continue;
                if (!_achievementLookup.TryGetValue(achievement.Trigger, out var list))
                {
                    list = new List<AchievementDefinition>();
                    _achievementLookup[achievement.Trigger] = list;
                }
                list.Add(achievement);
            }
        }

        private string GetPlayerName(NetworkObject playerObj)
        {
            if (playerObj == null) return "Crawler";

            var identity = playerObj.GetComponent<CrawlerIdentity>();
            if (identity != null)
            {
                string cls = identity.ClassName;
                if (!string.IsNullOrEmpty(cls)) return cls;
            }

            return $"Crawler {playerObj.OwnerClientId}";
        }

        private NetworkObject GetPlayerObject(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return null;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                return null;
            return client.PlayerObject;
        }

        private int GetCurrentFloor()
        {
            var fm = FloorManager.Instance;
            return fm != null ? fm.CurrentFloorNumber : 0;
        }
    }
}
