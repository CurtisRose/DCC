using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DCC.Core.Zones
{
    /// <summary>
    /// A crawler's personal space — purchasable room attached to safe rooms.
    /// Server-authoritative.
    ///
    /// In the DCC books:
    ///   - Purchasable from Floor 3 onward with gold
    ///   - Customizable storage/living area accessed via safe rooms
    ///   - Upgradeable with gold or by combining rooms
    ///   - Can add crafting stations (Alchemy Table, etc.)
    ///   - Beds provide regen bonuses while resting
    ///   - Mordecai (Carl's Game Guide / Manager) crafts potions at the Alchemy Table
    ///
    /// PersonalSpace is spawned per-player and persists across floors.
    /// Access is gated by being inside a SafeRoom zone.
    /// </summary>
    public class PersonalSpace : NetworkBehaviour
    {
        [SerializeField] private int _basePurchaseCost = 500;
        [SerializeField] private int _upgradeCostMultiplier = 2;

        // Networked state.
        private NetworkVariable<ulong> _ownerClientId = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private NetworkVariable<int> _tier = new(1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> _hasBed = new(false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public ulong OwnerClientId => _ownerClientId.Value;
        public int Tier => _tier.Value;
        public bool HasBed => _hasBed.Value;

        // Server-only: installed crafting stations.
        private readonly List<Items.CraftingStation> _installedStations = new();
        public IReadOnlyList<Items.CraftingStation> InstalledStations => _installedStations;

        // ── Initialization ─────────────────────────────────────────────────

        /// <summary>Called by server after purchasing. Sets owner and initial state.</summary>
        public void Initialize(ulong ownerClientId)
        {
            if (!IsServer) return;
            _ownerClientId.Value = ownerClientId;
            _tier.Value = 1;
        }

        // ── Upgrades ───────────────────────────────────────────────────────

        /// <summary>Upgrade the space to the next tier. Returns gold cost, or -1 if already max.</summary>
        public int GetUpgradeCost()
        {
            return _basePurchaseCost * _tier.Value * _upgradeCostMultiplier;
        }

        /// <summary>Upgrade the personal space. Server-only. Gold deduction handled by caller.</summary>
        public void Upgrade()
        {
            if (!IsServer) return;
            _tier.Value++;
            NotifyUpgradedClientRpc(_tier.Value);
        }

        // ── Crafting Stations ──────────────────────────────────────────────

        /// <summary>Install a crafting station. Server-only.</summary>
        public bool InstallStation(Items.CraftingStation station)
        {
            if (!IsServer || station == null) return false;
            if (_installedStations.Contains(station)) return false;

            // Max stations based on tier.
            int maxStations = _tier.Value;
            if (_installedStations.Count >= maxStations) return false;

            _installedStations.Add(station);
            NotifyStationInstalledClientRpc(station.DisplayName);
            return true;
        }

        // ── Bed ────────────────────────────────────────────────────────────

        /// <summary>Install a bed for regen bonus. Server-only. Gold deduction handled by caller.</summary>
        public void InstallBed()
        {
            if (!IsServer) return;
            _hasBed.Value = true;
        }

        // ── Network notifications ──────────────────────────────────────────

        [ClientRpc]
        private void NotifyUpgradedClientRpc(int newTier)
        {
            // Client shows "Personal Space upgraded to Tier {newTier}!"
        }

        [ClientRpc]
        private void NotifyStationInstalledClientRpc(string stationName)
        {
            // Client shows "{stationName} installed in your Personal Space."
        }
    }
}
