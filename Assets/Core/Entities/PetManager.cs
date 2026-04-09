using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Allegiance;
using DCC.Abilities;

namespace DCC.Core.Entities
{
    /// <summary>
    /// Manages a crawler's active pets. Server-authoritative.
    /// Attached to the player entity alongside EntityAttributes.
    ///
    /// In the DCC books:
    ///   - Max active pets is a function of Charisma (Cha / 5, minimum 1).
    ///   - Each pet has a Charisma cost (usually 1, larger/rarer pets cost more).
    ///   - Pets can be summoned and dismissed at will.
    ///   - Dead pets stay in the roster but can't be summoned until resurrected.
    ///   - Carl has Charisma 28 → 5 pet slots, but mostly just uses Donut.
    ///
    /// Flow:
    ///   1. Client calls SummonPetServerRpc(rosterIndex)
    ///   2. Server validates Charisma slots, spawns pet prefab
    ///   3. PetAI on the spawned pet handles follow/attack behavior
    ///   4. Client calls DismissPetServerRpc(rosterIndex) to despawn
    ///   5. Owner death dismisses all active pets (they don't die with the owner
    ///      unless the floor collapses)
    /// </summary>
    [RequireComponent(typeof(EntityAttributes))]
    public class PetManager : NetworkBehaviour
    {
        private EntityAttributes _attrs;
        private AllegianceComponent _allegiance;

        // Roster: all pets this crawler owns (alive or dead).
        private readonly List<PetSlot> _roster = new();

        // Active (spawned) pets mapped by roster index.
        private readonly Dictionary<int, NetworkObject> _activePets = new();

        // Networked for client UI.
        private NetworkVariable<int> _activeCount = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private NetworkVariable<int> _maxSlots = new(1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public int ActivePetCount => _activeCount.Value;
        public int MaxPetSlots => _maxSlots.Value;

        public event Action<PetDefinition> OnPetSummoned;
        public event Action<PetDefinition> OnPetDismissed;
        public event Action<PetDefinition> OnPetDied;

        private void Awake()
        {
            _attrs = GetComponent<EntityAttributes>();
            _allegiance = GetComponent<AllegianceComponent>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                RecalculateSlots();
                _attrs.OnDied += HandleOwnerDeath;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer)
            {
                DismissAll();
                _attrs.OnDied -= HandleOwnerDeath;
            }
        }

        // ── Queries ────────────────────────────────────────────────────────

        public int RosterCount => _roster.Count;

        public PetSlot GetRosterEntry(int index)
            => index >= 0 && index < _roster.Count ? _roster[index] : default;

        public bool IsPetActive(int rosterIndex)
            => _activePets.ContainsKey(rosterIndex);

        /// <summary>
        /// Max pet slots = Charisma / 5, minimum 1.
        /// </summary>
        public int CalculateMaxSlots()
        {
            int cha = _attrs.AttributeSet.Charisma;
            return Mathf.Max(1, cha / 5);
        }

        /// <summary>
        /// Total charisma cost of all currently active pets.
        /// </summary>
        public int ActiveCharismaCost()
        {
            int cost = 0;
            foreach (var kvp in _activePets)
            {
                int idx = kvp.Key;
                if (idx >= 0 && idx < _roster.Count)
                    cost += _roster[idx].Definition.CharismaCost;
            }
            return cost;
        }

        // ── Roster management (server only) ────────────────────────────────

        /// <summary>
        /// Add a pet to the crawler's roster (e.g., from a pet egg, taming, loot box).
        /// Returns the roster index.
        /// </summary>
        public int AddToRoster(PetDefinition petDef)
        {
            if (!IsServer || petDef == null) return -1;

            _roster.Add(new PetSlot
            {
                Definition = petDef,
                IsAlive = true,
                Level = 1
            });

            int index = _roster.Count - 1;
            NotifyPetAddedClientRpc(petDef.PetTypeName);
            return index;
        }

        /// <summary>
        /// Remove a pet from the roster entirely (e.g., permanent death, release).
        /// Dismisses first if active.
        /// </summary>
        public void RemoveFromRoster(int rosterIndex)
        {
            if (!IsServer) return;
            if (rosterIndex < 0 || rosterIndex >= _roster.Count) return;

            if (_activePets.ContainsKey(rosterIndex))
                DismissPet(rosterIndex);

            _roster.RemoveAt(rosterIndex);

            // Rekey active pets whose indices shifted.
            var rekeyed = new Dictionary<int, NetworkObject>();
            foreach (var kvp in _activePets)
            {
                int newIdx = kvp.Key > rosterIndex ? kvp.Key - 1 : kvp.Key;
                rekeyed[newIdx] = kvp.Value;
            }
            _activePets.Clear();
            foreach (var kvp in rekeyed)
                _activePets[kvp.Key] = kvp.Value;
        }

        // ── Summon / Dismiss ───────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void SummonPetServerRpc(int rosterIndex, ServerRpcParams rpcParams = default)
        {
            SummonPet(rosterIndex);
        }

