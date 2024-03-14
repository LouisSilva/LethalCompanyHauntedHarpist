using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost.EnforcerGhost;

public class EnforcerGhostAudioManager : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;
    
    private ShotgunItem _heldShotgun;
    private NetworkObjectReference _shotgunObjectRef;
    
    [Header("Audio")]
    [Space(5f)]
    #pragma warning disable 0649
    [SerializeField] private AudioSource creatureVoiceSource;
    [SerializeField] private AudioSource creatureSfxSource;
    #pragma warning restore 0649
    
    public AudioClip[] damageSfx;
    public AudioClip[] laughSfx;
    public AudioClip[] stunSfx;
    public AudioClip[] upsetSfx;
    public AudioClip dieSfx;

    [Space(5f)]
    [Header("Controllers")]
    #pragma warning disable 0649
    [SerializeField] private EnforcerGhostNetcodeController netcodeController;
    #pragma warning restore 0649

    private enum AudioClipTypes
    {
        Death = 0,
        Damage = 1,
        Laugh = 2,
        Stun = 3,
        Upset = 4
    }
    
    public enum NoiseIds
    {
        PlayerFootstepsLocal = 6,
        PlayerFootstepsServer = 7,
        Lightning = 11,
        PlayersTalking = 75,
        Harp = 540,
        BaboonHawkCaw = 1105,
        ShipHorn = 14155,
        WhoopieCushionFart = 101158,
        DropSoundEffect = 941,
        Boombox = 5,
        ItemDropship = 94,
        DoubleWing = 911,
        RadarBoosterPing = 1015,
        Jetpack = 41,
        DocileLocustBees = 14152,
    }
    
    public enum NoiseIDToIgnore
    {
        Harp = 540,
        DoubleWing = 911,
        Lightning = 11,
        DocileLocustBees = 14152,
        BaboonHawkCaw = 1105
    }

    private void Start()
    {
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Enforcer Ghost Audio  {_ghostId}");
        
        if (creatureSfxSource == null) _mls.LogError("creatureSfxSource is null");
        if (creatureVoiceSource == null) _mls.LogError("creatureVoiceSource is null");
        
        if (dieSfx == null) _mls.LogError("DieSfx is null");
    }

    private void OnEnable()
    {
        netcodeController.OnInitializeConfigValues += HandleOnInitializeConfigValues;
        netcodeController.OnPlayCreatureVoice += PlayVoice;
        netcodeController.OnEnterDeathState += HandleOnEnterDeathState;
        netcodeController.OnUpdateGhostIdentifier += HandleUpdateGhostIdentifier;
        netcodeController.OnGrabShotgunPhaseTwo += HandleGrabShotgunPhaseTwo;
    }

    private void OnDestroy()
    {
        netcodeController.OnInitializeConfigValues -= HandleOnInitializeConfigValues;
        netcodeController.OnPlayCreatureVoice -= PlayVoice;
        netcodeController.OnEnterDeathState -= HandleOnEnterDeathState;
        netcodeController.OnUpdateGhostIdentifier -= HandleUpdateGhostIdentifier;
        netcodeController.OnGrabShotgunPhaseTwo -= HandleGrabShotgunPhaseTwo;
    }
    
    private void HandleGrabShotgunPhaseTwo(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldShotgun != null) return;
        if (!_shotgunObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldShotgun = networkObject.gameObject.GetComponent<ShotgunItem>();
    }

    private void HandleOnEnterDeathState(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        creatureVoiceSource.Stop(true);
        creatureSfxSource.Stop(true);
        PlayVoice(_ghostId, (int)AudioClipTypes.Death, 1);
        Destroy(this);
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
        if (interrupt) creatureVoiceSource.Stop(true);
        creatureVoiceSource.pitch = Random.Range(0.8f, 1.1f);
        creatureVoiceSource.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(creatureVoiceSource, clip, creatureVoiceSource.volume);
    }
    
    private void PlaySfx(AudioClip clip, bool interrupt = true)
    {
        LogDebug($"Playing audio clip: {clip.name}");
        if (interrupt) creatureVoiceSource.Stop(true);
        creatureSfxSource.pitch = Random.Range(0.8f, 1.1f);
        creatureSfxSource.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(creatureSfxSource, clip, creatureSfxSource.volume);
    }
    
    private void HandleUpdateGhostIdentifier(string recievedGhostId)
    {
        _ghostId = recievedGhostId;
    }

    private void HandleOnInitializeConfigValues(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        creatureVoiceSource.volume = HarpGhostConfig.Default.HarpGhostVoiceSfxVolume.Value;
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls.LogInfo(msg);
        #endif
    }
}