using System;
using System.Collections;
using BepInEx.Logging;
using LethalCompanyHarpGhost.BagpipesGhost;
using LethalCompanyHarpGhost.HarpGhost;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost.Items;

public class InstrumentBehaviour : PhysicsProp
{
    private ManualLogSource _mls;

    private string _instrumentId;

    public AudioSource instrumentAudioSource;
    public AudioClip[] instrumentAudioClips;

    [SerializeField] private AudioLowPassFilter instrumentAudioLowPassFilter;
    [SerializeField] private AudioHighPassFilter instrumentAudioHighPassFilter;
    [SerializeField] private AudioEchoFilter instrumentAudioEchoFilter;
    [SerializeField] private AudioChorusFilter instrumentAudioChorusFilter;
    [SerializeField] private OccludeAudio instrumentOccludeAudio;
    
    private int _timesPlayedWithoutTurningOff;
    
    private float _noiseInterval;
    private float _defaultPitch;
    
    private bool _isPlayingMusic;
    private bool _isInAltPlayerOffset;
    
    [Serializable]
    public struct ItemOffset : INetworkSerializable
    {
        public Vector3 positionOffset = default;
        public Vector3 rotationOffset = default;
    
        public ItemOffset(Vector3 positionOffset = default, Vector3 rotationOffset = default)
        {
            this.positionOffset = positionOffset;
            this.rotationOffset = rotationOffset;
        }
    
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref positionOffset);
            serializer.SerializeValue(ref rotationOffset);
        }
    }

    [SerializeField] private ItemOffset playerInstrumentOffset;
    [SerializeField] private ItemOffset playerAltInstrumentOffset;
    [SerializeField] private ItemOffset enemyInstrumentOffset;

    private void Awake()
    {
        // This has to be done because for some reason the ItemOffset values in the unity inspector just get overriden for some reason
        switch (itemProperties.itemName)
        {
            case "Harp":
                playerInstrumentOffset = new ItemOffset(new Vector3(-0.8f, 0.22f, 0.07f), new Vector3(3f, 12f, -100f));
                playerAltInstrumentOffset = new ItemOffset(new Vector3(-0.4f, 0.2f, -0.1f), new Vector3(-70, 115, -200));
                enemyInstrumentOffset = new ItemOffset(new Vector3(0f, -0.6f, 0.6f));
                
                _defaultPitch = Mathf.Clamp(HarpGhostConfig.Default.HarpPitch.Value, -3f, 3f);
                instrumentAudioSource.pitch = _defaultPitch;
                instrumentAudioSource.volume = Mathf.Clamp(HarpGhostConfig.Default.HarpVolume.Value, 0f, 1f);
                instrumentAudioSource.bypassReverbZones = HarpGhostConfig.Default.HarpBypassReverbZones.Value;
                instrumentAudioSource.reverbZoneMix = Mathf.Clamp(HarpGhostConfig.Default.HarpReverbZoneMix.Value, 0f, 1.1f);
                instrumentAudioSource.dopplerLevel = Mathf.Clamp(HarpGhostConfig.Default.HarpDopplerLevel.Value, 0f, 5f);
                instrumentAudioSource.spread = Mathf.Clamp(HarpGhostConfig.Default.HarpSoundSpread.Value, 0, 360);
                instrumentAudioSource.maxDistance = Mathf.Clamp(HarpGhostConfig.Default.HarpSoundMaxDistance.Value, 0, Mathf.Infinity);
                
                instrumentAudioLowPassFilter = GetComponent<AudioLowPassFilter>();
                instrumentAudioLowPassFilter.enabled = HarpGhostConfig.Default.HarpAudioLowPassFilterEnabled.Value;
                instrumentAudioLowPassFilter.cutoffFrequency = Mathf.Clamp(HarpGhostConfig.Default.HarpAudioLowPassFilterCutoffFrequency.Value, 10, 2200);
                instrumentAudioLowPassFilter.lowpassResonanceQ = HarpGhostConfig.Default.HarpAudioLowPassFilterLowpassResonanceQ.Value;
                
                instrumentAudioHighPassFilter = GetComponent<AudioHighPassFilter>();
                instrumentAudioHighPassFilter.enabled = HarpGhostConfig.Default.HarpAudioHighPassFilterEnabled.Value;
                instrumentAudioHighPassFilter.cutoffFrequency = Mathf.Clamp(HarpGhostConfig.Default.HarpAudioHighPassFilterCutoffFrequency.Value, 10, 22000);
                instrumentAudioHighPassFilter.highpassResonanceQ = HarpGhostConfig.Default.HarpAudioHighPassFilterHighpassResonanceQ.Value;
                
                instrumentAudioEchoFilter = GetComponent<AudioEchoFilter>();
                instrumentAudioEchoFilter.enabled = HarpGhostConfig.Default.HarpAudioEchoFilterEnabled.Value;
                instrumentAudioEchoFilter.delay = HarpGhostConfig.Default.HarpAudioEchoFilterDelay.Value;
                instrumentAudioEchoFilter.decayRatio = Mathf.Clamp(HarpGhostConfig.Default.HarpAudioEchoFilterDecayRatio.Value, 0f, 1f);
                instrumentAudioEchoFilter.dryMix = HarpGhostConfig.Default.HarpAudioEchoFilterDryMix.Value;
                instrumentAudioEchoFilter.wetMix = HarpGhostConfig.Default.HarpAudioEchoFilterWetMix.Value;

                instrumentAudioChorusFilter = GetComponent<AudioChorusFilter>();
                instrumentAudioChorusFilter.enabled = HarpGhostConfig.Default.HarpAudioChorusFilterEnabled.Value;
                instrumentAudioChorusFilter.dryMix = HarpGhostConfig.Default.HarpAudioChorusFilterDryMix.Value;
                instrumentAudioChorusFilter.wetMix1 = HarpGhostConfig.Default.HarpAudioChorusFilterWetMix1.Value;
                instrumentAudioChorusFilter.wetMix2 = HarpGhostConfig.Default.HarpAudioChorusFilterWetMix2.Value;
                instrumentAudioChorusFilter.wetMix3 = HarpGhostConfig.Default.HarpAudioChorusFilterWetMix3.Value;
                instrumentAudioChorusFilter.delay = HarpGhostConfig.Default.HarpAudioChorusFilterDelay.Value;
                instrumentAudioChorusFilter.rate = HarpGhostConfig.Default.HarpAudioChorusFilterRate.Value;
                instrumentAudioChorusFilter.depth = HarpGhostConfig.Default.HarpAudioChorusFilterDepth.Value;

                instrumentOccludeAudio = GetComponent<OccludeAudio>();
                instrumentOccludeAudio.enabled = HarpGhostConfig.Default.HarpOccludeAudioEnabled.Value;
                instrumentOccludeAudio.useReverb = HarpGhostConfig.Default.HarpOccludeAudioUseReverbEnabled.Value;
                instrumentOccludeAudio.overridingLowPass = HarpGhostConfig.Default.HarpOccludeAudioOverridingLowPassEnabled.Value;
                instrumentOccludeAudio.lowPassOverride = HarpGhostConfig.Default.HarpOccludeAudioLowPassOverride.Value;
                
                break;
            
            case "Bagpipes":
                playerInstrumentOffset = new ItemOffset(new Vector3(-0.24f, 0.15f, 0.07f), new Vector3(-5f, 15f, -105f));
                enemyInstrumentOffset = new ItemOffset(new Vector3(0.5f, -0.25f, 0.4f), new Vector3(0, 90, 0));
                
                _defaultPitch = Mathf.Clamp(BagpipeGhostConfig.Default.BagpipesPitch.Value, -3f, 3f);
                instrumentAudioSource.pitch = _defaultPitch;
                instrumentAudioSource.volume = Mathf.Clamp(BagpipeGhostConfig.Default.BagpipesVolume.Value, 0f, 1f);
                instrumentAudioSource.maxDistance = Mathf.Clamp(BagpipeGhostConfig.Default.BagpipesSoundMaxDistance.Value, 0, Mathf.Infinity);
                
                break;
            
            case "Tuba":
                playerInstrumentOffset = new ItemOffset(new Vector3(-0.4f, 0.2f, -0.1f), new Vector3(-70, 115, -200));
                break;
            
            case "Sitar":
                break;
        }
    }

    public override void Start()
    {
        base.Start();
        
        _instrumentId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Instrument {_instrumentId}");
        
        Random.InitState(StartOfRound.Instance.randomMapSeed + _instrumentId.GetHashCode());
        
        if (!IsOwner) return;
        
        if (instrumentAudioSource == null)
        {
            _mls.LogError("instrumentAudioSource is null!");
            return;
        }
        
        if (instrumentAudioClips == null || instrumentAudioClips.Length == 0)
        {
            _mls.LogError("instrumentAudioClips is null or empty!");
        }
        
        _isPlayingMusic = false;
    }
    
    public override void Update()
    {
        base.Update();
        if (!IsOwner) return;

        if (!_isPlayingMusic) return;
        if (_noiseInterval <= 0.0)
        {
            _noiseInterval = 1f;
            ++_timesPlayedWithoutTurningOff;
            RoundManager.Instance.PlayAudibleNoise(
                transform.position,
                16f,
                3f,
                _timesPlayedWithoutTurningOff,
                isInShipRoom && StartOfRound.Instance.hangarDoorsClosed,
                540);
        }

        else _noiseInterval -= Time.deltaTime;
    }
    
    public override void LateUpdate()
    {
        if (parentObject != null)
        {
            Vector3 rotationOffset;
            Vector3 positionOffset;
            if (isHeldByEnemy)
            {
                rotationOffset = enemyInstrumentOffset.rotationOffset;
                positionOffset = enemyInstrumentOffset.positionOffset;
            }
            else
            {
                if (_isInAltPlayerOffset)
                {
                    rotationOffset = playerAltInstrumentOffset.rotationOffset;
                    positionOffset = playerAltInstrumentOffset.positionOffset;
                }
                else
                {
                    rotationOffset = playerInstrumentOffset.rotationOffset;
                    positionOffset = playerInstrumentOffset.positionOffset;
                }
            }
            
            transform.rotation = parentObject.rotation;
            transform.Rotate(rotationOffset);
            transform.position = parentObject.position;
            transform.position += parentObject.rotation * positionOffset;
            
        }
        if (!(radarIcon != null)) return;
        radarIcon.position = transform.position;
    }
    
    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);
        if (!IsOwner) return;
        switch (_isPlayingMusic)
        {
            case false:
                StartMusicServerRpc();
                break;
            
            case true:
                StopMusicServerRpc();
                break;
        }

        isBeingUsed = used;
    }

    private void StartMusic(int clipIndex)
    {
        AudioClip selectedClip = HarpGhostPlugin.GetInstrumentAudioClip(itemProperties.itemName, clipIndex);
        if (selectedClip == null)
        {
            _mls.LogWarning($"{itemProperties.itemName} audio clips not loaded yet!");
            selectedClip = instrumentAudioClips[clipIndex];
        }
        
        instrumentAudioSource.clip = selectedClip;
        instrumentAudioSource.pitch = _defaultPitch;
        instrumentAudioSource.Play();
        
        WalkieTalkie.TransmitOneShotAudio(instrumentAudioSource, instrumentAudioSource.clip, instrumentAudioSource.volume);
        
        _isPlayingMusic = true;
    }

    private void StopMusic()
    {
        StartCoroutine(MusicPitchDown());
        _timesPlayedWithoutTurningOff = 0;
        _isPlayingMusic = false;
    }

    public override void ItemInteractLeftRight(bool right)
    {
        base.ItemInteractLeftRight(right);
        if (!right || (playerAltInstrumentOffset.positionOffset == default &&
                       playerInstrumentOffset.rotationOffset == default))
        {
            return;
        }
        
        // Set alternative player offsets if it exists
        _isInAltPlayerOffset = !_isInAltPlayerOffset;
    }
    
    private IEnumerator MusicPitchDown()
    {
        for (int i = 0; i < 30; ++i)
        {
            yield return null;
            instrumentAudioSource.pitch -= 0.033f;
            if (instrumentAudioSource.pitch <= 0.0) break;
        }
        instrumentAudioSource.Stop();
    }

    public override void PocketItem()
    {
        base.PocketItem();
        StopMusicServerRpc();
    }

    public override void EquipItem()
    {
        base.EquipItem();
        isHeld = true;
    }

    public override void FallWithCurve()
    {
        base.FallWithCurve();
        isHeld = false;
        isHeldByEnemy = false;
    }

    public override void OnHitGround()
    {
        base.OnHitGround();
        isHeld = false;
        isHeldByEnemy = false;
        StopMusicServerRpc();
    }

    public override void GrabItemFromEnemy(EnemyAI enemy)
    {
        base.GrabItemFromEnemy(enemy);
        isHeldByEnemy = true;
        isHeld = true;
    }

    public override void DiscardItemFromEnemy()
    {
        base.DiscardItemFromEnemy();
        isHeldByEnemy = false;
        isHeld = false;
    }

    [ServerRpc(RequireOwnership = false)]
    internal void StopMusicServerRpc()
    {
        if (!_isPlayingMusic) return;
        StopMusicClientRpc();
    }
    
    [ClientRpc]
    private void StopMusicClientRpc()
    {
        StopMusic();
    }
    
    [ServerRpc(RequireOwnership = false)]
    internal void StartMusicServerRpc()
    {
        if (_isPlayingMusic) return;
        StartMusicClientRpc(Random.Range(0, instrumentAudioClips.Length));
    }

    [ClientRpc]
    private void StartMusicClientRpc(int clipNumber)
    {
        StartMusic(clipNumber);
    }
    
    private void LogDebug(string msg)
    {
#if DEBUG
        if (!IsOwner) return;
        _mls.LogInfo(msg);
#endif
    }
}