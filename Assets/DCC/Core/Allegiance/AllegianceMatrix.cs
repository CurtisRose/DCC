using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DCC.Core.Allegiance
{
    /// <summary>
    /// Server-authoritative relationship table between all players/entities.
    /// Governs XP attribution, AI targeting priority, and "team heal" ability targeting.
    ///
    /// Friendly fire is ALWAYS mechanically active — this class does NOT block damage.
    /// It only influences:
    ///   - XP/loot attribution ("who gets credit for this kill")
    ///   - AI aggro priority (enemies prefer to attack non-allied players)
    ///   - Discovery log labels ("you damaged an ally")
    ///   - Ability visuals (allied health bars shown in green)
    ///
    /// Team modes:
    ///   FreeForAll    — every player is their own team. No alliances.
    ///   Fixed         — teams assigned at lobby, cannot change.
    ///   Dynamic       — players can propose mid-game alliances (both must accept).
    ///
    /// Neutral relation means "not allied". Neutral entities CAN and DO harm each other.
    /// </summary>
    public class AllegianceMatrix : NetworkBehaviour
    {
        public static AllegianceMatrix Instance { get; private set; }

        public enum TeamMode { FreeForAll, Fixed, Dynamic }
        public enum Relation { Neutral, Allied }

        [SerializeField] private TeamMode _mode = TeamMode.FreeForAll;
        [SerializeField] private int _maxTeamSize = 2;
        [SerializeField] private bool _allowMidGameTeaming = true;

        // team ID per client. In FFA mode each client has a unique team ID = clientId.
        private readonly Dictionary<ulong, int> _teamMap = new();

        // Override table for Dynamic mode (mid-game alliances).
        // Key: sorted (low, high) client ID pair.
        private readonly Dictionary<(ulong, ulong), Relation> _overrides = new();

        // Pending alliance requests: requesterId → targetId.
        private readonly Dictionary<ulong, ulong> _pendingRequests = new();

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake() => Instance = this;

        public void RegisterPlayer(ulong clientId)
        {
            if (!IsServer) return;
            int teamId = _mode == TeamMode.FreeForAll ? (int)clientId : GetOrCreateTeam(clientId);
            _teamMap[clientId] = teamId;
        }

        public void UnregisterPlayer(ulong clientId)
        {
            if (!IsServer) return;
            _teamMap.Remove(clientId);
            _pendingRequests.Remove(clientId);
        }

        // ── Relation queries ───────────────────────────────────────────────

        public Relation GetRelation(ulong fromClientId, ulong toClientId)
        {
            if (fromClientId == toClientId) return Relation.Allied;

            // Check override table first (Dynamic mode alliances).
            var key = MakeKey(fromClientId, toClientId);
            if (_overrides.TryGetValue(key, out var overrideRel)) return overrideRel;

            // Fall back to team comparison.
            if (_teamMap.TryGetValue(fromClientId, out int teamA) &&
                _teamMap.TryGetValue(toClientId, out int teamB) &&
                teamA == teamB)
                return Relation.Allied;

            return Relation.Neutral;
        }

        public bool AreAllied(ulong a, ulong b) => GetRelation(a, b) == Relation.Allied;

        // ── Alliance requests (Dynamic mode) ──────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void RequestAllianceServerRpc(ulong targetClientId, ServerRpcParams rpc = default)
        {
            if (!_allowMidGameTeaming || _mode != TeamMode.Dynamic) return;
            ulong requesterId = rpc.Receive.SenderClientId;

            if (AreAllied(requesterId, targetClientId)) return; // already allied

            _pendingRequests[requesterId] = targetClientId;

            // Notify target (they'll see a UI prompt).
            NotifyAllianceRequestClientRpc(requesterId, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { targetClientId } }
            });
        }

        [ServerRpc(RequireOwnership = false)]
        public void AcceptAllianceServerRpc(ulong requesterId, ServerRpcParams rpc = default)
        {
            if (_mode != TeamMode.Dynamic) return;
            ulong acceptorId = rpc.Receive.SenderClientId;

            if (!_pendingRequests.TryGetValue(requesterId, out var intended) ||
                intended != acceptorId) return;

            _pendingRequests.Remove(requesterId);

            // Check team size limit.
            int currentTeamSize = CountAllies(requesterId);
            if (_maxTeamSize > 0 && currentTeamSize >= _maxTeamSize)
            {
                RejectAllianceClientRpc("Team is full.", new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { requesterId } }
                });
                return;
            }

            // Form the alliance.
            var key = MakeKey(requesterId, acceptorId);
            _overrides[key] = Relation.Allied;

            AnnounceAllianceFormedClientRpc(requesterId, acceptorId);
        }

        [ServerRpc(RequireOwnership = false)]
        public void DissolveAllianceServerRpc(ulong partnerId, ServerRpcParams rpc = default)
        {
            if (_mode != TeamMode.Dynamic) return;
            ulong requesterId = rpc.Receive.SenderClientId;

            var key = MakeKey(requesterId, partnerId);
            _overrides.Remove(key);
            AnnounceAllianceDissolvedClientRpc(requesterId, partnerId);
        }

        // ── Client RPCs ────────────────────────────────────────────────────

        [ClientRpc]
        private void NotifyAllianceRequestClientRpc(ulong requesterId, ClientRpcParams rpc = default) { }

        [ClientRpc]
        private void RejectAllianceClientRpc(string reason, ClientRpcParams rpc = default) { }

        [ClientRpc]
        private void AnnounceAllianceFormedClientRpc(ulong a, ulong b) { }

        [ClientRpc]
        private void AnnounceAllianceDissolvedClientRpc(ulong a, ulong b) { }

        // ── Helpers ────────────────────────────────────────────────────────

        private static (ulong, ulong) MakeKey(ulong a, ulong b)
            => a < b ? (a, b) : (b, a);

        private int CountAllies(ulong clientId)
        {
            int count = 0;
            foreach (var kvp in _overrides)
                if ((kvp.Key.Item1 == clientId || kvp.Key.Item2 == clientId) &&
                    kvp.Value == Relation.Allied)
                    count++;
            return count;
        }

        private int GetOrCreateTeam(ulong clientId)
        {
            // Fixed-team logic: find a non-full team, or create a new one.
            foreach (var kvp in _teamMap)
                if (CountTeamMembers(kvp.Value) < _maxTeamSize)
                    return kvp.Value;
            return (int)clientId; // new solo team
        }

        private int CountTeamMembers(int teamId)
        {
            int n = 0;
            foreach (var v in _teamMap.Values) if (v == teamId) n++;
            return n;
        }
    }
}
