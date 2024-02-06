using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using static LethalCompanyHarpGhost.HarpGhostPlugin;

namespace LethalCompanyHarpGhost;

public class HarpGhostAI : EnemyAI
{
    private ManualLogSource mls;

    [Header("AI and Pathfinding")]
    public AISearchRoutine roamMap;
    public AISearchRoutine searchForPlayers;
    
    [SerializeField] private float agentMaxAcceleration;
    [SerializeField] private float agentMaxSpeed;
    [SerializeField] private float annoyanceLevel;
    [SerializeField] private float annoyanceDecayRate;
    [SerializeField] private float annoyanceThreshold;
    [SerializeField] private float maxSearchRadius;
    
    [SerializeField] private int currentGhostBehaviourStateIndex = 0;
    [SerializeField] private int previousGhostBehaviourStateIndex = -1;
    
    private float timeSinceHittingLocalPlayer;
    private float hearNoiseCooldown;
    
    private bool hasBegunInvestigating;
    
    private Vector3 targetPlayerLastSeenPos;

    [Header("Audio")]
    public AudioClip[] damageSfx;
    public AudioClip[] laughSfx;
    public AudioClip[] stunSfx;
    public AudioClip[] upsetSfx;

    [Header("Transforms")]
    public Transform turnCompass;
    public Transform grabTarget;
    
    private NetworkObjectReference harpObjectRef;

    [SerializeField] private bool harpGhostDebug = true;

    private HarpBehaviour heldHarp;

    private enum NoiseIDToIgnore
    {
        Harp = 540,
        DoubleWing = 911,
        Lightning = 11,
        DocileLocustBees = 14152,
        BaboonHawkCaw = 1105
    }
    
    private enum NoiseIds
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
    

    private enum AudioClipTypes
    {
        Death = 0,
        Damage = 1,
        Laugh = 2,
        Stun = 3,
        Upset = 4
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

        AudioMixer audioMixer = SoundManager.Instance.diageticMixer;
        creatureVoice.outputAudioMixerGroup = audioMixer.FindMatchingGroups("SFX")[0];
        creatureSFX.outputAudioMixerGroup = audioMixer.FindMatchingGroups("SFX")[0];

        this.dieSFX = HarpGhostPlugin.dieSfx;
        this.damageSfx = HarpGhostPlugin.damageSfx;
        this.stunSfx = HarpGhostPlugin.stunSfx;
        this.laughSfx = HarpGhostPlugin.laughSfx;
        this.upsetSfx = HarpGhostPlugin.upsetSfx;
        
        timeSinceHittingLocalPlayer = 0;
        agentMaxAcceleration = 200f;
        agentMaxSpeed = 0.5f;
        openDoorSpeedMultiplier = 0.6f;
        annoyanceLevel = 0.0f;
        annoyanceDecayRate = 0.3f;
        annoyanceThreshold = 8f;
        maxSearchRadius = 100f;
        movingTowardsTargetPlayer = false;
        hasBegunInvestigating = false;
        targetPlayerLastSeenPos = default;
        
        LogDebug($"Behaviour states: {enemyBehaviourStates.Length}");
        
        UnityEngine.Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
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

        switch (currentGhostBehaviourStateIndex)
        {
            case 0: // harp ghost playing music and chilling
            {
                if (annoyanceLevel > 0)
                {
                    annoyanceLevel -= annoyanceDecayRate * Time.deltaTime;
                    annoyanceLevel = Mathf.Clamp(annoyanceLevel, 0, Mathf.Infinity);
                }

                if (annoyanceLevel >= annoyanceThreshold)
                {
                    SwitchBehaviourStateServerRpc(1);
                }

                break;
            }

            case 1: // harp ghost is angry and trying to find players to attack
            {
                break;
            }

            case 2: // ghost is investigating last seen player pos
            {
                break;
            }

            case 3: // ghost is chasing player
            {
                ChangeAnimationParameterBoolServerRpc(IsRunning, agent.speed > 3f);
                break;
            }

            case 4: // ghost is dead
            {
                break;
            }
        }

