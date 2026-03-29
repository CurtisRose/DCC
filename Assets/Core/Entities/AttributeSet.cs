using System;
using System.Collections.Generic;
using UnityEngine;
using DCC.Core.Effects;

namespace DCC.Core.Entities
{
    /// <summary>
    /// All numeric stats for an entity: health, armor, speed, etc.
    /// Held by EntityAttributes. Modified by effects, items, and level-up.
    ///
    /// Armor reduces damage multiplicatively: damage_taken = damage * (1 - armor_ratio).
    /// armor_ratio = Armor / (Armor + ArmorConstant), inspired by Dota/LoL models.
    /// </summary>
    [Serializable]
    public class AttributeSet
    {
        // ── Constants ──────────────────────────────────────────────────────
        private const float ArmorConstant = 100f; // tuning knob

        // ── Persistent stats (serialized for save/load) ────────────────────
        [field: SerializeField] public float MaxHealth { get; set; } = 100f;
        [field: SerializeField] public float BaseArmor { get; set; } = 0f;
        [field: SerializeField] public float BaseMoveSpeed { get; set; } = 5f;

        // ── Runtime state ──────────────────────────────────────────────────
        public float CurrentHealth { get; private set; }

        private float _bonusArmor;
        private readonly List<float> _moveSpeedMultipliers = new();

        // ── Events ─────────────────────────────────────────────────────────
        public event Action<float, float, EffectContext> OnDamageTaken;  // (raw, final, ctx)
        public event Action<float> OnHealReceived;
        public event Action OnDeath;

        // ── Initialise ─────────────────────────────────────────────────────
        public void Initialize()
        {
            CurrentHealth = MaxHealth;
        }

        // ── Derived stats ──────────────────────────────────────────────────
        public float TotalArmor => BaseArmor + _bonusArmor;
        public float ArmorDamageReduction => TotalArmor / (TotalArmor + ArmorConstant);

        public float MoveSpeed
        {
            get
            {
                float speed = BaseMoveSpeed;
                foreach (var m in _moveSpeedMultipliers) speed *= m;
                return Mathf.Max(0f, speed);
            }
        }

        public bool IsAlive => CurrentHealth > 0f;

        // ── Modification ───────────────────────────────────────────────────
        public void ApplyDamage(float rawDamage, EffectContext context)
        {
            float final = rawDamage; // armor already applied upstream in DamageEffect
            CurrentHealth = Mathf.Max(0f, CurrentHealth - final);
            OnDamageTaken?.Invoke(rawDamage, final, context);
            if (CurrentHealth <= 0f) OnDeath?.Invoke();
        }

        public void ApplyHeal(float amount, bool allowOverheal = false)
        {
            float prev = CurrentHealth;
            CurrentHealth = allowOverheal
                ? CurrentHealth + amount
                : Mathf.Min(MaxHealth, CurrentHealth + amount);
            float actual = CurrentHealth - prev;
            if (actual > 0f) OnHealReceived?.Invoke(actual);
        }

        public float CalculateDamageAfterArmor(float rawDamage)
        {
            return rawDamage * (1f - ArmorDamageReduction);
        }

        public void AddBonusArmor(float amount) => _bonusArmor += amount;
        public void RemoveBonusArmor(float amount) => _bonusArmor -= amount;

        public void AddMoveSpeedMultiplier(float multiplier) => _moveSpeedMultipliers.Add(multiplier);
        public void RemoveMoveSpeedMultiplier(float multiplier) => _moveSpeedMultipliers.Remove(multiplier);
    }
}
