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

    private Vector3 targetPlayerLastSeenPos;

    private NetworkObjectReference harpObjectRef;
    
    private float timeSinceHittingLocalPlayer;
    private float hearNoiseCooldown;
    [SerializeField] private float agentMaxAcceleration;
    [SerializeField] private float agentMaxSpeed;
    [SerializeField] private float annoyanceLevel;
    [SerializeField] private float annoyanceDecayRate;
    [SerializeField] private float annoyanceThreshold;
    [SerializeField] private float maxSearchRadius;

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
        LogDebug($"Animation Ids: Dead: {Dead}, Stunned: {Stunned}, Recover: {Recover}, Attack: {Attack}");

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
        annoyanceLevel = 0.0f;
        annoyanceDecayRate = 0.9f;
        annoyanceThreshold = 2.0f;
        maxSearchRadius = 100f;
        movingTowardsTargetPlayer = false;
        inChase = false;
        targetPlayerLastSeenPos = default;
        
        ChangeAnimationParameterBoolServerRpc(Dead, false);
        ChangeAnimationParameterBoolServerRpc(IsRunning, false);
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
                    AIIntervalTime = 0.5f; // In this state the ghost barely does anything so this can be quite high
                    LogDebug($"Harp Ghost '{gameObject.name}': Switched to behaviour state 0");
                }

                if (annoyanceLevel > 0)
                {
                    annoyanceLevel -= annoyanceDecayRate * Time.deltaTime;
                    annoyanceLevel -= Mathf.Max(annoyanceLevel, 0);
                }

                if (annoyanceLevel >= annoyanceThreshold)
                {
                    SwitchToBehaviourState(1);
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

                if (inChase && !creatureAnimator.GetBool(IsRunning) && agent.speed > 0) ChangeAnimationParameterBoolServerRpc(IsRunning, true);
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
                    BeginChasingPlayerServerRpc((int)targetPlayer.playerClientId);
                    // ChangeOwnershipOfEnemy(targetPlayer.actualClientId);
                }
                
                else
                {
                    EndChasingPlayerServerRpc();
                    if (!searchForPlayers.inProgress)
                    {
                        searchForPlayers.searchWidth = 10f;
                        StartSearch(targetPlayerLastSeenPos == default ? transform.position : targetPlayerLastSeenPos, searchForPlayers);
                    }
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
        DoAnimationServerRpc(Recover);
    }

    private IEnumerator DelayedHarpMusicActivate()
    {
        yield return new WaitForSeconds(0.5f);
        heldHarp.ItemActivate(true);
    }

    private IEnumerator DamagePlayerAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (targetPlayer != null)
        {
            targetPlayer.DamagePlayer(35, causeOfDeath: CauseOfDeath.Strangulation);
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX);
        enemyHP -= force;

        if (!IsOwner) return;
        if (enemyHP > 0) return;
        
        DropHarp(transform.position);
        ChangeAnimationParameterBoolServerRpc(Dead, true);
        KillEnemyOnOwnerClient();
    }

    public override void SetEnemyStunned(
        bool setToStunned, 
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        DoAnimationServerRpc(Stunned);
        StartCoroutine(StunnedAnimation());
        DropHarp(transform.position);
        SwitchToBehaviourState(1);
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        if (currentBehaviourStateIndex == 0 || timeSinceHittingLocalPlayer < 1f) return;
        
        PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
        if (playerControllerB == null) return;
        
        LogDebug($"Harp Ghost '{gameObject.name}': Collision with player '{playerControllerB.name}'");
        timeSinceHittingLocalPlayer = 0f;
        DoAnimationServerRpc(Attack);
    }

    public void AttackShiftComplete() // Is called by the animation event
    {
        LogDebug("AttackShiftComplete called");
        StartCoroutine(DamagePlayerAfterDelay(0.05f));
    }

    public override void FinishedCurrentSearchRoutine()
    {
        base.FinishedCurrentSearchRoutine();
        if (searchForPlayers.inProgress)
            searchForPlayers.searchWidth = Mathf.Clamp(searchForPlayers.searchWidth + 10f, 1f, maxSearchRadius);
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
        float noiseThreshold = 20f * noiseLoudness;
        LogDebug($"Harp Ghost '{gameObject.name}': Heard Noise | Distance: {distanceToNoise} meters away | Noise threshold: {noiseThreshold}");

        if (Physics.Linecast(transform.position, noisePosition, 256))
        {
            noiseLoudness /= 2f;
            noiseThreshold /= 2f;
        }

        if (noiseLoudness < 0.25) return;
        if (currentBehaviourStateIndex != 1 && distanceToNoise < noiseThreshold)
        {
            annoyanceLevel += noiseLoudness;
            LogDebug($"Harp Ghost annoyance level: {annoyanceLevel}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void BeginChasingPlayerServerRpc(int targetPlayerObjectId)
    {
        if (inChase) return;
        BeginChasingPlayerClientRpc(targetPlayerObjectId);
    }
    
    [ClientRpc]
    public void BeginChasingPlayerClientRpc(int targetPlayerObjectId)
    {
        LogDebug("BeginChasingPlayerClientRpc called");
        SetMovingTowardsTargetPlayer(StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId]);
        targetPlayerLastSeenPos = targetPlayer.transform.position;
        inChase = true;
        agentMaxSpeed = 5f;
    }

    [ServerRpc(RequireOwnership = false)]
    public void EndChasingPlayerServerRpc()
    {
        if (!inChase) return;
        EndChasingPlayerClientRpc();
    }

    [ClientRpc]
    public void EndChasingPlayerClientRpc()
    {
        LogDebug("EndChasingPlayerClientRpc called");
        movingTowardsTargetPlayer = false;
        inChase = false;
        agentMaxSpeed = 3f;
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
        const int harpScrapValue = 300;
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
        
        harpObject.GetComponent<GrabbableObject>().fallTime = 0f;
        harpObject.AddComponent<ScanNodeProperties>().scrapValue = harpScrapValue;
        harpObject.GetComponent<GrabbableObject>().SetScrapValue(harpScrapValue);
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
        
        harpObject.GetComponent<NetworkObject>().Spawn();
        SpawnHarpClientRpc(harpObject);
    }

    [ClientRpc]
    public void SpawnHarpClientRpc(NetworkObjectReference harpObject)
    {
        harpObjectRef = harpObject;
    }

    [ServerRpc(RequireOwnership = false)]
    public void DoAnimationServerRpc(int animationId)
    {
        DoAnimationClientRpc(animationId);
    }

    [ClientRpc]
    public void DoAnimationClientRpc(int animationId)
    {
        LogDebug($"DoAnimationClientRpc called, Animation: {animationId}");
        creatureAnimator.SetTrigger(animationId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ChangeAnimationParameterBoolServerRpc(int animationId, bool value)
    {
        ChangeAnimationParameterBoolClientRpc(animationId, value);
    }

    [ClientRpc]
    public void ChangeAnimationParameterBoolClientRpc(int animationId, bool value)
    {
        LogDebug("ChangeAnimationParameterBoolClientRpc called");
        creatureAnimator.SetBool(animationId, value);
    }
    
}

/*
 TODO:

1). Fix ghost noise annoyance level logic
2). Make ghost able to open doors
3). Make ghost able to change elevation
4). Add ghost voice
6). Make player last seen logic better
7). Fix harp scan node value
 
 */

