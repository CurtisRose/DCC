using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects;
using DCC.Core.Effects.Modifiers;
using DCC.Abilities;

namespace DCC.Core.Entities
{
    /// <summary>
    /// Tracks a crawler's Race, Class, Subclass, and active Benefits.
    /// Server-authoritative. Applies all identity-driven bonuses on initialization.
    ///
    /// Lifecycle:
    ///   1. Player spawns with CrawlerIdentity component.
    ///   2. Server calls Initialize(race, class) after spawn (race/class chosen in lobby).
    ///   3. CrawlerIdentity applies race stat bonuses, grants skills, tags, benefits.
    ///   4. On Floor 6, server calls ChooseSubclass(sub) to specialize.
    ///
    /// CrawlerIdentity does NOT own stats or effects — it delegates to:
    ///   - AttributeSet (stats, stat points)
    ///   - TagContainer (identity tags)
    ///   - SkillTracker (granted skills, Primal flag)
    ///   - EntityAttributes (passive effects from benefits)
    ///   - AbilityCaster (granted abilities) — equipped into ability slots
    /// </summary>
    [RequireComponent(typeof(EntityAttributes))]
    [RequireComponent(typeof(TagContainer))]
    public class CrawlerIdentity : NetworkBehaviour
    {
        // Networked for client UI display.
        private NetworkVariable<FixedString64Bytes> _networkRaceName = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        private NetworkVariable<FixedString64Bytes> _networkClassName = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        private NetworkVariable<FixedString64Bytes> _networkSubclassName = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        public string RaceName => _networkRaceName.Value.ToString();
        public string ClassName => _networkClassName.Value.ToString();
        public string SubclassName => _networkSubclassName.Value.ToString();

        // Server-only references.
        private RaceDefinition _race;
        private ClassDefinition _class;
        private SubclassDefinition _subclass;
        private readonly List<BenefitDefinition> _activeBenefits = new();
        private readonly List<EffectInstance> _benefitEffects = new();

        // Sibling components.
        private EntityAttributes _attrs;
        private TagContainer _tags;
        private SkillTracker _skills;
        private AbilityCaster _abilityCaster;

        public RaceDefinition Race => _race;
        public ClassDefinition Class => _class;
        public SubclassDefinition Subclass => _subclass;
        public IReadOnlyList<BenefitDefinition> ActiveBenefits => _activeBenefits;

        private void Awake()
        {
            _attrs = GetComponent<EntityAttributes>();
            _tags = GetComponent<TagContainer>();
            _skills = GetComponent<SkillTracker>();
            _abilityCaster = GetComponent<AbilityCaster>();
        }

        // ── Initialization (server only) ───────────────────────────────────

        /// <summary>
        /// Called by NetworkGameManager after spawning the player entity.
        /// Applies all race and class bonuses.
        /// </summary>
        public void Initialize(RaceDefinition race, ClassDefinition cls)
        {
            if (!IsServer) return;
            if (race == null || cls == null) return;

            _race = race;
            _class = cls;

            _networkRaceName.Value = race.DisplayName;
            _networkClassName.Value = cls.DisplayName;

            ApplyRace(race);
            ApplyClass(cls);

            NotifyIdentityChosenClientRpc(race.DisplayName, cls.DisplayName);
        }

        // ── Race application ───────────────────────────────────────────────

        private void ApplyRace(RaceDefinition race)
        {
            // Bonus stat points (Human +10, Primal -5, etc.).
            _attrs.AttributeSet.UnspentStatPoints += race.BonusStatPoints;

            // Permanent stat modifiers.
            if (race.StatBonuses != null)
                foreach (var mod in race.StatBonuses)
                    ApplyStatBonus(mod);

            // Identity tags.
            if (race.GrantedTags != null)
                _tags.AddTags(race.GrantedTags);

            // Granted skills.
            if (race.GrantedSkills != null && _skills != null)
                foreach (var skill in race.GrantedSkills)
                    if (skill != null) _skills.GrantSkill(skill);

            // Primal flag.
            if (race.IsPrimal && _skills != null)
                _skills.SetPrimal(true);

            // Benefits.
            if (race.GrantedBenefits != null)
                foreach (var benefit in race.GrantedBenefits)
                    if (benefit != null) GrantBenefit(benefit);
        }

