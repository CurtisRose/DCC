using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;

namespace DCC.Multiplayer
{
    /// <summary>
    /// Top-level server-authoritative game manager.
    ///
    /// Responsibilities:
    ///   - Bootstrapping all singletons (TagRegistry, InteractionEngine, DiscoverySystem)
    ///   - Handling player connect/disconnect
    ///   - Managing match state (Lobby → InProgress → GameOver)
    ///   - Spawning player prefabs
    ///
    /// Not a singleton accessed by gameplay code — systems use their own Instance refs.
    /// This class orchestrates the startup sequence only.
    /// </summary>
    public class NetworkGameManager : NetworkBehaviour
    {
        [Header("Spawn Configuration")]
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private Transform[] _spawnPoints;

        [Header("Tag Configuration")]
        [SerializeField, Tooltip("All TagDefinition assets in the project. Load via Resources.LoadAll in production.")]
        private TagDefinition[] _allTags;

        [Header("Race & Class")]
        [SerializeField, Tooltip("All available races. Indexed by client selection.")]
        private RaceDefinition[] _availableRaces;
        [SerializeField, Tooltip("All available classes. Indexed by client selection.")]
        private ClassDefinition[] _availableClasses;

        // Stores client race/class choices made in lobby before spawn.
        private readonly Dictionary<ulong, (int raceIndex, int classIndex)> _pendingChoices = new();

        private readonly Dictionary<ulong, GameObject> _spawnedPlayers = new();
        private int _spawnIndex = 0;

        public enum MatchState { Lobby, InProgress, GameOver }
        private NetworkVariable<MatchState> _matchState = new(MatchState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public MatchState CurrentState => _matchState.Value;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            // Initialize the tag registry before any scene objects awaken.
            // In a shipped game, load tags via Addressables instead.
            if (_allTags != null && _allTags.Length > 0)
                TagRegistry.Initialize(_allTags);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer) return;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (!IsServer) return;

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        // ── Connection handling ────────────────────────────────────────────

        private void OnClientConnected(ulong clientId)
        {
            if (_matchState.Value != MatchState.InProgress)
            {
                // Still in lobby — don't spawn yet, wait for StartMatch.
                return;
            }
            SpawnPlayer(clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (_spawnedPlayers.TryGetValue(clientId, out var player))
            {
                if (player != null)
                    player.GetComponent<NetworkObject>()?.Despawn(true);
                _spawnedPlayers.Remove(clientId);
            }
        }

        // ── Match flow ────────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void StartMatchServerRpc()
        {
            if (_matchState.Value != MatchState.Lobby) return;
            _matchState.Value = MatchState.InProgress;

            // Spawn all connected clients.
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
                SpawnPlayer(clientId);

            // Initialize floor progression (FloorManager starts itself on OnNetworkSpawn
            // if _startingFloor is set, but we can also kick it off explicitly here
            // if the FloorManager is a scene object that was already spawned).

            Debug.Log("[NetworkGameManager] Match started.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void EndMatchServerRpc()
        {
            _matchState.Value = MatchState.GameOver;
            AnnounceGameOverClientRpc();
        }

        [ClientRpc]
        private void AnnounceGameOverClientRpc() { /* Show game over screen. */ }

        // ── Race/Class Selection (Lobby) ──────────────────────────────────

        /// <summary>
        /// Called by clients in the lobby to choose their Race and Class.
        /// Selection is stored until match start, then applied during spawn.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SelectRaceAndClassServerRpc(int raceIndex, int classIndex, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (_matchState.Value != MatchState.Lobby) return;

            // Validate indices.
            if (_availableRaces == null || raceIndex < 0 || raceIndex >= _availableRaces.Length) return;
            if (_availableClasses == null || classIndex < 0 || classIndex >= _availableClasses.Length) return;

            _pendingChoices[clientId] = (raceIndex, classIndex);
        }

        // ── Spawning ──────────────────────────────────────────────────────

        private void SpawnPlayer(ulong clientId)
        {
            if (_playerPrefab == null) return;

            Vector3 spawnPos = _spawnPoints != null && _spawnPoints.Length > 0
                ? _spawnPoints[_spawnIndex % _spawnPoints.Length].position
                : Vector3.zero;
            _spawnIndex++;

            var go = Instantiate(_playerPrefab, spawnPos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);
            _spawnedPlayers[clientId] = go;

            // Apply race/class if the client made a selection in lobby.
            var identity = go.GetComponent<CrawlerIdentity>();
            if (identity != null && _pendingChoices.TryGetValue(clientId, out var choice))
            {
                var race = _availableRaces[choice.raceIndex];
                var cls = _availableClasses[choice.classIndex];

                // Validate class requirements before applying.
                if (identity.MeetsClassRequirements(cls))
                    identity.Initialize(race, cls);
                else
                    Debug.LogWarning($"[NetworkGameManager] Client {clientId} doesn't meet requirements for {cls.DisplayName}.");
            }
        }
    }
}
