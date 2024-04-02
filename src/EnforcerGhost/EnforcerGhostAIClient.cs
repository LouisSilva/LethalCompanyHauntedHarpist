
using BepInEx.Logging;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.VFX;

namespace LethalCompanyHarpGhost.EnforcerGhost;

public class EnforcerGhostAIClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;

    private ShotgunItem _heldShotgun;
    private NetworkObjectReference _shotgunObjectRef;
    private int _shotgunScrapValue;

    private PlayerControllerB _targetPlayer;
    
    #pragma warning disable 0649
    [Header("Transforms")]
    [Space(3f)]
    [SerializeField] private Transform grabTarget;
    [SerializeField] private Transform eye;
    
    [Header("Audio")]
    [Space(5f)]
    [SerializeField] private AudioSource creatureVoiceSource;
    [SerializeField] private AudioSource creatureSfxSource;
    
    public AudioClip[] damageSfx;
    public AudioClip[] laughSfx;
    public AudioClip[] stunSfx;
    public AudioClip[] upsetSfx;
    public AudioClip dieSfx;
    
    public AudioClip shotgunOpenBarrelSfx;
    public AudioClip shotgunReloadSfx;
    public AudioClip shotgunCloseBarrelSfx;
    public AudioClip shotgunGrabShellSfx;
    public AudioClip shotgunDropShellSfx;
    public AudioClip grabShotgunSfx;
    
    [Space(5f)]
    [Header("Controllers")]
    [SerializeField] private EnforcerGhostNetcodeController netcodeController;
    [SerializeField] private EnforcerGhostAIServer enforcerGhostAIServer;
    [SerializeField] private Animator animator;
    [SerializeField] private SkinnedMeshRenderer bodyRenderer;
    [SerializeField] private VisualEffect teleportVfx;
    #pragma warning restore 0649
    
    private enum AudioClipTypes
    {
        Death = 0,
        Damage = 1,
        Laugh = 2,
        Stun = 3,
        Upset = 4
    }

    public static readonly int IsRunning = Animator.StringToHash("isRunning");
    public static readonly int IsStunned = Animator.StringToHash("isStunned");
    public static readonly int IsDead = Animator.StringToHash("isDead");
    public static readonly int IsHoldingShotgun = Animator.StringToHash("isHoldingShotgun");
    public static readonly int Death = Animator.StringToHash("death");
    public static readonly int Stunned = Animator.StringToHash("stunned");
    public static readonly int Recover = Animator.StringToHash("recover");
    public static readonly int Attack = Animator.StringToHash("attack");
    public static readonly int PickupShotgun = Animator.StringToHash("pickupShotgun");
    public static readonly int ReloadShotgun = Animator.StringToHash("reloadShotgun");
    private static readonly int Reload = Animator.StringToHash("reload");

    private void OnEnable()
    {
        netcodeController.OnUpdateGhostIdentifier += HandleUpdateGhostIdentifier;
        netcodeController.OnEnterDeathState += HandleEnterDeathState;
        netcodeController.OnSpawnShotgun += HandleSpawnShotgun;
        netcodeController.OnGrabShotgunPhaseTwo += HandleGrabShotgunPhaseTwo;
        netcodeController.OnDropShotgun += HandleDropShotgun;
        netcodeController.OnShootGun += HandleShootShotgun;
        netcodeController.OnChangeTargetPlayer += HandleChangeTargetPlayer;
        netcodeController.OnIncreaseTargetPlayerFearLevel += HandleIncreaseTargetPlayerFearLevel;
        netcodeController.OnUpdateShotgunShellsLoaded += HandleUpdateShotgunShellsLoaded;
        
        netcodeController.OnDoAnimation += SetTrigger;
        netcodeController.OnChangeAnimationParameterBool += SetBool;
        netcodeController.OnDoShotgunAnimation += HandleDoShotgunAnimation;
        netcodeController.OnGrabShotgun += HandleGrabShotgun;
        netcodeController.OnSetMeshEnabled += HandleSetMeshEnabled;
        netcodeController.OnPlayTeleportVfx += HandlePlayTeleportVfx;

        netcodeController.OnPlayCreatureVoice += PlayVoice;
    }

    private void OnDestroy()
    {
        netcodeController.OnUpdateGhostIdentifier -= HandleUpdateGhostIdentifier;
        netcodeController.OnEnterDeathState -= HandleEnterDeathState;
        netcodeController.OnSpawnShotgun -= HandleSpawnShotgun;
        netcodeController.OnGrabShotgunPhaseTwo -= HandleGrabShotgunPhaseTwo;
        netcodeController.OnDropShotgun -= HandleDropShotgun;
        netcodeController.OnShootGun -= HandleShootShotgun;
        netcodeController.OnChangeTargetPlayer -= HandleChangeTargetPlayer;
        netcodeController.OnIncreaseTargetPlayerFearLevel -= HandleIncreaseTargetPlayerFearLevel;
        netcodeController.OnUpdateShotgunShellsLoaded -= HandleUpdateShotgunShellsLoaded;

        netcodeController.OnDoAnimation -= SetTrigger;
        netcodeController.OnChangeAnimationParameterBool -= SetBool;
        netcodeController.OnDoShotgunAnimation -= HandleDoShotgunAnimation;
        netcodeController.OnGrabShotgun -= HandleGrabShotgun;
        netcodeController.OnSetMeshEnabled -= HandleSetMeshEnabled;
        netcodeController.OnPlayTeleportVfx -= HandlePlayTeleportVfx;
        
        netcodeController.OnPlayCreatureVoice -= PlayVoice;
    }

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Enforcer Ghost AI {_ghostId} | Client");
        
        animator = GetComponent<Animator>();
        if (animator == null) _mls.LogError("Animator is null");

        netcodeController = GetComponent<EnforcerGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("netcodeController is null");
        
        if (creatureSfxSource == null) _mls.LogError("creatureSfxSource is null");
        if (creatureVoiceSource == null) _mls.LogError("creatureVoiceSource is null");
        
        if (dieSfx == null) _mls.LogError("DieSfx is null");
        if (shotgunOpenBarrelSfx == null) _mls.LogError("ShotgunOpenBarrelSfx is null");
        if (shotgunReloadSfx == null) _mls.LogError("ShotgunReloadSfx is null");
        if (shotgunCloseBarrelSfx == null) _mls.LogError("ShotgunCloseBarrelSfx is null");
        if (shotgunGrabShellSfx == null) _mls.LogError("ShotgunGrabShellSfx is null");
        if (shotgunDropShellSfx == null) _mls.LogError("ShotgunDropShellSfx is null");
        if (grabShotgunSfx == null) _mls.LogError("GrabShotgunSfx is null");
    }
    
    private void HandlePlayTeleportVfx(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        teleportVfx.SendEvent("OnPlayTeleport");
    }
    
    private void HandleSetMeshEnabled(string recievedGhostId, bool meshEnabled)
    {
        if (_ghostId != recievedGhostId) return;
        bodyRenderer.enabled = meshEnabled;
    }

    private void HandleUpdateShotgunShellsLoaded(string recievedGhostId, int shells)
    {
        if (_ghostId != recievedGhostId) return;
        LogDebug($"current shells: {_heldShotgun.shellsLoaded}, changing to: {shells}");
        _heldShotgun.shellsLoaded = shells;
    }

    private void HandleShootShotgun(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        _heldShotgun.ShootGun(_heldShotgun.transform.position, _heldShotgun.transform.forward);
    }
    
    private void HandleDoShotgunAnimation(string recievedGhostId, string animationId)
    {
        if (_ghostId != recievedGhostId) return;
        _heldShotgun.gunAnimator.SetTrigger(animationId);
    }
    
    private void HandleGrabShotgun(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        SetTrigger(_ghostId, PickupShotgun);
        SetBool(_ghostId, IsHoldingShotgun, true);
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
        _heldShotgun.gunAnimator.runtimeAnimatorController = HarpGhostPlugin.CustomShotgunAnimator;
        _heldShotgun.parentObject = grabTarget;
        _heldShotgun.isHeldByEnemy = true;
        _heldShotgun.grabbableToEnemies = false;
        _heldShotgun.grabbable = false;
        _heldShotgun.shellsLoaded = 2;
        _heldShotgun.GrabItemFromEnemy(enforcerGhostAIServer);
        PlaySfx(grabShotgunSfx);
    }
    
    private void HandleDropShotgun(string recievedGhostId, Vector3 dropPosition)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldShotgun == null) return;
        _heldShotgun.parentObject = null;
        _heldShotgun.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _heldShotgun.gunAnimator.runtimeAnimatorController = ShotgunPatches.DefaultShotgunAnimationController;
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
    
    private void HandleIncreaseTargetPlayerFearLevel(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (GameNetworkManager.Instance.localPlayerController != _targetPlayer) return;
        
        if (_targetPlayer == null)
        {
            return;
        }
        
        if (_targetPlayer.HasLineOfSightToPosition(eye.position, 90f, 40, 3f))
        {
            _targetPlayer.JumpToFearLevel(0.7f);
            _targetPlayer.IncreaseFearLevelOverTime(0.5f);;
        }
        
        else if (Vector3.Distance(eye.transform.position, _targetPlayer.transform.position) < 3)
        {
            _targetPlayer.JumpToFearLevel(0.3f);
            _targetPlayer.IncreaseFearLevelOverTime(0.3f);;
        }
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
    }

    private void HandleEnterDeathState(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        SetTrigger(_ghostId, Death);
        SetBool(_ghostId, IsDead, true);
        SetBool(_ghostId, IsRunning, false);
        SetBool(_ghostId, IsStunned, false);
        SetBool(_ghostId, IsHoldingShotgun, false);
        creatureVoiceSource.Stop(true);
        creatureSfxSource.Stop(true);
        PlayVoice(_ghostId, (int)AudioClipTypes.Death, 1);
        Destroy(this);
    }
    
    private void OnAnimationEventStartReloadShotgun()
    {
        netcodeController.DoShotgunAnimationServerRpc(_ghostId, "reload");
        PlaySfx(shotgunDropShellSfx);
    }

    private void OnAnimationEventPickupShotgun()
    {
        netcodeController.GrabShotgunPhaseTwoServerRpc(_ghostId);
    }

    private void OnAnimationEventPlayShotgunOpenBarrelAudio()
    {
        PlaySfx(shotgunOpenBarrelSfx);
    }

    private void OnAnimationEventPlayShotgunReloadAudio()
    {
        PlaySfx(shotgunReloadSfx);
    }

    private void OnAnimationEventPlayShotgunCloseBarrelAudio()
    {
        PlaySfx(shotgunCloseBarrelSfx);
    }

    private void OnAnimationEventPlayShotgunGrabShellAudio()
    {
        PlaySfx(shotgunGrabShellSfx);
    }
    
    private void SetBool(string recievedGhostId, int parameter, bool value)
    {
        if (_ghostId != recievedGhostId) return;
        animator.SetBool(parameter, value);
    }
    
    private void SetTrigger(string recievedGhostId, int parameter)
    {
        if (_ghostId != recievedGhostId) return;
        animator.SetTrigger(parameter);
    }
    
    private void PlayVoice(string recievedGhostId, int typeIndex, int randomNum, bool interrupt = true)
    {
        if (_ghostId != recievedGhostId) return;
        
        AudioClip audioClip = typeIndex switch
        {
            (int)AudioClipTypes.Death => dieSfx,
            (int)AudioClipTypes.Damage => damageSfx[randomNum],
            (int)AudioClipTypes.Laugh => laughSfx[randomNum],
            (int)AudioClipTypes.Stun => stunSfx[randomNum],
            (int)AudioClipTypes.Upset => upsetSfx[randomNum],
            _ => null
        };

        if (audioClip == null)
        {
            _mls.LogError($"Enforcer ghost voice audio clip index '{typeIndex}' and randomNum: '{randomNum}' is null");
            return;
        }
        
        PlayVoice(audioClip, interrupt);
    }
    
    private void PlayVoice(AudioClip clip, bool interrupt = true)
    {
        LogDebug($"Playing audio clip: {clip.name}");
        if (clip == null) return;
        if (interrupt) creatureVoiceSource.Stop(true);
        creatureVoiceSource.pitch = Random.Range(0.8f, 1.1f);
        creatureVoiceSource.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(creatureVoiceSource, clip, creatureVoiceSource.volume);
    }
    
    private void PlaySfx(AudioClip clip, bool interrupt = true)
    {
        LogDebug($"Playing audio clip: {clip.name}");
        if (clip == null) return;
        if (interrupt) creatureVoiceSource.Stop(true);
        creatureSfxSource.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(creatureSfxSource, clip, creatureSfxSource.volume);
    }

    private void HandleInitializeConfigValues(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        creatureVoiceSource.volume = HarpGhostConfig.Default.HarpGhostVoiceSfxVolume.Value;
    }

    private void HandleUpdateGhostIdentifier(string recievedGhostId)
    {
        _ghostId = recievedGhostId;
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}