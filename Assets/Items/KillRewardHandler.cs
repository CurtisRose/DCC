using Unity.Netcode;
using UnityEngine;
using DCC.Core.Entities;
using DCC.Core.Economy;
using DCC.Core.Achievements;

namespace DCC.Items
{
    /// <summary>
    /// Subscribes to EntityAttributes.OnKilled events and distributes kill rewards:
    /// gold, viewer boosts, and achievement notifications.
    ///
    /// Attached as a server singleton (e.g., on the GameManager object).
    /// This exists in the Gameplay assembly to bridge Core (EntityAttributes, GoldManager)
    /// with Items (ViewerSystem) without circular assembly references.
    /// </summary>
    public class KillRewardHandler : NetworkBehaviour
    {
        public static KillRewardHandler Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        /// <summary>
        /// Register a spawned entity so its death triggers rewards.
        /// Call this for all mobs and players after spawning.
        /// </summary>
        public void RegisterEntity(EntityAttributes entity)
        {
            if (!IsServer || entity == null) return;
            entity.OnKilled += HandleKill;
        }

        public void UnregisterEntity(EntityAttributes entity)
        {
            if (entity == null) return;
            entity.OnKilled -= HandleKill;
        }

        private void HandleKill(NetworkObject killerObj, EntityDefinition victimDef)
        {
            if (!IsServer || killerObj == null) return;

            // Award gold.
            int baseXp = victimDef != null ? victimDef.BaseXP : 10;
            float lootMult = victimDef != null ? victimDef.LootMultiplier : 1f;
            int gold = Mathf.RoundToInt(baseXp * lootMult);

            if (gold > 0)
            {
                var killerGold = killerObj.GetComponent<GoldManager>();
                if (killerGold != null)
                    killerGold.AddGold(gold);
            }

            ulong killerClientId = killerObj.OwnerClientId;

            // Notify SystemAI for kill achievements.
            var systemAI = SystemAI.Instance;
            if (systemAI != null)
                systemAI.NotifyKill(killerClientId, killerObj);

            // Notify ViewerSystem for dramatic event.
            var viewerSystem = ViewerSystem.Instance;
            if (viewerSystem != null)
                viewerSystem.ReportDramaticEvent(killerClientId, DramaticEventType.MobKill);
        }
    }
}
