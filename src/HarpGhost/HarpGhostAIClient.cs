using BepInEx.Logging;
using GameNetcodeStuff;
using LethalCompanyHarpGhost.Items;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAIClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;
    
    private static readonly int AlternativeColourFadeInTimer = Shader.PropertyToID("_AlternativeColourFadeInTimer");
    
#pragma warning disable 0649
    [SerializeField] private Transform grabTarget;
    [SerializeField] private Transform eye;
    
    [SerializeField] private HarpGhostNetcodeController netcodeController;
    
    [Header("Materials and Renderers")] [Space(3f)]
    [SerializeField] private bool enableGhostAngryModel = true;
    [SerializeField] private Renderer rendererLeftEye;
    [SerializeField] private Renderer rendererRightEye;
    [SerializeField] private MaterialPropertyBlock _propertyBlock;
#pragma warning restore 0649
    
    private readonly NullableObject<InstrumentBehaviour> _heldInstrument = new();
    
    private readonly NullableObject<PlayerControllerB> _targetPlayer = new();
    
    private NetworkObjectReference _instrumentObjectRef;
    
    private bool _isTransitioningMaterial;
    
    private float _transitioningMaterialTimer;

    private int _instrumentScrapValue;
    

    private void OnEnable()
    {
        netcodeController.OnDropHarp += HandleDropInstrument;
        netcodeController.OnSpawnHarp += HandleSpawnInstrument;
        netcodeController.OnGrabHarp += HandleGrabInstrument;
        netcodeController.OnPlayHarpMusic += HandleOnPlayInstrumentMusic;
        netcodeController.OnStopHarpMusic += HandleOnStopInstrumentMusic;
        netcodeController.OnChangeTargetPlayer += HandleChangeTargetPlayer;
        netcodeController.OnDamageTargetPlayer += HandleDamageTargetPlayer;
        netcodeController.OnIncreaseTargetPlayerFearLevel += HandleIncreaseTargetPlayerFearLevel;
        netcodeController.OnUpdateGhostIdentifier += HandleUpdateGhostIdentifier;
        netcodeController.OnGhostEyesTurnRed += HandleGhostEyesTurnRed;
    }

    private void OnDisable()
    {
        netcodeController.OnDropHarp -= HandleDropInstrument;
        netcodeController.OnSpawnHarp -= HandleSpawnInstrument;
        netcodeController.OnGrabHarp -= HandleGrabInstrument;
        netcodeController.OnPlayHarpMusic -= HandleOnPlayInstrumentMusic;
        netcodeController.OnStopHarpMusic -= HandleOnStopInstrumentMusic;
        netcodeController.OnChangeTargetPlayer -= HandleChangeTargetPlayer;
        netcodeController.OnDamageTargetPlayer -= HandleDamageTargetPlayer;
        netcodeController.OnIncreaseTargetPlayerFearLevel -= HandleIncreaseTargetPlayerFearLevel;
        netcodeController.OnUpdateGhostIdentifier -= HandleUpdateGhostIdentifier;
        netcodeController.OnGhostEyesTurnRed -= HandleGhostEyesTurnRed;
    }

    private void Start()
    {
        _mls = Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Harp Ghost AI {_ghostId} | Client");
        _propertyBlock = new MaterialPropertyBlock();
        
        InitializeConfigValues();
    }

    private void InitializeConfigValues()
    {
        enableGhostAngryModel = HarpGhostConfig.Default.HarpGhostAngryEyesEnabled.Value;
    }

    private void HandleUpdateGhostIdentifier(string receivedGhostId)
    {
        _ghostId = receivedGhostId;
    }

    private void HandleGhostEyesTurnRed(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (enableGhostAngryModel && !_isTransitioningMaterial) StartCoroutine(GhostEyesTurnRed());
    }

    private IEnumerator GhostEyesTurnRed()
    {
        _isTransitioningMaterial = true;
        _transitioningMaterialTimer = 0;
        
        while (true)
        {
            _transitioningMaterialTimer += Time.deltaTime;
            float transitionValue = Mathf.Clamp01(_transitioningMaterialTimer / 5f);
            
            rendererLeftEye.GetPropertyBlock(_propertyBlock);
            rendererRightEye.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(AlternativeColourFadeInTimer, transitionValue);
            rendererLeftEye.SetPropertyBlock(_propertyBlock);
            rendererRightEye.SetPropertyBlock(_propertyBlock);
            
            if (_transitioningMaterialTimer >= 5f) yield break;

            LogDebug($"Transition material timer: {_transitioningMaterialTimer}");
            yield return new WaitForSeconds(0.01f);
        }
    }

    private void HandleIncreaseTargetPlayerFearLevel(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (!_targetPlayer.IsNotNull || GameNetworkManager.Instance.localPlayerController != _targetPlayer.Value) return;
        
        if (_targetPlayer.Value.HasLineOfSightToPosition(eye.position, 115f, 50, 3f))
        {
            _targetPlayer.Value.JumpToFearLevel(1);
            _targetPlayer.Value.IncreaseFearLevelOverTime(0.8f);
        }
        
        else if (Vector3.Distance(eye.transform.position, _targetPlayer.Value.transform.position) < 3)
        {
            _targetPlayer.Value.JumpToFearLevel(0.6f);
            _targetPlayer.Value.IncreaseFearLevelOverTime(0.4f);
        }
    }

    private void HandleOnPlayInstrumentMusic(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId || !_heldInstrument.IsNotNull) return;
        _heldInstrument.Value.StartMusicServerRpc();
    }
    
    private void HandleOnStopInstrumentMusic(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId || !_heldInstrument.IsNotNull) return;
        _heldInstrument.Value.StopMusicServerRpc();
    }

    private void HandleDropInstrument(string receivedGhostId, Vector3 dropPosition)
    {
        if (_ghostId != receivedGhostId || !_heldInstrument.IsNotNull) return;
        _heldInstrument.Value.parentObject = null;
        _heldInstrument.Value.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _heldInstrument.Value.EnablePhysics(true);
        _heldInstrument.Value.fallTime = 0f;
        
        Transform parent;
        _heldInstrument.Value.startFallingPosition =
            (parent = _heldInstrument.Value.transform.parent).InverseTransformPoint(_heldInstrument.Value.transform.position);
        _heldInstrument.Value.targetFloorPosition = parent.InverseTransformPoint(dropPosition);
        _heldInstrument.Value.floorYRot = -1;
        _heldInstrument.Value.grabbable = true;
        _heldInstrument.Value.grabbableToEnemies = true;
        _heldInstrument.Value.isHeld = false;
        _heldInstrument.Value.isHeldByEnemy = false;
        _heldInstrument.Value.StopMusicServerRpc();
        _heldInstrument.Value = null;
    }

    private void HandleGrabInstrument(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId || !_heldInstrument.IsNotNull) return;
        if (!_instrumentObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldInstrument.Value = networkObject.gameObject.GetComponent<InstrumentBehaviour>();

        _heldInstrument.Value.SetScrapValue(_instrumentScrapValue);
        _heldInstrument.Value.parentObject = grabTarget;
        _heldInstrument.Value.isHeldByEnemy = true;
        _heldInstrument.Value.grabbableToEnemies = false;
        _heldInstrument.Value.grabbable = false;
    }
    
    private void HandleSpawnInstrument(string receivedGhostId, NetworkObjectReference instrumentObject, int instrumentScrapValue)
    {
        if (_ghostId != receivedGhostId) return;
        _instrumentObjectRef = instrumentObject;
        _instrumentScrapValue = instrumentScrapValue;
    }

    private void HandleChangeTargetPlayer(string receivedGhostId, ulong targetPlayerObjectId)
    {
        if (_ghostId != receivedGhostId) return;
        if (targetPlayerObjectId == 69420)
        {
            _targetPlayer.Value = null;
            return;
        }
        
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        _targetPlayer.Value = player;
    }

    private void HandleDamageTargetPlayer(string receivedGhostId, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        if (_ghostId != receivedGhostId) return;
        _targetPlayer.Value.DamagePlayer(damage, causeOfDeath: causeOfDeath);
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}