        // ── Class application ──────────────────────────────────────────────

        private void ApplyClass(ClassDefinition cls)
        {
            // Stat bonuses.
            if (cls.StatBonuses != null)
                foreach (var mod in cls.StatBonuses)
                    ApplyStatBonus(mod);

            // Tags.
            if (cls.GrantedTags != null)
                _tags.AddTags(cls.GrantedTags);

            // Skills.
            if (cls.GrantedSkills != null && _skills != null)
                foreach (var skill in cls.GrantedSkills)
                    if (skill != null) _skills.GrantSkill(skill);

            // Benefits.
            if (cls.GrantedBenefits != null)
                foreach (var benefit in cls.GrantedBenefits)
                    if (benefit != null) GrantBenefit(benefit);

            // Note: GrantedAbilities are equipped into AbilityCaster slots.
            // This requires the AbilityCaster to support dynamic ability assignment,
            // which is a future enhancement. For now, abilities are configured in the editor.
        }

        // ── Subclass (Floor 6) ─────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void ChooseSubclassServerRpc(int subclassIndex, ServerRpcParams rpcParams = default)
        {
            if (_class == null || _class.AvailableSubclasses == null) return;
            if (subclassIndex < 0 || subclassIndex >= _class.AvailableSubclasses.Length) return;
            if (_subclass != null) return; // Already chosen.

            var sub = _class.AvailableSubclasses[subclassIndex];
            if (sub == null || sub.ParentClass != _class) return;

            // Validate additional stat requirements.
            if (sub.AdditionalStatRequirements != null)
                foreach (var req in sub.AdditionalStatRequirements)
                    if (GetEffectiveStat(req.Stat) < req.MinValue) return;

            _subclass = sub;
            _networkSubclassName.Value = sub.DisplayName;

            ApplySubclass(sub);
            NotifySubclassChosenClientRpc(sub.DisplayName);
        }

        private void ApplySubclass(SubclassDefinition sub)
        {
            if (sub.StatBonuses != null)
                foreach (var mod in sub.StatBonuses)
                    ApplyStatBonus(mod);

            if (sub.AdditionalTags != null)
                _tags.AddTags(sub.AdditionalTags);

            if (sub.AdditionalSkills != null && _skills != null)
                foreach (var skill in sub.AdditionalSkills)
                    if (skill != null) _skills.GrantSkill(skill);

            if (sub.AdditionalBenefits != null)
                foreach (var benefit in sub.AdditionalBenefits)
                    if (benefit != null) GrantBenefit(benefit);
        }

        // ── Benefits ───────────────────────────────────────────────────────

        /// <summary>Grant a benefit to this crawler. Applies tags, stats, and passive effects.</summary>
        public void GrantBenefit(BenefitDefinition benefit)
        {
            if (!IsServer || benefit == null) return;
            if (_activeBenefits.Contains(benefit)) return; // No duplicates.

            _activeBenefits.Add(benefit);

            // Tags.
            if (benefit.GrantedTags != null)
                _tags.AddTags(benefit.GrantedTags);

            // Stat bonuses.
            if (benefit.StatBonuses != null)
                foreach (var mod in benefit.StatBonuses)
                    ApplyStatBonus(mod);

            // Armor.
            if (benefit.BonusArmor != 0f)
                _attrs.AttributeSet.AddBonusArmor(benefit.BonusArmor);

            // Passive effects (Duration: -1 = permanent).
            if (benefit.PassiveEffects != null)
            {
                foreach (var effectDef in benefit.PassiveEffects)
                {
                    if (effectDef == null) continue;
                    var ctx = EffectContext.Environment(transform.position);
                    var instance = EffectInstance.Create(effectDef, ctx, effectDef.BaseMagnitude);
                    _attrs.ApplyEffect(instance);
                    _benefitEffects.Add(instance);
                }
            }
        }