        CalculateAgentSpeed();
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (StartOfRound.Instance.allPlayersDead)
        {
            if (roamMap.inProgress) StopSearch(roamMap);
            if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
            return;
        }

        switch (currentGhostBehaviourStateIndex)
        {
            case 0: // playing music state
            {
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                if (!roamMap.inProgress) StartSearch(transform.position, roamMap);
                break;
            }

            case 1: // searching for player state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                
                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(80f, 80, 1);
                LogDebug($"Temp player: {tempTargetPlayer}");
                if (tempTargetPlayer != null)
                {
                    SwitchBehaviourStateServerRpc(3);
                }
                
                else
                {
                    if (!searchForPlayers.inProgress)
                    {
                        searchForPlayers.searchWidth = 30f;
                        StartSearch(transform.position, searchForPlayers);
                    }
                }

                break;
            }

            case 2: // investigating last seen player position state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);

                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(80f, 80, 1);
                LogDebug($"Temp target player: {tempTargetPlayer}");
                if (tempTargetPlayer != null)
                {
                    SwitchBehaviourStateServerRpc(3);
                }
                
                if (!hasBegunInvestigating)
                {
                    if (targetPlayerLastSeenPos == default) SwitchBehaviourStateServerRpc(1);
                    else SetDestinationToPosition(targetPlayerLastSeenPos);
                    hasBegunInvestigating = true;
                }

                if (Vector3.Distance(transform.position, targetPlayerLastSeenPos) <= 1)
                {
                    SwitchBehaviourStateServerRpc(1);
                }
                
                break;
            }

            case 3: // chasing player state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                
                PlayerControllerB[] playersInLineOfSight = GetAllPlayersInLineOfSight(80f, 80, eye, 1f,
                    layerMask: StartOfRound.Instance.collidersAndRoomMaskAndDefault);
                
                if (playersInLineOfSight is { Length: > 0 })
                {
                    LogDebug($"playersinlos: {playersInLineOfSight.Length}");
                    bool ourTargetFound = playersInLineOfSight.Any(playerControllerB => playerControllerB == targetPlayer && playerControllerB != null);
                    if (ourTargetFound) break;
                    
                    // If our target player is not among the crowd, target the next closest player
                    BeginChasingPlayerServerRpc((int)CheckLineOfSightForClosestPlayer(80f, 80, 1).playerClientId);
                }

                else SwitchBehaviourStateServerRpc(2);
                if (targetPlayerLastSeenPos != targetPlayer.transform.position) UpdateTargetPlayerLastSeenPosServerRpc(targetPlayer.transform.position);
                
