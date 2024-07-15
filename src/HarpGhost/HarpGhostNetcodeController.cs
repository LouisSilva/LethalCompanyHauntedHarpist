using System;
using BepInEx.Logging;
using LethalCompanyHarpGhost.Items;
using System.Diagnostics.CodeAnalysis;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

[SuppressMessage("ReSharper", "Unity.RedundantHideInInspectorAttribute")]
public class HarpGhostNetcodeController : NetworkBehaviour
{
    private ManualLogSource _mls;
    
    public event Action<string, int> OnSetAnimationTrigger;
    public event Action<string> OnEnterDeathState;
    public event Action<string> OnGrabHarp;
    public event Action<string, NetworkObjectReference, int> OnSpawnHarp;
    public event Action<string, Vector3> OnDropHarp;
    public event Action<string> OnPlayHarpMusic;
    public event Action<string> OnStopHarpMusic;
    public event Action<string, int, CauseOfDeath> OnDamageTargetPlayer;
    public event Action<string, float, float> OnChangeAgentMaxSpeed;
    public event Action<string> OnFixAgentSpeedAfterAttack;
    public event Action<string> OnIncreaseTargetPlayerFearLevel;
    public event Action<string> OnSyncGhostIdentifier;
    public event Action<string> OnGhostEyesTurnRed;
    public event Action<string, HarpGhostClient.AudioClipTypes, int, bool> OnPlayAudioClipType;
    
    [HideInInspector] public readonly NetworkVariable<ulong> TargetPlayerClientId = new();
    [HideInInspector] public readonly NetworkVariable<int> CurrentBehaviourStateIndex = new();
    
    [HideInInspector] public readonly NetworkVariable<bool> AnimationParamStunned = new();
    [HideInInspector] public readonly NetworkVariable<bool> AnimationParamDead = new();

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Harp Ghost Netcode Controller");
    }

    [ClientRpc]
    public void SyncGhostIdentifierClientRpc(string receivedGhostId)
    {
        OnSyncGhostIdentifier?.Invoke(receivedGhostId);
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
    public void DamageTargetPlayerClientRpc(string receivedGhostId, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        OnDamageTargetPlayer?.Invoke(receivedGhostId, damage, causeOfDeath);
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
    public void SetAnimationTriggerClientRpc(string receivedGhostId, int animationId)
    {
        OnSetAnimationTrigger?.Invoke(receivedGhostId, animationId);
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
            transform.position,
            Quaternion.identity,
            RoundManager.Instance.spawnedScrapContainer);

        InstrumentBehaviour harpBehaviour = harpObject.GetComponent<InstrumentBehaviour>();
        if (harpBehaviour == null) _mls.LogError("harpBehaviour is null");

        int harpScrapValue = UnityEngine.Random.Range(HarpGhostConfig.Instance.HarpMinValue.Value, HarpGhostConfig.Instance.HarpMaxValue.Value + 1);
        harpObject.GetComponent<GrabbableObject>().fallTime = 0f;
        harpObject.GetComponent<GrabbableObject>().SetScrapValue(harpScrapValue);
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
    
    [ServerRpc]
    public void PlayAudioClipTypeServerRpc(string receivedGhostId, HarpGhostClient.AudioClipTypes audioClipType, bool interrupt = false)
    {
        HarpGhostClient ghostClient = GetComponent<HarpGhostClient>();
        if (ghostClient == null)
        {
            _mls.LogError("Harpist client was null, cannot play audio clip");
            return;
        }
        
        int numberOfAudioClips = audioClipType switch
        {
            HarpGhostClient.AudioClipTypes.Death => ghostClient.dieSfx.Length,
            HarpGhostClient.AudioClipTypes.Damage => ghostClient.damageSfx.Length,
            HarpGhostClient.AudioClipTypes.Laugh => ghostClient.laughSfx.Length,
            HarpGhostClient.AudioClipTypes.Stun => ghostClient.stunSfx.Length,
            HarpGhostClient.AudioClipTypes.Upset => ghostClient.upsetSfx.Length,
            HarpGhostClient.AudioClipTypes.Hit => ghostClient.hitSfx.Length,
            _ => -1
        };

        switch (numberOfAudioClips)
        {
            case 0:
                _mls.LogError($"There are no audio clips for audio clip type {audioClipType}.");
                return;
            
            case -1:
                _mls.LogError($"Audio Clip Type was not listed, cannot play audio clip. Number of audio clips: {numberOfAudioClips}.");
                return;
            
            default:
            {
                int clipIndex = UnityEngine.Random.Range(0, numberOfAudioClips);
                PlayAudioClipTypeClientRpc(receivedGhostId, audioClipType, clipIndex, interrupt);
                break;
            }
        }
    }

    [ClientRpc]
    private void PlayAudioClipTypeClientRpc(
        string receivedGhostId, 
        HarpGhostClient.AudioClipTypes audioClipType, 
        int clipIndex, 
        bool interrupt = false)
    {
        OnPlayAudioClipType?.Invoke(receivedGhostId, audioClipType, clipIndex, interrupt);
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}