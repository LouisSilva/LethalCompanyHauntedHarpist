using System;
using BepInEx.Logging;
using LethalCompanyHarpGhost.Items;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.BagpipesGhost;

public class BagpipesGhostNetcodeController : NetworkBehaviour
{
    private ManualLogSource _mls;
    
    #pragma warning disable 0649
    [SerializeField] private BagpipesGhostAIServer bagpipesGhostAIServer;
    #pragma warning restore 0649
    
    public event Action<string, int> OnDoAnimation;
    public event Action<string, int, bool> OnChangeAnimationParameterBool;
    public event Action<string> OnInitializeConfigValues;
    public event Action<string> OnUpdateGhostIdentifier;
    public event Action<string> OnGrabBagpipes;
    public event Action<string, NetworkObjectReference, int> OnSpawnBagpipes;
    public event Action<string, Vector3> OnDropBagpipes;
    public event Action<string> OnDestroyBagpipes;
    public event Action<string> OnPlayBagpipesMusic;
    public event Action<string> OnStopBagpipesMusic;
    public event Action<string> OnEnterDeathState;
    public event Action<string> OnPlayTeleportVfx;
    public event Action<string, int, int, bool> OnPlayCreatureVoice;
    public event Action<string, bool> OnSetMeshEnabled; 

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Bagpipe Ghost Netcode Controller");
        
        bagpipesGhostAIServer = GetComponent<BagpipesGhostAIServer>();
        if (bagpipesGhostAIServer == null) _mls.LogError("bagpipesGhostAI is null");
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

    [ClientRpc]
    public void DespawnHeldBagpipesClientRpc(string receivedGhostId)
    {
        OnDestroyBagpipes?.Invoke(receivedGhostId);
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
    public void PlayTeleportVfxClientRpc(string receivedGhostId)
    {
        OnPlayTeleportVfx?.Invoke(receivedGhostId);
    }
    
    [ClientRpc]
    public void SyncGhostIdentifierClientRpc(string receivedGhostId)
    {
        OnUpdateGhostIdentifier?.Invoke(receivedGhostId);
    }
    
    [ClientRpc]
    public void GrabBagpipesClientRpc(string receivedGhostId)
    {
        OnGrabBagpipes?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    public void PlayBagpipesMusicClientRpc(string receivedGhostId)
    {
        OnPlayBagpipesMusic?.Invoke(receivedGhostId);
    }
    
    [ClientRpc]
    public void StopBagpipesMusicClientRpc(string receivedGhostId)
    {
        OnStopBagpipesMusic?.Invoke(receivedGhostId);
    }
    
    [ServerRpc]
    public void SpawnBagpipesServerRpc(string receivedGhostId)
    {
        GameObject bagpipesObject = Instantiate(
            HarpGhostPlugin.BagpipesItem.spawnPrefab,
            bagpipesGhostAIServer.TransformPosition,
            Quaternion.identity,
            bagpipesGhostAIServer.RoundManagerInstance.spawnedScrapContainer);
        
        AudioSource bagpipesAudioSource = bagpipesObject.GetComponentInChildren<AudioSource>();
        if (bagpipesAudioSource == null) _mls.LogError("bagpipesAudioSource is null");

        InstrumentBehaviour bagpipesBehaviour = bagpipesObject.GetComponent<InstrumentBehaviour>();
        if (bagpipesBehaviour == null) _mls.LogError("bagpipesBehaviour is null");

        int bagpipesScrapValue = UnityEngine.Random.Range(BagpipeGhostConfig.Instance.BagpipesMinValue.Value, BagpipeGhostConfig.Instance.BagpipesMaxValue.Value);
        bagpipesObject.GetComponent<GrabbableObject>().fallTime = 0f;
        bagpipesObject.GetComponent<GrabbableObject>().SetScrapValue(bagpipesScrapValue);
        bagpipesGhostAIServer.RoundManagerInstance.totalScrapValueInLevel += bagpipesScrapValue;

        bagpipesObject.GetComponent<NetworkObject>().Spawn();
        SpawnBagpipesClientRpc(receivedGhostId, bagpipesObject, bagpipesScrapValue);
    }

    [ClientRpc]
    private void SpawnBagpipesClientRpc(string receivedGhostId, NetworkObjectReference bagpipesObject, int bagpipesScrapValue)
    {
        OnSpawnBagpipes?.Invoke(receivedGhostId, bagpipesObject, bagpipesScrapValue);
    }

    [ClientRpc]
    public void DropBagpipesClientRpc(string receivedGhostId, Vector3 targetPosition)
    {
        OnDropBagpipes?.Invoke(receivedGhostId, targetPosition);
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