                break;
            }

            case 4: // dead state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                break;
            }
        }
    }

    private void LogDebug(string logMessage)
    {
        if (harpGhostDebug) mls.LogInfo(logMessage);
    }

    private void BeginChasingPlayer(int targetPlayerObjectId)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        targetPlayer = player;
        ChangeOwnershipOfEnemy((ulong)targetPlayerObjectId);
        SetMovingTowardsTargetPlayer(player);
        agentMaxSpeed = 5f;
    }

    public void SwitchBehaviourStateLocally(int state)
    {
        switch (state)
        {
            case 0: // playing music state
            {
                LogDebug("Switched to behaviour state 0!");
                targetPlayer = null;
                targetPlayerLastSeenPos = default;
                agentMaxSpeed = 0.3f;
                movingTowardsTargetPlayer = false;
                hasBegunInvestigating = false;
                
                break; 
            }

            case 1: // searching for player state
            {
                LogDebug("Switched to behaviour state 1!");
                agentMaxSpeed = 3f;
                movingTowardsTargetPlayer = false;
                hasBegunInvestigating = false;
                
                DropHarpServerRpc(transform.position);
                
                break;
            }

            case 2:
            {
                LogDebug("Switched to behaviour state 2!");
                agentMaxSpeed = 3f;
                movingTowardsTargetPlayer = false;
                hasBegunInvestigating = false;
                
                DropHarpServerRpc(transform.position);
                
                break;
            }

            case 3:
            {
                LogDebug("Switched to behaviour state 3!");
                agentMaxSpeed = 5f;
                movingTowardsTargetPlayer = true;
                hasBegunInvestigating = false;
                targetPlayerLastSeenPos = default;
                
                DropHarpServerRpc(transform.position);
                
                break;
            }

            case 4:
            {
                LogDebug("Switched to behaviour state 4!");
                agentMaxSpeed = 0;
                movingTowardsTargetPlayer = false;
                targetPlayer = null;
                agent.speed = 0;
                agent.enabled = false;
                isEnemyDead = true;
                hasBegunInvestigating = false;
                
                DropHarpServerRpc(transform.position);
                ChangeAnimationParameterBoolServerRpc(Dead, true);
                
                break;
            }
               
        }
        
        if (currentGhostBehaviourStateIndex == state) return;
        previousGhostBehaviourStateIndex = currentGhostBehaviourStateIndex;
        currentGhostBehaviourStateIndex = state;
    }

    private void CalculateAgentSpeed()
    {
        if (stunNormalizedTimer > 0)
        {
            agent.speed = 0;
            agent.acceleration = agentMaxAcceleration;
            return;
        }

        if (currentGhostBehaviourStateIndex >= 0)
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
        SwitchBehaviourStateServerRpc(1);
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
        
        DropHarpServerRpc(transform.position);
        if (enemyHP > 0)
        {
            PlayCreatureSFXServerRpc((int)AudioClipTypes.Damage, damageSfx.Length);
            if (playerWhoHit == null) return;
            BeginChasingPlayerServerRpc((int)playerWhoHit.playerClientId);
            SwitchBehaviourStateServerRpc(3);
            return;
        }
        
        creatureVoice.Stop();
        creatureSFX.Stop();
        PlayCreatureSFXServerRpc((int)AudioClipTypes.Death, 1);
        ChangeAnimationParameterBoolServerRpc(Dead, true);
        KillEnemyOnOwnerClient();
    }

    public override void SetEnemyStunned(
        bool setToStunned, 
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        PlayCreatureSFXServerRpc((int)AudioClipTypes.Stun, stunSfx.Length);
        DropHarpServerRpc(transform.position);
        DoAnimationServerRpc(Stunned);
        StartCoroutine(StunnedAnimation());
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        if (currentGhostBehaviourStateIndex == 0 || timeSinceHittingLocalPlayer < 1f) return;
        
        PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
        if (playerControllerB == null) return;
        
        LogDebug($"Harp Ghost '{gameObject.name}': Collision with player '{playerControllerB.name}'");
        timeSinceHittingLocalPlayer = 0f;
        DoAnimationServerRpc(Attack);
    }

    public void AttackShiftComplete() // Is called by the animation event
    {
        LogDebug("AttackShiftComplete called");
        PlayCreatureSFXServerRpc((int)AudioClipTypes.Laugh, laughSfx.Length);
        StartCoroutine(DamagePlayerAfterDelay(0.05f));
    }

    public override void KillEnemy(bool destroy = false)
    {
        base.KillEnemy(destroy);
        agentMaxSpeed = 0;
        agent.speed = 0;
        agent.enabled = false;
        isEnemyDead = true;
        SwitchBehaviourStateServerRpc(4);
    }

    public override void FinishedCurrentSearchRoutine()
    {
        base.FinishedCurrentSearchRoutine();
        if (searchForPlayers.inProgress)
            searchForPlayers.searchWidth = Mathf.Clamp(searchForPlayers.searchWidth + 10f, 1f, maxSearchRadius);
    }

    private void PlayCreatureSFX(int index, int randomNum = 0, float volume = 1f)
    {
        creatureVoice.pitch = UnityEngine.Random.Range(0.8f, 1.1f);
        LogDebug($"Audio clip index: {index}, audio clip random number: {randomNum}");
        AudioClip audioClip = index switch
        {
            (int)AudioClipTypes.Death => dieSFX,
            (int)AudioClipTypes.Damage => damageSfx[randomNum],
            (int)AudioClipTypes.Laugh => laughSfx[randomNum],
            (int)AudioClipTypes.Stun => stunSfx[randomNum],
            (int)AudioClipTypes.Upset => upsetSfx[randomNum],
            _ => null
        };

        creatureVoice.clip = audioClip;
        creatureVoice.volume = 1f;
        creatureVoice.Play();
        WalkieTalkie.TransmitOneShotAudio(creatureVoice, audioClip, volume);
    }
    
    public override void DetectNoise(
        Vector3 noisePosition, 
        float noiseLoudness, 
        int timesNoisePlayedInOneSpot = 0,
        int noiseID = 0)
    {
        base.DetectNoise(noisePosition, noiseLoudness, timesNoisePlayedInOneSpot, noiseID);
        if ((double)stunNormalizedTimer > 0 || (double)hearNoiseCooldown >= 0.0 || currentGhostBehaviourStateIndex != 0 || Enum.IsDefined(typeof(NoiseIDToIgnore), noiseID)) return;
        hearNoiseCooldown = 0.03f;

        float distanceToNoise = Vector3.Distance(transform.position, noisePosition);
        float noiseThreshold = 8f * noiseLoudness;
        LogDebug($"Harp Ghost '{gameObject.name}': Heard Noise | Distance: {distanceToNoise} meters away | Noise loudness: {noiseLoudness}");

        if (Physics.Linecast(transform.position, noisePosition, 256))
        {
            noiseLoudness /= 2f;
            noiseThreshold /= 2f;
        }

        if (noiseLoudness < 0.25 || distanceToNoise >= noiseThreshold) return;
        if (noiseID is (int)NoiseIds.Boombox or (int)NoiseIds.PlayersTalking or (int)NoiseIds.RadarBoosterPing)
            noiseLoudness *= 2;
        annoyanceLevel += noiseLoudness;
        LogDebug($"Harp Ghost annoyance level: {annoyanceLevel}");
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void UpdateTargetPlayerLastSeenPosServerRpc(Vector3 targetPlayerPos)
    {
        UpdateTargetPlayerLastSeenPosClientRpc(targetPlayerPos);
    }

    [ClientRpc]
    public void UpdateTargetPlayerLastSeenPosClientRpc(Vector3 targetPlayerPos)
    {
        targetPlayerLastSeenPos = targetPlayerPos;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SwitchBehaviourStateServerRpc(int state)
    {
        SwitchBehaviourStateClientRpc(state);
    }

    [ClientRpc]
    public void SwitchBehaviourStateClientRpc(int state)
    {
        SwitchBehaviourStateLocally(state);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayCreatureSFXServerRpc(int index, int lengthOfClipArray, float volume = 1f)
    {
        int randomNum = UnityEngine.Random.Range(0, lengthOfClipArray);
        PlayCreatureSFXClientRpc(index, randomNum, volume);
    }

    [ClientRpc]
    public void PlayCreatureSFXClientRpc(int index, int randomNum = 0, float volume = 1f)
    {
        PlayCreatureSFX(index, randomNum, volume);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void BeginChasingPlayerServerRpc(int targetPlayerObjectId)
    {
        BeginChasingPlayerClientRpc(targetPlayerObjectId);
    }
    
    [ClientRpc]
    public void BeginChasingPlayerClientRpc(int targetPlayerObjectId)
    {
        LogDebug("BeginChasingPlayerClientRpc called");
        BeginChasingPlayer(targetPlayerObjectId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DropHarpServerRpc(Vector3 dropPosition)
    {
        if (heldHarp == null) return;
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
        if (creatureAnimator.GetBool(animationId) != value) ChangeAnimationParameterBoolClientRpc(animationId, value);
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

5). Add fear thingy when ghost enteres chase with player
7). Fix harp scan node value
 
 */

