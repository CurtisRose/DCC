using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;

namespace DCC.Abilities
{
    /// <summary>
    /// Tracks all skills known by an entity and their current levels.
    /// Server-authoritative: skill leveling only happens on the server.
    ///
    /// In the DCC books:
    ///   - Skills level through USE, not through spending points.
    ///   - Normal max level is 15. The Primal race allows training to 20.
    ///   - Skills can be granted by gear (removed when unequipped), potions,
    ///     class selection, guildhalls, or dungeon events.
    ///
    /// Usage:
    ///   1. Call GrantSkill() to give an entity a new skill at level 1.
    ///   2. Call RecordSkillUse() each time the entity uses the skill.
    ///   3. Skill XP accumulates and auto-levels when threshold is reached.
    ///   4. AbilityCaster checks GetSkillLevel() to gate abilities by skill requirements.
    ///   5. Effect magnitude can scale with skill level via GetSkillEffectiveness().
    /// </summary>
    [RequireComponent(typeof(TagContainer))]
    public class SkillTracker : NetworkBehaviour
    {
        [SerializeField, Tooltip("If true, this entity can train skills to level 20 (Primal race).")]
        private bool _isPrimal;

        public bool IsPrimal => _isPrimal;

        /// <summary>Set by CrawlerIdentity when a Primal race is selected.</summary>
        public void SetPrimal(bool value) => _isPrimal = value;

        private TagContainer _tags;
        private readonly Dictionary<SkillDefinition, SkillState> _skills = new();

        // Events for UI notifications.
        public event Action<SkillDefinition, int> OnSkillLevelUp;   // (skill, newLevel)
        public event Action<SkillDefinition> OnSkillGranted;

        private void Awake()
        {
            _tags = GetComponent<TagContainer>();
        }

        // ── Queries ────────────────────────────────────────────────────────

        public bool HasSkill(SkillDefinition skill) => _skills.ContainsKey(skill);

        public int GetSkillLevel(SkillDefinition skill)
            => _skills.TryGetValue(skill, out var state) ? state.Level : 0;

        /// <summary>
        /// Effectiveness multiplier for an ability/effect that scales with this skill.
        /// Level 1 = 1.0, each additional level adds 0.1 (level 15 = 2.4).
        /// </summary>
        public float GetSkillEffectiveness(SkillDefinition skill)
        {
            int level = GetSkillLevel(skill);
            return level <= 0 ? 0f : 1f + (level - 1) * 0.1f;
        }

        // ── Mutation (server only) ─────────────────────────────────────────

        /// <summary>Grant a new skill at level 1. No-op if already known.</summary>
        public void GrantSkill(SkillDefinition skill)
        {
            if (!IsServer || skill == null) return;
            if (_skills.ContainsKey(skill)) return;

            _skills[skill] = new SkillState { Level = 1, Xp = 0 };
            ApplySkillTags(skill, 1);
            OnSkillGranted?.Invoke(skill);
            NotifySkillGrantedClientRpc(skill.DisplayName, 1);
        }

        /// <summary>Record one use of a skill. Accumulates XP and levels up if threshold met.</summary>
        public void RecordSkillUse(SkillDefinition skill)
        {
            if (!IsServer || skill == null) return;
            if (!_skills.TryGetValue(skill, out var state)) return;

            int maxLevel = _isPrimal ? 20 : skill.MaxLevel;
            if (state.Level >= maxLevel) return;

            state.Xp += skill.XpPerUse;
            int required = skill.XpRequiredForLevel(state.Level);

            while (state.Xp >= required && state.Level < maxLevel)
            {
                state.Xp -= required;
                state.Level++;
                ApplyMilestoneTags(skill, state.Level);
                OnSkillLevelUp?.Invoke(skill, state.Level);
                NotifySkillLevelUpClientRpc(skill.DisplayName, state.Level);
                required = skill.XpRequiredForLevel(state.Level);
            }

            _skills[skill] = state;
        }

        /// <summary>Remove a skill entirely (e.g., unequipping gear that granted it).</summary>
        public void RemoveSkill(SkillDefinition skill)
        {
            if (!IsServer || skill == null) return;
            if (!_skills.TryGetValue(skill, out var state)) return;

            RemoveAllSkillTags(skill, state.Level);
            _skills.Remove(skill);
        }

        // ── Tag management ─────────────────────────────────────────────────

        private void ApplySkillTags(SkillDefinition skill, int level)
        {
            if (skill.GrantedWhileKnown != null)
                _tags.AddTags(skill.GrantedWhileKnown);

            ApplyMilestoneTags(skill, level);
        }

        private void ApplyMilestoneTags(SkillDefinition skill, int currentLevel)
        {
            if (skill.Milestones == null) return;
            foreach (var milestone in skill.Milestones)
            {
                if (milestone.RequiredLevel == currentLevel && milestone.GrantedTags != null)
                    _tags.AddTags(milestone.GrantedTags);
            }
        }

        private void RemoveAllSkillTags(SkillDefinition skill, int level)
        {
            if (skill.GrantedWhileKnown != null)
                _tags.RemoveTags(skill.GrantedWhileKnown);

            if (skill.Milestones != null)
            {
                foreach (var milestone in skill.Milestones)
                {
                    if (milestone.RequiredLevel <= level && milestone.GrantedTags != null)
                        _tags.RemoveTags(milestone.GrantedTags);
                }
            }
        }

        // ── Network notifications ──────────────────────────────────────────

        [ClientRpc]
        private void NotifySkillGrantedClientRpc(string skillName, int level)
        {
            // Client plays "New Skill" notification, updates skill UI.
        }

        [ClientRpc]
        private void NotifySkillLevelUpClientRpc(string skillName, int newLevel)
        {
            // Client plays level-up sound, shows "Skill X is now level Y" toast.
        }

        // ── Internal state ─────────────────────────────────────────────────

        private struct SkillState
        {
            public int Level;
            public int Xp;
        }
    }
}
