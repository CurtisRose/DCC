using System;
using System.Collections.Generic;
using UnityEngine;
using DCC.Core.Effects;

namespace DCC.Core.Entities
{
    /// <summary>
    /// All numeric stats for an entity: crawler stats, health, mana, armor, speed, etc.
    /// Held by EntityAttributes. Modified by effects, items, and level-up.
    ///
    /// Crawler Stats (faithful to the DCC books):
    ///   Strength     — melee damage, carrying, climbing, athletics
    ///   Constitution — max health, health regen, potion cooldown reduction
    ///   Dexterity    — move speed bonus, dodge, stealth, crafting
    ///   Intelligence — max mana (MP = Int), mana regen rate, spell power
    ///   Charisma     — NPC manipulation, pet slots, bard magic, sponsor appeal
    ///
    /// Stat scale: 0 = unconscious, 3 = low average, 4 = average, 6 = above average,
    ///             9–10 = peak human. Crawlers gain 3 stat points per level.
    ///             At stat 100, crawler gets an achievement and picks a milestone perk.
    ///
    /// Armor reduces damage multiplicatively: damage_taken = damage * (1 - armor_ratio).
    /// armor_ratio = Armor / (Armor + ArmorConstant), inspired by Dota/LoL models.
    /// </summary>
    [Serializable]
    public class AttributeSet
    {
        // ── Constants ──────────────────────────────────────────────────────
        private const float ArmorConstant = 100f;

        // Health per point of Constitution.
        private const float HealthPerConstitution = 25f;
        // Base health before Con bonus (a crawler with 0 Con is unconscious, but
        // this ensures the formula doesn't start at zero for very low Con values).
        private const float BaseHealthFloor = 50f;

        // ── Crawler Stats (the 5 core stats from the books) ────────────────
        //
        // Base values are the permanent, level-up-allocated scores.
        // Bonus values come from gear, buffs, potions — removed when the source expires.
        // Effective = Base + Bonus (clamped to 0 minimum).

        [field: SerializeField] public int BaseStrength { get; set; } = 4;
        [field: SerializeField] public int BaseConstitution { get; set; } = 4;
        [field: SerializeField] public int BaseDexterity { get; set; } = 4;
        [field: SerializeField] public int BaseIntelligence { get; set; } = 4;
        [field: SerializeField] public int BaseCharisma { get; set; } = 4;

        private int _bonusStrength;
        private int _bonusConstitution;
        private int _bonusDexterity;
        private int _bonusIntelligence;
        private int _bonusCharisma;

        public int Strength => Mathf.Max(0, BaseStrength + _bonusStrength);
        public int Constitution => Mathf.Max(0, BaseConstitution + _bonusConstitution);
        public int Dexterity => Mathf.Max(0, BaseDexterity + _bonusDexterity);
        public int Intelligence => Mathf.Max(0, BaseIntelligence + _bonusIntelligence);
        public int Charisma => Mathf.Max(0, BaseCharisma + _bonusCharisma);

        // ── Stat modifiers (called by StatModifierEffect) ──────────────────

        public void AddBonusStrength(int amount) => _bonusStrength += amount;
        public void RemoveBonusStrength(int amount) => _bonusStrength -= amount;
        public void AddBonusConstitution(int amount) { _bonusConstitution += amount; RecalculateDerivedStats(); }
        public void RemoveBonusConstitution(int amount) { _bonusConstitution -= amount; RecalculateDerivedStats(); }
        public void AddBonusDexterity(int amount) => _bonusDexterity += amount;
        public void RemoveBonusDexterity(int amount) => _bonusDexterity -= amount;
        public void AddBonusIntelligence(int amount) { _bonusIntelligence += amount; RecalculateDerivedStats(); }
        public void RemoveBonusIntelligence(int amount) { _bonusIntelligence -= amount; RecalculateDerivedStats(); }
        public void AddBonusCharisma(int amount) => _bonusCharisma += amount;
        public void RemoveBonusCharisma(int amount) => _bonusCharisma -= amount;

        // ── Persistent stats (serialized for save/load) ────────────────────
        [field: SerializeField] public float BaseArmor { get; set; } = 0f;
        [field: SerializeField] public float BaseMoveSpeed { get; set; } = 5f;

        // ── Leveling ───────────────────────────────────────────────────────
        [field: SerializeField] public int Level { get; set; } = 1;
        [field: SerializeField] public int UnspentStatPoints { get; set; } = 0;

        // ── Runtime state ──────────────────────────────────────────────────
        public float CurrentHealth { get; private set; }
        public float CurrentMana { get; private set; }

        private float _bonusArmor;
        private readonly List<float> _moveSpeedMultipliers = new();

        // ── Events ─────────────────────────────────────────────────────────
        public event Action<float, float, EffectContext> OnDamageTaken;  // (raw, final, ctx)
        public event Action<float> OnHealReceived;
        public event Action<float> OnManaSpent;
        public event Action<float> OnManaRestored;
        public event Action OnDeath;

        // ── Derived stats ──────────────────────────────────────────────────

        /// <summary>Max HP = floor + Constitution * HealthPerConstitution.</summary>
        public float MaxHealth => BaseHealthFloor + Constitution * HealthPerConstitution;

        /// <summary>Max MP = Intelligence (1 point of Int = 1 mana, faithful to books).</summary>
        public float MaxMana => Mathf.Max(0f, Intelligence);

