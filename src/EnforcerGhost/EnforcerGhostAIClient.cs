using System.Collections;
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

    private bool _shieldBehaviourEnabled = true;
    private bool _isShieldAnimationPlaying = false;
    private const float ShieldAnimationDuration = 0.25f;

    private PlayerControllerB _targetPlayer;
    
    #pragma warning disable 0649
    [Header("Transforms")] [Space(3f)]
    [SerializeField] private Transform grabTarget;
    [SerializeField] private Transform eye;
    
    [Header("Audio")] [Space(5f)]
    [SerializeField] private AudioSource creatureVoiceSource;
    [SerializeField] private AudioSource creatureSfxSource;
    
    public AudioClip[] damageSfx;
    public AudioClip[] laughSfx;
    public AudioClip[] stunSfx;
    public AudioClip[] upsetSfx;
    public AudioClip[] fartSfx;
    public AudioClip[] spawnSfx;
    public AudioClip[] shieldBreakSfx;
    public AudioClip[] shieldRegenSfx;
    public AudioClip dieSfx;
    
    public AudioClip shotgunOpenBarrelSfx;
    public AudioClip shotgunReloadSfx;
    public AudioClip shotgunCloseBarrelSfx;
    public AudioClip shotgunGrabShellSfx;
    public AudioClip shotgunDropShellSfx;
    public AudioClip grabShotgunSfx;
    
    [Header("Animations, Vfx and Renderers")] [Space(5f)]
    [SerializeField] private Animator animator;
    [SerializeField] private SkinnedMeshRenderer bodyRenderer;
    [SerializeField] private VisualEffect teleportVfx;
    [SerializeField] private VisualEffect shieldVfx;
    [SerializeField] private Renderer shieldRenderer;
    
    [Header("Controllers")] [Space(5f)]
    [SerializeField] private EnforcerGhostNetcodeController netcodeController;
    [SerializeField] private EnforcerGhostAIServer enforcerGhostAIServer;
    #pragma warning restore 0649

    internal enum AudioClipTypes
    {
        Death = 0,
        Damage = 1,
        Laugh = 2,
        Stun = 3,
        Upset = 4,
        Fart = 5,
        Spawn = 6,
        ShieldRegen,
        ShieldBreak,
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
        netcodeController.OnInitializeConfigValues += HandleInitializeConfigValues;
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
        netcodeController.OnEnableShield += HandleEnableShield;
        netcodeController.OnDisableShield += HandleDisableShield;

        netcodeController.OnPlayCreatureVoice += PlayVoice;
    }

    private void OnDisable()
    {
        if (netcodeController == null) return;
        netcodeController.OnUpdateGhostIdentifier -= HandleUpdateGhostIdentifier;
        netcodeController.OnInitializeConfigValues -= HandleInitializeConfigValues;
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
        netcodeController.OnEnableShield -= HandleEnableShield;
        netcodeController.OnDisableShield -= HandleDisableShield;
        
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

        shieldRenderer.enabled = false;
    }
    
    private IEnumerator ShieldAnimation(float startVertexPosition, float endVertexPosition, float startAlpha, float endAlpha)
    {
        _isShieldAnimationPlaying = true;
        float timer = 0;

        shieldRenderer.enabled = true;
        shieldVfx.SetBool("ManualVertexPositioning", true);

        while (timer <= ShieldAnimationDuration)
        {
            float progress = timer / ShieldAnimationDuration;     
    
            float currentVertexPosition = Mathf.Lerp(startVertexPosition, endVertexPosition, progress);
            shieldVfx.SetVector3("VertexAmount", new Vector3(currentVertexPosition, currentVertexPosition, currentVertexPosition));
            shieldVfx.SetFloat("Alpha", Mathf.Lerp(startAlpha, endAlpha, progress));

            timer += Time.deltaTime;
            yield return null;
        }

        shieldVfx.SetVector3("VertexAmount", new Vector3(endVertexPosition, endVertexPosition, endVertexPosition));
        shieldVfx.SetFloat("Alpha", endAlpha);
        shieldVfx.SetBool("ManualVertexPositioning", false);
        
        if (endAlpha == 0f) shieldRenderer.enabled = false;
        else
        {
            yield return null;
            shieldVfx.SetVector3("VertexAmount", new Vector3(0.0005f, 0.0005f, 0.0005f));
        }
        
        _isShieldAnimationPlaying = false;
    }
    
    private void HandleDisableShield(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (!_shieldBehaviourEnabled) return;
        if (_isShieldAnimationPlaying) return;
        
        StartCoroutine(ShieldAnimation(0f, 0.005f, 1f, 0f));
        creatureSfxSource.pitch = Random.Range(0.8f, 1.1f);
        PlaySfx(shieldBreakSfx[0], false);
        LogDebug("disable shield");
    }

    private void HandleEnableShield(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (!_shieldBehaviourEnabled) return;
        if (_isShieldAnimationPlaying) return;
        
        StartCoroutine(ShieldAnimation(0.005f, 0f, 0f, 1f));
        creatureSfxSource.pitch = Random.Range(0.8f, 1.1f);
        PlaySfx(shieldBreakSfx[Random.Range(0, 2)], false);
        LogDebug("enable shield");
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

    private void HandleGrabShotgunAfterStun(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldShotgun.isHeld)
        {
            LogDebug("Someone else already picked it up!");
            return;
        }
        
        _heldShotgun.gunAnimator.runtimeAnimatorController = HarpGhostPlugin.CustomShotgunAnimator;
        _heldShotgun.parentObject = grabTarget;
        _heldShotgun.isHeldByEnemy = true;
        _heldShotgun.grabbableToEnemies = false;
        _heldShotgun.grabbable = false;
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

    private void HandleDropShotgunWhenStunned(string recievedGhostId, Vector3 dropPosition)
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
        _heldShotgun.grabbableToEnemies = false;
        _heldShotgun.isHeld = false;
        _heldShotgun.isHeldByEnemy = false;
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
            _targetPlayer.IncreaseFearLevelOverTime(0.5f);
        }
        
        else if (Vector3.Distance(eye.transform.position, _targetPlayer.transform.position) < 3)
        {
            _targetPlayer.JumpToFearLevel(0.3f);
            _targetPlayer.IncreaseFearLevelOverTime(0.3f);
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
        shieldRenderer.enabled = false;
        
        if (Random.value > 0.5f) StartCoroutine(PlayFartAfterTime(3f));
        else Destroy(this);
    }

    private IEnumerator PlayFartAfterTime(float time)
    {
        yield return new WaitForSeconds(time);
        creatureSfxSource.pitch = Random.Range(0.6f, 1.3f);
        PlaySfx(fartSfx[0], false);
        yield return new WaitForSeconds(3f);
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

    private void SetFloat(string recievedGhostId, int parameter, float value)
    {
        if (_ghostId != recievedGhostId) return;
        animator.SetFloat(parameter, value);
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
            (int)AudioClipTypes.Fart => fartSfx[randomNum],
            (int)AudioClipTypes.Spawn => spawnSfx[randomNum],
            (int)AudioClipTypes.ShieldRegen => shieldRegenSfx[randomNum],
            (int)AudioClipTypes.ShieldBreak => shieldBreakSfx[randomNum],
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
        creatureVoiceSource.pitch = clip == fartSfx[0] ? Random.Range(0.6f, 1.3f) : Random.Range(0.8f, 1.1f);
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

        _shieldBehaviourEnabled = EnforcerGhostConfig.Instance.EnforcerGhostShieldEnabled.Value;
        creatureSfxSource.volume = EnforcerGhostConfig.Default.EnforcerGhostSfxVolume.Value;
        creatureVoiceSource.volume = EnforcerGhostConfig.Default.EnforcerGhostVoiceSfxVolume.Value;
    }

    private void HandleUpdateGhostIdentifier(string recievedGhostId)
    {
        _ghostId = recievedGhostId;
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Enforcer Ghost AI {_ghostId} | Client");
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}