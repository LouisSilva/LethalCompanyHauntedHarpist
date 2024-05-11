using System.Collections;
using System.Diagnostics.CodeAnalysis;
using BepInEx.Logging;
using GameNetcodeStuff;
using LethalCompanyHarpGhost.Items;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

[SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
public class HarpGhostAIClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;
    
    private NetworkObjectReference _instrumentObjectRef;

    private int _instrumentScrapValue;

    private InstrumentBehaviour _heldInstrument;

    private PlayerControllerB _targetPlayer;
    
    #pragma warning disable 0649
    [SerializeField] private Transform grabTarget;
    [SerializeField] private Transform eye;
    
    [SerializeField] private HarpGhostNetcodeController netcodeController;
    
    [Header("Materials and Renderers")]
    [Space(3f)]
    [SerializeField] private bool enableGhostAngryModel = true;
    [SerializeField] private Renderer rendererLeftEye;
    [SerializeField] private Renderer rendererRightEye;
    [SerializeField] private MaterialPropertyBlock _propertyBlock;
    
    private bool _isTransitioningMaterial = false;
    
    private float _transitioningMaterialTimer = 0f;
    
    private static readonly int AlternativeColourFadeInTimer = Shader.PropertyToID("_AlternativeColourFadeInTimer");
    #pragma warning restore 0649
    

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

    private void OnDestroy()
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
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Harp Ghost AI {_ghostId} | Client");
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
            
            if (_transitioningMaterialTimer >= 5f)
            {
                yield break;
            }

            LogDebug($"transition material timer: {_transitioningMaterialTimer}");
            yield return new WaitForSeconds(0.01f);
        }
    }

    private void HandleIncreaseTargetPlayerFearLevel(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (GameNetworkManager.Instance.localPlayerController != _targetPlayer) return;
        
        if (_targetPlayer == null)
        {
            return;
        }
        
        if (_targetPlayer.HasLineOfSightToPosition(eye.position, 115f, 50, 3f))
        {
            _targetPlayer.JumpToFearLevel(1);
            _targetPlayer.IncreaseFearLevelOverTime(0.8f);
        }
        
        else if (Vector3.Distance(eye.transform.position, _targetPlayer.transform.position) < 3)
        {
            _targetPlayer.JumpToFearLevel(0.6f);
            _targetPlayer.IncreaseFearLevelOverTime(0.4f);
        }
    }

    private void HandleOnPlayInstrumentMusic(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (_heldInstrument == null) return;
        _heldInstrument.StartMusicServerRpc();
    }
    
    private void HandleOnStopInstrumentMusic(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (_heldInstrument == null) return;
        _heldInstrument.StopMusicServerRpc();
    }

    private void HandleDropInstrument(string receivedGhostId, Vector3 dropPosition)
    {
        if (_ghostId != receivedGhostId) return;
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

    private void HandleGrabInstrument(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (_heldInstrument != null) return;
        if (!_instrumentObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldInstrument = networkObject.gameObject.GetComponent<InstrumentBehaviour>();

        _heldInstrument.SetScrapValue(_instrumentScrapValue);
        _heldInstrument.parentObject = grabTarget;
        _heldInstrument.isHeldByEnemy = true;
        _heldInstrument.grabbableToEnemies = false;
        _heldInstrument.grabbable = false;
    }
    
    private void HandleSpawnInstrument(string receivedGhostId, NetworkObjectReference instrumentObject, int instrumentScrapValue)
    {
        if (_ghostId != receivedGhostId) return;
        _instrumentObjectRef = instrumentObject;
        _instrumentScrapValue = instrumentScrapValue;
    }

    private void HandleChangeTargetPlayer(string receivedGhostId, int targetPlayerObjectId)
    {
        if (_ghostId != receivedGhostId) return;
        if (targetPlayerObjectId == -69420)
        {
            _targetPlayer = null;
            return;
        }
        
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        _targetPlayer = player;
    }

    private void HandleDamageTargetPlayer(string receivedGhostId, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        if (_ghostId != receivedGhostId) return;
        _targetPlayer.DamagePlayer(damage, causeOfDeath: causeOfDeath);
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}