using System.Collections;
using BepInEx.Logging;
using GameNetcodeStuff;
using LethalCompanyHarpGhost.Items;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;
    
    public enum AudioClipTypes
    {
        Death,
        Damage,
        Laugh,
        Stun,
        Upset,
        Hit,
    }
    
    private static readonly int AlternativeColourFadeInTimer = Shader.PropertyToID("_AlternativeColourFadeInTimer");
    private static readonly int Running = Animator.StringToHash("Running");
    public static readonly int Stunned = Animator.StringToHash("Stunned");
    public static readonly int Dead = Animator.StringToHash("Dead");
    public static readonly int Attack = Animator.StringToHash("Attack");
    
#pragma warning disable 0649
    [Header("Audio")] [Space(5f)]
    [SerializeField] private AudioSource creatureVoiceSource;
    [SerializeField] private AudioSource creatureSfxSource;
    public AudioClip[] damageSfx;
    public AudioClip[] laughSfx;
    public AudioClip[] stunSfx;
    public AudioClip[] upsetSfx;
    public AudioClip[] dieSfx;
    public AudioClip[] hitSfx;
    
    [Header("Transforms")] [Space(5f)]
    [SerializeField] private Transform grabTarget;
    [SerializeField] private Transform eye;
    
    [Header("Materials and Renderers")] [Space(5f)]
    [SerializeField] private bool enableGhostAngryModel = true;
    [SerializeField] private Renderer rendererLeftEye;
    [SerializeField] private Renderer rendererRightEye;
    [SerializeField] private MaterialPropertyBlock _propertyBlock;

    [Header("Controllers")] [Space(5f)] 
    [SerializeField] private Animator animator;
    [SerializeField] private HarpGhostNetcodeController netcodeController;
