using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

namespace DCC.Multiplayer.Lobby
{
    /// <summary>
    /// Manages the pre-game lobby: player readiness, team assignment display,
    /// and match start handshake.
    ///
    /// Simplified version — a shipped game would integrate Unity Gaming Services
    /// (Lobby + Relay) for matchmaking and NAT traversal.
    /// </summary>
    public class LobbyManager : NetworkBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        [SerializeField] private int _minPlayersToStart = 2;
        [SerializeField] private int _maxPlayers = 8;

        // Networked list of player slots visible to all clients.
        private NetworkList<LobbySlot> _slots;

        private void Awake()
        {
            Instance = this;
            _slots = new NetworkList<LobbySlot>(
                writePerm: NetworkVariableWritePermission.Server);
        }

        public IReadOnlyList<LobbySlot> Slots => _slots;

        // ── Server RPCs ────────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void SetReadyServerRpc(bool ready, ServerRpcParams rpc = default)
        {
            ulong clientId = rpc.Receive.SenderClientId;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].ClientId == clientId)
                {
                    var slot = _slots[i];
                    slot.Ready = ready;
                    _slots[i] = slot;
                    break;
                }
            }
            CheckAutoStart();
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetDisplayNameServerRpc(FixedString32Bytes displayName, ServerRpcParams rpc = default)
        {
            ulong clientId = rpc.Receive.SenderClientId;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].ClientId == clientId)
                {
                    var slot = _slots[i];
                    slot.DisplayName = displayName;
                    _slots[i] = slot;
                    break;
                }
            }
        }

        // ── Connection handling (called by NetworkGameManager) ─────────────

        public void AddPlayer(ulong clientId)
        {
            if (!IsServer) return;
            if (_slots.Count >= _maxPlayers)
            {
                NetworkManager.Singleton.DisconnectClient(clientId);
                return;
            }
            _slots.Add(new LobbySlot
            {
                ClientId = clientId,
                DisplayName = $"Player {clientId}",
                Ready = false
            });
        }

        public void RemovePlayer(ulong clientId)
        {
            if (!IsServer) return;
            for (int i = _slots.Count - 1; i >= 0; i--)
                if (_slots[i].ClientId == clientId) { _slots.RemoveAt(i); break; }
        }

        // ── Auto-start ─────────────────────────────────────────────────────

        private void CheckAutoStart()
        {
            if (_slots.Count < _minPlayersToStart) return;
            foreach (var slot in _slots)
                if (!slot.Ready) return;

            FindObjectOfType<NetworkGameManager>()?.StartMatchServerRpc();
        }
    }

    // ── Data ───────────────────────────────────────────────────────────────────

    public struct LobbySlot : INetworkSerializable, System.IEquatable<LobbySlot>
    {
        public ulong ClientId;
        public FixedString32Bytes DisplayName;
        public bool Ready;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref DisplayName);
            serializer.SerializeValue(ref Ready);
        }

        public bool Equals(LobbySlot other) => ClientId == other.ClientId;
    }
}
