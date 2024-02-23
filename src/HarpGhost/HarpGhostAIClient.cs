using System;
using System.Diagnostics.CodeAnalysis;
using BepInEx.Logging;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

[SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
public class HarpGhostAIClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;
    
    private NetworkObjectReference _harpObjectRef;

    private int _harpScrapValue;

    private InstrumentBehaviour _heldHarp;

    private PlayerControllerB _targetPlayer;
    
    public Transform grabTarget;

    public Transform eye;
    
    #pragma warning disable 0649
    [SerializeField] private HarpGhostNetcodeController netcodeController;
    #pragma warning restore 0649
    

    private void OnEnable()
    {
        netcodeController.OnDropHarp += HandleDropHarp;
        netcodeController.OnSpawnHarp += HandleSpawnHarp;
        netcodeController.OnGrabHarp += HandleGrabHarp;
        netcodeController.OnPlayHarpMusic += HandleOnPlayHarpMusic;
        netcodeController.OnStopHarpMusic += HandleOnStopHarpMusic;
        netcodeController.OnChangeTargetPlayer += HandleChangeTargetPlayer;
        netcodeController.OnDamageTargetPlayer += HandleDamageTargetPlayer;
        netcodeController.OnIncreaseTargetPlayerFearLevel += HandleIncreaseTargetPlayerFearLevel;
        netcodeController.OnUpdateGhostIdentifier += HandleUpdateGhostIdentifier;
    }

    private void OnDestroy()
    {
        netcodeController.OnDropHarp -= HandleDropHarp;
        netcodeController.OnSpawnHarp -= HandleSpawnHarp;
        netcodeController.OnGrabHarp -= HandleGrabHarp;
        netcodeController.OnPlayHarpMusic -= HandleOnPlayHarpMusic;
        netcodeController.OnStopHarpMusic -= HandleOnStopHarpMusic;
        netcodeController.OnChangeTargetPlayer -= HandleChangeTargetPlayer;
        netcodeController.OnDamageTargetPlayer -= HandleDamageTargetPlayer;
        netcodeController.OnIncreaseTargetPlayerFearLevel -= HandleIncreaseTargetPlayerFearLevel;
        netcodeController.OnUpdateGhostIdentifier -= HandleUpdateGhostIdentifier;
    }

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Harp Ghost AI | Client");
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

    private void HandleIncreaseTargetPlayerFearLevel(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (GameNetworkManager.Instance.localPlayerController != _targetPlayer) return; LogDebug("localplayercontroller is not target player");
        
        if (_targetPlayer == null)
        {
            return;
        }
        
        LogDebug($"Increasing fear level for {_targetPlayer.name}");
        if (_targetPlayer.HasLineOfSightToPosition(eye.position, 115f, 50, 3f))
        {
            _targetPlayer.JumpToFearLevel(1);
            _targetPlayer.IncreaseFearLevelOverTime(0.8f);;
        }
        
        else if (Vector3.Distance(eye.transform.position, _targetPlayer.transform.position) < 3)
        {
            _targetPlayer.JumpToFearLevel(0.6f);
            _targetPlayer.IncreaseFearLevelOverTime(0.4f);;
        }
    }

    private void HandleOnPlayHarpMusic(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldHarp == null) return;
        _heldHarp.StartMusicServerRpc();
    }
    
    private void HandleOnStopHarpMusic(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldHarp == null) return;
        _heldHarp.StopMusicServerRpc();
    }

    private void HandleDropHarp(string recievedGhostId, Vector3 dropPosition)
    {
        if (_ghostId != recievedGhostId) return;
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

    private void HandleGrabHarp(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldHarp != null) return;
        if (!_harpObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldHarp = networkObject.gameObject.GetComponent<InstrumentBehaviour>();

        _heldHarp.SetScrapValue(_harpScrapValue);
        _heldHarp.parentObject = grabTarget;
        _heldHarp.isHeldByEnemy = true;
        _heldHarp.grabbableToEnemies = false;
        _heldHarp.grabbable = false;
    }
    
    private void HandleSpawnHarp(string recievedGhostId, NetworkObjectReference harpObject, int harpScrapValue)
    {
        if (_ghostId != recievedGhostId) return;
        _harpObjectRef = harpObject;
        _harpScrapValue = harpScrapValue;
    }

    private void HandleChangeTargetPlayer(string recievedGhostId, int targetPlayerObjectId)
    {
        if (_ghostId != recievedGhostId) return;
        if (targetPlayerObjectId == -69420)
        {
            _targetPlayer = null;
            return;
        }
        
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        _targetPlayer = player;
        LogDebug($"Target player is now: {player.name}");
    }

    private void HandleDamageTargetPlayer(string recievedGhostId, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        if (_ghostId != recievedGhostId) return;
        _targetPlayer.DamagePlayer(damage, causeOfDeath: causeOfDeath);
    }
}