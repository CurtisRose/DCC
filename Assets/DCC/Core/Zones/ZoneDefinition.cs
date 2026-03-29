using UnityEngine;
using DCC.Core.Tags;
using DCC.Core.Effects;

namespace DCC.Core.Zones
{
    /// <summary>
    /// Archetype for a zone type. Configures the static properties of the zone —
    /// its initial tags, tick rate, max duration, visual prefab, etc.
    ///
    /// Zones are the spatial surface where emergence happens most visibly.
    /// When effects are "infused" into a zone, the zone's TagContainer grows,
    /// and any entity overlapping the zone inherits all of those effects.
    ///
    /// Designer examples:
    ///
    ///   Smoke Zone:
    ///     Initial Tags: [Gas, Smoke, Obscuring, Dispersible]
    ///     Lifetime: 15s. Dispersal rate 0.1/s (shrinks over time).
    ///
    ///   Fire Puddle:
    ///     Initial Tags: [Fire, Hot, Burning, Persistent]
    ///     Initial Effects: [FireDamageEffect (2/s)]
    ///     Cannot be infused with [Cold] — blocked by [Hot] suppression.
    ///
    ///   Holy Circle:
    ///     Initial Tags: [Radiant, Magical, Blessed]
    ///     Initial Effects: [HealEffect (5/s), TurnUndeadEffect]
    ///     RequiredTargetTags on HealEffect: [Living] → only heals living.
    ///     RequiredTargetTags on TurnUndeadEffect: [Undead] → only affects undead.
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Zone Definition", fileName = "ZoneDef_New")]
    public class ZoneDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }

        [field: SerializeField, Tooltip("Tags this zone starts with when spawned.")]
        public TagDefinition[] InitialTags { get; private set; }

        [field: SerializeField, Tooltip("Effects active in this zone from the moment it spawns.")]
        public EffectDefinition[] InitialEffects { get; private set; }

        [field: SerializeField, Tooltip("Seconds. -1 = permanent until destroyed.")]
        public float Lifetime { get; private set; } = 10f;

        [field: SerializeField, Tooltip("How often the zone ticks effects onto overlapping entities (seconds).")]
        public float TickInterval { get; private set; } = 1f;

        [field: SerializeField, Tooltip("Initial radius of the zone trigger.")]
        public float Radius { get; private set; } = 3f;

        [field: SerializeField, Tooltip("Layers that can be affected by this zone.")]
        public LayerMask AffectedLayers { get; private set; }

        [field: SerializeField, Tooltip("If true, this zone can receive additional infused effects from items or abilities.")]
        public bool AcceptsInfusion { get; private set; } = true;

        [field: SerializeField, Tooltip("Maximum number of effects that can be infused into this zone. 0 = unlimited.")]
        public int MaxInfusedEffects { get; private set; } = 0;

        [field: SerializeField, Tooltip("Visual prefab (particles, decal). Spawned at zone creation.")]
        public GameObject VisualPrefab { get; private set; }
    }
}