#pragma warning restore 0649
    
    private NetworkObjectReference _instrumentObjectRef;
    
    private readonly NullableObject<InstrumentBehaviour> _heldInstrument = new();
    private readonly NullableObject<PlayerControllerB> _targetPlayer = new();

    private Vector3 _agentLastPosition;
    
    private int _instrumentScrapValue;
    private int _attackDamage = 35;
    private int _currentBehaviourStateIndex;
    
    private bool _isTransitioningMaterial;
    private bool _networkEventsSubscribed;
    
    private float _transitioningMaterialTimer;
    private float _agentCurrentSpeed;

    private void Awake()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Harp Ghost Client {_ghostId}");
        _propertyBlock = new MaterialPropertyBlock();

        if (netcodeController == null) netcodeController = GetComponent<HarpGhostNetcodeController>();
    }
    
    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeToNetworkEvents();
    }

    private void Start()
    {
        SubscribeToNetworkEvents();
        InitializeConfigValues();
    }

    private void Update()
    {
        _currentBehaviourStateIndex = netcodeController.CurrentBehaviourStateIndex.Value;
        
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;
        
        animator.SetBool(Dead, netcodeController.AnimationParamDead.Value);
        animator.SetBool(Stunned, netcodeController.AnimationParamStunned.Value);
    }

    private void LateUpdate()
    { 
        bool isRunning = _agentCurrentSpeed >= 3f;
        animator.SetBool(Running, isRunning);
    }

    private void HandlePlayAudioClipType(string receivedGhostId, AudioClipTypes audioClipType, int clipIndex, bool interrupt = false)
    {
        if (_ghostId != receivedGhostId) return;

        AudioClip audioClipToPlay = audioClipType switch
        {
            AudioClipTypes.Stun => stunSfx[clipIndex],
            AudioClipTypes.Hit => hitSfx[clipIndex],
            _ => null
        };

        if (audioClipToPlay == null)
        {
            _mls.LogError($"Invalid audio clip with type: {audioClipType} and index: {clipIndex}");
            return;
        }
        
        LogDebug($"Playing audio clip: {audioClipToPlay.name}");
        if (interrupt) creatureVoiceSource.Stop(true);
        creatureVoiceSource.PlayOneShot(audioClipToPlay);
        WalkieTalkie.TransmitOneShotAudio(creatureVoiceSource, audioClipToPlay, creatureVoiceSource.volume);
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
        if (_ghostId != receivedGhostId) return;
        if (!_heldInstrument.IsNotNull) return;
        _heldInstrument.Value.StartMusicServerRpc();
    }
    
    private void HandleOnStopInstrumentMusic(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (!_heldInstrument.IsNotNull) return;
        _heldInstrument.Value.StopMusicServerRpc();
    }

    private void HandleDropInstrument(string receivedGhostId, Vector3 dropPosition)
    {
        if (_ghostId != receivedGhostId) return;
        if (!_heldInstrument.IsNotNull) return;
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
        if (_ghostId != receivedGhostId) return;
        if (_heldInstrument.IsNotNull) return;
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

    private void HandleGhostEyesTurnRed(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (enableGhostAngryModel && !_isTransitioningMaterial) StartCoroutine(GhostEyesTurnRed());
    }
    
    private void HandleSetAnimationTrigger(string receivedGhostId, int parameter)
    {
        if (_ghostId != receivedGhostId) return;
        animator.SetTrigger(parameter);
    }
    
    public void OnAnimationEventFixAgentSpeedAfterAttack()
    {
        if (NetworkManager.Singleton.IsClient && netcodeController.IsOwner)
        {
            netcodeController.FixAgentSpeedAfterAttackServerRpc(_ghostId);
        }
    }
    
    public void OnAnimationEventAttackShiftComplete()
    {
        if (netcodeController.IsServer) 
        {
            netcodeController.ChangeAgentMaxSpeedServerRpc(_ghostId, 0f, 0f); // Ghost is frozen while doing the second attack anim
            netcodeController.PlayAudioClipTypeServerRpc(_ghostId, AudioClipTypes.Laugh);
        }
        
        StartCoroutine(DamageTargetPlayerAfterDelay(0.05f, _attackDamage, CauseOfDeath.Strangulation));
    }
    
    private IEnumerator DamageTargetPlayerAfterDelay(float delay, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown) // Damages the player in time with the correct point in the animation
    {
        yield return new WaitForSeconds(delay);
        netcodeController.DamageTargetPlayerServerRpc(_ghostId, damage, causeOfDeath);
    }

    private void HandleTargetPlayerChanged(ulong oldValue, ulong newValue)
    {
        _targetPlayer.Value = newValue == 69420 ? null : StartOfRound.Instance.allPlayerScripts[newValue];
        LogDebug(_targetPlayer.IsNotNull
            ? $"Changed target player to {_targetPlayer.Value?.playerUsername}."
            : "Changed target player to null.");
    }

    private void HandleDamageTargetPlayer(string receivedGhostId, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        if (_ghostId != receivedGhostId) return;
        if (!_targetPlayer.IsNotNull) return;
        _targetPlayer.Value.DamagePlayer(damage, causeOfDeath: causeOfDeath);
        LogDebug($"Damaged {_targetPlayer.Value.playerUsername} for {damage} health.");
    }
    
    private void HandleEnterDeathState(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        LogDebug("Entering death state on client.");
        HandlePlayAudioClipType(_ghostId, AudioClipTypes.Death, 0, true);
        Destroy(this);
    }
    
    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed) return;
        
        netcodeController.OnDropHarp += HandleDropInstrument;
        netcodeController.OnSpawnHarp += HandleSpawnInstrument;
        netcodeController.OnGrabHarp += HandleGrabInstrument;
        netcodeController.OnPlayHarpMusic += HandleOnPlayInstrumentMusic;
        netcodeController.OnStopHarpMusic += HandleOnStopInstrumentMusic;
        netcodeController.OnDamageTargetPlayer += HandleDamageTargetPlayer;
        netcodeController.OnIncreaseTargetPlayerFearLevel += HandleIncreaseTargetPlayerFearLevel;
        netcodeController.OnSyncGhostIdentifier += HandleSyncGhostIdentifier;
        netcodeController.OnGhostEyesTurnRed += HandleGhostEyesTurnRed;
        netcodeController.OnEnterDeathState += HandleEnterDeathState;
        netcodeController.OnSetAnimationTrigger += HandleSetAnimationTrigger;
        netcodeController.OnPlayAudioClipType += HandlePlayAudioClipType;

        netcodeController.TargetPlayerClientId.OnValueChanged += HandleTargetPlayerChanged;

        _networkEventsSubscribed = true;
    }

    private void UnsubscribeToNetworkEvents()
    {
        if (!_networkEventsSubscribed) return;
        
        netcodeController.OnDropHarp -= HandleDropInstrument;
        netcodeController.OnSpawnHarp -= HandleSpawnInstrument;
        netcodeController.OnGrabHarp -= HandleGrabInstrument;
        netcodeController.OnPlayHarpMusic -= HandleOnPlayInstrumentMusic;
        netcodeController.OnStopHarpMusic -= HandleOnStopInstrumentMusic;
        netcodeController.OnDamageTargetPlayer -= HandleDamageTargetPlayer;
        netcodeController.OnIncreaseTargetPlayerFearLevel -= HandleIncreaseTargetPlayerFearLevel;
        netcodeController.OnSyncGhostIdentifier -= HandleSyncGhostIdentifier;
        netcodeController.OnGhostEyesTurnRed -= HandleGhostEyesTurnRed;
        netcodeController.OnEnterDeathState -= HandleEnterDeathState;
        netcodeController.OnSetAnimationTrigger -= HandleSetAnimationTrigger;
        netcodeController.OnPlayAudioClipType -= HandlePlayAudioClipType;
        
        netcodeController.TargetPlayerClientId.OnValueChanged -= HandleTargetPlayerChanged;

        _networkEventsSubscribed = false;
    }
    
    private void InitializeConfigValues()
    {
        enableGhostAngryModel = HarpGhostConfig.Default.HarpGhostAngryEyesEnabled.Value;
        creatureVoiceSource.volume = HarpGhostConfig.Default.HarpGhostVoiceSfxVolume.Value;
        _attackDamage = HarpGhostConfig.Instance.HarpGhostAttackDamage.Value;
    }

    private void HandleSyncGhostIdentifier(string receivedGhostId)
    {
        _ghostId = receivedGhostId;
        _mls?.Dispose();
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Harp Ghost Client {_ghostId}");
        
        LogDebug("Successfully synced ghost id.");
    }
    
    /// <summary>
    /// Only logs the given message if the assembly version is in debug, not release
    /// </summary>
    /// <param name="msg">The debug message to log.</param>
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}