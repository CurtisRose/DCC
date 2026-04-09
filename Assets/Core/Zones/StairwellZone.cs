using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;

namespace DCC.Core.Zones
{
    /// <summary>
    /// Stairwell component attached to stairwell zone prefabs. Server-authoritative.
    ///
    /// In the DCC books:
    ///   - Entering a stairwell fully heals the crawler (HP and MP restored instantly).
    ///   - Stairwells protect crawlers from floor collapse.
    ///   - Entering a stairwell early (before the threshold) puts you in stasis
    ///     until the next floor begins.
    ///   - Each floor has progressively fewer stairwells (half of previous, rounded up).
    ///
    /// StairwellZone works with the existing ZoneInstance/TagContainer infrastructure:
    ///   - The zone's tags should include [Stairwell, SafeZone, NoCombat]
    ///   - On trigger enter: full heal, register with FloorManager as safe
    ///   - On trigger exit: unregister from FloorManager
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class StairwellZone : NetworkBehaviour
    {
        [SerializeField, Tooltip("Tag added to players while inside the stairwell.")]
        private TagDefinition _inStairwellTag;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;

            var entity = other.GetComponentInParent<EntityAttributes>();
            if (entity == null || !entity.IsAlive) return;

            // Only affect player-owned entities.
            var netObj = entity.GetComponent<NetworkObject>();
            if (netObj == null) return;

            ulong clientId = netObj.OwnerClientId;

            // Full heal — faithful to the books.
            var attrs = entity.AttributeSet;
            attrs.ApplyHeal(attrs.MaxHealth);
            attrs.RestoreMana(attrs.MaxMana);

            // Add stairwell tag.
            if (_inStairwellTag != null)
                entity.Tags.AddTag(_inStairwellTag);

            // Register with FloorManager as safe.
            FloorManager.Instance?.RegisterPlayerInStairwell(clientId);

            NotifyStairwellEnteredClientRpc(clientId);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServer) return;

            var entity = other.GetComponentInParent<EntityAttributes>();
            if (entity == null) return;

            var netObj = entity.GetComponent<NetworkObject>();
            if (netObj == null) return;

            ulong clientId = netObj.OwnerClientId;

            // Remove stairwell tag.
            if (_inStairwellTag != null)
                entity.Tags.RemoveTag(_inStairwellTag);

            // Unregister from FloorManager.
            FloorManager.Instance?.UnregisterPlayerFromStairwell(clientId);
        }

        [ClientRpc]
        private void NotifyStairwellEnteredClientRpc(ulong clientId)
        {
            // Client shows "You have entered the stairwell. You are safe." notification.
            // Play healing VFX, full health/mana bar flash.
        }
    }
}
