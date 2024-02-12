using System;
using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostNetcodeController : NetworkBehaviour
{
    #pragma warning disable 0649
    [SerializeField] private HarpGhostAI harpGhostAI;
    #pragma warning restore 0649

    private ManualLogSource _mls;

    [SerializeField] private bool harpGhostNetcodeControllerDebug = true;
    
    public event Action<Vector3> OnTargetPlayerLastSeenPosUpdated;
    public event Action<int> OnSwitchBehaviourState;
    public event Action<int, int, float> OnPlayCreatureVoice;
    public event Action<int> OnBeginChasingPlayer;
    public event Action<Vector3> OnDropHarp;
    public event Action<NetworkObjectReference, int> OnSpawnHarp;
    public event Action OnGrapHarp;
    public event Action<int> OnDoAnimation;
    public event Action<int, bool> OnChangeAnimationParameterBool;
    public event Action<float> OnChangeAgentMaxAcceleration;
    public event Action<float, float> OnChangeAgentMaxSpeed;
    public event Action OnEnterDeathState;

    private void Awake()
    {
        _mls = new ManualLogSource("HarpGhostNetcodeController");
    }

    private void Start()
    {
        harpGhostAI = GetComponent<HarpGhostAI>();
        if (harpGhostAI == null) _mls.LogError("harpGhostAI is null");
    }

    private void LogDebug(string msg)
    {
        if (harpGhostNetcodeControllerDebug) _mls.LogInfo(msg);
    }

    [ServerRpc(RequireOwnership = false)]
    public void EnterDeathStateServerRpc()
    {
        EnterDeathStateClientRpc();
    }

    [ClientRpc]
    private void EnterDeathStateClientRpc()
    {
        OnEnterDeathState?.Invoke();
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ChangeAgentMaxSpeedServerRpc(float newMaxSpeed, float newMaxSpeed2)
    {
        ChangeAgentMaxSpeedClientRpc(newMaxSpeed, newMaxSpeed2);
    }

    [ClientRpc]
    private void ChangeAgentMaxSpeedClientRpc(float newMaxSpeed, float newMaxSpeed2)
    {
        OnChangeAgentMaxSpeed?.Invoke(newMaxSpeed, newMaxSpeed2);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ChangeAgentMaxAccelerationServerRpc(float newAcceleration)
    {
        OnChangeAgentMaxAccelerationClientRpc(newAcceleration);
    }

    [ClientRpc]
    private void OnChangeAgentMaxAccelerationClientRpc(float newAcceleration)
    {
        OnChangeAgentMaxAcceleration?.Invoke(newAcceleration);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ChangeAnimationParameterBoolServerRpc(int animationId, bool value)
    {
        ChangeAnimationParameterBoolClientRpc(animationId, value);
    }

    [ClientRpc]
    private void ChangeAnimationParameterBoolClientRpc(int animationId, bool value)
    {
        OnChangeAnimationParameterBool?.Invoke(animationId, value);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void DoAnimationServerRpc(int animationId)
    {
        DoAnimationClientRpc(animationId);
    }

    [ClientRpc]
    private void DoAnimationClientRpc(int animationId)
    {
        OnDoAnimation?.Invoke(animationId);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void GrabHarpServerRpc()
    {
        GrabHarpClientRpc();
    }

    [ClientRpc]
    private void GrabHarpClientRpc()
    {
        OnGrapHarp?.Invoke();
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void SpawnHarpServerRpc()
    {
        GameObject harpObject = Instantiate(
            HarpGhostPlugin.HarpItem.spawnPrefab,
            harpGhostAI.TransformPosition,
            Quaternion.identity,
            harpGhostAI.RoundManagerInstance.spawnedScrapContainer);
        
        AudioSource harpAudioSource = harpObject.GetComponent<AudioSource>();
        if (harpAudioSource == null) _mls.LogError("harpAudioSource is null");

        HarpBehaviour harpBehaviour = harpObject.GetComponent<HarpBehaviour>();
        if (harpBehaviour == null) _mls.LogError("harpBehaviour is null");

        int harpScrapValue = UnityEngine.Random.Range(150, 301);
        harpObject.GetComponent<GrabbableObject>().fallTime = 0f;
        harpObject.GetComponent<GrabbableObject>().SetScrapValue(harpScrapValue);
        harpGhostAI.RoundManagerInstance.totalScrapValueInLevel += harpScrapValue;

        harpObject.GetComponent<NetworkObject>().Spawn();
        SpawnHarpClientRpc(harpObject, harpScrapValue);
    }

    [ClientRpc]
    private void SpawnHarpClientRpc(NetworkObjectReference harpObject, int harpScrapValue)
    {
        OnSpawnHarp?.Invoke(harpObject, harpScrapValue);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void DropHarpServerRpc(Vector3 targetPosition)
    {
        if (harpGhostAI.HeldHarp == null) return;
        DropHarpClientRpc(targetPosition);
    }

    [ClientRpc]
    private void DropHarpClientRpc(Vector3 targetPosition)
    {
        OnDropHarp?.Invoke(targetPosition);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void BeginChasingPlayerServerRpc(int targetPlayerObjectId)
    {
        BeginChasingPlayerClientRpc(targetPlayerObjectId);
    }

    [ClientRpc]
    private void BeginChasingPlayerClientRpc(int targetPlayerObjectId)
    {
        OnBeginChasingPlayer?.Invoke(targetPlayerObjectId);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void PlayCreatureVoiceServerRpc(int typeIndex, int clipArrayLength, float volume)
    {
        int randomNum = UnityEngine.Random.Range(0, clipArrayLength);
        LogDebug($"Audio clip index: {typeIndex}, audio clip array length: {clipArrayLength}, audio clip random number: {randomNum}");
        PlayCreatureVoiceClientRpc(typeIndex, randomNum, volume);
    }

    [ClientRpc]
    private void PlayCreatureVoiceClientRpc(int typeIndex, int randomNum, float volume)
    {
        LogDebug($"Audio clip index: {typeIndex}, audio clip random number: {randomNum}");
        OnPlayCreatureVoice?.Invoke(typeIndex, randomNum, volume);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SwitchBehaviourStateServerRpc(int state)
    {
        SwitchBehaviourStateClientRpc(state);
    }

    [ClientRpc]
    private void SwitchBehaviourStateClientRpc(int state)
    {
        OnSwitchBehaviourState?.Invoke(state);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void UpdateTargetPlayerLastSeenPosServerRpc(Vector3 targetPlayerPos)
    {
        UpdateTargetPlayerLastSeenPosClientRpc(targetPlayerPos);
    }

    [ClientRpc]
    private void UpdateTargetPlayerLastSeenPosClientRpc(Vector3 targetPlayerPos)
    {
        OnTargetPlayerLastSeenPosUpdated?.Invoke(targetPlayerPos);
    }
}