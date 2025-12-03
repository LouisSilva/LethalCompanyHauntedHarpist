using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAudioManager : MonoBehaviour
{
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
        if (!creatureSfxSource) HarpGhostPlugin.Logger.LogError("creatureSfxSource is null");
        if (!creatureVoiceSource) HarpGhostPlugin.Logger.LogError("creatureVoiceSource is null");

        if (damageSfx == null || damageSfx.Length == 0) HarpGhostPlugin.Logger.LogError("DamageSfx is null or empty");
        if (laughSfx == null || laughSfx.Length == 0) HarpGhostPlugin.Logger.LogError("LaughSfx is null or empty");
        if (stunSfx == null || stunSfx.Length == 0) HarpGhostPlugin.Logger.LogError("StunSfx is null or empty");
        if (upsetSfx == null || upsetSfx.Length == 0) HarpGhostPlugin.Logger.LogError("UpsetSfx is null or empty");
        if (!dieSfx) HarpGhostPlugin.Logger.LogError("DieSfx is null");
    }

    private void OnEnable()
    {
        if (!netcodeController) return;
        netcodeController.OnInitializeConfigValues += HandleOnInitializeConfigValues;
        netcodeController.OnPlayCreatureVoice += PlayVoice;
        netcodeController.OnEnterDeathState += HandleOnEnterDeathState;
    }

    private void OnDisable()
    {
        if (!netcodeController) return;
        netcodeController.OnInitializeConfigValues -= HandleOnInitializeConfigValues;
        netcodeController.OnPlayCreatureVoice -= PlayVoice;
        netcodeController.OnEnterDeathState -= HandleOnEnterDeathState;
    }

    private void HandleOnInitializeConfigValues()
    {
        creatureVoiceSource.volume = HarpGhostConfig.Default.HarpGhostVoiceSfxVolume.Value;
    }

    private void HandleOnEnterDeathState()
    {
        creatureVoiceSource.Stop(true);
        creatureSfxSource.Stop(true);
        PlayVoice((int)AudioClipTypes.Death, 1, audibleByEnemies: true);
        Destroy(this);
    }

    private void PlayVoice(int typeIndex, int randomNum, bool interrupt = true, bool audibleByEnemies = false)
    {
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

        if (!audioClip)
        {
            HarpGhostPlugin.Logger.LogError($"Harp ghost voice audio clip index '{typeIndex}' and randomNum: '{randomNum}' is null");
            return;
        }

        HarpGhostPlugin.LogVerbose($"Playing audio clip: {audioClip.name}");
        if (interrupt) creatureVoiceSource.Stop(true);
        creatureVoiceSource.PlayOneShot(audioClip);
        WalkieTalkie.TransmitOneShotAudio(creatureVoiceSource, audioClip, creatureVoiceSource.volume);
        if (audibleByEnemies) RoundManager.Instance.PlayAudibleNoise(creatureVoiceSource.transform.position);
    }
}