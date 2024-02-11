using UnityEngine;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAudioManager : MonoBehaviour
{
    [SerializeField] private AudioSource creatureVoiceSource;
    [SerializeField] private AudioSource creatureSfxSource;

    public void PlayVoice(AudioClip clip, float volume = 1f)
    {
        creatureVoiceSource.pitch = UnityEngine.Random.Range(0.8f, 1.1f);
        creatureVoiceSource.volume = volume;
        creatureVoiceSource.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(creatureVoiceSource, clip, volume);
    }
    
    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        creatureSfxSource.volume = volume;
        creatureSfxSource.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(creatureSfxSource, clip, volume);
    }
}