        /// <summary>Revoke a benefit. Removes tags, stats, and passive effects.</summary>
        public void RevokeBenefit(BenefitDefinition benefit)
        {
            if (!IsServer || benefit == null) return;
            if (!_activeBenefits.Remove(benefit)) return;

            if (benefit.GrantedTags != null)
                _tags.RemoveTags(benefit.GrantedTags);

            if (benefit.StatBonuses != null)
                foreach (var mod in benefit.StatBonuses)
                    RemoveStatBonus(mod);

            if (benefit.BonusArmor != 0f)
                _attrs.AttributeSet.RemoveBonusArmor(benefit.BonusArmor);

            // Passive effects are removed by EntityAttributes when they expire or are revoked.
            // For permanent effects, we'd need to track and remove them explicitly.
            // The _benefitEffects list handles this.
            for (int i = _benefitEffects.Count - 1; i >= 0; i--)
            {
                var inst = _benefitEffects[i];
                if (inst.Definition != null && System.Array.Exists(benefit.PassiveEffects, e => e == inst.Definition))
                {
                    _attrs.RemoveEffect(inst);
                    _benefitEffects.RemoveAt(i);
                }
            }
        }

        // ── Validation helpers ─────────────────────────────────────────────

        /// <summary>Check if this crawler meets all requirements for a class.</summary>
        public bool MeetsClassRequirements(ClassDefinition cls)
        {
            if (cls.StatRequirements != null)
                foreach (var req in cls.StatRequirements)
                    if (GetEffectiveStat(req.Stat) < req.MinValue) return false;

            if (cls.SkillRequirements != null && _skills != null)
                foreach (var req in cls.SkillRequirements)
                    if (req.Skill != null && _skills.GetSkillLevel(req.Skill) < req.MinLevel) return false;

            return true;
        }

        private int GetEffectiveStat(CrawlerStat stat)
        {
            var attrs = _attrs.AttributeSet;
            return stat switch
            {
                CrawlerStat.Strength => attrs.Strength,
                CrawlerStat.Constitution => attrs.Constitution,
                CrawlerStat.Dexterity => attrs.Dexterity,
                CrawlerStat.Intelligence => attrs.Intelligence,
                CrawlerStat.Charisma => attrs.Charisma,
                _ => 0
            };
        }

        private void ApplyStatBonus(StatModifier mod)
        {
            var attrs = _attrs.AttributeSet;
            switch (mod.Stat)
            {
                case CrawlerStat.Strength:     attrs.AddBonusStrength(mod.Amount); break;
                case CrawlerStat.Constitution:  attrs.AddBonusConstitution(mod.Amount); break;
                case CrawlerStat.Dexterity:     attrs.AddBonusDexterity(mod.Amount); break;
                case CrawlerStat.Intelligence:  attrs.AddBonusIntelligence(mod.Amount); break;
                case CrawlerStat.Charisma:      attrs.AddBonusCharisma(mod.Amount); break;
            }
        }

        private void RemoveStatBonus(StatModifier mod)
        {
            var attrs = _attrs.AttributeSet;
            switch (mod.Stat)
            {
                case CrawlerStat.Strength:     attrs.RemoveBonusStrength(mod.Amount); break;
                case CrawlerStat.Constitution:  attrs.RemoveBonusConstitution(mod.Amount); break;
                case CrawlerStat.Dexterity:     attrs.RemoveBonusDexterity(mod.Amount); break;
                case CrawlerStat.Intelligence:  attrs.RemoveBonusIntelligence(mod.Amount); break;
                case CrawlerStat.Charisma:      attrs.RemoveBonusCharisma(mod.Amount); break;
            }
        }

        // ── Network notifications ──────────────────────────────────────────

        [ClientRpc]
        private void NotifyIdentityChosenClientRpc(string raceName, string className)
        {
            // Client shows "You are a {raceName} {className}" notification.
        }

        [ClientRpc]
        private void NotifySubclassChosenClientRpc(string subclassName)
        {
            // Client shows "You have specialized as a {subclassName}" notification.
        }
    }
}
