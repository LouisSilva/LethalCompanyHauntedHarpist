using System;
using System.Collections;
using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost;

public class InstrumentBehaviour : PhysicsProp
{
    private ManualLogSource _mls;

    private string _instrumentId;

    public AudioSource instrumentAudioSource;
    public AudioClip[] instrumentAudioClips;
    
    private RoundManager _roundManager;
    
    private int _timesPlayedWithoutTurningOff;
    
    private float _noiseInterval;
    
    private bool _isPlayingMusic;
    
    [Serializable]
    public struct ItemOffset
    {
        public Vector3 positionOffset = default;
        public Vector3 rotationOffset = default;

        public ItemOffset(Vector3 positionOffset = default, Vector3 rotationOffset = default)
        {
            this.positionOffset = positionOffset;
            this.rotationOffset = rotationOffset;
        }

        // public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        // {
        //     serializer.SerializeValue(ref positionOffset);
        //     serializer.SerializeValue(ref rotationOffset);
        // }
    }

    #pragma warning disable 0649
    [SerializeField] private ItemOffset playerInstrumentOffset;
    [SerializeField] private ItemOffset enemyInstrumentOffset;
    #pragma warning restore 0649
    
    public override void Start()
    {
        base.Start();
        if (!IsOwner) return;

        _instrumentId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Instrument {_instrumentId}");
        
        _roundManager = FindObjectOfType<RoundManager>();
        Random.InitState(FindObjectOfType<StartOfRound>().randomMapSeed - 10);
        
        if (instrumentAudioSource == null)
        {
            _mls.LogError("instrumentAudioSource is null!");
            return;
        }

        // ReSharper disable once InvertIf
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
            _roundManager.PlayAudibleNoise(transform.position, 16f, 3f, _timesPlayedWithoutTurningOff, noiseID: 540);
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
                rotationOffset = playerInstrumentOffset.rotationOffset;
                positionOffset = playerInstrumentOffset.positionOffset;
            }
            
            transform.rotation = parentObject.rotation;
            transform.Rotate(rotationOffset);
            transform.position = parentObject.position;
            transform.position += parentObject.rotation * positionOffset;
            
        }
        if (!(radarIcon != null)) return;
        radarIcon.position = transform.position;
    }
    
    private void LogDebug(string logMessage)
    {
        #if DEBUG
        _mls.LogInfo(logMessage);
        #endif
    }
    
    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);
        if (!IsOwner) return;
        LogDebug("Instrument ItemActivate() called");
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

    private void StartMusic()
    {
        instrumentAudioSource.clip = instrumentAudioClips[Random.Range(0, instrumentAudioClips.Length)];
        instrumentAudioSource.pitch = 1f;
        instrumentAudioSource.volume = Mathf.Clamp(HarpGhostConfig.Default.InstrumentVolume.Value, 0f, 1f);
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
        StartMusicClientRpc();
    }

    [ClientRpc]
    private void StartMusicClientRpc()
    {
        StartMusic();
    }
}