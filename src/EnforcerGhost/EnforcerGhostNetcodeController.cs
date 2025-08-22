using System;
using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.EnforcerGhost;

public class EnforcerGhostNetcodeController : NetworkBehaviour
{
    private ManualLogSource _mls;

#pragma warning disable 0649
    [SerializeField] private EnforcerGhostAIServer enforcerGhostAIServer;
#pragma warning restore 0649

    internal event Action<string, int> OnDoAnimation;
    internal event Action<string, int, bool> OnChangeAnimationParameterBool;
    internal event Action<string> OnInitializeConfigValues;
    internal event Action<string> OnUpdateGhostIdentifier;
    internal event Action<string> OnEnterDeathState;
    internal event Action<string, int, int, bool> OnPlayCreatureVoice;
    internal event Action<string, NetworkObjectReference, int> OnSpawnShotgun;
    internal event Action<string> OnGrabShotgun;
    internal event Action<string> OnGrabShotgunPhaseTwo;
    internal event Action<string> OnGrabShotgunAfterStun;
    internal event Action<string, Vector3> OnDropShotgun;
    internal event Action<string, Vector3> OnDropShotgunWhenStunned;
    internal event Action<string> OnIncreaseTargetPlayerFearLevel;
    internal event Action<string, ulong> OnChangeTargetPlayer;
    internal event Action<string> OnShootGun;
    internal event Action<string, int> OnUpdateShotgunShellsLoaded;
    internal event Action<string, string> OnDoShotgunAnimation;
    internal event Action<string, bool> OnSetMeshEnabled;
    internal event Action<string> OnPlayTeleportVfx;
    internal event Action<string> OnEnableShield;
    internal event Action<string> OnDisableShield;

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Enforcer Ghost Netcode Controller");

        enforcerGhostAIServer = GetComponent<EnforcerGhostAIServer>();
        if (enforcerGhostAIServer == null) _mls.LogError("enforcerGhostAI is null");
    }

    [ClientRpc]
    internal void DisableShieldClientRpc(string receivedGhostId)
    {
        OnDisableShield?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    internal void EnableShieldClientRpc(string receivedGhostId)
    {
        OnEnableShield?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    internal void DropShotgunForStunClientRpc(string receivedGhostId, Vector3 dropPosition)
    {
        OnDropShotgunWhenStunned?.Invoke(receivedGhostId, dropPosition);
    }

    [ClientRpc]
    internal void GrabShotgunAfterStunClientRpc(string receivedGhostId)
    {
        OnGrabShotgunAfterStun?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    internal void PlayTeleportVfxClientRpc(string receivedGhostId)
    {
        OnPlayTeleportVfx?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    internal void SetMeshEnabledClientRpc(string receivedGhostId, bool meshEnabled)
    {
        OnSetMeshEnabled?.Invoke(receivedGhostId, meshEnabled);
    }

    [ClientRpc]
    internal void InitializeConfigValuesClientRpc(string receivedGhostId)
    {
        OnInitializeConfigValues?.Invoke(receivedGhostId);
    }

    [ServerRpc]
    internal void SpawnShotgunServerRpc(string receivedGhostId)
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
        SpawnShotgunClientRpc(receivedGhostId, shotgunObject, shotgunScrapValue);
    }

    [ClientRpc]
    private void SpawnShotgunClientRpc(string receivedGhostId, NetworkObjectReference shotgunObject,
        int shotgunScrapValue)
    {
        OnSpawnShotgun?.Invoke(receivedGhostId, shotgunObject, shotgunScrapValue);
    }

    [ClientRpc]
    internal void GrabShotgunClientRpc(string receivedGhostId)
    {
        OnGrabShotgun?.Invoke(receivedGhostId);
    }

    [ServerRpc(RequireOwnership = false)]
    internal void GrabShotgunPhaseTwoServerRpc(string receivedGhostId)
    {
        GrabShotgunPhaseTwoClientRpc(receivedGhostId);
    }

    [ClientRpc]
    internal void ChangeTargetPlayerClientRpc(string receivedGhostId, ulong playerClientId)
    {
        OnChangeTargetPlayer?.Invoke(receivedGhostId, playerClientId);
    }

    [ClientRpc]
    internal void IncreaseTargetPlayerFearLevelClientRpc(string receivedGhostId)
    {
        OnIncreaseTargetPlayerFearLevel?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    internal void UpdateShotgunShellsLoadedClientRpc(string receivedGhostId, int shells)
    {
        OnUpdateShotgunShellsLoaded?.Invoke(receivedGhostId, shells);
    }

    [ServerRpc(RequireOwnership = false)]
    internal void DoShotgunAnimationServerRpc(string receivedGhostId, string animationId)
    {
        DoShotgunAnimationClientRpc(receivedGhostId, animationId);
    }

    [ClientRpc]
    private void DoShotgunAnimationClientRpc(string receivedGhostId, string animationId)
    {
        OnDoShotgunAnimation?.Invoke(receivedGhostId, animationId);
    }

    [ClientRpc]
    private void GrabShotgunPhaseTwoClientRpc(string receivedGhostId)
    {
        OnGrabShotgunPhaseTwo?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    internal void DropShotgunClientRpc(string receivedGhostId, Vector3 targetPosition)
    {
        OnDropShotgun?.Invoke(receivedGhostId, targetPosition);
    }

    [ClientRpc]
    internal void ShootGunClientRpc(string receivedGhostId)
    {
        OnShootGun?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    internal void ChangeAnimationParameterBoolClientRpc(string receivedGhostId, int animationId, bool value)
    {
        OnChangeAnimationParameterBool?.Invoke(receivedGhostId, animationId, value);
    }

    [ClientRpc]
    internal void DoAnimationClientRpc(string receivedGhostId, int animationId)
    {
        OnDoAnimation?.Invoke(receivedGhostId, animationId);
    }

    [ClientRpc]
    internal void SyncGhostIdentifierClientRpc(string receivedGhostId)
    {
        OnUpdateGhostIdentifier?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    internal void PlayCreatureVoiceClientRpc(string receivedGhostId, int typeIndex, int clipArrayLength,
        bool interrupt = true)
    {
        int randomNum = UnityEngine.Random.Range(0, clipArrayLength);
        LogDebug(
            $"Invoking OnPlayCreatureVoice | Audio clip index: {typeIndex}, audio clip random number: {randomNum}");
        OnPlayCreatureVoice?.Invoke(receivedGhostId, typeIndex, randomNum, interrupt);
    }

    [ClientRpc]
    internal void EnterDeathStateClientRpc(string receivedGhostId)
    {
        OnEnterDeathState?.Invoke(receivedGhostId);
    }

    private void LogDebug(string msg)
    {
#if DEBUG
        _mls?.LogInfo(msg);
#endif
    }
}