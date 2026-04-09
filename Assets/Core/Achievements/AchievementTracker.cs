using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects;
using DCC.Core.Entities;
using DCC.Items;
using DCC.Abilities;
using DCC.Core.Economy;

namespace DCC.Core.Achievements
{
    /// <summary>
    /// Per-player achievement tracking. Server-authoritative.
    /// Checks achievement conditions and awards rewards when triggered.
    ///
    /// AchievementTracker doesn't poll — it is called by SystemAI when
    /// relevant events occur (kill, floor change, stat change, tag change, etc.).
    /// </summary>
    [RequireComponent(typeof(EntityAttributes))]
    [RequireComponent(typeof(TagContainer))]
    public class AchievementTracker : NetworkBehaviour
    {
        private EntityAttributes _attrs;
        private TagContainer _tags;
        private SkillTracker _skills;
        private Inventory _inventory;
        private LootBoxInventory _lootBoxInventory;
        private CrawlerIdentity _identity;

        private readonly HashSet<AchievementDefinition> _earned = new();

        // Networked count for client UI.
        private NetworkVariable<int> _achievementCount = new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        public int AchievementCount => _achievementCount.Value;

        public event Action<AchievementDefinition> OnAchievementEarned;

        private void Awake()
        {
            _attrs = GetComponent<EntityAttributes>();
            _tags = GetComponent<TagContainer>();
            _skills = GetComponent<SkillTracker>();
            _inventory = GetComponent<Inventory>();
            _lootBoxInventory = GetComponent<LootBoxInventory>();
            _identity = GetComponent<CrawlerIdentity>();
        }

        // ── Queries ────────────────────────────────────────────────────────

        public bool HasEarned(AchievementDefinition achievement) => _earned.Contains(achievement);

        // ── Evaluation (called by SystemAI) ────────────────────────────────

        /// <summary>
        /// Check a specific achievement against this crawler's current state.
        /// Awards it if conditions are met and not already earned.
        /// </summary>
        public bool TryAward(AchievementDefinition achievement)
        {
            if (!IsServer || achievement == null) return false;
            if (_earned.Contains(achievement)) return false;

            if (!EvaluateCondition(achievement)) return false;

            Award(achievement);
            return true;
        }

        /// <summary>
        /// Check all provided achievements. Used by SystemAI on event triggers.
        /// </summary>
        public void EvaluateAll(AchievementDefinition[] achievements)
        {
            if (!IsServer || achievements == null) return;
            foreach (var achievement in achievements)
                TryAward(achievement);
        }

        // ── Condition evaluation ───────────────────────────────────────────

        private bool EvaluateCondition(AchievementDefinition achievement)
        {
            switch (achievement.Trigger)
            {
                case AchievementTrigger.TagCombination:
                case AchievementTrigger.FirstTagCombination:
                    return CheckTagCombination(achievement);

                case AchievementTrigger.FirstKill:
                    // Always passes when called from a kill event context.
                    return true;

                case AchievementTrigger.StatMilestone:
                    return CheckStatMilestone(achievement);

                case AchievementTrigger.FloorReached:
                    return CheckFloorReached(achievement);

                case AchievementTrigger.SkillMaxed:
                    return CheckSkillMaxed(achievement);

                case AchievementTrigger.FirstPvpKill:
                    return true; // Passes when called from PVP kill context.

                case AchievementTrigger.Custom:
                    return true; // Custom triggers are pre-validated by caller.

                default:
                    return false;
            }
        }

        private bool CheckTagCombination(AchievementDefinition achievement)
        {
            if (achievement.RequiredTags == null || achievement.RequiredTags.Length == 0)
                return false;

            foreach (var tag in achievement.RequiredTags)
                if (tag != null && !_tags.HasTag(tag)) return false;

            return true;
        }

        private bool CheckStatMilestone(AchievementDefinition achievement)
        {
            int value = achievement.MilestoneStat switch
            {
                CrawlerStat.Strength => _attrs.AttributeSet.Strength,
                CrawlerStat.Constitution => _attrs.AttributeSet.Constitution,
                CrawlerStat.Dexterity => _attrs.AttributeSet.Dexterity,
                CrawlerStat.Intelligence => _attrs.AttributeSet.Intelligence,
                CrawlerStat.Charisma => _attrs.AttributeSet.Charisma,
                _ => 0
            };
            return value >= achievement.MilestoneValue;
        }

        private bool CheckFloorReached(AchievementDefinition achievement)
        {
            var floorManager = Zones.FloorManager.Instance;
            return floorManager != null && floorManager.CurrentFloorNumber >= achievement.RequiredFloor;
        }

        private bool CheckSkillMaxed(AchievementDefinition achievement)
        {
            if (achievement.RequiredSkill == null || _skills == null) return false;
            int level = _skills.GetSkillLevel(achievement.RequiredSkill);
            int max = _skills.IsPrimal ? 20 : achievement.RequiredSkill.MaxLevel;
            return level >= max;
        }

        // ── Award ──────────────────────────────────────────────────────────

        private void Award(AchievementDefinition achievement)
        {
            _earned.Add(achievement);
            _achievementCount.Value = _earned.Count;

            // Loot box reward.
            if (achievement.RewardLootBox != null && _lootBoxInventory != null)
                _lootBoxInventory.AddBox(achievement.RewardLootBox);

            // Benefit reward.
            if (achievement.RewardBenefit != null && _identity != null)
                _identity.GrantBenefit(achievement.RewardBenefit);

            // Item rewards.
            if (achievement.RewardItems != null && _inventory != null)
                foreach (var item in achievement.RewardItems)
                    if (item != null) _inventory.AddItem(item);

            // Effect rewards (instant buffs).
            if (achievement.RewardEffects != null)
            {
                var ctx = Effects.EffectContext.Environment(transform.position);
                foreach (var effectDef in achievement.RewardEffects)
                {
                    if (effectDef == null) continue;
                    var instance = Effects.EffectInstance.Create(effectDef, ctx, effectDef.BaseMagnitude);
                    _attrs.ApplyEffect(instance);
                }
            }

            // Stat point reward.
            if (achievement.StatPointReward > 0)
                _attrs.AttributeSet.UnspentStatPoints += achievement.StatPointReward;

            // Gold reward.
            if (achievement.GoldReward > 0)
            {
                var gold = GetComponent<GoldManager>();
                if (gold != null) gold.AddGold(achievement.GoldReward);
            }

            OnAchievementEarned?.Invoke(achievement);
            NotifyAchievementClientRpc(achievement.DisplayName, achievement.Description);
        }

        [ClientRpc]
        private void NotifyAchievementClientRpc(string name, string description)
        {
            // Client shows achievement popup with snarky System AI description.
        }
    }
}