        public float TotalArmor => BaseArmor + _bonusArmor;
        public float ArmorDamageReduction => TotalArmor / (TotalArmor + ArmorConstant);

        /// <summary>
        /// Mana regen per second. Faithful to books:
        ///   Int 3  → ~1 MP/hour  (0.000278/s)
        ///   Int 17 → ~1 MP/min   (0.0167/s)
        /// We use an exponential curve: regen = 0.0002 * 1.25^Int MP/s.
        /// This gives roughly correct book values and scales naturally.
        /// </summary>
        public float ManaRegenPerSecond => Intelligence <= 0 ? 0f : 0.0002f * Mathf.Pow(1.25f, Intelligence);

        /// <summary>
        /// Health regen per second. Constitution drives recovery speed.
        /// Light passive regen: Con * 0.1 HP/s (Con 4 = 0.4 HP/s, Con 20 = 2 HP/s).
        /// </summary>
        public float HealthRegenPerSecond => Constitution <= 0 ? 0f : Constitution * 0.1f;

        /// <summary>
        /// Potion cooldown in seconds. Higher Con = shorter cooldown.
        /// Base 30s, reduced by 0.5s per point of Con. Minimum 5s.
        /// Faithful to the books: drinking another potion before cooldown expires → Poisoned.
        /// </summary>
        public float PotionCooldownDuration => Mathf.Max(5f, 30f - Constitution * 0.5f);

        /// <summary>
        /// Melee damage multiplier from Strength.
        /// Each point above 4 (average) adds 10% damage. Below 4 reduces.
        /// </summary>
        public float MeleeDamageMultiplier => 1f + (Strength - 4) * 0.1f;

        /// <summary>
        /// Spell power multiplier from Intelligence.
        /// Each point above 4 adds 8% spell effect magnitude.
        /// </summary>
        public float SpellPowerMultiplier => 1f + (Intelligence - 4) * 0.08f;

        /// <summary>
        /// Dexterity-based move speed bonus. Each point above 4 adds 3% speed.
        /// </summary>
        public float MoveSpeed
        {
            get
            {
                float dexBonus = 1f + (Dexterity - 4) * 0.03f;
                float speed = BaseMoveSpeed * dexBonus;
                foreach (var m in _moveSpeedMultipliers) speed *= m;
                return Mathf.Max(0f, speed);
            }
        }

        public bool IsAlive => CurrentHealth > 0f;

        // ── Initialise ─────────────────────────────────────────────────────

        public void Initialize()
        {
            CurrentHealth = MaxHealth;
            CurrentMana = MaxMana;
        }

        /// <summary>
        /// Recalculate derived stats when Constitution or Intelligence change at runtime
        /// (from buffs/debuffs). Clamps current values to new maximums.
        /// </summary>
        private void RecalculateDerivedStats()
        {
            if (CurrentHealth > MaxHealth)
                CurrentHealth = MaxHealth;
            if (CurrentMana > MaxMana)
                CurrentMana = MaxMana;
        }

        // ── Stat point allocation (safe room only in DCC) ──────────────────

        /// <summary>
        /// Spend one stat point to increase a base stat. Returns false if no points available.
        /// In the books, stat points can only be allocated inside a safe room.
        /// </summary>
        public bool AllocateStatPoint(CrawlerStat stat)
        {
            if (UnspentStatPoints <= 0) return false;

            switch (stat)
            {
                case CrawlerStat.Strength:     BaseStrength++; break;
                case CrawlerStat.Constitution:  BaseConstitution++; RecalculateDerivedStats(); break;
                case CrawlerStat.Dexterity:     BaseDexterity++; break;
                case CrawlerStat.Intelligence:  BaseIntelligence++; RecalculateDerivedStats(); break;
                case CrawlerStat.Charisma:      BaseCharisma++; break;
                default: return false;
            }

            UnspentStatPoints--;
            return true;
        }

        /// <summary>
        /// Called when the crawler gains a level. Grants 3 stat points per level (faithful to books).
        /// </summary>
        public void GainLevel()
        {
            Level++;
            UnspentStatPoints += 3;
        }

        // ── Mana ───────────────────────────────────────────────────────────

        /// <summary>Returns true if the entity has enough mana. Does not spend it.</summary>
        public bool HasMana(float amount) => CurrentMana >= amount;

        /// <summary>Spend mana. Returns false if insufficient.</summary>
        public bool SpendMana(float amount)
        {
            if (CurrentMana < amount) return false;
            CurrentMana -= amount;
            OnManaSpent?.Invoke(amount);
            return true;
        }

        /// <summary>Restore mana up to MaxMana.</summary>
        public void RestoreMana(float amount)
        {
            float prev = CurrentMana;
            CurrentMana = Mathf.Min(MaxMana, CurrentMana + amount);
            float actual = CurrentMana - prev;
            if (actual > 0f) OnManaRestored?.Invoke(actual);
        }

        // ── Modification ───────────────────────────────────────────────────

        public void ApplyDamage(float rawDamage, EffectContext context)
        {
            float final_ = rawDamage; // armor already applied upstream in DamageEffect
            CurrentHealth = Mathf.Max(0f, CurrentHealth - final_);
            OnDamageTaken?.Invoke(rawDamage, final_, context);
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

    /// <summary>The five core crawler stats from the DCC books.</summary>
    public enum CrawlerStat
    {
        Strength,
        Constitution,
        Dexterity,
        Intelligence,
        Charisma
    }
}
