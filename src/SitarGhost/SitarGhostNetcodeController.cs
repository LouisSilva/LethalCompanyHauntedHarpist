using System;
using BepInEx.Logging;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.Serialization;

namespace LethalCompanyHarpGhost.SitarGhost;

public class SitarGhostNetcodeController : NetworkBehaviour
{
    private ManualLogSource _mls;
    
    #pragma warning disable 0649
    [SerializeField] private SitarGhostAIServer sitarGhostAIServer;
    #pragma warning restore 0649
    
    public event Action<string, int> OnDoAnimation;
    public event Action<string, int, bool> OnChangeAnimationParameterBool;
    public event Action<string> OnInitializeConfigValues;
    public event Action<string> OnUpdateGhostIdentifier;

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Netcode Controller");
        
        sitarGhostAIServer = GetComponent<SitarGhostAIServer>();
        if (sitarGhostAIServer == null) _mls.LogError("tubaGhostAI is null");
    }
    
    [ClientRpc]
    public void InitializeConfigValuesClientRpc(string recievedGhostId)
    {
        OnInitializeConfigValues?.Invoke(recievedGhostId);
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
    public void SyncGhostIdentifierClientRpc(string recievedGhostId)
    {
        OnUpdateGhostIdentifier?.Invoke(recievedGhostId);
    }

    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls.LogInfo(msg);
        #endif
    }
}