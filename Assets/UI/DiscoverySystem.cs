using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;

namespace DCC.UI
{
    /// <summary>
    /// Tracks which tag combinations each player has discovered and broadcasts
    /// "aha moment" notifications.
    ///
    /// A combination is "discovered" when a player causes an interaction whose
    /// composite tag mask has never been seen by that player before.
    ///
    /// Global vs personal:
    ///   - Personal discovery: "Healing Cloud" pops up for the player who first made it.
    ///   - Global first: optional broadcast "Carl discovered Healing Cloud!" for everyone.
    ///
    /// The discovery name is derived from the tag names present in the interaction.
    /// Designers can override names via the DiscoveryNameOverride table.
    ///
    /// This system is purely informational — it does NOT gate anything.
    /// Players can use combinations they haven't "discovered" yet.
    /// </summary>
    public class DiscoverySystem : NetworkBehaviour
    {
        public static DiscoverySystem Instance { get; private set; }

        [SerializeField, Tooltip("Optional name overrides for known combinations.")]
        private DiscoveryNameEntry[] _nameOverrides = Array.Empty<DiscoveryNameEntry>();

        [SerializeField, Tooltip("Broadcast to all players when a combination is discovered for the first time globally.")]
        private bool _announceGlobalFirstDiscoveries = true;

        // Globally seen interaction hashes this session.
        private readonly HashSet<int> _globalDiscoveries = new();

        // Per-player seen interaction hashes.
        private readonly Dictionary<ulong, HashSet<int>> _playerDiscoveries = new();

        // Name override lookup (built at startup).
        private readonly Dictionary<int, string> _nameOverrideLookup = new();

        // Event fired on the server when a new interaction is logged.
        public event Action<TagMask, string, ulong> OnNewDiscovery;

        private void Awake()
        {
            Instance = this;
            foreach (var entry in _nameOverrides)
                _nameOverrideLookup[entry.SignatureHash] = entry.DisplayName;
        }

        // ── Recording (Server only) ────────────────────────────────────────

        /// <summary>
        /// Called by InteractionEngine and EffectComposer whenever effects fire.
        /// compositeTagMask: the resolved union of all active effect tags.
        /// ownerClientId: the player whose action caused this interaction.
        /// </summary>
        public void RecordInteraction(TagMask compositeTagMask, ulong ownerClientId)
        {
            if (!IsServer) return;
            if (compositeTagMask.IsEmpty()) return;

            int sig = compositeTagMask.GetHashCode();

            bool globalFirst = _globalDiscoveries.Add(sig);

            if (!_playerDiscoveries.TryGetValue(ownerClientId, out var playerSet))
            {
                playerSet = new HashSet<int>();
                _playerDiscoveries[ownerClientId] = playerSet;
            }
            bool playerFirst = playerSet.Add(sig);

            if (!playerFirst) return; // Player has seen this before.

            string discoveryName = BuildDiscoveryName(compositeTagMask, sig);
            OnNewDiscovery?.Invoke(compositeTagMask, discoveryName, ownerClientId);

            // Notify the discovering player.
            SendDiscoveryToPlayerClientRpc(sig, discoveryName, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { ownerClientId } }
            });

            // Announce globally if first in session.
            if (globalFirst && _announceGlobalFirstDiscoveries)
                AnnounceGlobalDiscoveryClientRpc(ownerClientId, discoveryName);
        }

        public bool HasDiscovered(ulong clientId, int signatureHash)
        {
            return _playerDiscoveries.TryGetValue(clientId, out var set) && set.Contains(signatureHash);
        }

        // ── Client RPCs ────────────────────────────────────────────────────

        [ClientRpc]
        private void SendDiscoveryToPlayerClientRpc(
            int signatureHash,
            FixedString128Bytes name,
            ClientRpcParams rpc = default)
        {
            OnLocalDiscovery?.Invoke(signatureHash, name.ToString());
            Debug.Log($"[Discovery] You discovered: {name}");
        }

        [ClientRpc]
        private void AnnounceGlobalDiscoveryClientRpc(ulong discovererClientId, FixedString128Bytes name)
        {
            OnGlobalDiscovery?.Invoke(discovererClientId, name.ToString());
            Debug.Log($"[Discovery] Player {discovererClientId} discovered: {name} (world first!)");
        }

        // Client-side events for UI to hook into.
        public static event Action<int, string> OnLocalDiscovery;
        public static event Action<ulong, string> OnGlobalDiscovery;

        // ── Name building ──────────────────────────────────────────────────

        private string BuildDiscoveryName(TagMask mask, int sig)
        {
            if (_nameOverrideLookup.TryGetValue(sig, out var overrideName))
                return overrideName;

            // Auto-generate from tag names.
            var names = new List<string>();
            foreach (var tag in mask.GetSetTags())
                names.Add(tag.DisplayName);
            names.Sort();
            return string.Join(" + ", names);
        }
    }

    // ── Supporting types ───────────────────────────────────────────────────────

    [Serializable]
    public struct DiscoveryNameEntry
    {
        [Tooltip("Hash of the TagMask signature. Compute via TagMask.GetHashCode() in editor tooling.")]
        public int SignatureHash;
        public string DisplayName;
    }
}
