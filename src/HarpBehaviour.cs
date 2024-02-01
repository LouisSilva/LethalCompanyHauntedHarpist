using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost;

public class HarpBehaviour : PhysicsProp
{
    private ManualLogSource mls;

    public AudioSource harpAudioSource;
    public List<AudioClip> harpAudioClips;
    private RoundManager roundManager;
    private System.Random musicRandomizer;
    private bool isPlayingMusic;
    private int timesPlayedWithoutTurningOff;
    private float noiseInterval;

    [Serializable] private struct ItemOffset(
        Vector3 positionOffset = default,
        Vector3 rotationOffset = default,
        Vector3 restingRotation = default)
    {
        public Vector3 positionOffset = positionOffset;
        public Vector3 rotationOffset = rotationOffset;
        public Vector3 restingRotation = restingRotation;
    }

    [SerializeField] private ItemOffset playerHarpOffset;
    [SerializeField] private ItemOffset enemyHarpOffset;
    
    public override void Start()
    {
        base.Start();
        mls = BepInEx.Logging.Logger.CreateLogSource("Harp Behaviour");
        
        roundManager = FindObjectOfType<RoundManager>();
        musicRandomizer = new System.Random(FindObjectOfType<StartOfRound>().randomMapSeed - 10);
        playerHarpOffset = new ItemOffset(new Vector3(-0.8f, 0.22f, 0.07f), new Vector3(3, 12, -100));
        enemyHarpOffset = new ItemOffset(new Vector3(0, -0.6f, 0.6f));
        
        mls.LogInfo("Harp spawned");
    }

    public override void Update()
    {
        base.Update();
        if (isHeldByEnemy)
        {
            itemProperties.positionOffset = enemyHarpOffset.positionOffset;
            itemProperties.rotationOffset = enemyHarpOffset.rotationOffset;
            itemProperties.restingRotation = enemyHarpOffset.restingRotation;
        }

        else
        {
            itemProperties.positionOffset = playerHarpOffset.positionOffset;
            itemProperties.rotationOffset = playerHarpOffset.rotationOffset;
            itemProperties.restingRotation = playerHarpOffset.restingRotation;
        }
        
        if (!isPlayingMusic) return;
        if (noiseInterval <= 0.0)
        {
            noiseInterval = 1f;
            ++timesPlayedWithoutTurningOff;
            roundManager.PlayAudibleNoise(transform.position, 16f, 0.9f, timesPlayedWithoutTurningOff, noiseID: 590);
        }

        else noiseInterval -= Time.deltaTime;
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);
        StartMusic(used);
    }

    private void StartMusic(bool startMusic, bool pitchDown = false)
    {
        if (startMusic)
        {
            int musicClipIndex = musicRandomizer.Next(0, harpAudioClips.Count - 1);
            harpAudioSource.clip = harpAudioClips[musicClipIndex];
            harpAudioSource.pitch = 1f;
            harpAudioSource.Play();
            
            PlayMusicServerRpc(true, musicClipIndex);
        }
        
        else if (isPlayingMusic)
        {
            PlayMusicServerRpc(false, -1);
            if (pitchDown) StartCoroutine(musicPitchDown());
            else
            {
                harpAudioSource.Stop();
            }

            timesPlayedWithoutTurningOff = 0;
        }

        isBeingUsed = startMusic;
        isPlayingMusic = startMusic;
    }
    
    private IEnumerator musicPitchDown()
    {
        for (int i = 0; i < 30; ++i)
        {
            yield return null;
            harpAudioSource.pitch -= 0.033f;
            if (harpAudioSource.pitch <= 0.0)
                break;
        }
        harpAudioSource.Stop();
    }

    public override void PocketItem()
    {
        base.PocketItem();
        StartMusic(false);
    }

    public override void OnHitGround()
    {
        base.OnHitGround();
        StartMusic(false);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayMusicServerRpc(bool startMusic, int musicClipIndex)
    {
        PlayMusicClientRpc(startMusic, musicClipIndex);
    }

    [ClientRpc]
    public void PlayMusicClientRpc(bool startMusic, int musicClipIndex)
    {
        if (startMusic)
        {
            harpAudioSource.clip = harpAudioClips[musicClipIndex];
            harpAudioSource.Play();
        }

        else
        {
            harpAudioSource.Stop();
        }
    }
}