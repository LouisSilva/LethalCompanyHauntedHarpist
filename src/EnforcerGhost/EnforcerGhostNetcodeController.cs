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
    public event Action<string, Vector3> OnDropShotgun;
    public event Action<string> OnIncreaseTargetPlayerFearLevel;
    public event Action<string, int> OnChangeTargetPlayer;
    public event Action<string> OnShootGun;
    public event Action<string, int> OnUpdateShotgunShellsLoaded;
    public event Action<string, string, bool> OnChangeShotgunAnimationParameterBool;
    public event Action<string, string> OnDoShotgunAnimation;

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Enforcer Ghost Netcode Controller");
        
        enforcerGhostAIServer = GetComponent<EnforcerGhostAIServer>();
        if (enforcerGhostAIServer == null) _mls.LogError("enforcerGhostAI is null");
    }
    
    [ClientRpc]
    public void InitializeConfigValuesClientRpc(string recievedGhostId)
    {
        OnInitializeConfigValues?.Invoke(recievedGhostId);
    }

    [ServerRpc]
    public void SpawnShotgunServerRpc(string recievedGhostId)
    {
        GameObject shotgunObject = Instantiate(
            HarpGhostPlugin.ShotgunPrefab,
            enforcerGhostAIServer.TransformPosition,
            Quaternion.identity,
            enforcerGhostAIServer.RoundManagerInstance.spawnedScrapContainer
            );

        int shotgunScrapValue = UnityEngine.Random.Range(30, 90);
        shotgunObject.GetComponent<GrabbableObject>().fallTime = 0f;
        shotgunObject.GetComponent<GrabbableObject>().SetScrapValue(shotgunScrapValue);
        enforcerGhostAIServer.RoundManagerInstance.totalScrapValueInLevel += shotgunScrapValue;
        
        shotgunObject.GetComponent<NetworkObject>().Spawn();
        SpawnShotgunClientRpc(recievedGhostId, shotgunObject, shotgunScrapValue);
    }

    [ClientRpc]
    public void SpawnShotgunClientRpc(string recievedGhostId, NetworkObjectReference shotgunObject, int shotgunScrapValue)
    {
        OnSpawnShotgun?.Invoke(recievedGhostId, shotgunObject, shotgunScrapValue);
    }

    [ClientRpc]
    public void GrabShotgunClientRpc(string recievedGhostId)
    {
        OnGrabShotgun?.Invoke(recievedGhostId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void GrabShotgunPhaseTwoServerRpc(string recievedGhostId)
    {
        GrabShotgunPhaseTwoClientRpc(recievedGhostId);
    }
    
    [ClientRpc]
    public void ChangeTargetPlayerClientRpc(string recievedGhostId, int playerClientId)
    {
        OnChangeTargetPlayer?.Invoke(recievedGhostId, playerClientId);
    }
    
    [ClientRpc]
    public void IncreaseTargetPlayerFearLevelClientRpc(string recievedGhostId)
    {
        OnIncreaseTargetPlayerFearLevel?.Invoke(recievedGhostId);
    }

    [ClientRpc]
    public void UpdateShotgunShellsLoadedClientRpc(string recievedGhostId, int shells)
    {
        OnUpdateShotgunShellsLoaded?.Invoke(recievedGhostId, shells);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void DoShotgunAnimationServerRpc(string recievedGhostId, string animationId)
    {
        DoShotgunAnimationClientRpc(recievedGhostId, animationId);
    }
    
    [ClientRpc]
    public void DoShotgunAnimationClientRpc(string recievedGhostId, string animationId)
    {
        OnDoShotgunAnimation?.Invoke(recievedGhostId, animationId);
    }

    [ClientRpc]
    public void ShotgunAnimatorSetBoolClientRpc(string recievedGhostId, string animationName, bool value)
    {
        OnChangeShotgunAnimationParameterBool?.Invoke(recievedGhostId, animationName, value);
    }

    [ClientRpc]
    private void GrabShotgunPhaseTwoClientRpc(string recievedGhostId)
    {
        OnGrabShotgunPhaseTwo?.Invoke(recievedGhostId);
    }
    
    [ClientRpc]
    public void DropShotgunClientRpc(string recievedGhostId, Vector3 targetPosition)
    {
        OnDropShotgun?.Invoke(recievedGhostId, targetPosition);
    }

    [ClientRpc]
    public void ShootGunClientRpc(string recievedGhostId)
    {
        LogDebug("In the ShootGunClientRpc");
        OnShootGun?.Invoke(recievedGhostId);
    }
    
    [ClientRpc]
    public void ChangeAnimationParameterBoolClientRpc(string recievedGhostId, int animationId, bool value)
    {
        OnChangeAnimationParameterBool?.Invoke(recievedGhostId, animationId, value);
    }

    [ClientRpc]
    public void DoAnimationClientRpc(string recievedGhostId, int animationId)
    {
        OnDoAnimation?.Invoke(recievedGhostId, animationId);
    }
    
    [ClientRpc]
    public void SyncGhostIdentifierClientRpc(string recievedGhostId)
    {
        OnUpdateGhostIdentifier?.Invoke(recievedGhostId);
    }
    
    [ClientRpc]
    public void PlayCreatureVoiceClientRpc(string recievedGhostId, int typeIndex, int clipArrayLength, bool interrupt = true)
    {
        int randomNum = UnityEngine.Random.Range(0, clipArrayLength);
        LogDebug($"Invoking OnPlayCreatureVoice | Audio clip index: {typeIndex}, audio clip random number: {randomNum}");
        OnPlayCreatureVoice?.Invoke(recievedGhostId, typeIndex, randomNum, interrupt);
    }
    
    [ClientRpc]
    public void EnterDeathStateClientRpc(string recievedGhostId)
    {
        OnEnterDeathState?.Invoke(recievedGhostId);
    }

    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}