        [ServerRpc(RequireOwnership = false)]
        public void DismissPetServerRpc(int rosterIndex, ServerRpcParams rpcParams = default)
        {
            DismissPet(rosterIndex);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetPetBehaviorServerRpc(int rosterIndex, PetAIBehavior behavior, ServerRpcParams rpcParams = default)
        {
            if (!_activePets.TryGetValue(rosterIndex, out var petObj)) return;
            var ai = petObj.GetComponent<PetAI>();
            if (ai != null) ai.SetBehavior(behavior);
        }

        public bool SummonPet(int rosterIndex)
        {
            if (!IsServer) return false;
            if (rosterIndex < 0 || rosterIndex >= _roster.Count) return false;

            var slot = _roster[rosterIndex];
            if (!slot.IsAlive) return false;
            if (_activePets.ContainsKey(rosterIndex)) return false;

            // Check charisma cost.
            int currentCost = ActiveCharismaCost();
            int maxSlots = CalculateMaxSlots();
            if (currentCost + slot.Definition.CharismaCost > maxSlots) return false;

            // Spawn pet.
            if (slot.Definition.Prefab == null) return false;

            Vector3 spawnPos = transform.position + transform.right * 2f;
            var go = Instantiate(slot.Definition.Prefab, spawnPos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Destroy(go);
                return false;
            }

            netObj.Spawn(destroyWithScene: true);

            // Initialize entity stats from pet definition.
            var attrs = go.GetComponent<EntityAttributes>();
            if (attrs != null && slot.Definition.EntityDefinition != null)
                slot.Definition.EntityDefinition.InitializeAttributes(attrs.AttributeSet);

            // Set allegiance to match owner.
            var allegiance = go.GetComponent<AllegianceComponent>();
            if (allegiance != null && _allegiance != null)
                allegiance.SetOwnership(_allegiance.OwnerClientId, _allegiance.TeamId);

            // Initialize PetAI.
            var ai = go.GetComponent<PetAI>();
            if (ai != null)
                ai.Initialize(this, rosterIndex, slot.Definition);

            // Grant starting skills.
            var skills = go.GetComponent<SkillTracker>();
            if (skills != null && slot.Definition.StartingSkills != null)
            {
                foreach (var skill in slot.Definition.StartingSkills)
                    if (skill != null) skills.GrantSkill(skill);
            }

            _activePets[rosterIndex] = netObj;
            _activeCount.Value = _activePets.Count;
            RecalculateSlots();

            OnPetSummoned?.Invoke(slot.Definition);
            NotifyPetSummonedClientRpc(slot.Definition.PetTypeName);
            return true;
        }

        public void DismissPet(int rosterIndex)
        {
            if (!IsServer) return;
            if (!_activePets.TryGetValue(rosterIndex, out var petObj)) return;

            _activePets.Remove(rosterIndex);
            _activeCount.Value = _activePets.Count;

            string petName = rosterIndex < _roster.Count
                ? _roster[rosterIndex].Definition.PetTypeName
                : "Pet";

            if (petObj != null && petObj.IsSpawned)
                petObj.Despawn(destroy: true);

            if (rosterIndex < _roster.Count)
                OnPetDismissed?.Invoke(_roster[rosterIndex].Definition);

            NotifyPetDismissedClientRpc(petName);
        }

        public void DismissAll()
        {
            if (!IsServer) return;
            var indices = new List<int>(_activePets.Keys);
            foreach (int idx in indices)
                DismissPet(idx);
        }

        // ── Pet death (called by PetAI) ────────────────────────────────────

        /// <summary>
        /// Called by PetAI when the pet entity dies.
        /// Removes from active, marks dead in roster.
        /// </summary>
        public void NotifyPetDeath(int rosterIndex)
        {
            if (!IsServer) return;

            if (_activePets.TryGetValue(rosterIndex, out var petObj))
            {
                _activePets.Remove(rosterIndex);
                _activeCount.Value = _activePets.Count;

                if (petObj != null && petObj.IsSpawned)
                    petObj.Despawn(destroy: true);
            }

            if (rosterIndex >= 0 && rosterIndex < _roster.Count)
            {
                var slot = _roster[rosterIndex];
                slot.IsAlive = false;
                _roster[rosterIndex] = slot;

                OnPetDied?.Invoke(slot.Definition);
                NotifyPetDiedClientRpc(slot.Definition.PetTypeName);
            }
        }

        /// <summary>
        /// Resurrect a dead pet in the roster (via item, ability, or safe room service).
        /// </summary>
        public void ResurrectPet(int rosterIndex)
        {
            if (!IsServer) return;
            if (rosterIndex < 0 || rosterIndex >= _roster.Count) return;

            var slot = _roster[rosterIndex];
            if (slot.IsAlive) return;
            if (!slot.Definition.CanResurrect) return;

            slot.IsAlive = true;
            _roster[rosterIndex] = slot;
            NotifyPetResurrectedClientRpc(slot.Definition.PetTypeName);
        }

        // ── Internals ──────────────────────────────────────────────────────

        private void HandleOwnerDeath()
        {
            // Owner dies → dismiss all pets (they don't die, just return to roster).
            DismissAll();
        }

        private void RecalculateSlots()
        {
            if (!IsServer) return;
            _maxSlots.Value = CalculateMaxSlots();
        }

        // ── Network notifications ──────────────────────────────────────────

        [ClientRpc] private void NotifyPetAddedClientRpc(string petName) { }
        [ClientRpc] private void NotifyPetSummonedClientRpc(string petName) { }
        [ClientRpc] private void NotifyPetDismissedClientRpc(string petName) { }
        [ClientRpc] private void NotifyPetDiedClientRpc(string petName) { }
        [ClientRpc] private void NotifyPetResurrectedClientRpc(string petName) { }

        // ── Data ───────────────────────────────────────────────────────────

        [Serializable]
        public struct PetSlot
        {
            public PetDefinition Definition;
            public bool IsAlive;
            public int Level;
        }
    }
}
