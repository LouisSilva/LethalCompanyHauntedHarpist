using System;
using System.Collections;
using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace LethalCompanyHarpGhost.HarpGhost;

// For things involving more than just changing 1 variable, they are done with rpcs instead of network vars

public class HarpGhostNetcodeController : NetworkBehaviour
{
    #pragma warning disable 0649
    [FormerlySerializedAs("harpGhostAI")] [SerializeField] private HarpGhostAIServer harpGhostAIServer;
    #pragma warning restore 0649

    private readonly ManualLogSource _mls = BepInEx.Logging.Logger.CreateLogSource("Harp Ghost Netcode Controller");

    [SerializeField] private bool harpGhostNetcodeControllerDebug = true;
    
    public event Action<int> OnDoAnimation;
    public event Action<int, bool> OnChangeAnimationParameterBool;
    public static event Action<int, int, float, bool> OnPlayCreatureVoice;
    public static event Action OnEnterDeathState;
    public static event Action OnGrabHarp;
    public static event Action<NetworkObjectReference, int> OnSpawnHarp;
    public static event Action<Vector3> OnDropHarp;
    public static event Action OnPlayHarpMusic;
    public static event Action OnStopHarpMusic;

    private void Start()
    {
        harpGhostAIServer = GetComponent<HarpGhostAIServer>();
        if (harpGhostAIServer == null) _mls.LogError("harpGhostAI is null");
    }

    private void LogDebug(string msg)
    {
        if (harpGhostNetcodeControllerDebug) _mls.LogInfo(msg);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ChangeMaxAccelerationServerRpc(float multiplier)
    {
        harpGhostAIServer.AgentMaxAcceleration *= multiplier;
    }

    [ServerRpc(RequireOwnership = false)]
    public void FixAgentSpeedAfterAttackServerRpc()
    {
        LogDebug("FixAgentSpeedAfterAttackServerRpc called");
        float newMaxSpeed, newMaxSpeed2;
        switch (harpGhostAIServer.CurrentBehaviourStateIndex)
        {
            case 0:
                newMaxSpeed = 0.3f;
                newMaxSpeed2 = 0.3f;
                break;
            case 1:
                newMaxSpeed = 3f;
                newMaxSpeed2 = 1f;
                break;
            case 2:
                newMaxSpeed = 6f;
                newMaxSpeed2 = 1f;
                break;
            case 3:
                newMaxSpeed = 8f;
                newMaxSpeed2 = 1f;
                break;
            default:
                newMaxSpeed = 3f;
                newMaxSpeed2 = 1f;
                break;
        }

        harpGhostAIServer.agent.speed = newMaxSpeed;
        harpGhostAIServer.AgentMaxSpeed = newMaxSpeed2;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ChangeAgentMaxSpeedServerRpc(float newMaxSpeed, float newMaxSpeed2)
    {
        harpGhostAIServer.agent.speed = newMaxSpeed;
        harpGhostAIServer.AgentMaxSpeed = newMaxSpeed2;
    }

    [ServerRpc(RequireOwnership = false)]
    public void DamageTargetPlayerServerRpc(int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        LogDebug("DamageTargetPlayerServerRpc called");
        harpGhostAIServer.DamageTargetPlayer(damage, causeOfDeath: causeOfDeath);
    }

    [ClientRpc]
    public void EnterDeathStateClientRpc()
    {
        OnEnterDeathState?.Invoke();
    }

    [ClientRpc]
    public void ChangeAnimationParameterBoolClientRpc(int animationId, bool value)
    {
        OnChangeAnimationParameterBool?.Invoke(animationId, value);
    }

    [ClientRpc]
    public void DoAnimationClientRpc(int animationId)
    {
        OnDoAnimation?.Invoke(animationId);
    }

    [ClientRpc]
    public void GrabHarpClientRpc()
    {
        OnGrabHarp?.Invoke();
    }

    [ClientRpc]
    public void PlayHarpMusicClientRpc()
    {
        OnPlayHarpMusic?.Invoke();
    }
    
    [ClientRpc]
    public void StopHarpMusicClientRpc()
    {
        OnStopHarpMusic?.Invoke();
    }
    
    [ServerRpc]
    public void SpawnHarpServerRpc()
    {
        GameObject harpObject = Instantiate(
            HarpGhostPlugin.HarpItem.spawnPrefab,
            harpGhostAIServer.TransformPosition,
            Quaternion.identity,
            harpGhostAIServer.RoundManagerInstance.spawnedScrapContainer);
        
        AudioSource harpAudioSource = harpObject.GetComponent<AudioSource>();
        if (harpAudioSource == null) _mls.LogError("harpAudioSource is null");

        HarpBehaviour harpBehaviour = harpObject.GetComponent<HarpBehaviour>();
        if (harpBehaviour == null) _mls.LogError("harpBehaviour is null");

        int harpScrapValue = UnityEngine.Random.Range(150, 301);
        harpObject.GetComponent<GrabbableObject>().fallTime = 0f;
        harpObject.GetComponent<GrabbableObject>().SetScrapValue(harpScrapValue);
        harpGhostAIServer.RoundManagerInstance.totalScrapValueInLevel += harpScrapValue;

        harpObject.GetComponent<NetworkObject>().Spawn();
        SpawnHarpClientRpc(harpObject, harpScrapValue);
    }

    [ClientRpc]
    private void SpawnHarpClientRpc(NetworkObjectReference harpObject, int harpScrapValue)
    {
        OnSpawnHarp?.Invoke(harpObject, harpScrapValue);
    }

    [ClientRpc]
    public void DropHarpClientRpc(Vector3 targetPosition)
    {
        OnDropHarp?.Invoke(targetPosition);
    }
    
    [ClientRpc]
    public void PlayCreatureVoiceClientRpc(int typeIndex, int clipArrayLength, float volume = 1f, bool interrupt = true)
    {
        int randomNum = UnityEngine.Random.Range(0, clipArrayLength);
        LogDebug($"Invoking OnPlayCreatureVoice | Audio clip index: {typeIndex}, audio clip random number: {randomNum}");
        OnPlayCreatureVoice?.Invoke(typeIndex, randomNum, volume, interrupt);
    }
}