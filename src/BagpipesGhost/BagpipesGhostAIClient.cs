using LethalCompanyHarpGhost.Items;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.VFX;

namespace LethalCompanyHarpGhost.BagpipesGhost;

public class BagpipesGhostAIClient : MonoBehaviour
{
    public enum AudioClipTypes
    {
        Death = 0,
        Damage = 1,
        Laugh = 2,
        Stun = 3,
        Upset = 4,
        LongLaugh = 5,
        Shocked = 6,
        Taunt = 7,
    }

    public AudioClip[] longLaughSfx;
    public AudioClip[] shockedSfx;
    public AudioClip[] tauntSfx;
    public AudioClip[] damageSfx;
    public AudioClip[] laughSfx;
    public AudioClip[] stunSfx;
    public AudioClip[] upsetSfx;
    public AudioClip dieSfx;
    public AudioClip tornadoTeleportSfx;

    public static readonly int IsRunning = Animator.StringToHash("Running");
    public static readonly int IsStunned = Animator.StringToHash("Stunned");
    public static readonly int IsDead = Animator.StringToHash("Dead");
    public static readonly int Recover = Animator.StringToHash("recover");

#pragma warning disable 0649
    [Header("Audio")] [Space(5f)]
    [SerializeField] private AudioSource creatureVoiceSource;
    [SerializeField] private AudioSource creatureSfxSource;

    [Header("Visual Effects")] [Space(5f)]
    [SerializeField] private VisualEffect teleportVfx;
    [SerializeField] private Animator animator;

    [Header("Controllers")] [Space(5f)]
    [SerializeField] private Transform grabTarget;
    [SerializeField] private SkinnedMeshRenderer renderer;
    [SerializeField] private BagpipesGhostNetcodeController netcodeController;
#pragma warning restore 0649

    private NetworkObjectReference _instrumentObjectRef;

    private int _instrumentScrapValue;

    private InstrumentBehaviour _heldInstrument;

    private void OnEnable()
    {
        if (!netcodeController) return;
        netcodeController.OnDropBagpipes += HandleDropInstrument;
        netcodeController.OnSpawnBagpipes += HandleSpawnInstrument;
        netcodeController.OnGrabBagpipes += HandleGrabInstrument;
        netcodeController.OnPlayBagpipesMusic += HandleOnPlayInstrumentMusic;
        netcodeController.OnStopBagpipesMusic += HandleOnStopInstrumentMusic;
        netcodeController.OnDestroyBagpipes += HandleDestroyBagpipes;
        netcodeController.OnSetMeshEnabled += HandleSetMeshEnabled;

        netcodeController.OnInitializeConfigValues += HandleOnInitializeConfigValues;
        netcodeController.OnPlayCreatureVoice += PlayVoice;
        netcodeController.OnEnterDeathState += HandleOnEnterDeathState;
        netcodeController.OnPlayTeleportVfx += HandlePlayTeleportVfx;

        netcodeController.OnDoAnimation += SetTrigger;
        netcodeController.OnChangeAnimationParameterBool += SetBool;
    }

    private void OnDisable()
    {
        if (!netcodeController) return;
        netcodeController.OnDropBagpipes -= HandleDropInstrument;
        netcodeController.OnSpawnBagpipes -= HandleSpawnInstrument;
        netcodeController.OnGrabBagpipes -= HandleGrabInstrument;
        netcodeController.OnPlayBagpipesMusic -= HandleOnPlayInstrumentMusic;
        netcodeController.OnStopBagpipesMusic -= HandleOnStopInstrumentMusic;
        netcodeController.OnDestroyBagpipes -= HandleDestroyBagpipes;
        netcodeController.OnSetMeshEnabled -= HandleSetMeshEnabled;

        netcodeController.OnInitializeConfigValues -= HandleOnInitializeConfigValues;
        netcodeController.OnPlayCreatureVoice -= PlayVoice;
        netcodeController.OnEnterDeathState -= HandleOnEnterDeathState;
        netcodeController.OnPlayTeleportVfx -= HandlePlayTeleportVfx;

        netcodeController.OnDoAnimation -= SetTrigger;
        netcodeController.OnChangeAnimationParameterBool -= SetBool;
    }

    private void Start()
    {
        renderer.enabled = true;
    }

    private void HandlePlayTeleportVfx()
    {
        teleportVfx.SendEvent("OnPlayTeleport");
        PlaySfx(tornadoTeleportSfx);
    }

    private void HandleDestroyBagpipes()
    {
        if (_heldInstrument)
            Destroy(_heldInstrument.gameObject);

        _heldInstrument = null;
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

    private void HandleSetMeshEnabled(bool meshEnabled)
    {
        renderer.enabled = meshEnabled;
    }

    private void PlayVoice(int typeIndex, int randomNum, bool interrupt = true)
    {
        creatureVoiceSource.pitch = Random.Range(0.8f, 1.1f);

        AudioClip audioClip = typeIndex switch
        {
            (int)AudioClipTypes.Death => dieSfx,
            (int)AudioClipTypes.Damage => damageSfx[randomNum],
            (int)AudioClipTypes.Laugh => laughSfx[randomNum],
            (int)AudioClipTypes.Stun => stunSfx[randomNum],
            (int)AudioClipTypes.Upset => upsetSfx[randomNum],
            (int)AudioClipTypes.LongLaugh => longLaughSfx[randomNum],
            (int)AudioClipTypes.Shocked => shockedSfx[randomNum],
            (int)AudioClipTypes.Taunt => tauntSfx[randomNum],
            _ => null
        };

        if (!audioClip)
        {
            HarpGhostPlugin.Logger.LogError($"Bagpipes ghost voice audio clip index '{typeIndex}' and randomNum: '{randomNum}' is null");
            return;
        }

        HarpGhostPlugin.LogVerbose($"Playing audio clip: {audioClip.name}");
        if (interrupt) creatureVoiceSource.Stop(true);
        creatureVoiceSource.PlayOneShot(audioClip);
        WalkieTalkie.TransmitOneShotAudio(creatureVoiceSource, audioClip, creatureVoiceSource.volume);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PlaySfx(AudioClip clip, float volume = 1f)
    {
        creatureSfxSource.volume = volume;
        creatureSfxSource.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(creatureSfxSource, clip, volume);
    }

    private void SetBool(int parameter, bool value)
    {
        animator.SetBool(parameter, value);
    }

    public bool GetBool(int parameter)
    {
        return animator.GetBool(parameter);
    }

    private void SetTrigger(int parameter)
    {
        animator.SetTrigger(parameter);
    }

    private void HandleOnEnterDeathState()
    {
        HandleDropInstrument(transform.position);
        SetBool(IsDead, true);
        SetBool(IsRunning, false);
        SetBool(IsStunned, false);
        creatureVoiceSource.Stop(true);
        creatureSfxSource.Stop(true);
        PlayVoice((int)AudioClipTypes.Death, 1);
        Destroy(this);
    }

    private void HandleOnInitializeConfigValues()
    {
        creatureVoiceSource.volume = BagpipeGhostConfig.Default.BagpipeGhostVoiceSfxVolume.Value;
    }
}