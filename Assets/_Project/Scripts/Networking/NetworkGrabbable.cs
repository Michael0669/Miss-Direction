using Unity.Netcode;
using UnityEngine;

namespace MissDirection.Networking
{
    /// <summary>
    /// A physics object any player can carry. Whoever grabs it asks the host for ownership,
    /// then simulates it on their own computer (its NetworkTransform is owner-authoritative),
    /// so carrying feels as smooth online as it does solo. Everyone else sees it follow along.
    /// </summary>
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody))]
    public class NetworkGrabbable : NetworkBehaviour
    {
        Rigidbody body;

        void Awake() => body = GetComponent<Rigidbody>();

        /// <summary>True when this computer controls the object and can move it.</summary>
        public bool CanMoveLocally => IsSpawned && IsOwner && body != null && !body.isKinematic;

        public void RequestOwnership()
        {
            if (!IsSpawned || IsOwner) return;
            RequestOwnershipRpc();
        }

        [Rpc(SendTo.Server)]
        void RequestOwnershipRpc(RpcParams rpcParams = default)
        {
            ulong requester = rpcParams.Receive.SenderClientId;
            if (OwnerClientId != requester) NetworkObject.ChangeOwnership(requester);
        }
    }
}
