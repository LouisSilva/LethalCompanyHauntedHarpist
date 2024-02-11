using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static LethalCompanyHarpGhost.HarpGhostPlugin;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAI : EnemyAI
{
    private ManualLogSource _mls;

    [Header("AI and Pathfinding")]
    [Space(5f)]
    public AISearchRoutine roamMap;
    public AISearchRoutine searchForPlayers;
    
    [SerializeField] private float agentMaxAcceleration = 200f;
    [SerializeField] private float agentMaxSpeed = 0.3f;
    [SerializeField] private float annoyanceLevel = 0f;
    [SerializeField] private float annoyanceDecayRate = 0.3f;
    [SerializeField] private float annoyanceThreshold = 8f;
    [SerializeField] private float maxSearchRadius = 100f;
    private float _agentCurrentSpeed = 0f;
    private float _timeSinceHittingLocalPlayer = 0f;
    private float _hearNoiseCooldown = 0f;
    
    private int _harpScrapValue = 300;
    
    private bool _hasBegunInvestigating = false;
    
    private Vector3 _targetPlayerLastSeenPos = default;
    private Vector3 _agentLastPosition = default;

    [Header("Audio")]
    [Space(5f)]
    public static readonly AudioClip[] DamageSfx;
    public static readonly AudioClip[] LaughSfx;
    public static readonly AudioClip[] StunSfx;
    public static readonly AudioClip[] UpsetSfx;
    public static readonly AudioClip DieSfx;

    [Header("Transforms")]
    [Space(3f)]
    public Transform turnCompass;
    public Transform grabTarget;
    public BoxCollider attackArea;
    
    private NetworkObjectReference _harpObjectRef;

    [SerializeField] private bool harpGhostDebug = true;

    private HarpBehaviour _heldHarp;

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
    
    private static readonly int IsRunning = Animator.StringToHash("isRunning");
    private static readonly int IsAttacking = Animator.StringToHash("isAttacking");
    private static readonly int IsStunned = Animator.StringToHash("isStunned");
    private static readonly int IsDead = Animator.StringToHash("isDead");
    private static readonly int Death = Animator.StringToHash("death");
    private static readonly int Stunned = Animator.StringToHash("stunned");
    private static readonly int Recover = Animator.StringToHash("recover");
    private static readonly int Attack = Animator.StringToHash("attack");

    public override void Start()
    {
        base.Start();
        _mls = BepInEx.Logging.Logger.CreateLogSource("Harp Ghost");
        _mls.LogInfo("Harp Ghost Spawned");
        
        if (!IsServer) return;
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) _mls.LogError("NavMeshAgent component not found on " + name);

        creatureAnimator = GetComponent<Animator>();
        if (creatureAnimator == null) _mls.LogError("Animator component not found on " + name);
        
        UnityEngine.Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        ChangeAnimationParameterBoolServerRpc(IsDead, false);
        ChangeAnimationParameterBoolServerRpc(IsRunning, false);
        ChangeAnimationParameterBoolServerRpc(IsStunned, false);
        ChangeAnimationParameterBoolServerRpc(IsAttacking, false);
        SpawnHarpServerRpc();
        GrabHarpIfNotHoldingServerRpc();
        StartCoroutine(DelayedHarpMusicActivate()); // Needed otherwise the music won't play, maybe because multiple things are fighting for the heldHarp object
    }

    public void FixedUpdate()
    {
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed,
            (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;
    }

    public override void Update()
    {
        base.Update();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
        {
            ChangeAnimationParameterBoolServerRpc(IsRunning, false);
            return;
        }

        _timeSinceHittingLocalPlayer += Time.deltaTime;
        _hearNoiseCooldown -= Time.deltaTime;

        CalculateAgentSpeed();

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
                    if (stunNormalizedTimer > 0) break;
                    SwitchBehaviourStateServerRpc(1);
                }

                break;
            }

            case 1: // harp ghost is angry and trying to find players to attack
            {
                if (stunNormalizedTimer > 0) break;
                ChangeAnimationParameterBoolServerRpc(IsRunning, _agentCurrentSpeed > 1.5f);
                break;
            }

            case 2: // ghost is investigating last seen player pos
            {
                if (stunNormalizedTimer > 0) break;
                ChangeAnimationParameterBoolServerRpc(IsRunning, _agentCurrentSpeed > 1.5f);
                break;
            }

            case 3: // ghost is chasing player
            {
                if (stunNormalizedTimer > 0) break;
                ChangeAnimationParameterBoolServerRpc(IsRunning, _agentCurrentSpeed > 1.5f);
                break;
            }

            case 4: // ghost is dead
            {
                break;
            }
        }
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

        if (stunNormalizedTimer > 0) return;

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
                    break;
                }
                
                if (!searchForPlayers.inProgress)
                {
                    searchForPlayers.searchWidth = 30f;
                    StartSearch(transform.position, searchForPlayers);
                    break;
                }
                
                break;
            }

            case 2: // investigating last seen player position state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                if (!IsOwner) break;

                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(100f, 80, 3);
                LogDebug($"Temp target player: {tempTargetPlayer}");
                if (tempTargetPlayer != null)
                {
                    SwitchBehaviourStateServerRpc(3);
                    break;
                }
                
                if (!_hasBegunInvestigating)
                {
                    if (_targetPlayerLastSeenPos == default) SwitchBehaviourStateServerRpc(1);
                    else
                    {
                        if (!SetDestinationToPosition(_targetPlayerLastSeenPos, true))
                        {
                            SwitchBehaviourStateServerRpc(1);
                            break;
                        }
                    }
                    _hasBegunInvestigating = true;
                }

                if (Vector3.Distance(transform.position, _targetPlayerLastSeenPos) <= 1)
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
                if (!IsOwner) return;
                
                PlayerControllerB[] playersInLineOfSight = GetAllPlayersInLineOfSight(100f, 80, eye, 2f,
                    layerMask: StartOfRound.Instance.collidersAndRoomMaskAndDefault);

                bool ourTargetFound = false;
                if (playersInLineOfSight is { Length: > 0 })
                {
                    LogDebug($"playersinlos: {playersInLineOfSight.Length}");
                    ourTargetFound = targetPlayer != null && playersInLineOfSight.Any(playerControllerB => playerControllerB == targetPlayer && playerControllerB != null);
                }
                
                else
                {
                    LogDebug("No players found, switching to state 2");
                    SwitchBehaviourStateServerRpc(2);
                    break;
                }

                LogDebug($"Our target found: {ourTargetFound}");
                if (!ourTargetFound)
                {
                    PlayerControllerB playerControllerB = CheckLineOfSightForClosestPlayer(100f, 80, 3);
                    if (playerControllerB == null)
                    {
                        LogDebug("No players found for second time, switching to state 2");
                        SwitchBehaviourStateServerRpc(2);
                        break;
                    }
                    
                    LogDebug("New target found, beginning the chase");
                    BeginChasingPlayerServerRpc((int)playerControllerB.playerClientId);
                }
                
                if (_targetPlayerLastSeenPos != targetPlayer.transform.position) UpdateTargetPlayerLastSeenPosServerRpc(targetPlayer.transform.position);
                PlayerChasingFearIncrease();
                
                // Check if a player is in attack area and attack
                if (Vector3.Distance(transform.position, targetPlayer.transform.position) < 7) AttackPlayerIfClose();
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
        if (harpGhostDebug) _mls.LogInfo(logMessage);
    }

    private void BeginChasingPlayer(int targetPlayerObjectId)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        SetMovingTowardsTargetPlayer(player);
        ChangeOwnershipOfEnemy(player.actualClientId);
        LogDebug($"Now chasing {targetPlayer.name}");
    }

    public void SwitchBehaviourStateLocally(int state)
    {
        switch (state)
        {
            case 0: // playing music state
            {
                LogDebug("Switched to behaviour state 0!");
                
                targetPlayer = null;
                _targetPlayerLastSeenPos = default;
                agentMaxSpeed = 0.3f;
                agentMaxAcceleration = 200f;
                movingTowardsTargetPlayer = false;
                _hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 4;
                
                break; 
            }

            case 1: // searching for player state
            {
                LogDebug("Switched to behaviour state 1!");
                
                agentMaxSpeed = 3f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = false;
                _hasBegunInvestigating = false;
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
                _hasBegunInvestigating = false;
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
                _hasBegunInvestigating = false;
                _targetPlayerLastSeenPos = default;
                openDoorSpeedMultiplier = 0.6f;
                
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
                _hasBegunInvestigating = false;
                
                DropHarpServerRpc(transform.position);
                ChangeAnimationParameterBoolServerRpc(IsDead, true);
                
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
        if (_heldHarp != null) return;
        if (!_harpObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldHarp = networkObject.gameObject.GetComponent<HarpBehaviour>();
        GrabHarp(_heldHarp.gameObject);
    }

    private void GrabHarp(GameObject harpObject)
    {
        _heldHarp = harpObject.GetComponent<HarpBehaviour>();
        if (_heldHarp == null)
        {
            _mls.LogError("Harp in GrabHarp function did not contain harpItem component");
            return;
        }
        
        _heldHarp.parentObject = grabTarget;
        _heldHarp.isHeldByEnemy = true;
        _heldHarp.grabbableToEnemies = false;
        _heldHarp.grabbable = false;
        _heldHarp.GrabItemFromEnemy(this);
    }

    private void DropHarp(Vector3 dropPosition)
    {
        _heldHarp.parentObject = null;
        _heldHarp.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _heldHarp.EnablePhysics(true);
        _heldHarp.fallTime = 0.0f;
        Transform parent;
        _heldHarp.startFallingPosition = (parent = _heldHarp.transform.parent).InverseTransformPoint(_heldHarp.transform.position);
        _heldHarp.targetFloorPosition = parent.InverseTransformPoint(dropPosition);
        _heldHarp.floorYRot = -1;
        _heldHarp.grabbable = true;
        _heldHarp.grabbableToEnemies = true;
        _heldHarp.isHeld = false;
        _heldHarp.isHeldByEnemy = false;
        _heldHarp.ItemActivate(false);
        _heldHarp.DiscardItemFromEnemy();
        _heldHarp = null;
    }

    private IEnumerator DelayedHarpMusicActivate()
    {
        yield return new WaitForSeconds(0.5f);
        _heldHarp.ItemActivate(true);
    }

    private void PlayerChasingFearIncrease()
    {
        if (GameNetworkManager.Instance.localPlayerController.isPlayerDead ||
            !GameNetworkManager.Instance.localPlayerController.isInsideFactory ||
            currentBehaviourStateIndex != 3 ||
            targetPlayer != GameNetworkManager.Instance.localPlayerController ||
            !IsOwner) return;
        
        LogDebug($"Increasing fear level for {GameNetworkManager.Instance.localPlayerController.name}, IsOwner?: {IsOwner}");
        if (GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(eye.position, 100f, 50, 3f))
        {
            LogDebug("Player to add fear to is looking at the ghost");
            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(1);
            GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(0.8f);
        }
        
        else if (Vector3.Distance(transform.position, targetPlayer.transform.position) < 7)
        {
            LogDebug("Player to add fear to is not looking at the ghost, but is near");
            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.6f);
            GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(0.4f);
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX);
        if (isEnemyDead || creatureAnimator.GetBool(IsDead)) return;
        enemyHP -= force;
        
        DropHarpServerRpc(transform.position);
        if (enemyHP > 0)
        {
            PlayCreatureSFXServerRpc((int)AudioClipTypes.Damage, DamageSfx.Length);
            if (playerWhoHit != null)
            {
                LogDebug($"Player {playerWhoHit.name} hit ghost");
                BeginChasingPlayerServerRpc((int)playerWhoHit.playerClientId);
                SwitchBehaviourStateServerRpc(3);
            }

            else
            {
                LogDebug("Unknown player hit ghost");
                SwitchBehaviourStateServerRpc(1);
            }
            
            return;
        }
        
        // Ghost is dead
        LogDebug("Ghost is dead!");
        if (!IsOwner) return;
        EnterDeathStateServerRpc();
        KillEnemyOnOwnerClient();
    }

    private void EnterDeathState()
    {
        LogDebug("EnterDeathState() called");
        creatureVoice.Stop();
        creatureSFX.Stop();
        PlayCreatureSFX((int)AudioClipTypes.Death);
        
        creatureAnimator.SetTrigger(Death);
        creatureAnimator.SetBool(IsDead, true);
        creatureAnimator.SetBool(IsRunning, false);
        creatureAnimator.SetBool(IsAttacking, false);
        creatureAnimator.SetBool(IsStunned, false);
        
        SwitchBehaviourStateLocally(4);
    }

    public override void SetEnemyStunned(
        bool setToStunned, 
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        LogDebug("SetEnemyStunned called");
        
        PlayCreatureSFXServerRpc((int)AudioClipTypes.Stun, StunSfx.Length);
        DropHarpServerRpc(transform.position);
        ChangeAnimationParameterBoolServerRpc(IsStunned, true);
        DoAnimationServerRpc(Stunned);

        if (setStunnedByPlayer != null)
        {
            LogDebug($"Player {setStunnedByPlayer.name} stunned ghost");
            BeginChasingPlayerServerRpc((int)setStunnedByPlayer.playerClientId);
            SwitchBehaviourStateServerRpc(3);
        }

        else
        {
            LogDebug("Unknown player stunned ghost");
            SwitchBehaviourStateServerRpc(1);
        }
    }

    public void StunnedAnimationFreeze() // called by an animation event
    {
        LogDebug("StunnedAnimationFreeze() called");
        StartCoroutine(WaitUntilStunComplete());
    }

    private IEnumerator WaitUntilStunComplete()
    {
        LogDebug("WaitUntilStunComplete() called");
        while (stunNormalizedTimer > 0) yield return new WaitForSeconds(0.02f);
        if (creatureAnimator.GetBool(IsDead)) yield break; // Cancels the stun recover animation if the ghost is dead
        DoAnimationServerRpc(Recover);
    }

    private void AttackPlayerIfClose() // Checks if the player is in the ghost's attack area and if so, attacks
    {
        if (currentBehaviourStateIndex != 3 || _timeSinceHittingLocalPlayer < 2f || creatureAnimator.GetBool(IsAttacking) || creatureAnimator.GetBool(IsStunned)) return;
        LogDebug("AttackPlayerIfClose() called");
        Collider[] hitColliders = Physics.OverlapBox(attackArea.transform.position, attackArea.size * 0.5f, Quaternion.identity, 1 << 3);

        if (hitColliders.Length <= 0) return;
        foreach (Collider player in hitColliders)
        {
            PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(player);
            if (playerControllerB == null) continue;
            
            LogDebug($"Attacking player {playerControllerB.name}");
            if (playerControllerB != targetPlayer) BeginChasingPlayerServerRpc((int)playerControllerB.playerClientId);
            _timeSinceHittingLocalPlayer = 0f;
            ChangeAnimationParameterBoolServerRpc(IsAttacking, true);
            DoAnimationServerRpc(Attack);
            break;
        }
    }
    
    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        /*
        if (currentBehaviourStateIndex != 3 || timeSinceHittingLocalPlayer < 1.2f) return;
        
        PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
        if (playerControllerB == null) return;
        
        LogDebug($"Harp Ghost '{gameObject.name}': Collision with player '{playerControllerB.name}'");
        timeSinceHittingLocalPlayer = 0f;
        agentMaxSpeed = 0f;
        DoAnimationServerRpc(Attack);
        */
    }
    
    private IEnumerator DamagePlayerAfterDelay(float delay) // Damages the player in time with the correct point in the animation
    {
        yield return new WaitForSeconds(delay);
        if (targetPlayer == null || !creatureAnimator.GetBool(IsAttacking)) yield break;
        
        LogDebug("Damaging player!");
        targetPlayer.DamagePlayer(35, causeOfDeath: CauseOfDeath.Strangulation);
    }

    public void AttackShiftComplete() // Is called by an animation event
    {
        LogDebug("AttackShiftComplete called");
        ChangeAgentMaxSpeedServerRpc(0f, 0f); // Ghost is frozen while doing the second attack anim
        PlayCreatureSFXServerRpc((int)AudioClipTypes.Laugh, LaughSfx.Length);
        GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(1f);
        StartCoroutine(DamagePlayerAfterDelay(0.05f));
    }

    public void FixAgentSpeedAfterAttack() // Is called by an animation event
    {
        LogDebug("FixAgentSpeedAfterAttack() called");
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
        
        ChangeAnimationParameterBoolServerRpc(IsAttacking, false);
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
            (int)AudioClipTypes.Death => DieSfx,
            (int)AudioClipTypes.Damage => DamageSfx[randomNum],
            (int)AudioClipTypes.Laugh => LaughSfx[randomNum],
            (int)AudioClipTypes.Stun => StunSfx[randomNum],
            (int)AudioClipTypes.Upset => UpsetSfx[randomNum],
            _ => null
        };

        if (audioClip == null)
        {
            _mls.LogError($"Harp ghost voice audio clip index '{index}' and randomNum: '{randomNum}' is null");
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
        if ((double)stunNormalizedTimer > 0 || _hearNoiseCooldown >= 0.0 || currentBehaviourStateIndex != 0 || Enum.IsDefined(typeof(NoiseIDToIgnore), noiseID)) return;
        _hearNoiseCooldown = 0.02f;

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

        if (annoyanceLevel > annoyanceThreshold)
        {
            LogDebug("In detectnoise(), ghost is sufficciently annoyed and now going to state 1");
            SwitchBehaviourStateServerRpc(1);
        }
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
        _targetPlayerLastSeenPos = targetPlayerPos;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SwitchBehaviourStateServerRpc(int state)
    {
        if (currentBehaviourStateIndex == state) return;
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
        if (_heldHarp == null) return;
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
                _mls.LogError("Harp audio source is null");
            }
        }
        
        HarpBehaviour harpBehaviour = harpObject.GetComponent<HarpBehaviour>();
        harpBehaviour.harpAudioSource = harpAudioSource;
        harpBehaviour.harpAudioClips = harpAudioClips;
        
        _harpScrapValue = UnityEngine.Random.Range(150, 301);
        harpObject.GetComponent<GrabbableObject>().fallTime = 0f;
        harpObject.GetComponent<GrabbableObject>().SetScrapValue(_harpScrapValue);
        RoundManager.Instance.totalScrapValueInLevel += _harpScrapValue;
        
        harpObject.GetComponent<NetworkObject>().Spawn();
        SpawnHarpClientRpc(harpObject, _harpScrapValue);
    }

    [ClientRpc]
    public void SpawnHarpClientRpc(NetworkObjectReference harpObject, int recievedHarpScrapValue)
    {
        _harpScrapValue = recievedHarpScrapValue;
        _harpObjectRef = harpObject;
    }

    [ServerRpc(RequireOwnership = false)]
    public void GrabHarpIfNotHoldingServerRpc()
    {
        if (_heldHarp != null) return;
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
        LogDebug($"ChangeAnimationParameterBoolClientRpc, parameter: {animationId}, value: {value}");
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

    [ServerRpc(RequireOwnership = false)]
    public void EnterDeathStateServerRpc()
    {
        if (isEnemyDead) return;
        EnterDeathStateClientRpc();
    }

    [ClientRpc]
    public void EnterDeathStateClientRpc()
    {
        EnterDeathState();
    }
}

/*
 TODO:

1). Increase navagent size to include harp when in behaviour state 0
2). Client who attacks the ghost makes the ghost not do anything until the client leaves its fov
3). Fix issues with multiple harps on the same map

 */

