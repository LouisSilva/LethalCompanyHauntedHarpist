using System;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.EnforcerGhost;

public class EnforcerGhostNetcodeController : NetworkBehaviour
{
#pragma warning disable 0649
    [SerializeField] private EnforcerGhostAIServer enforcerGhostAIServer;
#pragma warning restore 0649

    internal event Action<int> OnDoAnimation;
    internal event Action<int, bool> OnChangeAnimationParameterBool;
    internal event Action OnInitializeConfigValues;
    internal event Action OnEnterDeathState;
    internal event Action<int, int, bool> OnPlayCreatureVoice;
    internal event Action<NetworkObjectReference, int> OnSpawnShotgun;
    internal event Action OnGrabShotgun;
    internal event Action OnGrabShotgunPhaseTwo;
    internal event Action OnGrabShotgunAfterStun;
    internal event Action<Vector3> OnDropShotgun;
    internal event Action<Vector3> OnDropShotgunWhenStunned;
    internal event Action OnIncreaseTargetPlayerFearLevel;
    internal event Action<ulong> OnChangeTargetPlayer;
    internal event Action OnShootGun;
    internal event Action<int> OnUpdateShotgunShellsLoaded;
    internal event Action<string> OnDoShotgunAnimation;
    internal event Action<bool> OnSetMeshEnabled;
    internal event Action OnPlayTeleportVfx;
    internal event Action OnEnableShield;
    internal event Action OnDisableShield;

    private void Start()
    {
        enforcerGhostAIServer = GetComponent<EnforcerGhostAIServer>();
        if (!enforcerGhostAIServer) HarpGhostPlugin.Logger.LogError("enforcerGhostAI is null");
    }

    [ClientRpc]
    internal void DisableShieldClientRpc()
    {
        OnDisableShield?.Invoke();
    }

    [ClientRpc]
    internal void EnableShieldClientRpc()
    {
        OnEnableShield?.Invoke();
    }

    [ClientRpc]
    internal void DropShotgunForStunClientRpc(Vector3 dropPosition)
    {
        OnDropShotgunWhenStunned?.Invoke(dropPosition);
    }

    [ClientRpc]
    internal void GrabShotgunAfterStunClientRpc()
    {
        OnGrabShotgunAfterStun?.Invoke();
    }

    [ClientRpc]
    internal void PlayTeleportVfxClientRpc()
    {
        OnPlayTeleportVfx?.Invoke();
    }

    [ClientRpc]
    internal void SetMeshEnabledClientRpc(bool meshEnabled)
    {
        OnSetMeshEnabled?.Invoke(meshEnabled);
    }

    [ClientRpc]
    internal void InitializeConfigValuesClientRpc()
    {
        OnInitializeConfigValues?.Invoke();
    }

    [ServerRpc]
    internal void SpawnShotgunServerRpc()
    {
        GameObject shotgunObject = Instantiate(
            HarpGhostPlugin.ShotgunPrefab,
            enforcerGhostAIServer.transform.position,
            Quaternion.identity,
            RoundManager.Instance.spawnedScrapContainer
        );

        int shotgunScrapValue = UnityEngine.Random.Range(EnforcerGhostConfig.Instance.ShotgunMinValue.Value, EnforcerGhostConfig.Instance.ShotgunMaxValue.Value);
        GrabbableObject shotgun = shotgunObject.GetComponent<GrabbableObject>();
        shotgun.fallTime = 0f;
        shotgun.SetScrapValue(shotgunScrapValue);
        RoundManager.Instance.totalScrapValueInLevel += shotgunScrapValue;

        shotgunObject.GetComponent<NetworkObject>().Spawn();
        SpawnShotgunClientRpc(shotgunObject, shotgunScrapValue);
    }

    [ClientRpc]
    private void SpawnShotgunClientRpc(NetworkObjectReference shotgunObject,
        int shotgunScrapValue)
    {
        OnSpawnShotgun?.Invoke(shotgunObject, shotgunScrapValue);
    }

    [ClientRpc]
    internal void GrabShotgunClientRpc()
    {
        OnGrabShotgun?.Invoke();
    }

    [ServerRpc(RequireOwnership = false)]
    internal void GrabShotgunPhaseTwoServerRpc()
    {
        GrabShotgunPhaseTwoClientRpc();
    }

    [ClientRpc]
    internal void ChangeTargetPlayerClientRpc(ulong playerClientId)
    {
        OnChangeTargetPlayer?.Invoke(playerClientId);
    }

    [ClientRpc]
    internal void IncreaseTargetPlayerFearLevelClientRpc()
    {
        OnIncreaseTargetPlayerFearLevel?.Invoke();
    }

    [ClientRpc]
    internal void UpdateShotgunShellsLoadedClientRpc(int shells)
    {
        OnUpdateShotgunShellsLoaded?.Invoke(shells);
    }

    [ServerRpc(RequireOwnership = false)]
    internal void DoShotgunAnimationServerRpc(string animationId)
    {
        DoShotgunAnimationClientRpc(animationId);
    }

    [ClientRpc]
    private void DoShotgunAnimationClientRpc(string animationId)
    {
        OnDoShotgunAnimation?.Invoke(animationId);
    }

    [ClientRpc]
    private void GrabShotgunPhaseTwoClientRpc()
    {
        OnGrabShotgunPhaseTwo?.Invoke();
    }

    [ClientRpc]
    internal void DropShotgunClientRpc(Vector3 targetPosition)
    {
        OnDropShotgun?.Invoke(targetPosition);
    }

    [ClientRpc]
    internal void ShootGunClientRpc()
    {
        OnShootGun?.Invoke();
    }

    [ClientRpc]
    internal void ChangeAnimationParameterBoolClientRpc(int animationId, bool value)
    {
        OnChangeAnimationParameterBool?.Invoke(animationId, value);
    }

    [ClientRpc]
    internal void DoAnimationClientRpc(int animationId)
    {
        OnDoAnimation?.Invoke(animationId);
    }

    [ClientRpc]
    internal void PlayCreatureVoiceClientRpc(int typeIndex, int clipArrayLength, bool interrupt = true)
    {
        int randomNum = UnityEngine.Random.Range(0, clipArrayLength);
        HarpGhostPlugin.LogVerbose($"Invoking OnPlayCreatureVoice | Audio clip index: {typeIndex}, audio clip random number: {randomNum}");
        OnPlayCreatureVoice?.Invoke(typeIndex, randomNum, interrupt);
    }

    [ClientRpc]
    internal void EnterDeathStateClientRpc()
    {
        OnEnterDeathState?.Invoke();
    }
}