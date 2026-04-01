using Unity.Netcode;
using UnityEngine;
using DCC.Core.Effects;
using DCC.Core.Entities;
using DCC.Core.Interactions;

namespace DCC.Items
{
    /// <summary>
    /// Tracks potion cooldown per-entity. Faithful to the DCC books:
    ///
    ///   After consuming any potion, a cooldown timer starts.
    ///   Drinking another potion before the cooldown expires inflicts the Poisoned debuff.
    ///   Constitution determines cooldown length (higher Con = shorter cooldown).
    ///
    /// Potion tiers (Good, Great, Superb, Cosmic) don't affect cooldown — any potion
    /// of any tier triggers the cooldown and any potion consumed during cooldown poisons.
    ///
    /// This component is attached alongside Inventory on player entities.
    /// Inventory.UseItem() calls NotifyPotionConsumed() when a potion-type item is used.
    /// </summary>
    [RequireComponent(typeof(EntityAttributes))]
    public class PotionCooldownTracker : NetworkBehaviour
    {
        [SerializeField, Tooltip(
            "The Poisoned debuff effect applied when drinking during cooldown. " +
            "Configure as a StatusEffect + DamageEffect combo with [Poisoned] tag.")]
        private EffectDefinition _poisonedEffect;

        private EntityAttributes _owner;
        private float _cooldownRemaining;

        public bool IsOnCooldown => _cooldownRemaining > 0f;
        public float CooldownRemaining => _cooldownRemaining;

        private void Awake()
        {
            _owner = GetComponent<EntityAttributes>();
        }

        private void Update()
        {
            if (!IsServer) return;
            if (_cooldownRemaining > 0f)
                _cooldownRemaining -= Time.deltaTime;
        }

        /// <summary>
        /// Called by Inventory when a potion-type item is consumed.
        /// If on cooldown, applies Poisoned. Then starts a new cooldown.
        /// </summary>
        public void NotifyPotionConsumed()
        {
            if (!IsServer) return;

            if (IsOnCooldown && _poisonedEffect != null)
            {
                var ctx = EffectContext.Environment(transform.position);

                if (InteractionEngine.Instance != null)
                    InteractionEngine.Instance.Resolve(
                        new[] { _poisonedEffect }, _owner, ctx);
                else
                    EffectComposer.ApplySingle(_poisonedEffect, _owner, ctx);

                NotifyPoisonedClientRpc();
            }

            // Start new cooldown based on Constitution.
            _cooldownRemaining = _owner.AttributeSet.PotionCooldownDuration;
        }

        [ClientRpc]
        private void NotifyPoisonedClientRpc()
        {
            // Client shows "You drank a potion too soon! You are Poisoned." notification.
        }
    }
}
