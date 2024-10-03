using BepInEx.Logging;
using LethalCompanyHarpGhost.Items;
using System;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostNetcodeController : NetworkBehaviour
{
#pragma warning disable 0649
    [SerializeField] private HarpGhostAIServer harpGhostAIServer;
#pragma warning restore 0649

    private ManualLogSource _mls;
    
    public event Action<string, int> OnDoAnimation;
    public event Action<string, int, bool> OnChangeAnimationParameterBool;
    public event Action<string, int, int, bool, bool> OnPlayCreatureVoice;
    public event Action<string> OnEnterDeathState;
    public event Action<string> OnGrabHarp;
    public event Action<string, NetworkObjectReference, int> OnSpawnHarp;
    public event Action<string, Vector3> OnDropHarp;
    public event Action<string> OnPlayHarpMusic;
    public event Action<string> OnStopHarpMusic;
    public event Action<string, ulong> OnChangeTargetPlayer;
    public event Action<string, int, CauseOfDeath> OnDamageTargetPlayer;
    public event Action<string, float, float> OnChangeAgentMaxSpeed;
    public event Action<string> OnFixAgentSpeedAfterAttack;
    public event Action<string> OnIncreaseTargetPlayerFearLevel;
    public event Action<string> OnInitializeConfigValues;
    public event Action<string> OnUpdateGhostIdentifier;
    public event Action<string> OnGhostEyesTurnRed;

    private void Start()
    {
        _mls = Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Harp Ghost Netcode Controller");
        
        harpGhostAIServer = GetComponent<HarpGhostAIServer>();
        if (harpGhostAIServer == null) _mls.LogError("harpGhostAI is null");
    }

    [ClientRpc]
    public void SyncGhostIdentifierClientRpc(string receivedGhostId)
    {
        OnUpdateGhostIdentifier?.Invoke(receivedGhostId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void FixAgentSpeedAfterAttackServerRpc(string receivedGhostId)
    {
        OnFixAgentSpeedAfterAttack?.Invoke(receivedGhostId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ChangeAgentMaxSpeedServerRpc(string receivedGhostId, float newMaxSpeed, float newMaxSpeed2)
    {
        OnChangeAgentMaxSpeed?.Invoke(receivedGhostId, newMaxSpeed, newMaxSpeed2);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DamageTargetPlayerServerRpc(string receivedGhostId, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        DamageTargetPlayerClientRpc(receivedGhostId, damage, causeOfDeath);
    }

    [ClientRpc]
    public void InitializeConfigValuesClientRpc(string receivedGhostId)
    {
        OnInitializeConfigValues?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    public void DamageTargetPlayerClientRpc(string receivedGhostId, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        OnDamageTargetPlayer?.Invoke(receivedGhostId, damage, causeOfDeath);
    }

    [ClientRpc]
    public void ChangeTargetPlayerClientRpc(string receivedGhostId, ulong playerClientId)
    {
        OnChangeTargetPlayer?.Invoke(receivedGhostId, playerClientId);
    }

    [ClientRpc]
    public void IncreaseTargetPlayerFearLevelClientRpc(string receivedGhostId)
    {
        OnIncreaseTargetPlayerFearLevel?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    public void EnterDeathStateClientRpc(string receivedGhostId)
    {
        OnEnterDeathState?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    public void TurnGhostEyesRedClientRpc(string receivedGhostId)
    {
        OnGhostEyesTurnRed?.Invoke(receivedGhostId);
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
    public void GrabHarpClientRpc(string receivedGhostId)
    {
        OnGrabHarp?.Invoke(receivedGhostId);
    }

    [ClientRpc]
    public void PlayHarpMusicClientRpc(string receivedGhostId)
    {
        OnPlayHarpMusic?.Invoke(receivedGhostId);
    }
    
    [ClientRpc]
    public void StopHarpMusicClientRpc(string receivedGhostId)
    {
        OnStopHarpMusic?.Invoke(receivedGhostId);
    }
    
    [ServerRpc]
    public void SpawnHarpServerRpc(string receivedGhostId)
    {
        GameObject harpObject = Instantiate(
            HarpGhostPlugin.HarpItem.spawnPrefab,
            harpGhostAIServer.transform.position,
            Quaternion.identity,
            RoundManager.Instance.spawnedScrapContainer);
        
        AudioSource harpAudioSource = harpObject.GetComponent<AudioSource>();
        if (harpAudioSource == null) _mls.LogError("harpAudioSource is null");

        InstrumentBehaviour harpBehaviour = harpObject.GetComponent<InstrumentBehaviour>();
        if (harpBehaviour == null) _mls.LogError("harpBehaviour is null");

        int harpScrapValue = Random.Range(HarpGhostConfig.Instance.HarpMinValue.Value, HarpGhostConfig.Instance.HarpMaxValue.Value + 1);
        harpBehaviour.fallTime = 0f;
        harpBehaviour.SetScrapValue(harpScrapValue);
        RoundManager.Instance.totalScrapValueInLevel += harpScrapValue;

        harpObject.GetComponent<NetworkObject>().Spawn();
        SpawnHarpClientRpc(receivedGhostId, harpObject, harpScrapValue);
    }

    [ClientRpc]
    private void SpawnHarpClientRpc(string receivedGhostId, NetworkObjectReference harpObject, int harpScrapValue)
    {
        OnSpawnHarp?.Invoke(receivedGhostId, harpObject, harpScrapValue);
    }

    [ClientRpc]
    public void DropHarpClientRpc(string receivedGhostId, Vector3 targetPosition)
    {
        OnDropHarp?.Invoke(receivedGhostId, targetPosition);
    }
    
    [ClientRpc]
    public void PlayCreatureVoiceClientRpc(string receivedGhostId, int typeIndex, int clipArrayLength, bool interrupt = true, bool audibleByEnemies = false)
    {
        int randomNum = Random.Range(0, clipArrayLength);
        LogDebug($"Invoking OnPlayCreatureVoice | Audio clip index: {typeIndex}, audio clip random number: {randomNum}");
        OnPlayCreatureVoice?.Invoke(receivedGhostId, typeIndex, randomNum, interrupt, audibleByEnemies);
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}