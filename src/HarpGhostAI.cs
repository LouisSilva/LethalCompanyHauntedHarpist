using System;
using System.Collections;
using BepInEx.Logging;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static LethalCompanyHarpGhost.HarpGhostPlugin;

namespace LethalCompanyHarpGhost;

public class HarpGhostAI : EnemyAI
{
    private ManualLogSource mls;

    public AISearchRoutine roamMap;
    public AISearchRoutine searchForPlayers;

    public Transform turnCompass;
    public Transform grabTarget;

    private NetworkObjectReference harpObjectRef;
    
    private float timeSinceHittingLocalPlayer;
    private float hearNoiseCooldown;
    [SerializeField] private float agentMaxAcceleration;
    [SerializeField] private float agentMaxSpeed;

    private int harpScrapValue;

    [SerializeField] private bool harpGhostDebug = true;
    private bool inChase;

    private HarpBehaviour heldHarp;

    private enum NoiseIDToIgnore
    {
        Harp = 540
    }
    
    private static readonly int Dead = Animator.StringToHash("Dead");
    private static readonly int Stunned = Animator.StringToHash("stunned");
    private static readonly int Recover = Animator.StringToHash("recover");
    private static readonly int Attack = Animator.StringToHash("attack");
    private static readonly int IsRunning = Animator.StringToHash("isRunning");

    public override void Start()
    {
        base.Start();
        mls = BepInEx.Logging.Logger.CreateLogSource("Harp Ghost");
        mls.LogInfo("Harp Ghost Spawned");

        if (!IsServer) return;
        
        SpawnHarpServerRpc();
        GrabHarpIfNotHolding();
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) mls.LogError("NavMeshAgent component not found on " + name);

        creatureAnimator = GetComponent<Animator>();
        if (creatureAnimator == null) mls.LogError("Animator component not found on " + name);
        
        timeSinceHittingLocalPlayer = 0;
        agentMaxAcceleration = 200f;
        agentMaxSpeed = 0.5f;
        openDoorSpeedMultiplier = 0.6f;
        movingTowardsTargetPlayer = false;
        inChase = false;
        
