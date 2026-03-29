using System;
using UnityEngine;
using DCC.Core.Tags;

namespace DCC.Core.Entities
{
    /// <summary>
    /// A ScriptableObject that defines how an entity's type responds to incoming effects
    /// based on the tags those effects carry.
    ///
    /// This is the cleanest answer to "healing grenade hurts undead":
    ///
    ///   UndeadResistance.asset:
    ///     { Tag: Healing,  Multiplier: 1.0, InvertsEffect: true  }
    ///     { Tag: Radiant,  Multiplier: 2.0, InvertsEffect: false }
    ///     { Tag: Necrotic, Multiplier: 0.0, InvertsEffect: false } // immune
    ///
    ///   Result: ANY effect tagged [Healing] hitting an Undead entity is inverted —
    ///   it deals damage equal to what it would have healed. This includes:
    ///   - Healing potions thrown at undead
    ///   - Healing smoke cloud that an undead walks into
    ///   - A healing aura cast near undead
    ///   - ANY future healing effect a designer creates
    ///   No special case. No RequiredTargetTags. Just this data asset.
    ///
    /// Multiple ResistanceProfiles can be stacked on one entity.
    /// More specific tags take priority over general ones (resolved by tag depth
    /// in the implication tree — deeper = more specific).
    ///
    /// Resistances are applied by EntityAttributes.ApplyEffect AFTER the
    /// InteractionEngine has resolved the composite context.
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Resistance Profile", fileName = "Resistance_New")]
    public class ResistanceProfile : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }

        [field: SerializeField] public TagResistance[] Resistances { get; private set; }

        [Serializable]
        public struct TagResistance
        {
            [Tooltip("The effect tag this resistance reacts to.")]
            public TagDefinition Tag;

            [Tooltip(
                "Multiplier applied to the effect's magnitude.\n" +
                "  1.0 = no change\n" +
                "  0.5 = resistant (half effect)\n" +
                "  2.0 = vulnerable (double effect)\n" +
                "  0.0 = immune")]
            public float Multiplier;

            [Tooltip(
                "If true, the effect's primary action is flipped:\n" +
                "  Heal → Damage  |  Damage → Heal  |  Buff → Debuff\n\n" +
                "This is what makes Healing damage Undead without special-casing " +
                "any individual healing item or ability.")]
            public bool InvertsEffect;
        }

        /// <summary>
        /// Evaluate this resistance profile against an incoming effect's tag set.
        /// Returns the final magnitude multiplier and whether the effect is inverted.
        ///
        /// When multiple resistances match, the most specific tag wins
        /// (the one deepest in the implication tree, i.e., highest RuntimeId,
        /// which TagRegistry assigns in specificity order during initialization).
        /// If equal specificity, InvertsEffect from the last match wins; multipliers stack.
        /// </summary>
        public void Evaluate(
            Core.Tags.TagMask effectTags,
            out float multiplier,
            out bool inverted)
        {
            multiplier = 1f;
            inverted = false;

            if (Resistances == null) return;

            foreach (var r in Resistances)
            {
                if (r.Tag == null) continue;
                if (!effectTags.HasTag(r.Tag)) continue;

                multiplier *= r.Multiplier;
                if (r.InvertsEffect) inverted = !inverted; // Toggle allows double-inversion back.
            }
        }
    }
}
