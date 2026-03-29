using UnityEngine;

namespace DCC.Core.Tags
{
    /// <summary>
    /// A single semantic tag in the game world. Tags are the vocabulary of the emergent
    /// interaction system — everything that can interact is described by tags.
    ///
    /// Tags are ScriptableObject assets, not enums, so designers can add new ones without
    /// touching code. Every new tag automatically participates in all existing interaction
    /// rules that reference its implied/suppressed tags.
    ///
    /// Examples: Gas, Healing, Fire, Undead, Living, Burning, Wet, Corporeal, Explosive
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Tag", fileName = "Tag_New")]
    public class TagDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }

        /// <summary>
        /// Tags that are automatically added when this tag is present.
        /// "Burning" implies "Hot". "Undead" implies "NotLiving".
        /// The implication graph is resolved transitively at startup by TagRegistry.
        /// </summary>
        [field: SerializeField] public TagDefinition[] ImpliedTags { get; private set; }

        /// <summary>
        /// Tags that are automatically removed when this tag is applied.
        /// "Wet" suppresses "Burning". "Frozen" suppresses "Burning" and "Wet".
        /// Suppression is checked each time the resolved mask is computed.
        /// </summary>
        [field: SerializeField] public TagDefinition[] SuppressedTags { get; private set; }

        /// <summary>
        /// Runtime index assigned by TagRegistry.Initialize(). Not serialized.
        /// Used as the bit index in TagMask.
        /// </summary>
        public int RuntimeId { get; internal set; } = -1;

        public override string ToString() => DisplayName;
    }
}