        creatureAnimator.SetBool(Dead, false);
        creatureAnimator.SetBool(IsRunning, false);
        StartCoroutine(DelayedHarpMusicActivate()); // Needed otherwise the music won't play, maybe because multiple things are fighting for the heldHarp object
    }
    
    public override void Update()
    {
        base.Update();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;

        timeSinceHittingLocalPlayer += Time.deltaTime;
        hearNoiseCooldown -= Time.deltaTime;

        switch (currentBehaviourStateIndex)
        {
            case 0: // harp ghost playing music and chilling
            {
                if (previousBehaviourStateIndex != 0)
                {
                    agentMaxSpeed = 0.5f;
                    openDoorSpeedMultiplier = 0.6f;
                    previousBehaviourStateIndex = 0;
                    movingTowardsTargetPlayer = false;
                    LogDebug($"Harp Ghost '{gameObject.name}': Switched to behaviour state 0");
                }
                
                break;
            }

            case 1: // harp ghost is angry and trying to find players to attack
            {
                if (previousBehaviourStateIndex != 1)
                {   
                    agentMaxSpeed = 3f;
                    openDoorSpeedMultiplier = 1f;
                    previousBehaviourStateIndex = 1;
                    movingTowardsTargetPlayer = false;
                    DropHarpServerRpc(transform.position);
                    LogDebug($"Harp Ghost '{gameObject.name}': Switched to behaviour state 1");
                }

                creatureAnimator.SetBool(IsRunning, agent.speed != 0 && inChase);
                break;
            }
        }
        
        CalculateAgentSpeed();
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;

        switch (currentBehaviourStateIndex)
        {
            case 0:
            {
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                if (!roamMap.inProgress) StartSearch(transform.position, roamMap);
                break;
            }

            case 1:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                bool targetPlayerBool = TargetClosestPlayer(1.5f, true, 165F);
                
                if (targetPlayerBool)
                {
                    if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                    movingTowardsTargetPlayer = true;
                    agentMaxSpeed = 5f;
                    inChase = true;
                }
                else
                {
                    movingTowardsTargetPlayer = false;
                    inChase = false;
                    agentMaxSpeed = 3f;
                    if (!searchForPlayers.inProgress) StartSearch(transform.position, searchForPlayers);
                }

                break;
            }
        }
    }

    private void LogDebug(string logMessage)
    {
        if (harpGhostDebug) mls.LogInfo(logMessage);
    }

    private void CalculateAgentSpeed()
    {
        if (stunNormalizedTimer > 0)
        {
            agent.speed = 0;
            agent.acceleration = agentMaxAcceleration;
            return;
        }

        if (currentBehaviourStateIndex >= 0)
        {
            MoveWithAcceleration();
        }
    }

    private void MoveWithAcceleration() {
        float speedAdjustment = Time.deltaTime / 2f;
        agent.speed = Mathf.Lerp(agent.speed, agentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        agent.acceleration = Mathf.Lerp(agent.acceleration, agentMaxAcceleration, accelerationAdjustment);
    }

    private void GrabHarpIfNotHolding()
    {
        if (heldHarp != null) return;
        if (!harpObjectRef.TryGet(out NetworkObject networkObject)) return;
        heldHarp = networkObject.gameObject.GetComponent<HarpBehaviour>();
        GrabHarp(heldHarp.gameObject);
    }

    private void GrabHarp(GameObject harpObject)
    {
        heldHarp = harpObject.GetComponent<HarpBehaviour>();
        if (heldHarp == null)
        {
            mls.LogError("Harp in GrabHarp function did not contain harpItem component");
            return;
        }
        
        heldHarp.SetScrapValue(harpScrapValue);
        RoundManager.Instance.totalScrapValueInLevel += heldHarp.scrapValue;
        heldHarp.parentObject = grabTarget;
        heldHarp.isHeldByEnemy = true;
        heldHarp.grabbableToEnemies = false;
        heldHarp.grabbable = false;
        heldHarp.GrabItemFromEnemy(this);
    }

    private void DropHarp(Vector3 dropPosition)
    {
        if (heldHarp == null) return;
        heldHarp.parentObject = null;
        heldHarp.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        heldHarp.EnablePhysics(true);
        heldHarp.fallTime = 0.0f;
        Transform parent;
        heldHarp.startFallingPosition = (parent = heldHarp.transform.parent).InverseTransformPoint(heldHarp.transform.position);
        heldHarp.targetFloorPosition = parent.InverseTransformPoint(dropPosition);
        heldHarp.floorYRot = -1;
        heldHarp.grabbable = true;
        heldHarp.grabbableToEnemies = true;
        heldHarp.isHeld = false;
        heldHarp.isHeldByEnemy = false;
        heldHarp.ItemActivate(false);
        heldHarp.DiscardItemFromEnemy();
        heldHarp = null;
    }

    private IEnumerator StunnedAnimation()
    {
        while (stunNormalizedTimer > 0) yield return new WaitForSeconds(0.02f);
        creatureAnimator.SetTrigger(Recover);
    }

    private IEnumerator DelayedHarpMusicActivate()
    {
        yield return new WaitForSeconds(0.5f);
        heldHarp.ItemActivate(true);
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX);
        enemyHP -= force;

        if (!IsOwner) return;
        if (enemyHP > 0) return;
        
        DropHarp(transform.position);
        creatureAnimator.SetBool(Dead, true);
        KillEnemyOnOwnerClient();
    }

    public override void SetEnemyStunned(
        bool setToStunned, 
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        creatureAnimator.SetTrigger(Stunned);
        StartCoroutine(StunnedAnimation());
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        if (timeSinceHittingLocalPlayer < 1f || currentBehaviourStateIndex == 0) return;
        
        PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
        if (playerControllerB == null) return;
        
        LogDebug($"Harp Ghost '{gameObject.name}': Collision with player '{playerControllerB.name}'");
        timeSinceHittingLocalPlayer = 0f;
        creatureAnimator.SetTrigger(Attack);
        playerControllerB.DamagePlayer(10);
    }
    
    public override void DetectNoise(
        Vector3 noisePosition, 
        float noiseLoudness, 
        int timesNoisePlayedInOneSpot = 0,
        int noiseID = 0)
    {
        base.DetectNoise(noisePosition, noiseLoudness, timesNoisePlayedInOneSpot, noiseID);
        if ((double)stunNormalizedTimer > 0 || (double)hearNoiseCooldown > 0 || currentBehaviourStateIndex == 1 || Enum.IsDefined(typeof(NoiseIDToIgnore), noiseID)) return;
        hearNoiseCooldown = 0.03f;

        float distanceToNoise = Vector3.Distance(transform.position, noisePosition);
        float noiseThreshold = 5f * noiseLoudness;
        LogDebug($"Harp Ghost '{gameObject.name}': Heard Noise | Distance: {distanceToNoise} meters away | Noise threshold: {noiseThreshold}");

        if (Physics.Linecast(transform.position, noisePosition, 256))
        {
            noiseLoudness /= 2f;
            noiseThreshold /= 2f;
        }

        if (noiseLoudness < 0.25) return;
        if (currentBehaviourStateIndex != 1 && distanceToNoise < noiseThreshold)
        {
            SwitchToBehaviourState(1);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void DropHarpServerRpc(Vector3 dropPosition)
    {
        DropHarpClientRpc(dropPosition);
    }

    [ClientRpc]
    public void DropHarpClientRpc(Vector3 dropPosition)
    {
        DropHarp(dropPosition);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnHarpServerRpc()
    {
        harpScrapValue = 150;
        GameObject harpObject = Instantiate(
            harpItem.spawnPrefab, 
            transform.position, 
            Quaternion.identity,
            RoundManager.Instance.spawnedScrapContainer
            );

        AudioSource harpAudioSource = harpObject.GetComponent<AudioSource>();
        if (harpAudioSource == null)
        {
            harpAudioSource = harpObject.AddComponent<AudioSource>();
            harpAudioSource.playOnAwake = false;
            harpAudioSource.loop = true;
            harpAudioSource.spatialBlend = 1;
            harpAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        }
        
        ScanNodeProperties harpScanNodeProperties = harpObject.AddComponent<ScanNodeProperties>();
        harpScanNodeProperties.scrapValue = harpScrapValue;
        
        harpObject.GetComponent<GrabbableObject>().fallTime = 0f;
        harpObject.GetComponent<GrabbableObject>().SetScrapValue(harpScrapValue);
        harpObject.GetComponent<NetworkObject>().Spawn();

        HarpBehaviour harpBehaviour = harpObject.GetComponent<HarpBehaviour>();
        if (harpBehaviour != null)
        {
            harpBehaviour.harpAudioSource = harpAudioSource;
            harpBehaviour.harpAudioClips = harpAudioClips;
        }

        else
        {
            mls.LogError("Spawned Harp object does not have HarpBehaviour component!");
        }
        
        SpawnHarpClientRpc(harpObject, harpScrapValue);
    }

    [ClientRpc]
    public void SpawnHarpClientRpc(NetworkObjectReference harpObject, int harpValue)
    {
        harpScrapValue = harpValue;
        harpObjectRef = harpObject;
    }
}

/*
 TODO:

1). Add better way of making ghost annoyed of noise
2). Make ghost able to open doors
3). Make ghost able to change elevation
 
 */
