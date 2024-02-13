using System;
using System.Collections;
using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAIClient : MonoBehaviour
{
    private readonly ManualLogSource _mls = BepInEx.Logging.Logger.CreateLogSource("Harp Ghost AI | Client");

    private NetworkObjectReference _harpObjectRef;

    private int _harpScrapValue;

    private HarpBehaviour _heldHarp;
    
    public Transform grabTarget;
    

    private void OnEnable()
    {
        HarpGhostNetcodeController.OnDropHarp += HandleDropHarp;
        HarpGhostNetcodeController.OnSpawnHarp += HandleSpawnHarp;
        HarpGhostNetcodeController.OnGrabHarp += HandleGrabHarp;
        HarpGhostNetcodeController.OnPlayHarpMusic += HandleOnPlayHarpMusic;
        HarpGhostNetcodeController.OnStopHarpMusic += HandleOnStopHarpMusic;
    }

    private void OnDestroy()
    {
        HarpGhostNetcodeController.OnDropHarp -= HandleDropHarp;
        HarpGhostNetcodeController.OnSpawnHarp -= HandleSpawnHarp;
        HarpGhostNetcodeController.OnGrabHarp -= HandleGrabHarp;
        HarpGhostNetcodeController.OnPlayHarpMusic -= HandleOnPlayHarpMusic;
        HarpGhostNetcodeController.OnStopHarpMusic -= HandleOnStopHarpMusic;
    }

    private void HandleOnPlayHarpMusic()
    {
        if (_heldHarp == null) return;
        _heldHarp.StartMusicServerRpc();
    }
    
    private void HandleOnStopHarpMusic()
    {
        if (_heldHarp == null) return;
        _heldHarp.StopMusicServerRpc();
    }

    private void HandleDropHarp(Vector3 dropPosition)
    {
        if (_heldHarp == null) return;
        _heldHarp.parentObject = null;
        _heldHarp.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _heldHarp.EnablePhysics(true);
        _heldHarp.fallTime = 0f;
        
        Transform parent;
        _heldHarp.startFallingPosition =
            (parent = _heldHarp.transform.parent).InverseTransformPoint(_heldHarp.transform.position);
        _heldHarp.targetFloorPosition = parent.InverseTransformPoint(dropPosition);
        _heldHarp.floorYRot = -1;
        _heldHarp.grabbable = true;
        _heldHarp.grabbableToEnemies = true;
        _heldHarp.isHeld = false;
        _heldHarp.isHeldByEnemy = false;
        _heldHarp.StopMusicServerRpc();
        _heldHarp = null;
    }

    private void HandleGrabHarp()
    {
        if (_heldHarp != null) return;
        if (!_harpObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldHarp = networkObject.gameObject.GetComponent<HarpBehaviour>();

        _heldHarp.SetScrapValue(_harpScrapValue);
        _heldHarp.parentObject = grabTarget;
        _heldHarp.isHeldByEnemy = true;
        _heldHarp.grabbableToEnemies = false;
        _heldHarp.grabbable = false;
    }
    
    private void HandleSpawnHarp(NetworkObjectReference harpObject, int harpScrapValue)
    {
        _harpObjectRef = harpObject;
        _harpScrapValue = harpScrapValue;
    }
}