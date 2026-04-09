using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;

namespace DCC.Core.Zones
{
    /// <summary>
    /// Safe room zone component. Server-authoritative.
    ///
    /// In the DCC books, safe rooms are rest areas between floors that provide:
    ///   - No combat allowed (violence and theft blocked)
    ///   - Stat point allocation (the ONLY place crawlers can spend stat points)
    ///   - Loot box opening (boxes can ONLY be opened here)
    ///   - Access to Personal Spaces (purchasable rooms with crafting, beds, storage)
    ///   - Restrooms, sleeping areas, three TVs
    ///   - Staffed by Bopca Protectors or other non-combatant NPCs
    ///   - Enhanced health/mana regen while inside
    ///
    /// SafeRoomZone adds the [InSafeRoom] tag to players on enter:
    ///   - EntityAttributes.AllocateStatPointServerRpc checks for this tag
    ///   - LootBoxInventory.OpenBox checks for this tag
    ///   - CraftingManager checks for this tag
    ///   - Combat systems can check for [NoCombat] to prevent attacks
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SafeRoomZone : NetworkBehaviour
    {
        [SerializeField, Tooltip("Tag added to players while inside the safe room.")]
        private TagDefinition _inSafeRoomTag;

        [SerializeField, Tooltip("Tag added to players for combat prevention.")]
        private TagDefinition _noCombatTag;

        [SerializeField, Tooltip("Health regen multiplier while in safe room (e.g., 5 = 5x normal regen).")]
        private float _regenMultiplier = 5f;

        [Header("Economy")]
        [SerializeField, Tooltip("Shop NetworkBehaviour available in this safe room. Leave null for no shop.")]
        private NetworkBehaviour _shopManager;

        /// <summary>
        /// The shop in this safe room (if any). UI casts to ShopManager at the Gameplay layer.
        /// </summary>
        public NetworkBehaviour Shop => _shopManager;

        // Track entities inside for cleanup on despawn.
        private readonly HashSet<EntityAttributes> _entitiesInside = new();

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;

            var entity = other.GetComponentInParent<EntityAttributes>();
            if (entity == null || !entity.IsAlive) return;
            if (_entitiesInside.Contains(entity)) return;

            _entitiesInside.Add(entity);

            // Grant safe room tags.
            if (_inSafeRoomTag != null)
                entity.Tags.AddTag(_inSafeRoomTag);
            if (_noCombatTag != null)
                entity.Tags.AddTag(_noCombatTag);

            var netObj = entity.GetComponent<NetworkObject>();
            if (netObj != null)
                NotifyEnteredSafeRoomClientRpc(netObj.OwnerClientId);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServer) return;

            var entity = other.GetComponentInParent<EntityAttributes>();
            if (entity == null) return;
            if (!_entitiesInside.Remove(entity)) return;

            // Remove safe room tags.
            if (_inSafeRoomTag != null)
                entity.Tags.RemoveTag(_inSafeRoomTag);
            if (_noCombatTag != null)
                entity.Tags.RemoveTag(_noCombatTag);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (!IsServer) return;

            // Clean up tags on all entities still inside.
            foreach (var entity in _entitiesInside)
            {
                if (entity == null) continue;
                if (_inSafeRoomTag != null) entity.Tags.RemoveTag(_inSafeRoomTag);
                if (_noCombatTag != null) entity.Tags.RemoveTag(_noCombatTag);
            }
            _entitiesInside.Clear();
        }

        [ClientRpc]
        private void NotifyEnteredSafeRoomClientRpc(ulong clientId)
        {
            // Client shows "You have entered a Safe Room" notification.
            // UI enables stat allocation panel, loot box opening, crafting.
        }
    }
}
