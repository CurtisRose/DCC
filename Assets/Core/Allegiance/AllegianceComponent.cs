using Unity.Netcode;
using UnityEngine;

namespace DCC.Core.Allegiance
{
    /// <summary>
    /// Per-entity component that stores owner identity and team membership.
    /// Provides a convenient query API over AllegianceMatrix.
    ///
    /// Attached to all players, NPC enemies, and owned objects (summons, traps).
    /// </summary>
    [DisallowMultipleComponent]
    public class AllegianceComponent : NetworkBehaviour
    {
        // Server writes, all clients read.
        private NetworkVariable<ulong> _ownerClientId = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        private NetworkVariable<int> _teamId = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        public ulong OwnerClientId => _ownerClientId.Value;
        public int TeamId => _teamId.Value;

        public bool IsPlayerOwned => _ownerClientId.Value != 0;
        public bool IsEnvironment => _ownerClientId.Value == 0;

        /// <summary>
        /// Server-only: assign ownership and team (for summoned pets, traps, etc.).
        /// </summary>
        public void SetOwnership(ulong ownerClientId, int teamId)
        {
            if (!IsServer) return;
            _ownerClientId.Value = ownerClientId;
            _teamId.Value = teamId;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer && IsOwner)
            {
                _ownerClientId.Value = OwnerClientId;
                AllegianceMatrix.Instance?.RegisterPlayer(OwnerClientId);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer && IsOwner)
                AllegianceMatrix.Instance?.UnregisterPlayer(OwnerClientId);
        }

        /// <summary>
        /// Friendly-fire note: this returns Allied/Neutral but damage is NEVER blocked.
        /// Use only for UI, XP attribution, and AI targeting priority.
        /// </summary>
        public AllegianceMatrix.Relation GetRelationTo(AllegianceComponent other)
        {
            if (AllegianceMatrix.Instance == null) return AllegianceMatrix.Relation.Neutral;
            return AllegianceMatrix.Instance.GetRelation(OwnerClientId, other.OwnerClientId);
        }

        public bool IsAlliedWith(AllegianceComponent other)
            => GetRelationTo(other) == AllegianceMatrix.Relation.Allied;
    }
}
