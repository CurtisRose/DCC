using System;
using Unity.Netcode;
using UnityEngine;

namespace DCC.Core.Economy
{
    /// <summary>
    /// Per-player gold tracking. Server-authoritative.
    ///
    /// In the DCC books:
    ///   - Gold is the universal currency in the dungeon.
    ///   - Mobs drop gold on death (scaled by mob difficulty and floor).
    ///   - Gold is used in safe room shops, personal space upgrades, crafting station installs.
    ///   - Loot boxes can contain gold.
    ///   - Achievements can reward gold.
    ///   - Sponsors sometimes gift gold based on viewer milestones.
    ///   - Carl's running gold total is tracked throughout the series.
    ///
    /// GoldManager is attached to the player entity alongside EntityAttributes.
    /// </summary>
    public class GoldManager : NetworkBehaviour
    {
        private NetworkVariable<int> _gold = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Lifetime earnings (for achievements/leaderboard).
        private NetworkVariable<int> _lifetimeGold = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public int Gold => _gold.Value;
        public int LifetimeGold => _lifetimeGold.Value;

        public event Action<int> OnGoldChanged;       // new total
        public event Action<int> OnGoldEarned;         // amount earned
        public event Action<int> OnGoldSpent;          // amount spent

        // ── Earning ────────────────────────────────────────────────────────

        /// <summary>
        /// Add gold to this crawler. Source-agnostic (mob kills, loot boxes, shops, sponsors).
        /// </summary>
        public void AddGold(int amount)
        {
            if (!IsServer || amount <= 0) return;

            _gold.Value += amount;
            _lifetimeGold.Value += amount;

            OnGoldEarned?.Invoke(amount);
            OnGoldChanged?.Invoke(_gold.Value);
            NotifyGoldChangedClientRpc(_gold.Value, amount);
        }

        // ── Spending ───────────────────────────────────────────────────────

        /// <summary>
        /// Attempt to spend gold. Returns true if the crawler has enough.
        /// </summary>
        public bool SpendGold(int amount)
        {
            if (!IsServer || amount <= 0) return false;
            if (_gold.Value < amount) return false;

            _gold.Value -= amount;

            OnGoldSpent?.Invoke(amount);
            OnGoldChanged?.Invoke(_gold.Value);
            NotifyGoldChangedClientRpc(_gold.Value, -amount);
            return true;
        }

        /// <summary>
        /// Check if this crawler can afford a purchase without spending.
        /// </summary>
        public bool CanAfford(int amount) => _gold.Value >= amount;

        // ── Transfer ───────────────────────────────────────────────────────

        /// <summary>
        /// Transfer gold to another crawler. Both must have GoldManager.
        /// </summary>
        public bool TransferTo(GoldManager recipient, int amount)
        {
            if (!IsServer || recipient == null || amount <= 0) return false;
            if (_gold.Value < amount) return false;

            _gold.Value -= amount;
            recipient._gold.Value += amount;
            recipient._lifetimeGold.Value += amount;

            OnGoldSpent?.Invoke(amount);
            OnGoldChanged?.Invoke(_gold.Value);
            recipient.OnGoldEarned?.Invoke(amount);
            recipient.OnGoldChanged?.Invoke(recipient._gold.Value);

            NotifyGoldChangedClientRpc(_gold.Value, -amount);
            return true;
        }

        // ── Network ────────────────────────────────────────────────────────

        [ClientRpc]
        private void NotifyGoldChangedClientRpc(int newTotal, int delta)
        {
            // Client updates gold UI, plays coin sound for positive delta.
            OnGoldChanged?.Invoke(newTotal);
        }
    }
}
