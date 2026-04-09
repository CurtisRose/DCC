using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using DCC.Core.Entities;

namespace DCC.Multiplayer
{
    /// <summary>
    /// Handles input-to-server communication for the local player.
    ///
    /// Architecture:
    ///   - Movement: client-authoritative (ClientNetworkTransform) for responsiveness.
    ///     Server validates position bounds; cheating clients get corrected.
    ///   - Ability use: client sends ServerRpc, server resolves and applies effects.
    ///     No client-side ability prediction — server is authoritative for all gameplay.
    ///   - Item use: same pattern as ability use.
    ///
    /// Input system: uses Unity's legacy Input for clarity. Replace with
    ///   UnityEngine.InputSystem for shipped builds.
    /// </summary>
    [RequireComponent(typeof(EntityAttributes))]
    [RequireComponent(typeof(ClientNetworkTransform))]
    public class PlayerNetworkController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private Camera _camera;

        [Header("Ability Slots")]
        [SerializeField, Tooltip("Max number of equipped ability slots.")]
        private int _abilitySlotCount = 4;

        private EntityAttributes _attributes;
        private Rigidbody _rb;
        private Items.Inventory _inventory;
        private Items.EquipmentManager _equipment;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            _attributes = GetComponent<EntityAttributes>();
            _rb = GetComponent<Rigidbody>();
            _inventory = GetComponent<Items.Inventory>();
            _equipment = GetComponent<Items.EquipmentManager>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner)
            {
                // Disable input components on non-owned instances.
                enabled = false;
                return;
            }

            if (_camera == null)
                _camera = Camera.main;
        }

        // ── Per-frame input ────────────────────────────────────────────────

        private void Update()
        {
            if (!IsOwner || !IsSpawned) return;
            if (!_attributes.IsAlive) return;

            HandleMovementInput();
            HandleAbilityInput();
            HandleItemInput();
            HandleAllianceInput();
        }

        private void HandleMovementInput()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 dir = new Vector3(h, 0, v).normalized;

            // Client-side movement — ClientNetworkTransform syncs position to server.
            if (_rb != null)
                _rb.MovePosition(transform.position + dir * _moveSpeed * Time.deltaTime);
        }

        private void HandleAbilityInput()
        {
            // Slots 1–4 mapped to keys Q, E, R, F.
            KeyCode[] abilityKeys = { KeyCode.Q, KeyCode.E, KeyCode.R, KeyCode.F };
            for (int i = 0; i < Mathf.Min(abilityKeys.Length, _abilitySlotCount); i++)
            {
                if (Input.GetKeyDown(abilityKeys[i]))
                {
                    Vector3 target = GetMouseWorldPosition();
                    UseAbilityServerRpc(i, target);
                }
            }
        }

        private void HandleItemInput()
        {
            // Item slots 1–4 mapped to keys 1–4.
            for (int i = 0; i < 4; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    Vector3 target = GetMouseWorldPosition();
                    UseItemServerRpc(i, target);
                }
            }
        }

        private void HandleAllianceInput()
        {
            // Click on another player + hold Alt to request an alliance.
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButtonDown(0))
            {
                var ray = _camera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 100f))
                {
                    var controller = hit.collider.GetComponentInParent<PlayerNetworkController>();
                    if (controller != null && controller != this)
                    {
                        var matrix = Core.Allegiance.AllegianceMatrix.Instance;
                        if (matrix != null)
                            matrix.RequestAllianceServerRpc(controller.OwnerClientId);
                    }
                }
            }
        }

        // ── Server RPCs ────────────────────────────────────────────────────

        [ServerRpc]
        private void UseAbilityServerRpc(int slot, Vector3 targetPos, ServerRpcParams rpc = default)
        {
            // Validate: is the ability off cooldown? Does the player have it?
            var caster = GetComponent<Abilities.AbilityCaster>();
            caster?.CastAbility(slot, targetPos, rpc.Receive.SenderClientId);
        }

        [ServerRpc]
        private void UseItemServerRpc(int slot, Vector3 targetPos, ServerRpcParams rpc = default)
        {
            _inventory?.UseItem(slot, targetPos, rpc.Receive.SenderClientId);
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private Vector3 GetMouseWorldPosition()
        {
            if (_camera == null) return transform.position;
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out var hit, 100f)
                ? hit.point
                : transform.position + transform.forward * 5f;
        }
    }
}
