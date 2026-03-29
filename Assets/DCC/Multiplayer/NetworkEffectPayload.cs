using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using DCC.Core.Tags;

namespace DCC.Multiplayer
{
    /// <summary>
    /// Serializable snapshot of an effect application event for network transport.
    ///
    /// EffectDefinitions are ScriptableObject assets — they can't be sent over the wire
    /// directly. Instead we send an Addressables key (GUID string) and re-resolve the
    /// asset on the receiving end. The TagMask snapshot lets clients reconstruct the
    /// emergent context even before the zone state propagates.
    ///
    /// Used in:
    ///   - ServerRpc: client requests an ability/item use
    ///   - ClientRpc: server notifies clients of resolved effects (for VFX/SFX)
    /// </summary>
    public struct NetworkEffectPayload : INetworkSerializable
    {
        /// <summary>Addressables key or Resources path for the EffectDefinition asset.</summary>
        public FixedString64Bytes EffectDefinitionKey;

        /// <summary>NetworkObjectId of the entity/zone that caused the effect. 0 = environment.</summary>
        public ulong SourceNetworkObjectId;

        /// <summary>Client ID of the player responsible. 0 = AI / environment.</summary>
        public ulong OwnerClientId;

        /// <summary>World position where the effect originates.</summary>
        public Vector3 OriginPosition;

        /// <summary>Snapshot of the source's effective tag mask at the moment of application.</summary>
        public TagMask SourceTagsSnapshot;

        /// <summary>Magnitude override. -1 = use EffectDefinition's BaseMagnitude.</summary>
        public float ValueOverride;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref EffectDefinitionKey);
            serializer.SerializeValue(ref SourceNetworkObjectId);
            serializer.SerializeValue(ref OwnerClientId);
            serializer.SerializeValue(ref OriginPosition);
            SourceTagsSnapshot.NetworkSerialize(serializer);
            serializer.SerializeValue(ref ValueOverride);
        }

        public Core.Effects.EffectContext ToEffectContext() => new Core.Effects.EffectContext
        {
            SourceNetworkObjectId = SourceNetworkObjectId,
            OwnerClientId = OwnerClientId,
            OriginPosition = OriginPosition,
            SourceTags = SourceTagsSnapshot,
            ValueOverride = ValueOverride
        };
    }

    /// <summary>
    /// Serializable payload for a zone-spawn event (e.g., grenade lands).
    /// </summary>
    public struct NetworkZoneSpawnPayload : INetworkSerializable
    {
        public FixedString64Bytes ZoneDefinitionKey;
        public Vector3 Position;
        public ulong OwnerClientId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ZoneDefinitionKey);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref OwnerClientId);
        }
    }
}
