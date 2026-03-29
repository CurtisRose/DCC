using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Effects;
using DCC.Core.Entities;
using DCC.Core.Zones;
using DCC.Core.Interactions;

namespace DCC.Items
{
    /// <summary>
    /// Manages a player's item slots. Server-authoritative.
    ///
    /// UseItem resolves the item's UseMode and routes to the appropriate system:
    ///   - SpawnZone → spawn a ZoneInstance at target position
    ///   - ApplyEffectAtCursor → ExplosionEffect-style AoE resolve
    ///   - InfuseZone → find zone at target, call zone.InfuseEffect for each OnUseEffect
    ///   - PlaceTrap → spawn the trap prefab (which has its own TagContainer + effects)
    ///   - ApplyEffectAtSelf / Target → route through InteractionEngine
    /// </summary>
    [RequireComponent(typeof(EntityAttributes))]
    public class Inventory : NetworkBehaviour
    {
        [SerializeField] private int _slotCount = 8;

        private ItemSlot[] _slots;
        private readonly Dictionary<int, float> _cooldowns = new();
        private EntityAttributes _owner;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            _owner = GetComponent<EntityAttributes>();
            _slots = new ItemSlot[_slotCount];
        }

        // ── Use ───────────────────────────────────────────────────────────

        /// <summary>Called by PlayerNetworkController via ServerRpc.</summary>
        public void UseItem(int slot, Vector3 targetPos, ulong callerClientId)
        {
            if (!IsServer) return;
            if (slot < 0 || slot >= _slots.Length) return;

            var itemSlot = _slots[slot];
            if (itemSlot.Definition == null || itemSlot.Quantity <= 0) return;

            var def = itemSlot.Definition;

            // Cooldown check.
            if (_cooldowns.TryGetValue(slot, out float lastUsed) &&
                Time.time - lastUsed < def.Cooldown) return;
            _cooldowns[slot] = Time.time;

            var ctx = EffectContext.FromNetworkObject(NetworkObject, -1f);
            ctx.OriginPosition = targetPos;

            switch (def.Mode)
            {
                case ItemDefinition.UseMode.ApplyEffectAtSelf:
                    InteractionEngine.Instance?.Resolve(def.OnUseEffects, _owner, ctx);
                    break;

                case ItemDefinition.UseMode.ApplyEffectAtCursor:
                case ItemDefinition.UseMode.ApplyEffectAtTarget:
                    ResolveAtPosition(def, targetPos, ctx);
                    break;

                case ItemDefinition.UseMode.SpawnZone:
                    SpawnZone(def, targetPos, ctx);
                    break;

                case ItemDefinition.UseMode.PlaceTrap:
                    PlaceTrap(def, targetPos);
                    break;

                case ItemDefinition.UseMode.InfuseZone:
                    InfuseNearestZone(def, targetPos, ctx);
                    break;
            }

            // Consume one stack.
            itemSlot.Quantity--;
            if (itemSlot.Quantity <= 0) itemSlot.Definition = null;
            _slots[slot] = itemSlot;
        }

        // ── Inventory management ───────────────────────────────────────────

        public bool AddItem(ItemDefinition def, int quantity = 1)
        {
            if (!IsServer || def == null) return false;

            // Find existing stack.
            if (def.MaxStackSize > 1)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].Definition == def &&
                        _slots[i].Quantity < def.MaxStackSize)
                    {
                        _slots[i].Quantity = Math.Min(_slots[i].Quantity + quantity, def.MaxStackSize);
                        return true;
                    }
                }
            }

            // Find empty slot.
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Definition == null)
                {
                    _slots[i] = new ItemSlot { Definition = def, Quantity = quantity };
                    return true;
                }
            }
            return false; // Inventory full.
        }

        public ItemDefinition GetItem(int slot) =>
            slot >= 0 && slot < _slots.Length ? _slots[slot].Definition : null;

        // ── Private resolution ─────────────────────────────────────────────

        private void ResolveAtPosition(ItemDefinition def, Vector3 pos, EffectContext ctx)
        {
            var cols = Physics.OverlapSphere(pos, 1f);
            foreach (var col in cols)
            {
                var attrs = col.GetComponentInParent<EntityAttributes>();
                if (attrs != null)
                    InteractionEngine.Instance?.Resolve(def.OnUseEffects, attrs, ctx);
            }
        }

        private void SpawnZone(ItemDefinition def, Vector3 pos, EffectContext ctx)
        {
            if (def.ZoneToSpawn == null) return;

            // ZoneInstance prefabs are registered in NetworkManager prefab list.
            // In production, use Addressables to load the prefab by key.
            // For now, assume a ZoneInstance prefab with matching ZoneDefinition exists.
            var prefab = def.ZoneToSpawn.VisualPrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[Inventory] Zone {def.ZoneToSpawn.name} has no prefab assigned.");
                return;
            }

            var go = Instantiate(prefab, pos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            netObj?.Spawn(destroyWithScene: true);
        }

        private void PlaceTrap(ItemDefinition def, Vector3 pos)
        {
            if (def.TrapPrefab == null) return;
            var go = Instantiate(def.TrapPrefab, pos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            netObj?.Spawn(destroyWithScene: true);
        }

        private void InfuseNearestZone(ItemDefinition def, Vector3 pos, EffectContext ctx)
        {
            // Find the nearest ZoneInstance within 5 units.
            var cols = Physics.OverlapSphere(pos, 5f);
            ZoneInstance bestZone = null;
            float bestDist = float.MaxValue;

            foreach (var col in cols)
            {
                var zone = col.GetComponentInParent<ZoneInstance>();
                if (zone == null) continue;
                float d = Vector3.Distance(pos, zone.transform.position);
                if (d < bestDist) { bestDist = d; bestZone = zone; }
            }

            if (bestZone == null) return;

            foreach (var effect in def.OnUseEffects)
                bestZone.InfuseEffect(effect, ctx);
        }
    }

    // ── Supporting types ───────────────────────────────────────────────────────

    [Serializable]
    public struct ItemSlot
    {
        public ItemDefinition Definition;
        public int Quantity;
    }
}
