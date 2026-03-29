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
    /// Example abilities:
    ///
    ///   Smoke Bomb:
    ///     Cast: SpawnZone(SmokeZone)
    ///     Requires tags on caster: (none)
    ///
    ///   Healing Aura:
    ///     Cast: AoEAtSelf(radius 4)
    ///     Effects: [HealEffect(10, RequiredTargetTags:[Living])]
    ///     When cast inside a SmokeZone: the zone already has [Gas] tags.
    ///     The HealEffect's GrantedTags:[Healing] get added to the composite.
    ///     Entities inside inherit both → healing smoke without programming it.
    ///
    ///   Blight Touch:
    ///     Cast: Melee
    ///     Effects: [DamageEffect(30, GrantedTags:[Necrotic, Cursed]),
    ///               StatusEffect(Poisoned, Duration:5s, RequiredTargetTags:[Living])]
    ///     → Applies Poisoned to living. If target is Undead:
    ///       Undead has RequiredTargetTags mismatch for Poisoned → no poison applied.
    ///       Necrotic DamageEffect has AmplifiedByTargetTags:[Undead] × 0 → heals instead.
    ///       (Configure HealEffect with RequiredTargetTags:[Undead] alongside DamageEffect.)
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
