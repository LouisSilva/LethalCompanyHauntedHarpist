using LethalCompanyHarpGhost.Items;
using System;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostNetcodeController : NetworkBehaviour
{
#pragma warning disable 0649
    [SerializeField] private HarpGhostAIServer harpGhostAIServer;
#pragma warning restore 0649

    internal event Action<int> OnDoAnimation;
    internal event Action<int, bool> OnChangeAnimationParameterBool;
    internal event Action<int, int, bool, bool> OnPlayCreatureVoice;
    internal event Action OnEnterDeathState;
    internal event Action OnGrabHarp;
    internal event Action<NetworkObjectReference, int> OnSpawnHarp;
    internal event Action<Vector3> OnDropHarp;
    internal event Action OnPlayHarpMusic;
    internal event Action OnStopHarpMusic;
    internal event Action<ulong> OnChangeTargetPlayer;
    internal event Action<int, CauseOfDeath> OnDamageTargetPlayer;
    internal event Action<float, float> OnChangeAgentMaxSpeed;
    internal event Action OnFixAgentSpeedAfterAttack;
    internal event Action OnIncreaseTargetPlayerFearLevel;
    internal event Action OnInitializeConfigValues;
    internal event Action OnGhostEyesTurnRed;

    private void Start()
    {
        harpGhostAIServer = GetComponent<HarpGhostAIServer>();
        if (!harpGhostAIServer) HarpGhostPlugin.Logger.LogError("harpGhostAI is null");
    }

    [ServerRpc(RequireOwnership = false)]
    internal void FixAgentSpeedAfterAttackServerRpc()
    {
        OnFixAgentSpeedAfterAttack?.Invoke();
    }

    [ServerRpc(RequireOwnership = false)]
    internal void ChangeAgentMaxSpeedServerRpc(float newMaxSpeed, float newMaxSpeed2)
    {
        OnChangeAgentMaxSpeed?.Invoke(newMaxSpeed, newMaxSpeed2);
    }

    [ServerRpc(RequireOwnership = false)]
    internal void DamageTargetPlayerServerRpc(int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        DamageTargetPlayerClientRpc(damage, causeOfDeath);
    }

    [ClientRpc]
    internal void InitializeConfigValuesClientRpc()
    {
        OnInitializeConfigValues?.Invoke();
    }

    [ClientRpc]
    internal void DamageTargetPlayerClientRpc(int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        OnDamageTargetPlayer?.Invoke(damage, causeOfDeath);
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
    internal void EnterDeathStateClientRpc()
    {
        OnEnterDeathState?.Invoke();
    }

    [ClientRpc]
    internal void TurnGhostEyesRedClientRpc()
    {
        OnGhostEyesTurnRed?.Invoke();
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
    internal void GrabHarpClientRpc()
    {
        OnGrabHarp?.Invoke();
    }

    [ClientRpc]
    internal void PlayHarpMusicClientRpc()
    {
        OnPlayHarpMusic?.Invoke();
    }

    [ClientRpc]
    internal void StopHarpMusicClientRpc()
    {
        OnStopHarpMusic?.Invoke();
    }

    [ServerRpc]
    internal void SpawnHarpServerRpc()
    {
        GameObject harpObject = Instantiate(
            HarpGhostPlugin.HarpItem.spawnPrefab,
            harpGhostAIServer.transform.position,
            Quaternion.identity,
            RoundManager.Instance.spawnedScrapContainer);

        AudioSource harpAudioSource = harpObject.GetComponent<AudioSource>();
        if (!harpAudioSource) HarpGhostPlugin.Logger.LogError("harpAudioSource is null");

        InstrumentBehaviour harpBehaviour = harpObject.GetComponent<InstrumentBehaviour>();
        if (!harpBehaviour) HarpGhostPlugin.Logger.LogError("harpBehaviour is null");

        int harpScrapValue = Random.Range(HarpGhostConfig.Instance.HarpMinValue.Value, HarpGhostConfig.Instance.HarpMaxValue.Value + 1);
        harpBehaviour.fallTime = 0f;
        harpBehaviour.SetScrapValue(harpScrapValue);
        RoundManager.Instance.totalScrapValueInLevel += harpScrapValue;

        harpObject.GetComponent<NetworkObject>().Spawn();
        SpawnHarpClientRpc(harpObject, harpScrapValue);
    }

    [ClientRpc]
    private void SpawnHarpClientRpc(NetworkObjectReference harpObject, int harpScrapValue)
    {
        OnSpawnHarp?.Invoke(harpObject, harpScrapValue);
    }

    [ClientRpc]
    internal void DropHarpClientRpc(Vector3 targetPosition)
    {
        OnDropHarp?.Invoke(targetPosition);
    }

    [ClientRpc]
    internal void PlayCreatureVoiceClientRpc(int typeIndex, int clipArrayLength, bool interrupt = true, bool audibleByEnemies = false)
    {
        int randomNum = Random.Range(0, clipArrayLength);
        HarpGhostPlugin.LogVerbose($"Invoking OnPlayCreatureVoice | Audio clip index: {typeIndex}, audio clip random number: {randomNum}");
        OnPlayCreatureVoice?.Invoke(typeIndex, randomNum, interrupt, audibleByEnemies);
    }
}