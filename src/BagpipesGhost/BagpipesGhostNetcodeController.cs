using System;
using BepInEx.Logging;
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
    public event Action<string> OnPlayBagpipesMusic;
    public event Action<string> OnStopBagpipesMusic;
    public event Action<string> OnEnterDeathState;
    public event Action<string, int, int, bool> OnPlayCreatureVoice;

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Netcode Controller");
        
        bagpipesGhostAIServer = GetComponent<BagpipesGhostAIServer>();
        if (bagpipesGhostAIServer == null) _mls.LogError("tubaGhostAI is null");
    }
    
    [ClientRpc]
    public void InitializeConfigValuesClientRpc(string recievedGhostId)
    {
        OnInitializeConfigValues?.Invoke(recievedGhostId);
    }
    
    [ClientRpc]
    public void ChangeAnimationParameterBoolClientRpc(string recievedGhostId, int animationId, bool value)
    {
        LogDebug($"{animationId}, {value}");
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
    public void GrabBagpipesClientRpc(string recievedGhostId)
    {
        OnGrabBagpipes?.Invoke(recievedGhostId);
    }

    [ClientRpc]
    public void PlayBagpipesMusicClientRpc(string recievedGhostId)
    {
        OnPlayBagpipesMusic?.Invoke(recievedGhostId);
    }
    
    [ClientRpc]
    public void StopBagpipesMusicClientRpc(string recievedGhostId)
    {
        OnStopBagpipesMusic?.Invoke(recievedGhostId);
    }
    
    [ServerRpc]
    public void SpawnBagpipesServerRpc(string recievedGhostId)
    {
        GameObject bagpipesObject = Instantiate(
            HarpGhostPlugin.BagpipesItem.spawnPrefab,
            bagpipesGhostAIServer.TransformPosition,
            Quaternion.identity,
            bagpipesGhostAIServer.RoundManagerInstance.spawnedScrapContainer);
        
        AudioSource bagpipesAudioSource = bagpipesObject.GetComponent<AudioSource>();
        if (bagpipesAudioSource == null) _mls.LogError("bagpipesAudioSource is null");

        InstrumentBehaviour bagpipesBehaviour = bagpipesObject.GetComponent<InstrumentBehaviour>();
        if (bagpipesBehaviour == null) _mls.LogError("bagpipesBehaviour is null");

        int bagpipesScrapValue = UnityEngine.Random.Range(150, 300);
        bagpipesObject.GetComponent<GrabbableObject>().fallTime = 0f;
        bagpipesObject.GetComponent<GrabbableObject>().SetScrapValue(bagpipesScrapValue);
        bagpipesGhostAIServer.RoundManagerInstance.totalScrapValueInLevel += bagpipesScrapValue;

        bagpipesObject.GetComponent<NetworkObject>().Spawn();
        SpawnBagpipesClientRpc(recievedGhostId, bagpipesObject, bagpipesScrapValue);
    }

    [ClientRpc]
    private void SpawnBagpipesClientRpc(string recievedGhostId, NetworkObjectReference bagpipesObject, int bagpipesScrapValue)
    {
        OnSpawnBagpipes?.Invoke(recievedGhostId, bagpipesObject, bagpipesScrapValue);
    }

    [ClientRpc]
    public void DropBagpipesClientRpc(string recievedGhostId, Vector3 targetPosition)
    {
        OnDropBagpipes?.Invoke(recievedGhostId, targetPosition);
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
        _mls.LogInfo(msg);
        #endif
    }
}