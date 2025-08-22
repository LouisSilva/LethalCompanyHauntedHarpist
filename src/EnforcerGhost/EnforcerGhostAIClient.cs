using System.Collections;
using BepInEx.Logging;
using GameNetcodeStuff;
using LethalCompanyHarpGhost.Types;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.VFX;

namespace LethalCompanyHarpGhost.EnforcerGhost;

public class EnforcerGhostAIClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;

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

    internal static readonly int IsRunning = Animator.StringToHash("Running");
    internal static readonly int IsStunned = Animator.StringToHash("Stunned");
    internal static readonly int IsDead = Animator.StringToHash("Dead");
    internal static readonly int IsHoldingShotgun = Animator.StringToHash("HoldingShotgun");
    internal static readonly int PickupShotgun = Animator.StringToHash("PickupShotgun");
    internal static readonly int ReloadShotgun = Animator.StringToHash("ReloadShotgun");

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

    private readonly NullableObject<ShotgunItem> _heldShotgun = new();
    
    private NetworkObjectReference _shotgunObjectRef;
    
    private readonly NullableObject<PlayerControllerB> _targetPlayer = new();
    
    private int _shotgunScrapValue;

    private bool _shieldBehaviourEnabled = true;
    private bool _isShieldAnimationPlaying;
    
    private const float ShieldAnimationDuration = 0.25f;
    

    private void OnEnable()
    {
        if (!netcodeController) return;
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
        if (!netcodeController) return;
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
        _mls = BepInEx.Logging.Logger.CreateLogSource(
            $"{HarpGhostPlugin.ModGuid} | Enforcer Ghost AI {_ghostId} | Client");

        animator = GetComponent<Animator>();
        if (!animator) _mls.LogError("Animator is null");

        netcodeController = GetComponent<EnforcerGhostNetcodeController>();
        if (!netcodeController) _mls.LogError("netcodeController is null");

        if (!creatureSfxSource) _mls.LogError("creatureSfxSource is null");
        if (!creatureVoiceSource) _mls.LogError("creatureVoiceSource is null");

        if (!dieSfx) _mls.LogError("DieSfx is null");
        if (!shotgunOpenBarrelSfx) _mls.LogError("ShotgunOpenBarrelSfx is null");
        if (!shotgunReloadSfx) _mls.LogError("ShotgunReloadSfx is null");
        if (!shotgunCloseBarrelSfx) _mls.LogError("ShotgunCloseBarrelSfx is null");
        if (!shotgunGrabShellSfx) _mls.LogError("ShotgunGrabShellSfx is null");
        if (!shotgunDropShellSfx) _mls.LogError("ShotgunDropShellSfx is null");
        if (!grabShotgunSfx) _mls.LogError("GrabShotgunSfx is null");

        shieldRenderer.enabled = false;
    }

    private IEnumerator ShieldAnimation(float startVertexPosition, float endVertexPosition, float startAlpha,
        float endAlpha)
    {
        _isShieldAnimationPlaying = true;
        float timer = 0;

        shieldRenderer.enabled = true;
        shieldVfx.SetBool("ManualVertexPositioning", true);

        while (timer <= ShieldAnimationDuration)
        {
            float progress = timer / ShieldAnimationDuration;

            float currentVertexPosition = Mathf.Lerp(startVertexPosition, endVertexPosition, progress);
            shieldVfx.SetVector3("VertexAmount",
                new Vector3(currentVertexPosition, currentVertexPosition, currentVertexPosition));
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

    private void HandleDisableShield(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (!_shieldBehaviourEnabled || _isShieldAnimationPlaying) return;

        StartCoroutine(ShieldAnimation(0f, 0.005f, 1f, 0f));
        creatureSfxSource.pitch = Random.Range(0.8f, 1.1f);
        PlaySfx(shieldBreakSfx[0], false);
        LogDebug("Disable shield");
    }

    private void HandleEnableShield(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (!_shieldBehaviourEnabled || _isShieldAnimationPlaying) return;

        StartCoroutine(ShieldAnimation(0.005f, 0f, 0f, 1f));
        creatureSfxSource.pitch = Random.Range(0.8f, 1.1f);
        PlaySfx(shieldBreakSfx[Random.Range(0, 2)], false);
        LogDebug("Enable shield");
    }

    private void HandlePlayTeleportVfx(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        teleportVfx.SendEvent("OnPlayTeleport");
    }

    private void HandleSetMeshEnabled(string receivedGhostId, bool meshEnabled)
    {
        if (_ghostId != receivedGhostId) return;
        bodyRenderer.enabled = meshEnabled;
    }

    private void HandleUpdateShotgunShellsLoaded(string receivedGhostId, int shells)
    {
        if (_ghostId != receivedGhostId) return;
        LogDebug($"current shells: {_heldShotgun.Value.shellsLoaded}, changing to: {shells}");
        _heldShotgun.Value.shellsLoaded = shells;
    }

    private void HandleShootShotgun(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        _heldShotgun.Value.ShootGun(_heldShotgun.Value.transform.position, _heldShotgun.Value.transform.forward);
    }

    private void HandleDoShotgunAnimation(string receivedGhostId, string animationId)
    {
        if (_ghostId != receivedGhostId) return;
        _heldShotgun.Value.gunAnimator.SetTrigger(animationId);
    }

    private void HandleGrabShotgun(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        SetTrigger(_ghostId, PickupShotgun);
        SetBool(_ghostId, IsHoldingShotgun, true);
    }

    private void HandleSpawnShotgun(string receivedGhostId, NetworkObjectReference shotgunObject, int shotgunScrapValue)
    {
        if (_ghostId != receivedGhostId) return;
        _shotgunObjectRef = shotgunObject;
        _shotgunScrapValue = shotgunScrapValue;
    }

    private void HandleGrabShotgunPhaseTwo(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (_heldShotgun.IsNotNull) return;
        if (!_shotgunObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldShotgun.Value = networkObject.gameObject.GetComponent<ShotgunItem>();

        _heldShotgun.Value.SetScrapValue(_shotgunScrapValue);
        _heldShotgun.Value.gunAnimator.runtimeAnimatorController = HarpGhostPlugin.CustomShotgunAnimator;
        _heldShotgun.Value.parentObject = grabTarget;
        _heldShotgun.Value.isHeldByEnemy = true;
        _heldShotgun.Value.grabbableToEnemies = false;
        _heldShotgun.Value.grabbable = false;
        _heldShotgun.Value.shellsLoaded = 2;
        _heldShotgun.Value.GrabItemFromEnemy(enforcerGhostAIServer);
        PlaySfx(grabShotgunSfx);
    }

    private void HandleGrabShotgunAfterStun(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (_heldShotgun.Value.isHeld)
        {
            LogDebug("Someone else already picked it up!");
            return;
        }

        _heldShotgun.Value.gunAnimator.runtimeAnimatorController = HarpGhostPlugin.CustomShotgunAnimator;
        _heldShotgun.Value.parentObject = grabTarget;
        _heldShotgun.Value.isHeldByEnemy = true;
        _heldShotgun.Value.grabbableToEnemies = false;
        _heldShotgun.Value.grabbable = false;
        _heldShotgun.Value.GrabItemFromEnemy(enforcerGhostAIServer);
        PlaySfx(grabShotgunSfx);
    }

    private void HandleDropShotgun(string receivedGhostId, Vector3 dropPosition)
    {
        if (_ghostId != receivedGhostId) return;
        if (!_heldShotgun.IsNotNull) return;
        _heldShotgun.Value.parentObject = null;
        _heldShotgun.Value.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _heldShotgun.Value.gunAnimator.runtimeAnimatorController = ShotgunPatches.DefaultShotgunAnimationController;
        _heldShotgun.Value.EnablePhysics(true);
        _heldShotgun.Value.fallTime = 0f;

        Transform parent;
        _heldShotgun.Value.startFallingPosition =
            (parent = _heldShotgun.Value.transform.parent).InverseTransformPoint(_heldShotgun.Value.transform.position);
        _heldShotgun.Value.targetFloorPosition = parent.InverseTransformPoint(dropPosition);
        _heldShotgun.Value.floorYRot = -1;
        _heldShotgun.Value.grabbable = true;
        _heldShotgun.Value.grabbableToEnemies = true;
        _heldShotgun.Value.isHeld = false;
        _heldShotgun.Value.isHeldByEnemy = false;
        _heldShotgun.Value = null;
    }

    private void HandleDropShotgunWhenStunned(string receivedGhostId, Vector3 dropPosition)
    {
        if (_ghostId != receivedGhostId) return;
        if (!_heldShotgun.IsNotNull) return;
        _heldShotgun.Value.parentObject = null;
        _heldShotgun.Value.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _heldShotgun.Value.gunAnimator.runtimeAnimatorController = ShotgunPatches.DefaultShotgunAnimationController;
        _heldShotgun.Value.EnablePhysics(true);
        _heldShotgun.Value.fallTime = 0f;

        Transform parent;
        _heldShotgun.Value.startFallingPosition =
            (parent = _heldShotgun.Value.transform.parent).InverseTransformPoint(_heldShotgun.Value.transform.position);
        _heldShotgun.Value.targetFloorPosition = parent.InverseTransformPoint(dropPosition);
        _heldShotgun.Value.floorYRot = -1;
        _heldShotgun.Value.grabbable = true;
        _heldShotgun.Value.grabbableToEnemies = false;
        _heldShotgun.Value.isHeld = false;
        _heldShotgun.Value.isHeldByEnemy = false;
    }

    private void HandleIncreaseTargetPlayerFearLevel(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (GameNetworkManager.Instance.localPlayerController != _targetPlayer.Value) return;

        if (!_targetPlayer.IsNotNull) return;

        if (_targetPlayer.Value.HasLineOfSightToPosition(eye.position, 90f, 40, 3f))
        {
            _targetPlayer.Value.JumpToFearLevel(0.7f);
            _targetPlayer.Value.IncreaseFearLevelOverTime(0.5f);
        }

        else if (Vector3.Distance(eye.transform.position, _targetPlayer.Value.transform.position) < 3)
        {
            _targetPlayer.Value.JumpToFearLevel(0.3f);
            _targetPlayer.Value.IncreaseFearLevelOverTime(0.3f);
        }
    }

    private void HandleChangeTargetPlayer(string receivedGhostId, ulong playerClientId)
    {
        if (_ghostId != receivedGhostId) return;
        if (playerClientId == MusicalGhost.NullPlayerId)
        {
            _targetPlayer.Value = null;
            return;
        }

        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerClientId];
        _targetPlayer.Value = player;
    }

    private void HandleEnterDeathState(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;

        SetBool(_ghostId, IsDead, true);
        SetBool(_ghostId, IsRunning, false);
        SetBool(_ghostId, IsStunned, false);
        SetBool(_ghostId, IsHoldingShotgun, false);

        creatureVoiceSource.Stop(true);
        creatureSfxSource.Stop(true);
        PlayVoice(_ghostId, (int)AudioClipTypes.Death, 1);

        shieldRenderer.enabled = false;
        HandleDropShotgun(_ghostId, transform.position);

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

    public void OnAnimationEventStartReloadShotgun()
    {
        netcodeController.DoShotgunAnimationServerRpc(_ghostId, "reload");
        PlaySfx(shotgunDropShellSfx);
    }

    public void OnAnimationEventPickupShotgun()
    {
        netcodeController.GrabShotgunPhaseTwoServerRpc(_ghostId);
    }

    public void OnAnimationEventPlayShotgunOpenBarrelAudio()
    {
        PlaySfx(shotgunOpenBarrelSfx);
    }

    public void OnAnimationEventPlayShotgunReloadAudio()
    {
        PlaySfx(shotgunReloadSfx);
    }

    public void OnAnimationEventPlayShotgunCloseBarrelAudio()
    {
        PlaySfx(shotgunCloseBarrelSfx);
    }

    public void OnAnimationEventPlayShotgunGrabShellAudio()
    {
        PlaySfx(shotgunGrabShellSfx);
    }

    private void SetBool(string receivedGhostId, int parameter, bool value)
    {
        if (_ghostId != receivedGhostId) return;
        animator.SetBool(parameter, value);
    }

    private void SetFloat(string receivedGhostId, int parameter, float value)
    {
        if (_ghostId != receivedGhostId) return;
        animator.SetFloat(parameter, value);
    }

    private void SetTrigger(string receivedGhostId, int parameter)
    {
        if (_ghostId != receivedGhostId) return;
        animator.SetTrigger(parameter);
    }

    private void PlayVoice(string receivedGhostId, int typeIndex, int randomNum, bool interrupt = true)
    {
        if (_ghostId != receivedGhostId) return;

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

        if (!audioClip)
        {
            _mls.LogError($"Enforcer ghost voice audio clip index '{typeIndex}' and randomNum: '{randomNum}' is null");
            return;
        }

        PlayVoice(audioClip, interrupt);
    }

    private void PlayVoice(AudioClip clip, bool interrupt = true)
    {
        LogDebug($"Playing audio clip: {clip.name}");
        if (!clip) return;
        if (interrupt) creatureVoiceSource.Stop(true);
        creatureVoiceSource.pitch = clip == fartSfx[0] ? Random.Range(0.6f, 1.3f) : Random.Range(0.8f, 1.1f);
        creatureVoiceSource.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(creatureVoiceSource, clip, creatureVoiceSource.volume);
    }

    private void PlaySfx(AudioClip clip, bool interrupt = true)
    {
        LogDebug($"Playing audio clip: {clip.name}");
        if (!clip) return;
        if (interrupt) creatureVoiceSource.Stop(true);
        creatureSfxSource.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(creatureSfxSource, clip, creatureSfxSource.volume);
    }

    private void HandleInitializeConfigValues(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;

        _shieldBehaviourEnabled = EnforcerGhostConfig.Instance.EnforcerGhostShieldEnabled.Value;
        creatureSfxSource.volume = EnforcerGhostConfig.Default.EnforcerGhostSfxVolume.Value;
        creatureVoiceSource.volume = EnforcerGhostConfig.Default.EnforcerGhostVoiceSfxVolume.Value;
    }

    private void HandleUpdateGhostIdentifier(string receivedGhostId)
    {
        _ghostId = receivedGhostId;
        _mls = BepInEx.Logging.Logger.CreateLogSource(
            $"{HarpGhostPlugin.ModGuid} | Enforcer Ghost AI {_ghostId} | Client");
    }

    private void LogDebug(string msg)
    {
#if DEBUG
        _mls?.LogInfo(msg);
#endif
    }
}