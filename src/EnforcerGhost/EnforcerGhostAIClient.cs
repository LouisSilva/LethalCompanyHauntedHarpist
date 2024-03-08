using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace LethalCompanyHarpGhost.EnforcerGhost;

/*



*/

public class EnforcerGhostAIClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;

    private NetworkObjectReference _shotgunObjectRef;

    private ShotgunItem _heldShotgun;

    private int _shotgunScrapValue;
    
    #pragma warning disable 0649
    [SerializeField] private Transform grabTarget;
    
    [SerializeField] private EnforcerGhostNetcodeController netcodeController;
    [SerializeField] private EnforcerGhostAIServer enforcerGhostAIServer;
    #pragma warning restore 0649
    
    private void OnEnable()
    {
        netcodeController.OnUpdateGhostIdentifier += HandleUpdateGhostIdentifier;
        netcodeController.OnSpawnShotgun += HandleSpawnShotgun;
        netcodeController.OnGrabShotgunPhaseTwo += HandleGrabShotgunPhaseTwo;
        netcodeController.OnDropShotgun += HandleDropShotgun;
    }

    private void OnDestroy()
    {
        netcodeController.OnUpdateGhostIdentifier -= HandleUpdateGhostIdentifier;
        netcodeController.OnSpawnShotgun -= HandleSpawnShotgun;
        netcodeController.OnGrabShotgunPhaseTwo -= HandleGrabShotgunPhaseTwo;
        netcodeController.OnDropShotgun -= HandleDropShotgun;
    }

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Enforcer Ghost AI {_ghostId} | Client");
    }

    private void HandleSpawnShotgun(string recievedGhostId, NetworkObjectReference shotgunObject, int shotgunScrapValue)
    {
        if (_ghostId != recievedGhostId) return;
        _shotgunObjectRef = shotgunObject;
        _shotgunScrapValue = shotgunScrapValue;
    }

    private void HandleGrabShotgunPhaseTwo(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldShotgun != null) return;
        if (!_shotgunObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldShotgun = networkObject.gameObject.GetComponent<ShotgunItem>();

        _heldShotgun.SetScrapValue(_shotgunScrapValue);
        _heldShotgun.itemProperties.rotationOffset = new Vector3(-180f, 180f, -90f);
        _heldShotgun.parentObject = grabTarget;
        _heldShotgun.isHeldByEnemy = true;
        _heldShotgun.grabbableToEnemies = false;
        _heldShotgun.grabbable = false;
        _heldShotgun.shellsLoaded = 2;
        _heldShotgun.GrabItemFromEnemy(enforcerGhostAIServer);
    }
    
    private void HandleDropShotgun(string recievedGhostId, Vector3 dropPosition)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldShotgun == null) return;
        _heldShotgun.parentObject = null;
        _heldShotgun.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _heldShotgun.itemProperties.rotationOffset = new Vector3(-90.89f, -1.5f, 0f);
        _heldShotgun.EnablePhysics(true);
        _heldShotgun.fallTime = 0f;
        
        Transform parent;
        _heldShotgun.startFallingPosition =
            (parent = _heldShotgun.transform.parent).InverseTransformPoint(_heldShotgun.transform.position);
        _heldShotgun.targetFloorPosition = parent.InverseTransformPoint(dropPosition);
        _heldShotgun.floorYRot = -1;
        _heldShotgun.grabbable = true;
        _heldShotgun.grabbableToEnemies = true;
        _heldShotgun.isHeld = false;
        _heldShotgun.isHeldByEnemy = false;
        _heldShotgun = null;
    }

    private void HandleUpdateGhostIdentifier(string recievedGhostId)
    {
        _ghostId = recievedGhostId;
    } 
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls.LogInfo(msg);
        #endif
    }
}