using System;
using System.Collections;
using System.Linq;
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

    [Header("AI and Pathfinding")]
    [Space(5f)]
    public AISearchRoutine roamMap;
    public AISearchRoutine searchForPlayers;
    
    [SerializeField] private float agentMaxAcceleration;
    [SerializeField] private float agentMaxSpeed;
    [SerializeField] private float annoyanceLevel;
    [SerializeField] private float annoyanceDecayRate;
    [SerializeField] private float annoyanceThreshold;
    [SerializeField] private float maxSearchRadius;
    private float agentCurrentSpeed;
    
    private float timeSinceHittingLocalPlayer;
    private float hearNoiseCooldown;
    
    private bool hasBegunInvestigating;
    
    private Vector3 targetPlayerLastSeenPos;
    private Vector3 agentLastPosition;

    [Header("Audio")]
    [Space(5f)]
    public AudioClip[] damageSfx;
    public AudioClip[] laughSfx;
    public AudioClip[] stunSfx;
    public AudioClip[] upsetSfx;

    [Header("Transforms")]
    [Space(3f)]
    public Transform turnCompass;
    public Transform grabTarget;
    public BoxCollider attackArea;
    
    private NetworkObjectReference harpObjectRef;

    [SerializeField] private bool harpGhostDebug = true;

    private HarpBehaviour heldHarp;
    private int harpScrapValue;

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
        
        if (!IsServer) return;
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) mls.LogError("NavMeshAgent component not found on " + name);

        creatureAnimator = GetComponent<Animator>();
        if (creatureAnimator == null) mls.LogError("Animator component not found on " + name);
        
        timeSinceHittingLocalPlayer = 0;
        agentMaxAcceleration = 200f;
        agentMaxSpeed = 0.3f;
        openDoorSpeedMultiplier = 4f;
        annoyanceLevel = 0.0f;
        annoyanceDecayRate = 0.3f;
        annoyanceThreshold = 8f;
        maxSearchRadius = 100f;
        movingTowardsTargetPlayer = false;
        hasBegunInvestigating = false;
        targetPlayerLastSeenPos = default;
        
        UnityEngine.Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        ChangeAnimationParameterBoolServerRpc(Dead, false);
        ChangeAnimationParameterBoolServerRpc(IsRunning, false);
        SpawnHarpServerRpc();
        GrabHarpIfNotHoldingServerRpc();
        StartCoroutine(DelayedHarpMusicActivate()); // Needed otherwise the music won't play, maybe because multiple things are fighting for the heldHarp object
    }

    public void FixedUpdate()
    {
        Vector3 position = transform.position;
        agentCurrentSpeed = Mathf.Lerp(agentCurrentSpeed,
            (position - agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        agentLastPosition = position;
    }

    public override void Update()
    {
        base.Update();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
        {
            ChangeAnimationParameterBoolServerRpc(IsRunning, false);
            return;
        }

        timeSinceHittingLocalPlayer += Time.deltaTime;
        hearNoiseCooldown -= Time.deltaTime;

        switch (currentBehaviourStateIndex)
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
                ChangeAnimationParameterBoolServerRpc(IsRunning, agentCurrentSpeed > 1.5f);
                break;
            }

            case 2: // ghost is investigating last seen player pos
            {
                ChangeAnimationParameterBoolServerRpc(IsRunning, agentCurrentSpeed > 1.5f);
                break;
            }

            case 3: // ghost is chasing player
            {
                ChangeAnimationParameterBoolServerRpc(IsRunning, agentCurrentSpeed > 1.5f);
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

        switch (currentBehaviourStateIndex)
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
                
                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(100f, 80, 3);
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

                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(100f, 80, 3);
                LogDebug($"Temp target player: {tempTargetPlayer}");
                if (tempTargetPlayer != null)
                {
                    SwitchBehaviourStateServerRpc(3);
                    break;
                }
                
                if (!hasBegunInvestigating)
                {
                    if (targetPlayerLastSeenPos == default) SwitchBehaviourStateServerRpc(1);
                    else
                    {
                        if (!SetDestinationToPosition(targetPlayerLastSeenPos, true))
                        {
                            SwitchBehaviourStateServerRpc(1);
                            break;
                        }
                    }
                    hasBegunInvestigating = true;
                }

                if (Vector3.Distance(transform.position, targetPlayerLastSeenPos) <= 1)
                {
                    SwitchBehaviourStateServerRpc(1);
                    break;
                }
                
                break;
            }

            case 3: // chasing player state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                
                PlayerControllerB[] playersInLineOfSight = GetAllPlayersInLineOfSight(100f, 80, eye, 2f,
                    layerMask: StartOfRound.Instance.collidersAndRoomMaskAndDefault);

                bool ourTargetFound = false;
                if (playersInLineOfSight is { Length: > 0 })
                {
                    LogDebug($"playersinlos: {playersInLineOfSight.Length}");
                    ourTargetFound = playersInLineOfSight.Any(playerControllerB => playerControllerB == targetPlayer && playerControllerB != null);
                }
                else SwitchBehaviourStateServerRpc(2);

                if (!ourTargetFound)
                {
                    PlayerControllerB playerControllerB = CheckLineOfSightForClosestPlayer(100f, 80, 3);
                    if (playerControllerB == null) SwitchBehaviourStateServerRpc(2);
                    else BeginChasingPlayerServerRpc((int)playerControllerB.playerClientId);
                }
                
                if (targetPlayerLastSeenPos != targetPlayer.transform.position) UpdateTargetPlayerLastSeenPosServerRpc(targetPlayer.transform.position);
                PlayerChasingFearIncrease();
                
                // Check if a player is in attack area and attack
                AttackPlayerIfClose();
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
        ChangeOwnershipOfEnemy(targetPlayer.actualClientId);
        SetMovingTowardsTargetPlayer(player);
        agentMaxSpeed = 9f;
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
                agentMaxAcceleration = 200f;
                movingTowardsTargetPlayer = false;
                hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 4;
                
                break; 
            }

            case 1: // searching for player state
            {
                LogDebug("Switched to behaviour state 1!");
                
                agentMaxSpeed = 3f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = false;
                hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 2;
                
                DropHarpServerRpc(transform.position);
                
                break;
            }

            case 2:
            {
                LogDebug("Switched to behaviour state 2!");
                
                agentMaxSpeed = 6f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = false;
                hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 1;
                
                DropHarpServerRpc(transform.position);
                
                break;
            }

            case 3:
            {
                LogDebug("Switched to behaviour state 3!");
                
                agentMaxSpeed = 9f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = true;
                hasBegunInvestigating = false;
                targetPlayerLastSeenPos = default;
                openDoorSpeedMultiplier = 0.5f;
                
                DropHarpServerRpc(transform.position);
                
                break;
            }

            case 4:
            {
                LogDebug("Switched to behaviour state 4!");
                
                agentMaxSpeed = 0;
                agentMaxAcceleration = 0;
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
        
        if (currentBehaviourStateIndex == state) return;
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
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

    private void PlayerChasingFearIncrease()
    {
        if (GameNetworkManager.Instance.localPlayerController.isPlayerDead ||
            !GameNetworkManager.Instance.localPlayerController.isInsideFactory) return;
        if (currentBehaviourStateIndex == 3 && targetPlayer == GameNetworkManager.Instance.localPlayerController && GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(eye.position, 100f, 50, 3f))
        {
            GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(0.8f);
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX);
        if (isEnemyDead) return;
        enemyHP -= force;
        
        DropHarpServerRpc(transform.position);
        if (enemyHP > 0)
        {
            PlayCreatureSFXServerRpc((int)AudioClipTypes.Damage, damageSfx.Length);
            if (playerWhoHit != null)
            {
                BeginChasingPlayerServerRpc((int)playerWhoHit.playerClientId);
                SwitchBehaviourStateServerRpc(3);
            }

            else
            {
                SwitchBehaviourStateServerRpc(1);
            }
            
            return;
        }
        
        // Ghost is dead
        creatureVoice.Stop();
        creatureSFX.Stop();  
        KillEnemyOnOwnerClient();
    }
    
    public override void KillEnemy(bool destroy = false)
    {
        base.KillEnemy(destroy);
        agentMaxSpeed = 0;
        agent.speed = 0;
        isEnemyDead = true;
        SwitchBehaviourStateServerRpc(4);
    }

    public override void SetEnemyStunned(
        bool setToStunned, 
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        LogDebug("SetEnemyStunned called");
        PlayCreatureSFXServerRpc((int)AudioClipTypes.Stun, stunSfx.Length);
        DropHarpServerRpc(transform.position);
        DoAnimationServerRpc(Stunned);
        StartCoroutine(StunnedAnimation());

        if (setStunnedByPlayer == null)
        {
            BeginChasingPlayerServerRpc((int)setStunnedByPlayer.playerClientId);
            SwitchBehaviourStateServerRpc(3);
        }

        else
        {
            SwitchBehaviourStateServerRpc(1);
        }
    }

    private void AttackPlayerIfClose()
    {
        LogDebug("AttackPlayerIfClose() called");
        if (currentBehaviourStateIndex != 3 || timeSinceHittingLocalPlayer < 2f) return;
        Collider[] hitColliders = Physics.OverlapBox(attackArea.transform.position, attackArea.size * 0.5f, Quaternion.identity, 1 << 3);

        if (hitColliders.Length <= 0) return;
        foreach (Collider player in hitColliders)
        {
            PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(player);
            if (playerControllerB == null) continue;
            
            LogDebug("Attacking player in AttackPlayerIfClose()");
            if (playerControllerB != targetPlayer) BeginChasingPlayerServerRpc((int)playerControllerB.playerClientId);
            timeSinceHittingLocalPlayer = 0f;
            DoAnimationServerRpc(Attack);
            break;
        }
    }
    
    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        if (currentBehaviourStateIndex != 3 || timeSinceHittingLocalPlayer < 1.2f) return;
        
        // PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
        // if (playerControllerB == null) return;
        //
        // LogDebug($"Harp Ghost '{gameObject.name}': Collision with player '{playerControllerB.name}'");
        // timeSinceHittingLocalPlayer = 0f;
        // agentMaxSpeed = 0f;
        // DoAnimationServerRpc(Attack);
    }

    public void AttackShiftComplete() // Is called by an animation event
    {
        LogDebug("AttackShiftComplete called");
        ChangeAgentMaxSpeedServerRpc(0f, 0f); // Ghost is frozen while doing the second attack anim
        PlayCreatureSFXServerRpc((int)AudioClipTypes.Laugh, laughSfx.Length);
        GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(1f);
        StartCoroutine(DamagePlayerAfterDelay(0.05f));
    }

    public void AttackAnimationComplete() // Is called by an animation event
    {
        float newMaxSpeed, newMaxSpeed2;
        switch (currentBehaviourStateIndex)
        {
            case 0:
                newMaxSpeed = 0.3f;
                newMaxSpeed2 = 0.3f;
                break;
            case 1:
                newMaxSpeed = 3f;
                newMaxSpeed2 = 1f;
                break;
            case 2:
                newMaxSpeed = 6f;
                newMaxSpeed2 = 1f;
                break;
            case 3:
                newMaxSpeed = 9f;
                newMaxSpeed2 = 1f;
                break;
            default:
                newMaxSpeed = 3f;
                newMaxSpeed2 = 1f;
                break;
        }
        
        ChangeAgentMaxSpeedServerRpc(newMaxSpeed, newMaxSpeed2);
    }

    public void IncreaseAccelerationGallop() // Is called by an animation event
    {
        ChangeAgentMaxAccelerationServerRpc(agentMaxAcceleration*4);
    }

    public void DecreaseAccelerationGallop() // Is called by an animation event
    {
        ChangeAgentMaxAccelerationServerRpc(agentMaxAcceleration/4);
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

        if (audioClip == null)
        {
            mls.LogError($"Harp ghost voice audio clip index '{index}' and randomNum: '{randomNum}' is null");
            return;
        }
        
        LogDebug($"Playing audio clip: {audioClip.name}");
        creatureVoice.volume = 1f;
        creatureVoice.PlayOneShot(audioClip);
        WalkieTalkie.TransmitOneShotAudio(creatureVoice, audioClip, volume);
    }
    
    public override void DetectNoise(
        Vector3 noisePosition, 
        float noiseLoudness, 
        int timesNoisePlayedInOneSpot = 0,
        int noiseID = 0)
    {
        base.DetectNoise(noisePosition, noiseLoudness, timesNoisePlayedInOneSpot, noiseID);
        if ((double)stunNormalizedTimer > 0 || (double)hearNoiseCooldown >= 0.0 || currentBehaviourStateIndex != 0 || Enum.IsDefined(typeof(NoiseIDToIgnore), noiseID)) return;
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
        GameObject harpObject = Instantiate(
            harpItem.spawnPrefab,
            transform.position,
            Quaternion.identity,
            RoundManager.Instance.spawnedScrapContainer
            );

        AudioSource harpAudioSource = harpObject.GetComponent<AudioSource>();
        if (harpGhostDebug)
        {
            if (harpAudioSource == null)
            {
                harpAudioSource = harpObject.AddComponent<AudioSource>();
                harpAudioSource.playOnAwake = false;
                harpAudioSource.loop = true;
                harpAudioSource.spatialBlend = 1;
                mls.LogError("Harp audio source is null");
            }
        }
        
        HarpBehaviour harpBehaviour = harpObject.GetComponent<HarpBehaviour>();
        harpBehaviour.harpAudioSource = harpAudioSource;
        harpBehaviour.harpAudioClips = harpAudioClips;
        
        harpScrapValue = UnityEngine.Random.Range(150, 301);
        harpObject.GetComponent<GrabbableObject>().fallTime = 0f;
        harpObject.GetComponent<GrabbableObject>().SetScrapValue(harpScrapValue);
        RoundManager.Instance.totalScrapValueInLevel += harpScrapValue;
        
        harpObject.GetComponent<NetworkObject>().Spawn();
        SpawnHarpClientRpc(harpObject, harpScrapValue);
    }

    [ClientRpc]
    public void SpawnHarpClientRpc(NetworkObjectReference harpObject, int recievedHarpScrapValue)
    {
        harpScrapValue = recievedHarpScrapValue;
        harpObjectRef = harpObject;
    }

    [ServerRpc(RequireOwnership = false)]
    public void GrabHarpIfNotHoldingServerRpc()
    {
        if (heldHarp != null) return;
        GrapHarpIfNotHoldingClientRpc();
    }

    [ClientRpc]
    public void GrapHarpIfNotHoldingClientRpc()
    {
        GrabHarpIfNotHolding();
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
        creatureAnimator.SetBool(animationId, value);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ChangeAgentMaxAccelerationServerRpc(float newAcceleration)
    {
        if ((int)agentMaxAcceleration == (int)newAcceleration)
            return; // round to int because the values never have decimals anyway
        ChangeAgentMaxAccelerationClientRpc(newAcceleration);
    }

    [ClientRpc]
    public void ChangeAgentMaxAccelerationClientRpc(float newAcceleration)
    {
        agentMaxAcceleration = newAcceleration;
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ChangeAgentMaxSpeedServerRpc(float newMaxSpeed, float newMaxSpeed2)
    {
        ChangeAgentMaxSpeedClientRpc(newMaxSpeed, newMaxSpeed2);
    }

    [ClientRpc]
    public void ChangeAgentMaxSpeedClientRpc(float newMaxSpeed, float newMaxSpeed2)
    {
        agentMaxSpeed = newMaxSpeed;
        agent.speed = newMaxSpeed2;
    }
}

/*
 TODO:

3). Fix issues with multiple harps on the same map

 */

