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
    
    internal event Action<string, int> OnDoAnimation;
    internal event Action<string, int, bool> OnChangeAnimationParameterBool;
    internal event Action<string> OnInitializeConfigValues;
    internal event Action<string> OnUpdateGhostIdentifier;
    internal event Action<string> OnGrabBagpipes;
    internal event Action<string, NetworkObjectReference, int> OnSpawnBagpipes;
    internal event Action<string, Vector3> OnDropBagpipes;
    internal event Action<string> OnDestroyBagpipes;
    internal event Action<string> OnPlayBagpipesMusic;
    internal event Action<string> OnStopBagpipesMusic;
    internal event Action<string> OnEnterDeathState;
    internal event Action<string> OnPlayTeleportVfx;
    internal event Action<string, int, int, bool> OnPlayCreatureVoice;
    internal event Action<string, bool> OnSetMeshEnabled; 

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Bagpipe Ghost Netcode Controller");
        
        bagpipesGhostAIServer = GetComponent<BagpipesGhostAIServer>();
        if (bagpipesGhostAIServer == null) _mls.LogError("bagpipesGhostAI is null");
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

    [ClientRpc]
    internal void DespawnHeldBagpipesClientRpc(string receivedGhostId)
    {
        OnDestroyBagpipes?.Invoke(receivedGhostId);
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
    internal void PlayTeleportVfxClientRpc(string receivedGhostId)
    {
        OnPlayTeleportVfx?.Invoke(receivedGhostId);
    }
    
    [ClientRpc]
    internal void SyncGhostIdentifierClientRpc(string receivedGhostId)
    {
        OnUpdateGhostIdentifier?.Invoke(receivedGhostId);
    }
    
    [ClientRpc]
    internal void GrabBagpipesClientRpc(string receivedGhostId)
    {
        OnGrabBagpipes?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    internal void PlayBagpipesMusicClientRpc(string receivedGhostId)
    {
        OnPlayBagpipesMusic?.Invoke(receivedGhostId);
    }
    
    [ClientRpc]
    internal void StopBagpipesMusicClientRpc(string receivedGhostId)
    {
        OnStopBagpipesMusic?.Invoke(receivedGhostId);
    }
    
    [ServerRpc]
    internal void SpawnBagpipesServerRpc(string receivedGhostId)
    {
        GameObject bagpipesObject = Instantiate(
            HarpGhostPlugin.BagpipesItem.spawnPrefab,
            bagpipesGhostAIServer.transform.position,
            Quaternion.identity,
            RoundManager.Instance.spawnedScrapContainer);
        
        AudioSource bagpipesAudioSource = bagpipesObject.GetComponentInChildren<AudioSource>();
        if (bagpipesAudioSource == null) _mls.LogError("bagpipesAudioSource is null");

        InstrumentBehaviour bagpipesBehaviour = bagpipesObject.GetComponent<InstrumentBehaviour>();
        if (bagpipesBehaviour == null) _mls.LogError("bagpipesBehaviour is null");

        int bagpipesScrapValue = UnityEngine.Random.Range(BagpipeGhostConfig.Instance.BagpipesMinValue.Value, BagpipeGhostConfig.Instance.BagpipesMaxValue.Value);
        bagpipesBehaviour.fallTime = 0f;
        bagpipesBehaviour.SetScrapValue(bagpipesScrapValue);
        RoundManager.Instance.totalScrapValueInLevel += bagpipesScrapValue;

        bagpipesObject.GetComponent<NetworkObject>().Spawn();
        SpawnBagpipesClientRpc(receivedGhostId, bagpipesObject, bagpipesScrapValue);
    }

    [ClientRpc]
    private void SpawnBagpipesClientRpc(string receivedGhostId, NetworkObjectReference bagpipesObject, int bagpipesScrapValue)
    {
        OnSpawnBagpipes?.Invoke(receivedGhostId, bagpipesObject, bagpipesScrapValue);
    }

    [ClientRpc]
    internal void DropBagpipesClientRpc(string receivedGhostId, Vector3 targetPosition)
    {
        OnDropBagpipes?.Invoke(receivedGhostId, targetPosition);
    }
    
    [ClientRpc]
    internal void PlayCreatureVoiceClientRpc(string receivedGhostId, int typeIndex, int clipArrayLength, bool interrupt = true)
    {
        int randomNum = UnityEngine.Random.Range(0, clipArrayLength);
        LogDebug($"Invoking OnPlayCreatureVoice | Audio clip index: {typeIndex}, audio clip random number: {randomNum}");
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