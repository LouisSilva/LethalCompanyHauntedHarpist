using System;
using BepInEx.Logging;
using LethalCompanyHarpGhost.Items;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.BagpipesGhost;

public class BagpipesGhostNetcodeController : NetworkBehaviour
{
#pragma warning disable 0649
    [SerializeField] private BagpipesGhostAIServer bagpipesGhostAIServer;
#pragma warning restore 0649

    internal event Action<int> OnDoAnimation;
    internal event Action<int, bool> OnChangeAnimationParameterBool;
    internal event Action OnInitializeConfigValues;
    internal event Action OnUpdateGhostIdentifier;
    internal event Action OnGrabBagpipes;
    internal event Action<NetworkObjectReference, int> OnSpawnBagpipes;
    internal event Action<Vector3> OnDropBagpipes;
    internal event Action OnDestroyBagpipes;
    internal event Action OnPlayBagpipesMusic;
    internal event Action OnStopBagpipesMusic;
    internal event Action OnEnterDeathState;
    internal event Action OnPlayTeleportVfx;
    internal event Action<int, int, bool> OnPlayCreatureVoice;
    internal event Action<bool> OnSetMeshEnabled;

    private void Start()
    {
        bagpipesGhostAIServer = GetComponent<BagpipesGhostAIServer>();
        if (!bagpipesGhostAIServer) HarpGhostPlugin.Logger.LogError("bagpipesGhostAI is null");
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

    [ClientRpc]
    internal void DespawnHeldBagpipesClientRpc()
    {
        OnDestroyBagpipes?.Invoke();
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
    internal void PlayTeleportVfxClientRpc()
    {
        OnPlayTeleportVfx?.Invoke();
    }

    [ClientRpc]
    internal void SyncGhostIdentifierClientRpc()
    {
        OnUpdateGhostIdentifier?.Invoke();
    }

    [ClientRpc]
    internal void GrabBagpipesClientRpc()
    {
        OnGrabBagpipes?.Invoke();
    }

    [ClientRpc]
    internal void PlayBagpipesMusicClientRpc()
    {
        OnPlayBagpipesMusic?.Invoke();
    }

    [ClientRpc]
    internal void StopBagpipesMusicClientRpc()
    {
        OnStopBagpipesMusic?.Invoke();
    }

    [ServerRpc]
    internal void SpawnBagpipesServerRpc()
    {
        GameObject bagpipesObject = Instantiate(
            HarpGhostPlugin.BagpipesItem.spawnPrefab,
            bagpipesGhostAIServer.transform.position,
            Quaternion.identity,
            RoundManager.Instance.spawnedScrapContainer);

        AudioSource bagpipesAudioSource = bagpipesObject.GetComponentInChildren<AudioSource>();
        if (!bagpipesAudioSource) HarpGhostPlugin.Logger.LogError("bagpipesAudioSource is null");

        InstrumentBehaviour bagpipesBehaviour = bagpipesObject.GetComponent<InstrumentBehaviour>();
        if (!bagpipesBehaviour) HarpGhostPlugin.Logger.LogError("bagpipesBehaviour is null");

        int bagpipesScrapValue = UnityEngine.Random.Range(BagpipeGhostConfig.Instance.BagpipesMinValue.Value, BagpipeGhostConfig.Instance.BagpipesMaxValue.Value);
        bagpipesBehaviour.fallTime = 0f;
        bagpipesBehaviour.SetScrapValue(bagpipesScrapValue);
        RoundManager.Instance.totalScrapValueInLevel += bagpipesScrapValue;

        bagpipesObject.GetComponent<NetworkObject>().Spawn();
        SpawnBagpipesClientRpc(bagpipesObject, bagpipesScrapValue);
    }

    [ClientRpc]
    private void SpawnBagpipesClientRpc(NetworkObjectReference bagpipesObject, int bagpipesScrapValue)
    {
        OnSpawnBagpipes?.Invoke(bagpipesObject, bagpipesScrapValue);
    }

    [ClientRpc]
    internal void DropBagpipesClientRpc(Vector3 targetPosition)
    {
        OnDropBagpipes?.Invoke(targetPosition);
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