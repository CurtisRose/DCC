using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;

namespace DCC.Core.Effects
{
    /// <summary>
    /// Snapshot of who applied an effect, from where, and with what tag context.
    /// Passed through the entire effect pipeline so modifiers can scale based on
    /// the source's tags (e.g., a cursed caster amplifies necrotic effects).
    /// </summary>
    public struct EffectContext
    {
        /// <summary>The NetworkObject that caused this effect (player, trap, zone). Zero = environment.</summary>
        public ulong SourceNetworkObjectId;

        /// <summary>Client ID of the owning player. Zero = AI / environment.</summary>
        public ulong OwnerClientId;

        /// <summary>World position where the effect originated.</summary>
        public Vector3 OriginPosition;

        /// <summary>Resolved tag mask of the source at the moment of application.</summary>
        public TagMask SourceTags;

        /// <summary>Optional scalar override. -1 means "use the EffectDefinition's default value".</summary>
        public float ValueOverride;

        public static EffectContext FromNetworkObject(NetworkObject source, float valueOverride = -1f)
        {
            return new EffectContext
            {
                SourceNetworkObjectId = source != null ? source.NetworkObjectId : 0,
                OwnerClientId = source != null ? source.OwnerClientId : 0,
                OriginPosition = source != null ? source.transform.position : Vector3.zero,
                SourceTags = source != null
                    ? source.GetComponent<Tags.TagContainer>()?.EffectiveMask ?? default
                    : default,
                ValueOverride = valueOverride
            };
        }

        public static EffectContext Environment(Vector3 origin) => new EffectContext
        {
            OriginPosition = origin,
            ValueOverride = -1f
        };
    }
}
