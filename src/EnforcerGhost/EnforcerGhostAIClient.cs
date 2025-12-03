using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.VFX;

namespace LethalCompanyHarpGhost.EnforcerGhost;

public class EnforcerGhostAIClient : MonoBehaviour
{
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

    private ShotgunItem _heldShotgun;

    private NetworkObjectReference _shotgunObjectRef;

    private PlayerControllerB _targetPlayer;

    private int _shotgunScrapValue;

    private bool _shieldBehaviourEnabled = true;
    private bool _isShieldAnimationPlaying;

    private const float ShieldAnimationDuration = 0.25f;


    private void OnEnable()
    {
        if (!netcodeController) return;
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
        animator = GetComponent<Animator>();
        if (!animator) HarpGhostPlugin.Logger.LogError("Animator is null");

        netcodeController = GetComponent<EnforcerGhostNetcodeController>();
        if (!netcodeController) HarpGhostPlugin.Logger.LogError("netcodeController is null");

        if (!creatureSfxSource) HarpGhostPlugin.Logger.LogError("creatureSfxSource is null");
        if (!creatureVoiceSource) HarpGhostPlugin.Logger.LogError("creatureVoiceSource is null");

        if (!dieSfx) HarpGhostPlugin.Logger.LogError("DieSfx is null");
        if (!shotgunOpenBarrelSfx) HarpGhostPlugin.Logger.LogError("ShotgunOpenBarrelSfx is null");
        if (!shotgunReloadSfx) HarpGhostPlugin.Logger.LogError("ShotgunReloadSfx is null");
        if (!shotgunCloseBarrelSfx) HarpGhostPlugin.Logger.LogError("ShotgunCloseBarrelSfx is null");
        if (!shotgunGrabShellSfx) HarpGhostPlugin.Logger.LogError("ShotgunGrabShellSfx is null");
        if (!shotgunDropShellSfx) HarpGhostPlugin.Logger.LogError("ShotgunDropShellSfx is null");
        if (!grabShotgunSfx) HarpGhostPlugin.Logger.LogError("GrabShotgunSfx is null");

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

    private void HandleDisableShield()
    {
        if (!_shieldBehaviourEnabled || _isShieldAnimationPlaying) return;

        StartCoroutine(ShieldAnimation(0f, 0.005f, 1f, 0f));
        creatureSfxSource.pitch = Random.Range(0.8f, 1.1f);
        PlaySfx(shieldBreakSfx[0], false);
        HarpGhostPlugin.LogVerbose("Disable shield");
    }

    private void HandleEnableShield()
    {
        if (!_shieldBehaviourEnabled || _isShieldAnimationPlaying) return;

        StartCoroutine(ShieldAnimation(0.005f, 0f, 0f, 1f));
        creatureSfxSource.pitch = Random.Range(0.8f, 1.1f);
        PlaySfx(shieldBreakSfx[Random.Range(0, 2)], false);
        HarpGhostPlugin.LogVerbose("Enable shield");
    }

    private void HandlePlayTeleportVfx()
    {
        teleportVfx.SendEvent("OnPlayTeleport");
    }

    private void HandleSetMeshEnabled(bool meshEnabled)
    {
        bodyRenderer.enabled = meshEnabled;
    }

    private void HandleUpdateShotgunShellsLoaded(int shells)
    {
        HarpGhostPlugin.LogVerbose($"current shells: {_heldShotgun.shellsLoaded}, changing to: {shells}");
        _heldShotgun.shellsLoaded = shells;
    }

    private void HandleShootShotgun()
    {
        _heldShotgun.ShootGun(_heldShotgun.transform.position, _heldShotgun.transform.forward);
    }

    private void HandleDoShotgunAnimation(string animationId)
    {
        _heldShotgun.gunAnimator.SetTrigger(animationId);
    }

    private void HandleGrabShotgun()
    {
        SetTrigger(PickupShotgun);
        SetBool(IsHoldingShotgun, true);
    }

    private void HandleSpawnShotgun(NetworkObjectReference shotgunObject, int shotgunScrapValue)
    {
        _shotgunObjectRef = shotgunObject;
        _shotgunScrapValue = shotgunScrapValue;
    }

    private void HandleGrabShotgunPhaseTwo()
    {
        if (_heldShotgun) return;
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

    private void HandleGrabShotgunAfterStun()
    {
        if (_heldShotgun.isHeld)
        {
            HarpGhostPlugin.LogVerbose("Someone else already picked it up!");
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

    private void HandleDropShotgun(Vector3 dropPosition)
    {
        if (!_heldShotgun) return;
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

    private void HandleDropShotgunWhenStunned(Vector3 dropPosition)
    {
        if (!_heldShotgun) return;
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

    private void HandleIncreaseTargetPlayerFearLevel()
    {
        if (GameNetworkManager.Instance.localPlayerController != _targetPlayer) return;

        if (!_targetPlayer) return;

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

    private void HandleChangeTargetPlayer(ulong playerClientId)
    {
        if (playerClientId == MusicalGhost.NullPlayerId)
        {
            _targetPlayer = null;
            return;
        }

        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerClientId];
        _targetPlayer = player;
    }

    private void HandleEnterDeathState()
    {
        SetBool(IsDead, true);
        SetBool(IsRunning, false);
        SetBool(IsStunned, false);
        SetBool(IsHoldingShotgun, false);

        creatureVoiceSource.Stop(true);
        creatureSfxSource.Stop(true);
        PlayVoice((int)AudioClipTypes.Death, 1);

        shieldRenderer.enabled = false;
        HandleDropShotgun(transform.position);

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
        netcodeController.DoShotgunAnimationServerRpc("reload");
        PlaySfx(shotgunDropShellSfx);
    }

    public void OnAnimationEventPickupShotgun()
    {
        netcodeController.GrabShotgunPhaseTwoServerRpc();
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

    private void SetBool(int parameter, bool value)
    {
        animator.SetBool(parameter, value);
    }

    private void SetFloat(int parameter, float value)
    {
        animator.SetFloat(parameter, value);
    }

    private void SetTrigger(int parameter)
    {
        animator.SetTrigger(parameter);
    }

    private void PlayVoice(int typeIndex, int randomNum, bool interrupt = true)
    {
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
            HarpGhostPlugin.Logger.LogError($"Enforcer ghost voice audio clip index '{typeIndex}' and randomNum: '{randomNum}' is null");
            return;
        }

        PlayVoice(audioClip, interrupt);
    }

    private void PlayVoice(AudioClip clip, bool interrupt = true)
    {
        HarpGhostPlugin.LogVerbose($"Playing audio clip: {clip.name}");
        if (!clip) return;
        if (interrupt) creatureVoiceSource.Stop(true);
        creatureVoiceSource.pitch = clip == fartSfx[0] ? Random.Range(0.6f, 1.3f) : Random.Range(0.8f, 1.1f);
        creatureVoiceSource.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(creatureVoiceSource, clip, creatureVoiceSource.volume);
    }

    private void PlaySfx(AudioClip clip, bool interrupt = true)
    {
        HarpGhostPlugin.LogVerbose($"Playing audio clip: {clip.name}");
        if (!clip) return;
        if (interrupt) creatureVoiceSource.Stop(true);
        creatureSfxSource.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(creatureSfxSource, clip, creatureSfxSource.volume);
    }

    private void HandleInitializeConfigValues()
    {
        _shieldBehaviourEnabled = EnforcerGhostConfig.Instance.EnforcerGhostShieldEnabled.Value;
        creatureSfxSource.volume = EnforcerGhostConfig.Default.EnforcerGhostSfxVolume.Value;
        creatureVoiceSource.volume = EnforcerGhostConfig.Default.EnforcerGhostVoiceSfxVolume.Value;
    }
}