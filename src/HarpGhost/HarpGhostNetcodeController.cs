using System;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostNetcodeController : NetworkBehaviour
{
    public event Action<Vector3> OnTargetPlayerLastSeenPosUpdated;
    
    [ServerRpc(RequireOwnership = false)]
    public void UpdateTargetPlayerLastSeenPosServerRpc(Vector3 targetPlayerPos)
    {
        UpdateTargetPlayerLastSeenPosClientRpc(targetPlayerPos);
    }

    [ClientRpc]
    public void UpdateTargetPlayerLastSeenPosClientRpc(Vector3 targetPlayerPos)
    {
        OnTargetPlayerLastSeenPosUpdated?.Invoke(targetPlayerPos);
    }
}