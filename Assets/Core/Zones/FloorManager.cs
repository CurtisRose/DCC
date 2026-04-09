using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;

namespace DCC.Core.Zones
{
    /// <summary>
    /// Server-authoritative singleton managing floor state, countdown, and transitions.
    ///
    /// In the DCC books:
    ///   - Each floor has a countdown timer. When it expires, the floor collapses.
    ///   - Anyone not inside a stairwell zone when the timer hits zero DIES.
    ///   - Stairwells fully heal the crawler on entry (HP and MP restored).
    ///   - Entering a stairwell early (before the timer threshold) puts you in stasis.
    ///   - After collapse, surviving crawlers transition to the next floor.
    ///
    /// Flow:
    ///   1. FloorManager.StartFloor(floorDef) — sets countdown, spawns stairwells
    ///   2. Countdown ticks. Warnings at 60s, 30s, 10s.
    ///   3. Timer hits zero → CollapseFloor() kills everyone not in a stairwell
    ///   4. TransitionToNextFloor() loads the next FloorDefinition
    ///   5. Repeat until Floor 18 (or all crawlers dead)
    /// </summary>
    public class FloorManager : NetworkBehaviour
    {
        public static FloorManager Instance { get; private set; }

        [SerializeField] private FloorDefinition _startingFloor;

        [SerializeField, Tooltip("Tag that stairwell zones must have. Used to detect safe players during collapse.")]
        private TagDefinition _stairwellTag;

        // Networked state for client UI.
        private NetworkVariable<int> _currentFloorNumber = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private NetworkVariable<float> _countdownRemaining = new(0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> _isCollapsing = new(false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public int CurrentFloorNumber => _currentFloorNumber.Value;
        public float CountdownRemaining => _countdownRemaining.Value;
        public bool IsCollapsing => _isCollapsing.Value;

        // Server-only state.
        private FloorDefinition _currentFloor;
        private readonly HashSet<ulong> _playersInStairwell = new();
        private readonly List<NetworkObject> _spawnedStairwells = new();
        private bool _collapseTriggered;

        // Warning thresholds (seconds remaining).
        private bool _warned60, _warned30, _warned10;

        // Events.
        public event Action<FloorDefinition> OnFloorStarted;
        public event Action<int> OnFloorCollapsed;          // floor number
        public event Action<FloorDefinition> OnFloorTransition;

        // ── Lifecycle ──────────────────────────────────────────────────────

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

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer && _startingFloor != null)
                StartFloor(_startingFloor);
        }

        private void Update()
        {
            if (!IsServer) return;
            if (_currentFloor == null || _collapseTriggered) return;

            // Tick countdown.
            _countdownRemaining.Value -= Time.deltaTime;

            // Collapse warnings.
            float remaining = _countdownRemaining.Value;
            if (remaining <= 60f && !_warned60) { _warned60 = true; NotifyCollapseWarningClientRpc(60); }
            if (remaining <= 30f && !_warned30) { _warned30 = true; NotifyCollapseWarningClientRpc(30); }
            if (remaining <= 10f && !_warned10) { _warned10 = true; NotifyCollapseWarningClientRpc(10); }

            // Floor collapse.
            if (remaining <= 0f)
            {
                _countdownRemaining.Value = 0f;
                CollapseFloor();
            }
        }

        // ── Floor lifecycle ────────────────────────────────────────────────

        /// <summary>Start a new floor. Server-only.</summary>
        public void StartFloor(FloorDefinition floor)
        {
            if (!IsServer || floor == null) return;

            _currentFloor = floor;
            _currentFloorNumber.Value = floor.FloorNumber;
            _countdownRemaining.Value = floor.CountdownDuration;
            _isCollapsing.Value = false;
            _collapseTriggered = false;
            _playersInStairwell.Clear();
            _warned60 = _warned30 = _warned10 = false;

            // Spawn stairwells.
            SpawnStairwells(floor);

            OnFloorStarted?.Invoke(floor);
            NotifyFloorStartedClientRpc(floor.FloorNumber, floor.FloorName ?? $"Floor {floor.FloorNumber}",
                floor.CountdownDuration);

            Debug.Log($"[FloorManager] Floor {floor.FloorNumber} started. " +
                      $"Countdown: {floor.CountdownDuration}s. Stairwells: {floor.StairwellCount}.");
        }

