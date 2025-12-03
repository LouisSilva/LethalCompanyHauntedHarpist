using GameNetcodeStuff;
using LethalCompanyHarpGhost.Items;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAIClient : MonoBehaviour
{
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

    private InstrumentBehaviour _heldInstrument;

    private PlayerControllerB _targetPlayer;

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
        netcodeController.OnGhostEyesTurnRed -= HandleGhostEyesTurnRed;
    }

    private void Start()
    {
        _propertyBlock = new MaterialPropertyBlock();

        InitializeConfigValues();
    }

    private void InitializeConfigValues()
    {
        enableGhostAngryModel = HarpGhostConfig.Default.HarpGhostAngryEyesEnabled.Value;
    }

    private void HandleGhostEyesTurnRed()
    {
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
            yield return new WaitForSeconds(0.01f);
        }
    }

    private void HandleIncreaseTargetPlayerFearLevel()
    {
        if (!_targetPlayer || GameNetworkManager.Instance.localPlayerController != _targetPlayer) return;

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

    private void HandleOnPlayInstrumentMusic()
    {
        if (!_heldInstrument) return;
        _heldInstrument.StartMusicServerRpc();
    }

    private void HandleOnStopInstrumentMusic()
    {
        if (!_heldInstrument) return;
        _heldInstrument.StopMusicServerRpc();
    }

    private void HandleDropInstrument(Vector3 dropPosition)
    {
        if (!_heldInstrument) return;
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

    private void HandleGrabInstrument()
    {
        if (_heldInstrument) return;
        if (!_instrumentObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldInstrument = networkObject.gameObject.GetComponent<InstrumentBehaviour>();

        _heldInstrument.SetScrapValue(_instrumentScrapValue);
        _heldInstrument.parentObject = grabTarget;
        _heldInstrument.isHeldByEnemy = true;
        _heldInstrument.grabbableToEnemies = false;
        _heldInstrument.grabbable = false;
    }

    private void HandleSpawnInstrument(NetworkObjectReference instrumentObject, int instrumentScrapValue)
    {

        _instrumentObjectRef = instrumentObject;
        _instrumentScrapValue = instrumentScrapValue;
    }

    private void HandleChangeTargetPlayer(ulong targetPlayerObjectId)
    {
        if (targetPlayerObjectId == MusicalGhost.NullPlayerId)
        {
            _targetPlayer = null;
            return;
        }

        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        _targetPlayer = player;
    }

    private void HandleDamageTargetPlayer(int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        _targetPlayer.DamagePlayer(damage, causeOfDeath: causeOfDeath);
    }
}