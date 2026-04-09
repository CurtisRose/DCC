using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;
using DCC.Core.Economy;

namespace DCC.Items
{
    /// <summary>
    /// Separate inventory for loot boxes. Server-authoritative.
    ///
    /// Loot boxes are collected during gameplay and stored here (not in the main Inventory).
    /// In the DCC books, boxes can ONLY be opened in Safe Rooms.
    ///
    /// Opening a box:
    ///   1. Client calls OpenBoxServerRpc(index)
    ///   2. Server validates the caller is in a Safe Room (checks for [InSafeRoom] tag)
    ///   3. Server rolls the loot table
    ///   4. Results are added to the player's Inventory (items) or EquipmentManager (gear)
    ///   5. Gold is added to GoldManager (Phase 8)
    ///   6. PVP Coupons tracked separately
    ///   7. Client receives notification of what they got
    ///
    /// The safe room check can be disabled initially (before Phase 5 ships)
    /// by leaving _safeRoomTag null.
    /// </summary>
    [RequireComponent(typeof(EntityAttributes))]
    public class LootBoxInventory : NetworkBehaviour
    {
        [SerializeField, Tooltip("Maximum loot boxes a crawler can carry.")]
        private int _maxCapacity = 50;

        [SerializeField, Tooltip(
            "Tag required to open boxes (e.g., InSafeRoom). " +
            "Leave null to allow opening anywhere (pre-Phase 5).")]
        private TagDefinition _safeRoomTag;

        private TagContainer _tags;
        private Inventory _inventory;
        private EquipmentManager _equipmentManager;

        // Server-only storage.
        private readonly List<LootBoxDefinition> _boxes = new();
        private System.Random _rng;

        // PVP Coupons — separate tracked currency from Savage boxes.
        private NetworkVariable<int> _pvpCoupons = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int PvpCoupons => _pvpCoupons.Value;

        // Networked box count for client UI.
        private NetworkVariable<int> _networkBoxCount = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int BoxCount => _networkBoxCount.Value;

        // Events.
        public event Action<LootBoxDefinition> OnBoxCollected;
        public event Action<List<LootResult>> OnBoxOpened;

        private void Awake()
        {
            _tags = GetComponent<TagContainer>();
            _inventory = GetComponent<Inventory>();
            _equipmentManager = GetComponent<EquipmentManager>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
                _rng = new System.Random(NetworkObjectId.GetHashCode() ^ System.Environment.TickCount);
        }

        // ── Collection ─────────────────────────────────────────────────────

        /// <summary>Add a loot box to the inventory. Returns false if full.</summary>
        public bool AddBox(LootBoxDefinition box)
        {
            if (!IsServer || box == null) return false;
            if (_boxes.Count >= _maxCapacity) return false;

            _boxes.Add(box);
            _networkBoxCount.Value = _boxes.Count;
            OnBoxCollected?.Invoke(box);
            NotifyBoxCollectedClientRpc(box.DisplayName, (int)box.Tier);
            return true;
        }

        // ── Opening ────────────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void OpenBoxServerRpc(int index, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            OpenBox(index);
        }

        /// <summary>Open a loot box by index. Server-only.</summary>
        public void OpenBox(int index)
        {
            if (!IsServer) return;
            if (index < 0 || index >= _boxes.Count) return;

            // Safe room check (skip if tag not configured — pre-Phase 5 compatibility).
            if (_safeRoomTag != null && _tags != null && !_tags.HasTag(_safeRoomTag))
            {
                NotifyCannotOpenClientRpc();
                return;
            }

            var box = _boxes[index];
            _boxes.RemoveAt(index);
            _networkBoxCount.Value = _boxes.Count;

            if (box.LootTable == null)
            {
                Debug.LogWarning($"[LootBoxInventory] Box {box.DisplayName} has no loot table.");
                return;
            }

            // Roll loot.
            // Temporarily increase the table's effective roll count by the box's bonus.
            var results = new List<LootResult>();
            int totalRolls = box.LootTable.RollCount + box.BonusRolls;

            // Guaranteed drops.
            if (box.LootTable.Entries != null)
            {
                foreach (var entry in box.LootTable.Entries)
                {
                    if (entry.Guaranteed)
                        results.Add(ResolveEntry(entry));
                }
            }

            // Random rolls.
            for (int i = 0; i < totalRolls; i++)
            {
                var entry = box.LootTable.Roll(_rng);
                if (entry.HasValue)
                    results.Add(ResolveEntry(entry.Value));
            }

            // PVP Coupons from Savage boxes.
            if (box.PvpCoupons > 0)
                _pvpCoupons.Value += box.PvpCoupons;

            // Distribute results.
            var itemNames = new List<string>();
            foreach (var result in results)
            {
                if (result.IsItem && _inventory != null)
                {
                    _inventory.AddItem(result.Item, result.Quantity);
                    itemNames.Add($"{result.Item.DisplayName} x{result.Quantity}");
                }
                else if (result.IsEquipment && _equipmentManager != null)
                {
                    // Equipment goes to inventory as a "held" item for now.
                    // Full integration: equipment stored in a separate equipment stash.
                    itemNames.Add($"{result.Equipment.DisplayName} (equipment)");
                }
                else if (result.IsGold)
                {
                    var gold = GetComponent<GoldManager>();
                    if (gold != null) gold.AddGold(result.GoldAmount);
                    itemNames.Add($"{result.GoldAmount} gold");
                }
            }

            OnBoxOpened?.Invoke(results);

            // Notify the client what they got.
            string lootSummary = string.Join(", ", itemNames);
            NotifyBoxOpenedClientRpc(box.DisplayName, lootSummary);
        }

        // ── Queries ────────────────────────────────────────────────────────

        /// <summary>Get the definition of a stored box by index (server-only).</summary>
        public LootBoxDefinition GetBox(int index)
            => index >= 0 && index < _boxes.Count ? _boxes[index] : null;

        /// <summary>Server-only box count (for server logic). Clients use BoxCount property.</summary>
        public int ServerBoxCount => _boxes.Count;

        // ── Helpers ────────────────────────────────────────────────────────

        private LootResult ResolveEntry(LootEntry entry)
        {
            int qty = entry.MinQuantity == entry.MaxQuantity
                ? entry.MinQuantity
                : _rng.Next(entry.MinQuantity, entry.MaxQuantity + 1);

            return new LootResult
            {
                Item = entry.Item,
                Equipment = entry.Equipment,
                GoldAmount = entry.GoldAmount > 0 ? entry.GoldAmount * qty : 0,
                Quantity = entry.GoldAmount > 0 ? 1 : qty
            };
        }

        // ── Network notifications ──────────────────────────────────────────

        [ClientRpc]
        private void NotifyBoxCollectedClientRpc(string boxName, int tier)
        {
            // Client shows "Collected: {boxName}" toast with tier-colored border.
        }

        [ClientRpc]
        private void NotifyBoxOpenedClientRpc(string boxName, string lootSummary)
        {
            // Client shows loot reveal animation: "{boxName} contained: {lootSummary}".
        }

        [ClientRpc]
        private void NotifyCannotOpenClientRpc()
        {
            // Client shows "Loot boxes can only be opened in Safe Rooms."
        }
    }
}
