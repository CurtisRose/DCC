using System;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Entities;

namespace DCC.Core.Effects.Modifiers
{
    /// <summary>
    /// Temporarily modifies one or more crawler stats for the effect's duration.
    /// Used for buff potions, gear enchantments, debuff curses, and environmental effects.
    ///
    /// Example configurations (all via ScriptableObject assets, no code):
    ///
    ///   Strength Potion (Good):
    ///     StatModifiers: [{ Stat: Strength, Amount: 3 }]
    ///     Duration: 300 (5 minutes)
    ///     GrantedTags: [Buffed, StrengthBoosted]
    ///
    ///   Enchanted Gauntlet of the Grull (passive while equipped):
    ///     StatModifiers: [{ Stat: Strength, Amount: 5 }, { Stat: Constitution, Amount: -2 }]
    ///     Duration: -1 (permanent, removed when unequipped)
    ///     GrantedTags: [Enchanted, Cursed]
    ///
    ///   Curse of Weakness (debuff from boss):
    ///     StatModifiers: [{ Stat: Strength, Amount: -4 }, { Stat: Dexterity, Amount: -3 }]
    ///     Duration: 60
    ///     GrantedTags: [Cursed, Weakened]
    ///     BlockedConcurrentTags: [Blessed] (can't be cursed while blessed)
    ///
    ///   Charisma Surge (sponsor gift):
    ///     StatModifiers: [{ Stat: Charisma, Amount: 10 }]
    ///     Duration: 120
    ///     GrantedTags: [Buffed, Radiant, Inspiring]
    ///
    /// Stat modifiers stack additively — two +3 Str effects give +6 total.
    /// All modifiers are cleanly removed when the effect expires.
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Effect/Stat Modifier", fileName = "Effect_StatMod")]
    public class StatModifierEffect : EffectDefinition
    {
        [field: SerializeField, Tooltip(
            "Which stats to modify and by how much. Positive = buff, negative = debuff. " +
            "All modifiers are removed when the effect expires.")]
        public StatModifier[] StatModifiers { get; private set; }

        [field: SerializeField, Tooltip(
            "Optional: also apply a move speed multiplier (like StatusEffect). 1 = no change.")]
        public float MoveSpeedMultiplier { get; private set; } = 1f;

        [field: SerializeField, Tooltip(
            "Optional: bonus armor while this effect is active.")]
        public float BonusArmor { get; private set; } = 0f;

        public override void OnApply(EffectInstance instance, EntityAttributes target)
        {
            if (StatModifiers != null)
            {
                foreach (var mod in StatModifiers)
                    ApplyModifier(target.AttributeSet, mod.Stat, mod.Amount);
            }

            if (!Mathf.Approximately(MoveSpeedMultiplier, 1f))
                target.AttributeSet.AddMoveSpeedMultiplier(MoveSpeedMultiplier);

            if (!Mathf.Approximately(BonusArmor, 0f))
                target.AttributeSet.AddBonusArmor(BonusArmor);
        }

        public override void OnRemove(EffectInstance instance, EntityAttributes target)
        {
            if (StatModifiers != null)
            {
                foreach (var mod in StatModifiers)
                    RemoveModifier(target.AttributeSet, mod.Stat, mod.Amount);
            }

            if (!Mathf.Approximately(MoveSpeedMultiplier, 1f))
                target.AttributeSet.RemoveMoveSpeedMultiplier(MoveSpeedMultiplier);

            if (!Mathf.Approximately(BonusArmor, 0f))
                target.AttributeSet.RemoveBonusArmor(BonusArmor);
        }

        private static void ApplyModifier(AttributeSet attrs, CrawlerStat stat, int amount)
        {
            switch (stat)
            {
                case CrawlerStat.Strength:     attrs.AddBonusStrength(amount); break;
                case CrawlerStat.Constitution:  attrs.AddBonusConstitution(amount); break;
                case CrawlerStat.Dexterity:     attrs.AddBonusDexterity(amount); break;
                case CrawlerStat.Intelligence:  attrs.AddBonusIntelligence(amount); break;
                case CrawlerStat.Charisma:      attrs.AddBonusCharisma(amount); break;
            }
        }

        private static void RemoveModifier(AttributeSet attrs, CrawlerStat stat, int amount)
        {
            switch (stat)
            {
                case CrawlerStat.Strength:     attrs.RemoveBonusStrength(amount); break;
                case CrawlerStat.Constitution:  attrs.RemoveBonusConstitution(amount); break;
                case CrawlerStat.Dexterity:     attrs.RemoveBonusDexterity(amount); break;
                case CrawlerStat.Intelligence:  attrs.RemoveBonusIntelligence(amount); break;
                case CrawlerStat.Charisma:      attrs.RemoveBonusCharisma(amount); break;
            }
        }
    }

    [Serializable]
    public struct StatModifier
    {
        public CrawlerStat Stat;

        [Tooltip("Positive = buff, negative = debuff. Stacks additively with other modifiers.")]
        public int Amount;

        public StatModifier(CrawlerStat stat, int amount)
        {
            Stat = stat;
            Amount = amount;
        }
    }
}
