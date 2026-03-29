using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DCC.Core.Tags
{
    /// <summary>
    /// Attached to every entity, zone, projectile, or item that participates in the
    /// emergent interaction system. Owns the authoritative tag set for its GameObject.
    ///
    /// Server-authoritative: mutations only on the server. All clients receive the
    /// resolved effective mask via NetworkVariable.
    ///
    /// The raw mask stores explicitly-set tags. EffectiveMask is the resolved mask
    /// (with implications expanded and suppressions applied). The interaction engine
    /// always queries EffectiveMask.
    /// </summary>
    [DisallowMultipleComponent]
    public class TagContainer : NetworkBehaviour, ITagged
    {
        [SerializeField, Tooltip("Base tags assigned in the editor (static, never removed).")]
        private TagDefinition[] _baseTags = Array.Empty<TagDefinition>();

        // Authoritative raw mask — synced to all clients.
        private NetworkVariable<TagMask> _networkMask = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        // Runtime tags added by effects, zones, etc. Not synced individually —
        // they are folded into _networkMask on the server.
        private readonly List<TagDefinition> _runtimeTags = new();

        /// <summary>The fully resolved effective tag set (implications + suppressions applied).</summary>
        public TagMask EffectiveMask { get; private set; }

        /// <summary>Fired when the effective mask changes. Args: (previousMask, newMask).</summary>
        public event Action<TagMask, TagMask> OnTagsChanged;

        TagContainer ITagged.Tags => this;

        // ── Lifecycle ──────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer) RebuildNetworkMask();

            _networkMask.OnValueChanged += OnNetworkMaskChanged;
            EffectiveMask = _networkMask.Value.Resolve();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _networkMask.OnValueChanged -= OnNetworkMaskChanged;
        }

        private void OnNetworkMaskChanged(TagMask previous, TagMask current)
        {
            var prev = previous.Resolve();
            EffectiveMask = current.Resolve();
            OnTagsChanged?.Invoke(prev, EffectiveMask);
        }

        // ── Mutation (Server only) ─────────────────────────────────────────

        public void AddTag(TagDefinition tag)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[TagContainer] AddTag called on client — ignored.");
                return;
            }
            if (tag == null || _runtimeTags.Contains(tag)) return;
            _runtimeTags.Add(tag);
            RebuildNetworkMask();
        }

        public void RemoveTag(TagDefinition tag)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[TagContainer] RemoveTag called on client — ignored.");
                return;
            }
            if (tag == null || !_runtimeTags.Remove(tag)) return;
            RebuildNetworkMask();
        }

        public void AddTags(IEnumerable<TagDefinition> tags)
        {
            if (!IsServer) return;
            bool changed = false;
            foreach (var t in tags)
            {
                if (t != null && !_runtimeTags.Contains(t))
                {
                    _runtimeTags.Add(t);
                    changed = true;
                }
            }
            if (changed) RebuildNetworkMask();
        }

        public void RemoveTags(IEnumerable<TagDefinition> tags)
        {
            if (!IsServer) return;
            bool changed = false;
            foreach (var t in tags)
                if (_runtimeTags.Remove(t)) changed = true;
            if (changed) RebuildNetworkMask();
        }

        // ── Query (safe from any context) ─────────────────────────────────

        public bool HasTag(TagDefinition tag) => EffectiveMask.HasTag(tag);

        public bool HasAllTags(TagMask required) => EffectiveMask.HasAll(required);

        public bool HasAnyTag(TagMask any) => EffectiveMask.HasAny(any);

        // ── Internal ──────────────────────────────────────────────────────

        private void RebuildNetworkMask()
        {
            var raw = new TagMask();
            foreach (var t in _baseTags)
                if (t != null && t.RuntimeId >= 0) raw.Set(t.RuntimeId);
            foreach (var t in _runtimeTags)
                if (t != null && t.RuntimeId >= 0) raw.Set(t.RuntimeId);
            _networkMask.Value = raw;
        }
    }

    // ── Interface ──────────────────────────────────────────────────────────────

    public interface ITagged
    {
        TagContainer Tags { get; }
    }
}
