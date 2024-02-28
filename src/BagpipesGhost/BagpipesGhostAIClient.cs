using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.BagpipesGhost;

public class BagpipesGhostAIClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;
    
    private NetworkObjectReference _instrumentObjectRef;

    private int _instrumentScrapValue;

    private InstrumentBehaviour _heldInstrument;
    
    public Transform grabTarget;
    
    #pragma warning disable 0649
    [SerializeField] private BagpipesGhostNetcodeController netcodeController;
    #pragma warning restore 0649
    

    private void OnEnable()
    {
        netcodeController.OnDropBagpipes += HandleDropInstrument;
        netcodeController.OnSpawnBagpipes += HandleSpawnInstrument;
        netcodeController.OnGrabBagpipes += HandleGrabInstrument;
        netcodeController.OnPlayBagpipesMusic += HandleOnPlayInstrumentMusic;
        netcodeController.OnStopBagpipesMusic += HandleOnStopInstrumentMusic;
        netcodeController.OnUpdateGhostIdentifier += HandleUpdateGhostIdentifier;
    }

    private void OnDestroy()
    {
        netcodeController.OnDropBagpipes -= HandleDropInstrument;
        netcodeController.OnSpawnBagpipes -= HandleSpawnInstrument;
        netcodeController.OnGrabBagpipes -= HandleGrabInstrument;
        netcodeController.OnPlayBagpipesMusic -= HandleOnPlayInstrumentMusic;
        netcodeController.OnStopBagpipesMusic -= HandleOnStopInstrumentMusic;
        netcodeController.OnUpdateGhostIdentifier -= HandleUpdateGhostIdentifier;
    }

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Bagpipes Ghost AI | Client");
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

    private void HandleOnPlayInstrumentMusic(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldInstrument == null) return;
        _heldInstrument.StartMusicServerRpc();
    }
    
    private void HandleOnStopInstrumentMusic(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldInstrument == null) return;
        _heldInstrument.StopMusicServerRpc();
    }

    private void HandleDropInstrument(string recievedGhostId, Vector3 dropPosition)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldInstrument == null) return;
        _heldInstrument.parentObject = null;
        _heldInstrument.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _heldInstrument.EnablePhysics(true);
        _heldInstrument.fallTime = 0f;
        
        Transform parent;
        _heldInstrument.startFallingPosition =
            (parent = _heldInstrument.transform.parent).InverseTransformPoint(_heldInstrument.transform.position);
        _heldInstrument.targetFloorPosition = parent.InverseTransformPoint(dropPosition);
        _heldInstrument.floorYRot = -1;
        _heldInstrument.grabbable = true;
        _heldInstrument.grabbableToEnemies = true;
        _heldInstrument.isHeld = false;
        _heldInstrument.isHeldByEnemy = false;
        _heldInstrument.StopMusicServerRpc();
        _heldInstrument = null;
    }

    private void HandleGrabInstrument(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldInstrument != null) return;
        if (!_instrumentObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldInstrument = networkObject.gameObject.GetComponent<InstrumentBehaviour>();

        _heldInstrument.SetScrapValue(_instrumentScrapValue);
        _heldInstrument.parentObject = grabTarget;
        _heldInstrument.isHeldByEnemy = true;
        _heldInstrument.grabbableToEnemies = false;
        _heldInstrument.grabbable = false;
    }
    
    private void HandleSpawnInstrument(string recievedGhostId, NetworkObjectReference instrumentObject, int instrumentScrapValue)
    {
        if (_ghostId != recievedGhostId) return;
        _instrumentObjectRef = instrumentObject;
        _instrumentScrapValue = instrumentScrapValue;
    }
}