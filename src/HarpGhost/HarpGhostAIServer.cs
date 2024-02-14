using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace LethalCompanyHarpGhost.HarpGhost;

[SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
public class HarpGhostAIServer : EnemyAI
{
    private readonly ManualLogSource _mls = BepInEx.Logging.Logger.CreateLogSource("Harp Ghost AI | Server");

    [Header("AI and Pathfinding")]
    [Space(5f)]
    public AISearchRoutine roamMap;
    public AISearchRoutine searchForPlayers;
    
    [SerializeField] private float agentMaxAcceleration = 50f;
    [SerializeField] private float agentMaxSpeed = 0.3f;
    [SerializeField] private float annoyanceLevel = 0f;
    [SerializeField] private float annoyanceDecayRate = 0.3f;
    [SerializeField] private float annoyanceThreshold = 8f;
    [SerializeField] private float maxSearchRadius = 100f;
    private float _agentCurrentSpeed = 0f;
    private float _timeSinceHittingLocalPlayer = 0f;
    private float _hearNoiseCooldown = 0f;
    
    private bool _hasBegunInvestigating = false;
    private bool _inStunAnimation = false;
    
    private Vector3 _targetPlayerLastSeenPos = default;
    private Vector3 _agentLastPosition = default;

    [Header("Transforms")]
    [Space(3f)]
    public BoxCollider attackArea;

    [SerializeField] private bool harpGhostDebug = true;

    [Header("Controllers and Managers")]
    [Space(5f)]
    #pragma warning disable 0649
    [SerializeField] private HarpGhostAudioManager audioManager;
    [SerializeField] private HarpGhostNetcodeController netcodeController;
    [SerializeField] private HarpGhostAnimationController animationController;
    #pragma warning restore 0649

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) _mls.LogError("NavMeshAgent component not found on " + name);
        agent.enabled = true;

        audioManager = GetComponent<HarpGhostAudioManager>();
        if (audioManager == null) _mls.LogError("Audio Manger is null");

        netcodeController = GetComponent<HarpGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("Netcode Controller is null");

        animationController = GetComponent<HarpGhostAnimationController>();
        if (animationController == null) _mls.LogError("Animation Controller is null");
        
        UnityEngine.Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsDead, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsStunned, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsRunning, false);
        
        netcodeController.SpawnHarpServerRpc();
        netcodeController.GrabHarpClientRpc();
        StartCoroutine(DelayedHarpMusicActivate());
        _mls.LogInfo("Harp Ghost Spawned");
    }
    
    private IEnumerator DelayedHarpMusicActivate() // Needed to mitigate race conditions
    {
        yield return new WaitForSeconds(0.5f);
        netcodeController.PlayHarpMusicClientRpc();
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;
    }

    public override void Update()
    {
        base.Update();
        if (!IsServer) return;
        CalculateAgentSpeed();

        if ((double) stunNormalizedTimer <= 0.0 && _inStunAnimation && !isEnemyDead)
        {
            LogDebug("Doing stun recover animation");
            netcodeController.DoAnimationClientRpc(HarpGhostAnimationController.Recover);
            _inStunAnimation = false;
            netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsStunned, false);
        }
        
        if (StartOfRound.Instance.allPlayersDead)
        {
            netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsRunning, false);
            return;
        }
        
        _timeSinceHittingLocalPlayer += Time.deltaTime;
        _hearNoiseCooldown -= Time.deltaTime;

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
                    SwitchBehaviourStateLocally(1);
                }

                break;
            }

            case 1: // harp ghost is angry and trying to find players to attack
            {
                bool isRunning = _agentCurrentSpeed >= 3f;
                if (animationController.GetBool(HarpGhostAnimationController.IsRunning) != isRunning && !_inStunAnimation)
                    netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsRunning, isRunning);
                break;
            }

            case 2: // ghost is investigating last seen player pos
            {
                bool isRunning = _agentCurrentSpeed >= 3f;
                if (animationController.GetBool(HarpGhostAnimationController.IsRunning) != isRunning && !_inStunAnimation)
                    netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsRunning, isRunning);
                break;
            }

            case 3: // ghost is chasing player
            {
                bool isRunning = _agentCurrentSpeed >= 3f;
                if (animationController.GetBool(HarpGhostAnimationController.IsRunning) != isRunning && !_inStunAnimation)
                    netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsRunning, isRunning);
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
        if (!IsServer) return;

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
                
                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(115f, 80, 2);
                LogDebug($"Temp player: {tempTargetPlayer}");
                if (tempTargetPlayer != null)
                {
                    SwitchBehaviourStateLocally(3);
                    break;
                }
                
                LogDebug($"Is searchforplayers in progress: {searchForPlayers.inProgress}");
                if (!searchForPlayers.inProgress)
                {
                    if (_targetPlayerLastSeenPos != default)
                    {
                        if (CheckForPath(_targetPlayerLastSeenPos))
                        {
                            searchForPlayers.searchWidth = 30f;
                            StartSearch(_targetPlayerLastSeenPos, searchForPlayers);
                            break;
                        }
                    }
                    
                    // If there is no target player last seen position, just search from where the ghost is currently at
                    searchForPlayers.searchWidth = 100f;
                    StartSearch(transform.position, searchForPlayers);
                    break;
                }
                
                break;
            }

            case 2: // investigating last seen player position state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);

                // Check for player in LOS
                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(135f, 80, 3);
                LogDebug($"Temp target player: {tempTargetPlayer}");
                if (tempTargetPlayer != null)
                {
                    SwitchBehaviourStateLocally(3);
                    break;
                }
                
                // begin investigating if not already
                if (!_hasBegunInvestigating) 
                {
                    if (_targetPlayerLastSeenPos == default) SwitchBehaviourStateLocally(1);
                    else
                    {
                        if (!SetDestinationToPosition(_targetPlayerLastSeenPos, true))
                        {
                            SwitchBehaviourStateLocally(1);
                            break;
                        }
                        _hasBegunInvestigating = true;
                    }
                }

                // If player isnt in LOS and ghost has reached the player's last known position, then switch to state 1
                if (Vector3.Distance(transform.position, _targetPlayerLastSeenPos) <= 1)
                {
                    SwitchBehaviourStateLocally(1);
                    break;
                }
                
                break;
            }

            case 3: // chasing player state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                
                // Check for players in LOS
                PlayerControllerB[] playersInLineOfSight = GetAllPlayersInLineOfSight(110f, 80, eye, 2f,
                    layerMask: StartOfRound.Instance.collidersAndRoomMaskAndDefault);

                // Check if our target is in LOS
                bool ourTargetFound = false;
                if (playersInLineOfSight is { Length: > 0 })
                {
                    LogDebug($"playersinlos: {playersInLineOfSight.Length}");
                    ourTargetFound = targetPlayer != null && playersInLineOfSight.Any(playerControllerB => playerControllerB == targetPlayer && playerControllerB != null);
                }
                
                // If no players were found, switch to state 2
                else
                {
                    LogDebug("No players found, switching to state 2");
                    SwitchBehaviourStateLocally(2);
                    break;
                }

                LogDebug($"Our target found: {ourTargetFound}");
                
                // If our target wasn't found, switch target
                if (!ourTargetFound)
                {
                    // Extra check done to make sure a player is still in LOS
                    PlayerControllerB playerControllerB = CheckLineOfSightForClosestPlayer(110f, 80, 3);
                    if (playerControllerB == null)
                    {
                        LogDebug("No players found for second time, switching to state 2");
                        SwitchBehaviourStateLocally(2);
                        break;
                    }
                    
                    LogDebug("New target found, beginning the chase");
                    BeginChasingPlayer((int)playerControllerB.playerClientId);
                }
                
                _targetPlayerLastSeenPos = targetPlayer.transform.position;
                PlayerChasingFearIncrease();
                
                // Check if a player is in attack area and attack
                if (Vector3.Distance(transform.position, targetPlayer.transform.position) < 8) AttackPlayerIfClose();
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
        if (harpGhostDebug && IsServer) _mls.LogInfo(logMessage);
    }

    private void SwitchBehaviourStateLocally(int state)
    {
        if (!IsServer) return;
        switch (state)
        {
            case 0: // playing music state
            {
                LogDebug("Switched to behaviour state 0!");
                
                targetPlayer = null;
                agentMaxSpeed = 0.3f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = false;
                _targetPlayerLastSeenPos = default;
                _hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 6;
                
                break; 
            }

            case 1: // searching for player state
            {
                LogDebug("Switched to behaviour state 1!");
                
                agentMaxSpeed = 3f; 
                agentMaxAcceleration = 100f;
                movingTowardsTargetPlayer = false;
                _hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 2;
                _targetPlayerLastSeenPos = default;
                
                netcodeController.DropHarpClientRpc(transform.position);
                
                break;
            }

            case 2:
            {
                LogDebug("Switched to behaviour state 2!");
                
                agentMaxSpeed = 6f;
                agentMaxAcceleration = 100f;
                movingTowardsTargetPlayer = false;
                _hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 1;
                
                netcodeController.DropHarpClientRpc(transform.position);
                
                break;
            }

            case 3:
            {
                LogDebug("Switched to behaviour state 3!");
                
                agentMaxSpeed = 8f;
                agentMaxAcceleration = 75f;
                movingTowardsTargetPlayer = true;
                _hasBegunInvestigating = false;
                _targetPlayerLastSeenPos = default;
                openDoorSpeedMultiplier = 0.6f;
                
                netcodeController.DropHarpClientRpc(transform.position);
                
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
                _targetPlayerLastSeenPos = default;
                
                netcodeController.DropHarpClientRpc(transform.position);
                netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsDead, true);
                
                break;
            }
               
        }
        
        if (currentBehaviourStateIndex == state) return;
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
    }

    private bool CheckForPath(Vector3 position)
    {
        position = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 1.75f);
        path1 = new NavMeshPath();
        // ReSharper disable once UseIndexFromEndExpression
        return agent.CalculatePath(position, path1) && !((double) Vector3.Distance(path1.corners[path1.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 2.7f)) > 1.5499999523162842);
    }

    private void CalculateAgentSpeed()
    {
        if (!IsServer) return;
        if (stunNormalizedTimer > 0)
        {
            agent.speed = 0;
            agent.acceleration = agentMaxAcceleration;
            return;
        }

        if (currentBehaviourStateIndex != 4)
        {
            MoveWithAcceleration();
        }
    }

    private void MoveWithAcceleration() {
        if (!IsServer) return;
        
        float speedAdjustment = Time.deltaTime / 2f;
        agent.speed = Mathf.Lerp(agent.speed, agentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        agent.acceleration = Mathf.Lerp(agent.acceleration, agentMaxAcceleration, accelerationAdjustment);
    }

    private void PlayerChasingFearIncrease()
    {
        if (!IsServer) return;
        if (targetPlayer.isPlayerDead ||
            !targetPlayer.isInsideFactory ||
            currentBehaviourStateIndex != 3) return;
        
        LogDebug($"Increasing fear level for {targetPlayer.name}");
        if (targetPlayer.HasLineOfSightToPosition(eye.position, 115f, 50, 3f))
        {
            LogDebug("Player to add fear to is looking at the ghost");
            targetPlayer.JumpToFearLevel(1);
            targetPlayer.IncreaseFearLevelOverTime(0.8f);
        }
        
        else if (Vector3.Distance(transform.position, targetPlayer.transform.position) < 3)
        {
            LogDebug("Player to add fear to is not looking at the ghost, but is near");
            targetPlayer.JumpToFearLevel(0.6f);
            targetPlayer.IncreaseFearLevelOverTime(0.4f);
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX);
        if (!IsServer) return;
        if (isEnemyDead) return;
        enemyHP -= force;
        
        if (enemyHP > 0)
        {
            netcodeController.PlayCreatureVoiceClientRpc((int)HarpGhostAudioManager.AudioClipTypes.Damage, audioManager.damageSfx.Length);
            if (playerWhoHit != null)
            {
                LogDebug($"Player {playerWhoHit.name} hit ghost");
                SwitchTargetPlayer((int)playerWhoHit.playerClientId);
                SwitchBehaviourStateLocally(3);
            }

            else
            {
                LogDebug("Unknown player hit ghost");
                if (currentBehaviourStateIndex == 0) SwitchBehaviourStateLocally(1);
            }
            
            return;
        }
        
        // Ghost is dead
        LogDebug("Ghost is dead!");
        netcodeController.EnterDeathStateClientRpc();
        KillEnemyClientRpc(false);
        SwitchBehaviourStateLocally(4);
    }

    public override void SetEnemyStunned(
        bool setToStunned, 
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer) return;
        LogDebug("SetEnemyStunned called");
        
        netcodeController.PlayCreatureVoiceClientRpc((int)HarpGhostAudioManager.AudioClipTypes.Stun, audioManager.stunSfx.Length);
        netcodeController.DropHarpClientRpc(transform.position);
        netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsStunned, true);
        netcodeController.DoAnimationClientRpc(HarpGhostAnimationController.Stunned);
        _inStunAnimation = true;

        if (setStunnedByPlayer != null)
        {
            LogDebug($"Player {setStunnedByPlayer.name} stunned ghost");
            SwitchTargetPlayer((int)setStunnedByPlayer.playerClientId);
            SwitchBehaviourStateLocally(3);
        }

        else
        {
            LogDebug("Unknown player stunned ghost");
            if (currentBehaviourStateIndex == 0) SwitchBehaviourStateLocally(1);
        }
    }

    public void DamageTargetPlayer(int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        targetPlayer.DamagePlayer(damage, causeOfDeath: causeOfDeath);
    }

    private void AttackPlayerIfClose() // Checks if the player is in the ghost's attack area and if so, attacks
    {
        if (!IsServer) return;
        if (currentBehaviourStateIndex != 3 || 
            _timeSinceHittingLocalPlayer < 2f || 
            _inStunAnimation) return;
        
        Collider[] hitColliders = Physics.OverlapBox(attackArea.transform.position, attackArea.size * 0.5f, Quaternion.identity, 1 << 3);
        
        if (hitColliders.Length <= 0) return;
        foreach (Collider player in hitColliders)
        {
            PlayerControllerB playerControllerB = PlayerMeetsStandardCollisionConditions(player);
            if (playerControllerB == null) continue;
            
            LogDebug($"Attacking player {playerControllerB.name}");
            SwitchTargetPlayer((int)playerControllerB.playerClientId);
            _timeSinceHittingLocalPlayer = 0f;
            netcodeController.DoAnimationClientRpc(HarpGhostAnimationController.Attack);
            break;
        }
    }

    private void SwitchTargetPlayer(int targetPlayerObjectId)
    {
        if (!IsServer) return;
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        targetPlayer = player;
        LogDebug($"Target player is now: {player.name}");
    }
    
    private void BeginChasingPlayer(int targetPlayerObjectId)
    {
        if (!IsServer) return;
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        targetPlayer = player;
        SetMovingTowardsTargetPlayer(player);
        LogDebug($"Now chasing {player.name}");
    }

    // Using custom player collision checker because the default one checks if the player is the owner of the enemy, and thats a no no for me
    private PlayerControllerB PlayerMeetsStandardCollisionConditions(Collider collider)
    {
        if (!IsServer) return null;
        if (isEnemyDead || currentBehaviourStateIndex == 4) return null;
        PlayerControllerB playerControllerB = collider.gameObject.GetComponent<PlayerControllerB>();

        if (playerControllerB == null) return null;
        if (playerControllerB.isInsideFactory && 
            !playerControllerB.isInHangarShipRoom &&
            !playerControllerB.isPlayerDead &&
            playerControllerB.sinkingValue < 0.7300000190734863) return playerControllerB;
        return null;
    }

    // Using custom damage player function for same reason as above
    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        if (!IsServer) return;
        if (currentBehaviourStateIndex is 0 or 4 || _timeSinceHittingLocalPlayer < 2f || _inStunAnimation) return;
        
        LogDebug("Collided with player!");
        PlayerControllerB playerControllerB = PlayerMeetsStandardCollisionConditions(other);
        if (playerControllerB == null) return;

        _timeSinceHittingLocalPlayer = 0f;
        if (currentBehaviourStateIndex != 3) SwitchBehaviourStateLocally(3);
        
        SwitchTargetPlayer((int)playerControllerB.playerClientId);
        netcodeController.DoAnimationClientRpc(HarpGhostAnimationController.Attack);
        LogDebug($"Attacking player {playerControllerB.name}");
    }

    public override void FinishedCurrentSearchRoutine()
    {
        base.FinishedCurrentSearchRoutine();
        if (!IsServer) return;
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
        if (!IsServer) return;
        
        if ((double)stunNormalizedTimer > 0 || _hearNoiseCooldown > 0.0 || currentBehaviourStateIndex != 0 || Enum.IsDefined(typeof(HarpGhostAudioManager.NoiseIDToIgnore), noiseID)) return;
        
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
        if (noiseID is (int)HarpGhostAudioManager.NoiseIds.Boombox or (int)HarpGhostAudioManager.NoiseIds.PlayersTalking or (int)HarpGhostAudioManager.NoiseIds.RadarBoosterPing)
            noiseLoudness *= 2;
        annoyanceLevel += noiseLoudness;

        if (annoyanceLevel > annoyanceThreshold) SwitchBehaviourStateLocally(1);
        LogDebug($"Harp Ghost annoyance level: {annoyanceLevel}");
    }

    // Using getters for encapsulation
    public float AgentMaxAcceleration { get; set; }
    public float AgentMaxSpeed { get; set; }
    public int CurrentBehaviourStateIndex => currentBehaviourStateIndex;
    public Vector3 TransformPosition => transform.position;
    public RoundManager RoundManagerInstance => RoundManager.Instance;
    public HarpBehaviour HeldHarp { get; set; }
}

/*

Fix bug that doesnt properly change the target player

*/