using UnityEngine;
using Unity.Netcode;
using DCC.Core.Entities;

namespace DCC.Core.Effects.Modifiers
{
    /// <summary>
    /// Teleports the target to a destination.
    ///
    /// This implements the "teleport trap that triggers on items OR people" requirement.
    /// The key is in how the trap is configured, not in this effect:
    ///
    ///   Trigger on ANYONE physical:
    ///     RequiredTargetTags: [Corporeal]
    ///     (items are Corporeal, players are Corporeal + Living)
    ///
    ///   Trigger on PLAYERS ONLY:
    ///     RequiredTargetTags: [Living, Player]
    ///
    ///   Trigger on ITEMS ONLY:
    ///     RequiredTargetTags: [Item]
    ///     (players don't have the Item tag)
    ///
    ///   Trigger on UNDEAD ONLY:
    ///     RequiredTargetTags: [Undead]
    ///     → Now it's a holy ward that only affects undead
    ///
    /// All of these are designer-configured in the asset. No code changes.
    ///
    /// Destination modes:
    ///   Fixed: teleport to a specific transform in the scene
    ///   Random: teleport to a random point within radius
    ///   Linked: teleport to a paired TeleportTrap's destination
    /// </summary>
    [CreateAssetMenu(menuName = "DCC/Effect/Teleport", fileName = "Effect_Teleport")]
    public class TeleportEffect : EffectDefinition
    {
        public enum DestinationMode { Fixed, Random, Linked }

        [field: SerializeField] public DestinationMode Mode { get; private set; }
        [field: SerializeField] public Vector3 FixedDestination { get; private set; }
        [field: SerializeField] public float RandomRadius { get; private set; } = 5f;

        [field: SerializeField, Tooltip("For Linked mode: the NetworkObjectId of the paired trap.")]
        public ulong LinkedTrapNetworkObjectId { get; private set; }

        public override void OnApply(EffectInstance instance, EntityAttributes target)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            Vector3 destination = ResolveDestination(instance);
            var netObj = target.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                target.transform.position = destination;
                // Sync position via NetworkTransform — NGO handles the replication.
            }
        }

        private Vector3 ResolveDestination(EffectInstance instance)
        {
            switch (Mode)
            {
                case DestinationMode.Fixed:
                    return FixedDestination;

                case DestinationMode.Random:
                    var rand = Random.insideUnitCircle * RandomRadius;
                    return instance.Context.OriginPosition + new Vector3(rand.x, 0, rand.y);

                case DestinationMode.Linked:
                    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
                        .TryGetValue(LinkedTrapNetworkObjectId, out var paired))
                    {
                        return paired.transform.position;
                    }
                    return instance.Context.OriginPosition;

                default:
                    return instance.Context.OriginPosition;
            }
        }
    }
}
