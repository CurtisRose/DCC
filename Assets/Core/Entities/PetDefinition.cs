using UnityEngine;
using DCC.Abilities;

namespace DCC.Core.Entities
{
    /// <summary>
    /// Extends EntityDefinition with pet-specific data.
    ///
    /// In the DCC books:
    ///   - Pets are companions that fight alongside the crawler.
    ///   - Max active pets = f(Charisma). High Cha crawlers can run a whole menagerie.
    ///   - Pets have their own classes and skills (e.g., Donut the cat is a "Tortie").
    ///   - Pets evolve and gain abilities as they level.
    ///   - Pet intelligence varies: some are tactical, some are dumb as rocks.
    ///   - Pets can die permanently (or be resurrected via rare items/abilities).
    ///
    /// Example pets from the books:
    ///
    ///   Donut (Tortoiseshell Cat):
    ///     EntityDef: Str 2, Con 3, Dex 8, Int 6, Cha 14
    ///     PetClass: "Princess Popper" (explosives, royal skills)
    ///     FollowDistance: 3f
    ///     AIBehavior: Aggressive
    ///     Abilities: Bejeweled Tiara beam, Royal Decree stun
    ///     → Carl's cat. Absurdly overpowered for a cat. Loves destruction.
    ///
    ///   Mongo (Velociraptor):
    ///     EntityDef: Str 12, Con 8, Dex 10, Int 2, Cha 1
    ///     PetClass: "Raptor"
    ///     FollowDistance: 5f
    ///     AIBehavior: Aggressive
    ///     Abilities: Bite, Pounce
    ///     → Big, strong, not very smart. Pure melee beast.
    ///
    ///   Katia's Dinosaur (Brontoroc):
    ///     EntityDef: Str 20, Con 15, Dex 3, Int 3, Cha 2
    ///     PetClass: "Mount"
    ///     FollowDistance: 2f (mount distance)
    ///     AIBehavior: Defensive
    ///     CanMount: true
    ///     → Rideable siege pet, slow but devastating.
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Pet Definition", fileName = "Pet_New")]
    public class PetDefinition : ScriptableObject
    {
        [Header("Entity")]
        [field: SerializeField, Tooltip("The base entity archetype for this pet (stats, tags, resistances).")]
        public EntityDefinition EntityDefinition { get; private set; }

        [field: SerializeField, Tooltip("Prefab to spawn for this pet (must have NetworkObject, EntityAttributes, TagContainer, PetAI).")]
        public GameObject Prefab { get; private set; }

        [Header("Pet Identity")]
        [field: SerializeField] public string PetTypeName { get; private set; }
        [field: SerializeField] public Sprite Portrait { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        [Header("Pet Class")]
        [field: SerializeField, Tooltip("Optional class for the pet (e.g., Princess Popper, Raptor, Mount).")]
        public ClassDefinition PetClass { get; private set; }

        [field: SerializeField, Tooltip("Skills the pet starts with.")]
        public SkillDefinition[] StartingSkills { get; private set; }

        [field: SerializeField, Tooltip("Abilities the pet can use in combat.")]
        public AbilityDefinition[] Abilities { get; private set; }

        [Header("AI Behavior")]
        [field: SerializeField] public PetAIBehavior DefaultBehavior { get; private set; } = PetAIBehavior.Aggressive;
        [field: SerializeField] public float FollowDistance { get; private set; } = 4f;
        [field: SerializeField] public float AggroRange { get; private set; } = 10f;
        [field: SerializeField] public float LeashRange { get; private set; } = 20f;

        [Header("Special")]
        [field: SerializeField, Tooltip("If true, the owner can mount this pet for riding.")]
        public bool CanMount { get; private set; }

        [field: SerializeField, Tooltip("If true, pet can be resurrected after death (otherwise permanent death).")]
        public bool CanResurrect { get; private set; } = true;

        [field: SerializeField, Tooltip("Charisma cost to maintain this pet (subtracted from max pet slots).")]
        public int CharismaCost { get; private set; } = 1;
    }

    public enum PetAIBehavior
    {
        /// <summary>Pet attacks any enemy in aggro range.</summary>
        Aggressive,

        /// <summary>Pet only attacks enemies that attack it or its owner.</summary>
        Defensive,

        /// <summary>Pet follows owner and does not attack unless commanded.</summary>
        Passive,

        /// <summary>Pet stays in place and guards an area.</summary>
        Guard
    }
}
