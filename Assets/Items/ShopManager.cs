using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;
using DCC.Core.Economy;

namespace DCC.Items
{
    /// <summary>
    /// Handles shop purchases. Server-authoritative.
    /// Attached to the safe room zone or as a standalone interactable.
    ///
    /// Purchase flow:
    ///   1. Client calls PurchaseServerRpc(shopIndex, entryIndex)
    ///   2. Server validates: player is in safe room, has enough gold, item in stock
    ///   3. Server deducts gold via GoldManager
    ///   4. Server adds item/equipment to player's Inventory/EquipmentManager
    ///   5. Server decrements stock (if limited)
    ///   6. Client gets purchase confirmation
    ///
    /// Sell flow:
    ///   1. Client calls SellItemServerRpc(inventorySlot)
    ///   2. Server calculates sell price (50% of GoldValue, Charisma-boosted)
    ///   3. Server removes item, adds gold
    /// </summary>
    public class ShopManager : NetworkBehaviour
    {
        [SerializeField, Tooltip("Shops available in this safe room.")]
        private ShopDefinition[] _shops;

        [SerializeField, Tooltip("Tag required to access shops (InSafeRoom). Leave null to allow shopping anywhere.")]
        private TagDefinition _safeRoomTag;

        [SerializeField, Tooltip("Sell price multiplier (0.5 = sell for half the buy price).")]
        private float _sellMultiplier = 0.5f;

        // Server-only: remaining stock per shop entry. Key = (shopIdx, entryIdx).
        private readonly Dictionary<(int, int), int> _remainingStock = new();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer) InitializeStock();
        }

        private void InitializeStock()
        {
            _remainingStock.Clear();
            if (_shops == null) return;

            for (int s = 0; s < _shops.Length; s++)
            {
                if (_shops[s]?.Stock == null) continue;
                for (int e = 0; e < _shops[s].Stock.Length; e++)
                {
                    int limit = _shops[s].Stock[e].StockLimit;
                    if (limit > 0)
                        _remainingStock[(s, e)] = limit;
                }
            }
        }

        // ── Purchase ───────────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void PurchaseServerRpc(int shopIndex, int entryIndex, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var playerObj = client.PlayerObject;
            if (playerObj == null) return;

            Purchase(playerObj, shopIndex, entryIndex);
        }

        public bool Purchase(NetworkObject playerObj, int shopIndex, int entryIndex)
        {
            if (!IsServer || playerObj == null) return false;

            // Validate shop/entry indices.
            if (_shops == null || shopIndex < 0 || shopIndex >= _shops.Length) return false;
            var shop = _shops[shopIndex];
            if (shop.Stock == null || entryIndex < 0 || entryIndex >= shop.Stock.Length) return false;

            var entry = shop.Stock[entryIndex];

            // Safe room check.
            var tags = playerObj.GetComponent<TagContainer>();
            if (_safeRoomTag != null && tags != null && !tags.HasTag(_safeRoomTag))
                return false;

            // Stock check.
            var key = (shopIndex, entryIndex);
            if (entry.StockLimit > 0)
            {
                if (!_remainingStock.TryGetValue(key, out int remaining) || remaining <= 0)
                    return false;
            }

            // Calculate price with Charisma discount.
            int basePrice = entry.EffectivePrice;
            int finalPrice = CalculatePrice(basePrice, shop, playerObj);

            // Gold check.
            var gold = playerObj.GetComponent<GoldManager>();
            if (gold == null || !gold.CanAfford(finalPrice)) return false;

            // Execute purchase.
            gold.SpendGold(finalPrice);

            // Deliver item or equipment.
            int qty = entry.Quantity > 0 ? entry.Quantity : 1;
            if (entry.Item != null)
            {
                var inventory = playerObj.GetComponent<Inventory>();
                if (inventory != null) inventory.AddItem(entry.Item, qty);
            }
            else if (entry.Equipment != null)
            {
                // Equipment goes to inventory as a special item (equip from there).
                // Future: dedicated equipment stash.
                var inventory = playerObj.GetComponent<Inventory>();
                if (inventory != null) inventory.AddItem(entry.Equipment.name);
            }

            // Decrement stock.
            if (entry.StockLimit > 0 && _remainingStock.ContainsKey(key))
                _remainingStock[key]--;

            NotifyPurchaseClientRpc(entry.DisplayName, qty, finalPrice);
            return true;
        }

        // ── Sell ───────────────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void SellItemServerRpc(int inventorySlot, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var playerObj = client.PlayerObject;
            if (playerObj == null) return;

            SellItem(playerObj, inventorySlot);
        }

        public bool SellItem(NetworkObject playerObj, int inventorySlot)
        {
            if (!IsServer || playerObj == null) return false;

            // Safe room check.
            var tags = playerObj.GetComponent<TagContainer>();
            if (_safeRoomTag != null && tags != null && !tags.HasTag(_safeRoomTag))
                return false;

            var inventory = playerObj.GetComponent<Inventory>();
            if (inventory == null) return false;

            var item = inventory.GetItem(inventorySlot);
            if (item == null) return false;

            // Calculate sell price.
            int sellPrice = Mathf.Max(1, Mathf.RoundToInt(item.GoldValue * _sellMultiplier));

            // Charisma bonus on sell price.
            var attrs = playerObj.GetComponent<EntityAttributes>();
            if (attrs != null)
            {
                int cha = attrs.AttributeSet.Charisma;
                float chaBonus = 1f + cha * 0.01f; // 1% per Cha point.
                sellPrice = Mathf.RoundToInt(sellPrice * chaBonus);
            }

            // Execute sale.
            inventory.RemoveItem(inventorySlot);
            var gold = playerObj.GetComponent<GoldManager>();
            if (gold != null) gold.AddGold(sellPrice);

            NotifySoldClientRpc(item.DisplayName, sellPrice);
            return true;
        }

        // ── Restock (called between floors or on timer) ────────────────────

        /// <summary>
        /// Reset all stock counts. Called by FloorManager on floor transition.
        /// </summary>
        public void Restock()
        {
            if (!IsServer) return;
            InitializeStock();
        }

        // ── Price calculation ──────────────────────────────────────────────

        private int CalculatePrice(int basePrice, ShopDefinition shop, NetworkObject playerObj)
        {
            if (!shop.CharismaDiscount) return basePrice;

            var attrs = playerObj.GetComponent<EntityAttributes>();
            if (attrs == null) return basePrice;

            int cha = attrs.AttributeSet.Charisma;
            float discount = Mathf.Min(cha * shop.DiscountPerCharisma, shop.MaxDiscount);
            return Mathf.Max(1, Mathf.RoundToInt(basePrice * (1f - discount)));
        }

        // ── Network ────────────────────────────────────────────────────────

        [ClientRpc]
        private void NotifyPurchaseClientRpc(string itemName, int quantity, int goldSpent)
        {
            // Client shows "Purchased: {itemName} x{quantity} for {goldSpent} gold".
        }

        [ClientRpc]
        private void NotifySoldClientRpc(string itemName, int goldEarned)
        {
            // Client shows "Sold: {itemName} for {goldEarned} gold".
        }
    }
}
