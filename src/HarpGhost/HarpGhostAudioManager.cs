using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAudioManager : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _ghostId;
    
#pragma warning disable 0649
    [Header("Audio")] [Space(5f)]
    [SerializeField] private AudioSource creatureVoiceSource;
    [SerializeField] private AudioSource creatureSfxSource;
    
    public AudioClip[] damageSfx;
    public AudioClip[] laughSfx;
    public AudioClip[] stunSfx;
    public AudioClip[] upsetSfx;
    public AudioClip dieSfx;

    [Space(5f)] [Header("Controllers")]
    [SerializeField] private HarpGhostNetcodeController netcodeController;
#pragma warning restore 0649
    
    internal enum AudioClipTypes
    {
        Death = 0,
        Damage = 1,
        Laugh = 2,
        Stun = 3,
        Upset = 4
    }
    
    internal enum NoiseIds
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
    
    internal enum NoiseIDToIgnore
    {
        Harp = 540,
        DoubleWing = 911,
        Lightning = 11,
        DocileLocustBees = 14152,
        BaboonHawkCaw = 1105
    }

    private void Start()
    {
        _mls = Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Harp Ghost Audio Controller {_ghostId}");
        
        if (creatureSfxSource == null) _mls.LogError("creatureSfxSource is null");
        if (creatureVoiceSource == null) _mls.LogError("creatureVoiceSource is null");
        
        if (damageSfx == null || damageSfx.Length == 0) _mls.LogError("DamageSfx is null or empty");
        if (laughSfx == null || laughSfx.Length == 0) _mls.LogError("LaughSfx is null or empty");
        if (stunSfx == null || stunSfx.Length == 0) _mls.LogError("StunSfx is null or empty");
        if (upsetSfx == null || upsetSfx.Length == 0) _mls.LogError("UpsetSfx is null or empty");
        if (dieSfx == null) _mls.LogError("DieSfx is null");
    }

    private void OnEnable()
    {
        if (netcodeController == null) return;
        netcodeController.OnInitializeConfigValues += HandleOnInitializeConfigValues;
        netcodeController.OnPlayCreatureVoice += PlayVoice;
        netcodeController.OnEnterDeathState += HandleOnEnterDeathState;
        netcodeController.OnUpdateGhostIdentifier += HandleUpdateGhostIdentifier;
    }

    private void OnDisable()
    {
        if (netcodeController == null) return;
        netcodeController.OnInitializeConfigValues -= HandleOnInitializeConfigValues;
        netcodeController.OnPlayCreatureVoice -= PlayVoice;
        netcodeController.OnEnterDeathState -= HandleOnEnterDeathState;
        netcodeController.OnUpdateGhostIdentifier -= HandleUpdateGhostIdentifier;
    }

    private void HandleUpdateGhostIdentifier(string receivedGhostId)
    {
        _ghostId = receivedGhostId;
    }

    private void HandleOnInitializeConfigValues(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        creatureVoiceSource.volume = HarpGhostConfig.Default.HarpGhostVoiceSfxVolume.Value;
    }

    private void HandleOnEnterDeathState(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        creatureVoiceSource.Stop(true);
        creatureSfxSource.Stop(true);
        PlayVoice(_ghostId, (int)AudioClipTypes.Death, 1, audibleByEnemies: true);
        Destroy(this);
    }

    private void PlayVoice(string receivedGhostId, int typeIndex, int randomNum, bool interrupt = true, bool audibleByEnemies = false)
    {
        if (_ghostId != receivedGhostId) return;
        creatureVoiceSource.pitch = Random.Range(0.8f, 1.1f);
        
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
            _mls.LogError($"Harp ghost voice audio clip index '{typeIndex}' and randomNum: '{randomNum}' is null");
            return;
        }
        
        LogDebug($"Playing audio clip: {audioClip.name}");
        if (interrupt) creatureVoiceSource.Stop(true);
        creatureVoiceSource.PlayOneShot(audioClip);
        WalkieTalkie.TransmitOneShotAudio(creatureVoiceSource, audioClip, creatureVoiceSource.volume);
        if (audibleByEnemies) RoundManager.Instance.PlayAudibleNoise(creatureVoiceSource.transform.position);
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}