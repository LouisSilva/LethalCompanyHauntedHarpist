using System;
using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostNetcodeController : NetworkBehaviour
{
    #pragma warning disable 0649
    [SerializeField] private HarpGhostAIServer harpGhostAIServer;
    #pragma warning restore 0649

    private ManualLogSource _mls;
    
    public event Action<string, int> OnDoAnimation;
    public event Action<string, int, bool> OnChangeAnimationParameterBool;
    public event Action<string, int, int, bool> OnPlayCreatureVoice;
    public event Action<string> OnEnterDeathState;
    public event Action<string> OnGrabHarp;
    public event Action<string, NetworkObjectReference, int> OnSpawnHarp;
    public event Action<string, Vector3> OnDropHarp;
    public event Action<string> OnPlayHarpMusic;
    public event Action<string> OnStopHarpMusic;
    public event Action<string, int> OnChangeTargetPlayer;
    public event Action<string, int, CauseOfDeath> OnDamageTargetPlayer;
    public event Action<string, float, float> OnChangeAgentMaxSpeed;
    public event Action<string> OnFixAgentSpeedAfterAttack;
    public event Action<string> OnIncreaseTargetPlayerFearLevel;
    public event Action<string> OnInitializeConfigValues;
    public event Action<string> OnUpdateGhostIdentifier;
    public event Action<string> OnGhostEyesTurnRed;

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Harp Ghost Netcode Controller");
        
        harpGhostAIServer = GetComponent<HarpGhostAIServer>();
        if (harpGhostAIServer == null) _mls.LogError("harpGhostAI is null");
    }

    [ClientRpc]
    public void SyncGhostIdentifierClientRpc(string recievedGhostId)
    {
        OnUpdateGhostIdentifier?.Invoke(recievedGhostId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void FixAgentSpeedAfterAttackServerRpc(string recievedGhostId)
    {
        OnFixAgentSpeedAfterAttack?.Invoke(recievedGhostId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ChangeAgentMaxSpeedServerRpc(string recievedGhostId, float newMaxSpeed, float newMaxSpeed2)
    {
        OnChangeAgentMaxSpeed?.Invoke(recievedGhostId, newMaxSpeed, newMaxSpeed2);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DamageTargetPlayerServerRpc(string recievedGhostId, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        DamageTargetPlayerClientRpc(recievedGhostId, damage, causeOfDeath);
    }

    [ClientRpc]
    public void InitializeConfigValuesClientRpc(string recievedGhostId)
    {
        OnInitializeConfigValues?.Invoke(recievedGhostId);
    }

    [ClientRpc]
    public void DamageTargetPlayerClientRpc(string recievedGhostId, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        OnDamageTargetPlayer?.Invoke(recievedGhostId, damage, causeOfDeath);
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
    public void EnterDeathStateClientRpc(string recievedGhostId)
    {
        OnEnterDeathState?.Invoke(recievedGhostId);
    }

    [ClientRpc]
    public void TurnGhostEyesRedClientRpc(string recievedGhostId)
    {
        OnGhostEyesTurnRed?.Invoke(recievedGhostId);
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
    public void GrabHarpClientRpc(string recievedGhostId)
    {
        OnGrabHarp?.Invoke(recievedGhostId);
    }

    [ClientRpc]
    public void PlayHarpMusicClientRpc(string recievedGhostId)
    {
        OnPlayHarpMusic?.Invoke(recievedGhostId);
    }
    
    [ClientRpc]
    public void StopHarpMusicClientRpc(string recievedGhostId)
    {
        OnStopHarpMusic?.Invoke(recievedGhostId);
    }
    
    [ServerRpc]
    public void SpawnHarpServerRpc(string recievedGhostId)
    {
        GameObject harpObject = Instantiate(
            HarpGhostPlugin.HarpItem.spawnPrefab,
            harpGhostAIServer.TransformPosition,
            Quaternion.identity,
            harpGhostAIServer.RoundManagerInstance.spawnedScrapContainer);
        
        AudioSource harpAudioSource = harpObject.GetComponent<AudioSource>();
        if (harpAudioSource == null) _mls.LogError("harpAudioSource is null");

        InstrumentBehaviour harpBehaviour = harpObject.GetComponent<InstrumentBehaviour>();
        if (harpBehaviour == null) _mls.LogError("harpBehaviour is null");

        int harpScrapValue = UnityEngine.Random.Range(HarpGhostConfig.Instance.HarpMinValue.Value, HarpGhostConfig.Instance.HarpMaxValue.Value + 1);
        harpObject.GetComponent<GrabbableObject>().fallTime = 0f;
        harpObject.GetComponent<GrabbableObject>().SetScrapValue(harpScrapValue);
        harpGhostAIServer.RoundManagerInstance.totalScrapValueInLevel += harpScrapValue;

        harpObject.GetComponent<NetworkObject>().Spawn();
        SpawnHarpClientRpc(recievedGhostId, harpObject, harpScrapValue);
    }

    [ClientRpc]
    private void SpawnHarpClientRpc(string recievedGhostId, NetworkObjectReference harpObject, int harpScrapValue)
    {
        OnSpawnHarp?.Invoke(recievedGhostId, harpObject, harpScrapValue);
    }

    [ClientRpc]
    public void DropHarpClientRpc(string recievedGhostId, Vector3 targetPosition)
    {
        OnDropHarp?.Invoke(recievedGhostId, targetPosition);
    }
    
    [ClientRpc]
    public void PlayCreatureVoiceClientRpc(string recievedGhostId, int typeIndex, int clipArrayLength, bool interrupt = true)
    {
        int randomNum = UnityEngine.Random.Range(0, clipArrayLength);
        LogDebug($"Invoking OnPlayCreatureVoice | Audio clip index: {typeIndex}, audio clip random number: {randomNum}");
        OnPlayCreatureVoice?.Invoke(recievedGhostId, typeIndex, randomNum, interrupt);
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}