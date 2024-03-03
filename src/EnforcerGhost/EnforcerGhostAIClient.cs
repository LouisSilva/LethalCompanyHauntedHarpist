using BepInEx.Logging;
using UnityEngine;

namespace LethalCompanyHarpGhost.EnforcerGhost;

public class EnforcerGhostAIClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;
    
    public Transform grabTarget;
    
    #pragma warning disable 0649
    [SerializeField] private EnforcerGhostNetcodeController netcodeController;
    #pragma warning restore 0649
    

    private void OnEnable()
    {
        netcodeController.OnUpdateGhostIdentifier += HandleUpdateGhostIdentifier;
    }

    private void OnDestroy()
    {
        netcodeController.OnUpdateGhostIdentifier -= HandleUpdateGhostIdentifier;
    }

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Enforcer Ghost AI {_ghostId} | Client");
    }

    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls.LogInfo(msg);
        #endif
    }

    private void HandleUpdateGhostIdentifier(string recievedGhostId)
    {
        _ghostId = recievedGhostId;
    }
}