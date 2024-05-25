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
    
    public event Action<string, int> OnDoAnimation;
    public event Action<string, int, bool> OnChangeAnimationParameterBool;
    public event Action<string> OnInitializeConfigValues;
    public event Action<string> OnUpdateGhostIdentifier;
    public event Action<string> OnEnterDeathState;
    public event Action<string, int, int, bool> OnPlayCreatureVoice;
    public event Action<string, NetworkObjectReference, int> OnSpawnShotgun;
    public event Action<string> OnGrabShotgun;
    public event Action<string> OnGrabShotgunPhaseTwo;
    public event Action<string> OnGrabShotgunAfterStun; 
    public event Action<string, Vector3> OnDropShotgun;
    public event Action<string, Vector3> OnDropShotgunWhenStunned;
    public event Action<string> OnIncreaseTargetPlayerFearLevel;
    public event Action<string, int> OnChangeTargetPlayer;
    public event Action<string> OnShootGun;
    public event Action<string, int> OnUpdateShotgunShellsLoaded;
    public event Action<string, string> OnDoShotgunAnimation;
    public event Action<string, bool> OnSetMeshEnabled;
    public event Action<string> OnPlayTeleportVfx;
    public event Action<string> OnEnableShield;
    public event Action<string> OnDisableShield; 

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Enforcer Ghost Netcode Controller");
        
        enforcerGhostAIServer = GetComponent<EnforcerGhostAIServer>();
        if (enforcerGhostAIServer == null) _mls.LogError("enforcerGhostAI is null");
    }

    [ClientRpc]
    public void DisableShieldClientRpc(string receivedGhostId)
    {
        OnDisableShield?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    public void EnableShieldClientRpc(string receivedGhostId)
    {
        OnEnableShield?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    public void DropShotgunForStunClientRpc(string receivedGhostId, Vector3 dropPosition)
    {
        OnDropShotgunWhenStunned?.Invoke(receivedGhostId, dropPosition);
    }

    [ClientRpc]
    public void GrabShotgunAfterStunClientRpc(string receivedGhostId)
    {
        OnGrabShotgunAfterStun?.Invoke(receivedGhostId);
    }
    
    [ClientRpc]
    public void PlayTeleportVfxClientRpc(string receivedGhostId)
    {
        OnPlayTeleportVfx?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    public void SetMeshEnabledClientRpc(string receivedGhostId, bool meshEnabled)
    {
        OnSetMeshEnabled?.Invoke(receivedGhostId, meshEnabled);
    }
    
    [ClientRpc]
    public void InitializeConfigValuesClientRpc(string receivedGhostId)
    {
        OnInitializeConfigValues?.Invoke(receivedGhostId);
    }

    [ServerRpc]
    public void SpawnShotgunServerRpc(string receivedGhostId)
    {
        GameObject shotgunObject = Instantiate(
            HarpGhostPlugin.ShotgunPrefab,
            enforcerGhostAIServer.TransformPosition,
            Quaternion.identity,
            enforcerGhostAIServer.RoundManagerInstance.spawnedScrapContainer
            );

        int shotgunScrapValue = UnityEngine.Random.Range(EnforcerGhostConfig.Instance.ShotgunMinValue.Value, EnforcerGhostConfig.Instance.ShotgunMaxValue.Value);
        shotgunObject.GetComponent<GrabbableObject>().fallTime = 0f;
        shotgunObject.GetComponent<GrabbableObject>().SetScrapValue(shotgunScrapValue);
        enforcerGhostAIServer.RoundManagerInstance.totalScrapValueInLevel += shotgunScrapValue;
        
        shotgunObject.GetComponent<NetworkObject>().Spawn();
        SpawnShotgunClientRpc(receivedGhostId, shotgunObject, shotgunScrapValue);
    }

    [ClientRpc]
    public void SpawnShotgunClientRpc(string receivedGhostId, NetworkObjectReference shotgunObject, int shotgunScrapValue)
    {
        OnSpawnShotgun?.Invoke(receivedGhostId, shotgunObject, shotgunScrapValue);
    }

    [ClientRpc]
    public void GrabShotgunClientRpc(string receivedGhostId)
    {
        OnGrabShotgun?.Invoke(receivedGhostId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void GrabShotgunPhaseTwoServerRpc(string receivedGhostId)
    {
        GrabShotgunPhaseTwoClientRpc(receivedGhostId);
    }
    
    [ClientRpc]
    public void ChangeTargetPlayerClientRpc(string receivedGhostId, int playerClientId)
    {
        OnChangeTargetPlayer?.Invoke(receivedGhostId, playerClientId);
    }
    
    [ClientRpc]
    public void IncreaseTargetPlayerFearLevelClientRpc(string receivedGhostId)
    {
        OnIncreaseTargetPlayerFearLevel?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    public void UpdateShotgunShellsLoadedClientRpc(string receivedGhostId, int shells)
    {
        OnUpdateShotgunShellsLoaded?.Invoke(receivedGhostId, shells);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void DoShotgunAnimationServerRpc(string receivedGhostId, string animationId)
    {
        DoShotgunAnimationClientRpc(receivedGhostId, animationId);
    }
    
    [ClientRpc]
    public void DoShotgunAnimationClientRpc(string receivedGhostId, string animationId)
    {
        OnDoShotgunAnimation?.Invoke(receivedGhostId, animationId);
    }

    [ClientRpc]
    private void GrabShotgunPhaseTwoClientRpc(string receivedGhostId)
    {
        OnGrabShotgunPhaseTwo?.Invoke(receivedGhostId);
    }
    
    [ClientRpc]
    public void DropShotgunClientRpc(string receivedGhostId, Vector3 targetPosition)
    {
        OnDropShotgun?.Invoke(receivedGhostId, targetPosition);
    }

    [ClientRpc]
    public void ShootGunClientRpc(string receivedGhostId)
    {
        OnShootGun?.Invoke(receivedGhostId);
    }
    
    [ClientRpc]
    public void ChangeAnimationParameterBoolClientRpc(string receivedGhostId, int animationId, bool value)
    {
        OnChangeAnimationParameterBool?.Invoke(receivedGhostId, animationId, value);
    }

    [ClientRpc]
    public void DoAnimationClientRpc(string receivedGhostId, int animationId)
    {
        OnDoAnimation?.Invoke(receivedGhostId, animationId);
    }
    
    [ClientRpc]
    public void SyncGhostIdentifierClientRpc(string receivedGhostId)
    {
        OnUpdateGhostIdentifier?.Invoke(receivedGhostId);
    }
    
    [ClientRpc]
    public void PlayCreatureVoiceClientRpc(string receivedGhostId, int typeIndex, int clipArrayLength, bool interrupt = true)
    {
        int randomNum = UnityEngine.Random.Range(0, clipArrayLength);
        LogDebug($"Invoking OnPlayCreatureVoice | Audio clip index: {typeIndex}, audio clip random number: {randomNum}");
        OnPlayCreatureVoice?.Invoke(receivedGhostId, typeIndex, randomNum, interrupt);
    }
    
    [ClientRpc]
    public void EnterDeathStateClientRpc(string receivedGhostId)
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