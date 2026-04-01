using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects;

namespace DCC.Abilities
{
    /// <summary>
    /// Archetype for a player ability. Like items, abilities are pure data.
    ///
    /// An ability fires one or more EffectDefinitions and optionally spawns a zone
    /// or projectile. All emergent behavior comes from the effects and their tags.
    ///
    /// Abilities can be either Skills (nonmagical, no mana cost) or Spells (magical,
    /// costs mana). In the DCC books, spells cost mana equal to their ManaCost field
    /// and are learned from tomes. Skills cost no mana and level through use.
    ///
    /// Example abilities:
    ///
    ///   Magic Missile (Spell):
    ///     Cast: Projectile
    ///     ManaCost: 3 (adjustable 3–6 at level 5 for more damage)
    ///     IsSpell: true
    ///     Effects: [DamageEffect(15, GrantedTags:[Arcane])]
    ///     → Laser bolts from eyes. Damage scales with Intelligence.
    ///
    ///   Heal (Spell — all crawlers learn in tutorial):
    ///     Cast: Instant (self or target)
    ///     ManaCost: 5
    ///     IsSpell: true
    ///     Effects: [HealEffect(30, RequiredTargetTags:[Living])]
    ///
    ///   Protective Shell (Spell):
    ///     Cast: SpawnZone
    ///     ManaCost: 8
    ///     IsSpell: true
    ///     Effects: [] (zone itself blocks corporeal entities)
    ///     GrantedCasterTags: [Shielded]
    ///     Note: Does NOT block magic or non-corporeal entities.
    ///
    ///   Iron Punch (Skill):
    ///     Cast: Instant (melee)
    ///     ManaCost: 0
    ///     IsSpell: false
    ///     RequiredSkill: Unarmed Combat (level 3)
    ///     Effects: [DamageEffect(40, GrantedTags:[Blunt, Impact])]
    ///     → Damage scales with Strength and Unarmed Combat skill level.
    ///
    ///   Smoke Bomb (Skill):
    ///     Cast: SpawnZone(SmokeZone)
    ///     RequiredSkill: Explosives Handling (level 1)
    ///
    ///   Blight Touch:
    ///     Cast: Melee
    ///     ManaCost: 4
    ///     Effects: [DamageEffect(30, GrantedTags:[Necrotic, Cursed]),
    ///               StatusEffect(Poisoned, Duration:5s, RequiredTargetTags:[Living])]
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Ability", fileName = "Ability_New")]
    public class AbilityDefinition : ScriptableObject
    {
        public enum CastMode
        {
            Instant,            // immediate at self or target
            Projectile,         // launches a Combat.Projectile
            SpawnZone,          // creates a zone at cast position or cursor
            Channeled           // repeating tick until cancelled
        }

        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [field: SerializeField] public CastMode Mode { get; private set; }

        [Header("Spell / Skill Classification")]
        [field: SerializeField, Tooltip(
            "If true, this is a spell (costs mana, learned from tomes, scales with Intelligence). " +
            "If false, this is a skill-based ability (no mana, scales with linked stat).")]
        public bool IsSpell { get; private set; }

        [field: SerializeField, Tooltip(
            "Mana cost to cast. Only applies to spells (IsSpell = true). " +
            "In the books, MP = Intelligence, so a 5-mana spell requires at least 5 Int.")]
        public float ManaCost { get; private set; } = 0f;

        [field: SerializeField, Tooltip(
            "Optional: skill required to use this ability. " +
            "The caster must have this skill at RequiredSkillLevel or higher.")]
        public SkillDefinition RequiredSkill { get; private set; }

        [field: SerializeField, Tooltip("Minimum skill level required. 0 = just needs the skill at any level.")]
        public int RequiredSkillLevel { get; private set; } = 0;

        [Header("Tags")]
        [field: SerializeField, Tooltip("Tags this ability REQUIRES the caster to have. Empty = no requirement.")]
        public TagDefinition[] RequiredCasterTags { get; private set; }

        [field: SerializeField, Tooltip("Tags temporarily added to the caster while this ability is active.")]
        public TagDefinition[] GrantedCasterTags { get; private set; }

        [Header("Effects")]
        [field: SerializeField] public EffectDefinition[] Effects { get; private set; }

        [Header("Area")]
        [field: SerializeField] public float CastRange { get; private set; } = 10f;
        [field: SerializeField] public float AoERadius { get; private set; } = 0f;   // 0 = single target

        [Header("Projectile (if Mode == Projectile)")]
        [field: SerializeField] public GameObject ProjectilePrefab { get; private set; }
        [field: SerializeField] public float ProjectileSpeed { get; private set; } = 20f;

        [Header("Zone (if Mode == SpawnZone)")]
        [field: SerializeField] public Zones.ZoneDefinition ZoneToSpawn { get; private set; }

        [Header("Timing")]
        [field: SerializeField] public float Cooldown { get; private set; } = 1f;
        [field: SerializeField] public float CastTime { get; private set; } = 0f;
        [field: SerializeField] public float ChannelDuration { get; private set; } = 3f;
        [field: SerializeField] public float ChannelTickInterval { get; private set; } = 0.5f;
    }
}