        /// <summary>Kill all players not in a stairwell. Server-only.</summary>
        private void CollapseFloor()
        {
            if (_collapseTriggered) return;
            _collapseTriggered = true;
            _isCollapsing.Value = true;

            // Find all player entities.
            var allPlayers = FindObjectsByType<EntityAttributes>(FindObjectsSortMode.None);
            int killed = 0;

            foreach (var player in allPlayers)
            {
                if (!player.IsAlive) continue;

                ulong clientId = player.NetworkObject.OwnerClientId;

                // Check if this player is safe (in a stairwell).
                if (_playersInStairwell.Contains(clientId))
                    continue;

                // Also check via tag — if the player has [InStairwell] tag.
                if (_stairwellTag != null && player.Tags.HasTag(_stairwellTag))
                    continue;

                // Kill. Floor collapse is instant death — ignores armor and resistances.
                player.AttributeSet.ApplyDamage(float.MaxValue,
                    Effects.EffectContext.Environment(player.transform.position));
                killed++;
            }

            OnFloorCollapsed?.Invoke(_currentFloor.FloorNumber);
            NotifyFloorCollapsedClientRpc(_currentFloor.FloorNumber, killed);

            Debug.Log($"[FloorManager] Floor {_currentFloor.FloorNumber} collapsed. {killed} killed.");

            // After a delay, transition survivors to the next floor.
            if (_currentFloor.NextFloor != null)
                StartCoroutine(DelayedTransition(_currentFloor.NextFloor, 5f));
        }

        private System.Collections.IEnumerator DelayedTransition(FloorDefinition nextFloor, float delay)
        {
            yield return new WaitForSeconds(delay);
            TransitionToNextFloor(nextFloor);
        }

        /// <summary>Clean up current floor and start the next one.</summary>
        private void TransitionToNextFloor(FloorDefinition nextFloor)
        {
            if (!IsServer) return;

            // Despawn current stairwells.
            foreach (var stairwell in _spawnedStairwells)
            {
                if (stairwell != null)
                    stairwell.Despawn(true);
            }
            _spawnedStairwells.Clear();

            OnFloorTransition?.Invoke(nextFloor);
            NotifyFloorTransitionClientRpc(nextFloor.FloorNumber);

            StartFloor(nextFloor);
        }

        // ── Stairwell management ───────────────────────────────────────────

        private void SpawnStairwells(FloorDefinition floor)
        {
            if (floor.StairwellZoneDef?.VisualPrefab == null)
            {
                Debug.LogWarning($"[FloorManager] Floor {floor.FloorNumber} has no stairwell zone prefab.");
                return;
            }

            for (int i = 0; i < floor.StairwellCount; i++)
            {
                // Generate spawn positions spread around the floor.
                // In production, use designated spawn points from the floor's scene/layout.
                float angle = (360f / floor.StairwellCount) * i;
                float radius = 20f + floor.StairwellCount * 2f;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                );

                var go = Instantiate(floor.StairwellZoneDef.VisualPrefab, pos, Quaternion.identity);
                var netObj = go.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(destroyWithScene: true);
                    _spawnedStairwells.Add(netObj);
                }

                // Attach StairwellZone component if not already on the prefab.
                var stairwell = go.GetComponent<StairwellZone>();
                if (stairwell == null)
                    stairwell = go.AddComponent<StairwellZone>();
            }
        }

        /// <summary>Called by StairwellZone when a player enters.</summary>
        public void RegisterPlayerInStairwell(ulong clientId)
        {
            _playersInStairwell.Add(clientId);
        }

        /// <summary>Called by StairwellZone when a player exits.</summary>
        public void UnregisterPlayerFromStairwell(ulong clientId)
        {
            _playersInStairwell.Remove(clientId);
        }

        // ── Network notifications ──────────────────────────────────────────

        [ClientRpc]
        private void NotifyFloorStartedClientRpc(int floorNumber, string floorName, float countdown)
        {
            // Client shows "Floor {floorNumber}: {floorName}" with countdown timer.
        }

        [ClientRpc]
        private void NotifyCollapseWarningClientRpc(int secondsRemaining)
        {
            // Client shows urgent warning: "FLOOR COLLAPSING IN {seconds} SECONDS"
        }

        [ClientRpc]
        private void NotifyFloorCollapsedClientRpc(int floorNumber, int killCount)
        {
            // Client shows floor collapse VFX, death count.
        }

        [ClientRpc]
        private void NotifyFloorTransitionClientRpc(int newFloorNumber)
        {
            // Client shows loading/transition screen.
        }
    }
}
