using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.AI;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostServer : EnemyAI
{
    private ManualLogSource _mls;
    private string _ghostId;
    
    private enum States
    {
        PlayingMusic = 0,
        SearchingForPlayers = 1,
        InvestigatingTargetPosition = 2,
        ChasingTargetPlayer = 3,
        Dead = 4
    }
    
    private enum NoiseIDsToAnnoy
    {
        PlayersTalking = 75,
        ShipHorn = 14155,
        Boombox = 5,
        RadarBoosterPing = 1015,
        Jetpack = 41,
    }

    private enum NoiseIDsToIgnore
    {
        DoubleWing = 911,
        Lightning = 11,
        DocileLocustBees = 14152,
        BaboonHawkCaw = 1105
    }

    [Header("AI and Pathfinding")]
    [Space(5f)]
    public AISearchRoutine roamMap;
    public AISearchRoutine searchForPlayers;
    
    [SerializeField] private float agentMaxAcceleration = 50f;
    [SerializeField] private float agentMaxSpeed = 0.3f;
    [SerializeField] private float annoyanceLevel;
    [SerializeField] private float annoyanceDecayRate = 0.3f;
    [SerializeField] private float annoyanceThreshold = 8f;
    [SerializeField] private float maxSearchRadius = 100f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float viewWidth = 135f;
    [SerializeField] private int viewRange = 80;
    [SerializeField] private int proximityAwareness = 3;
    [SerializeField] private float hearingPrecision = 90f;
    [SerializeField] private bool canHearPlayers = true;
    [SerializeField] private bool friendlyFire = true;
    
#pragma warning disable 0649
    [Header("Colliders")] [Space(3f)]
    [SerializeField] private BoxCollider attackArea;
    
    [Header("Controllers")] [Space(5f)]
    [SerializeField] private HarpGhostNetcodeController netcodeController;
#pragma warning restore 0649
    
    private Vector3 _targetPosition;
    private Vector3 _agentLastPosition;

    private readonly NullableObject<PlayerControllerB> actualTargetPlayer = new();
    
    private float _agentCurrentSpeed;
    private float _timeSinceHittingLocalPlayer;
    private float _hearNoiseCooldown;
    
    private bool _hasBegunInvestigating;
    private bool _inStunAnimation;
    private bool _hasTransitionedMaterial;
    private bool _networkEventsSubscribed;
    
    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        _ghostId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Harp Ghost Server {_ghostId}");
        
        if (netcodeController == null) netcodeController = GetComponent<HarpGhostNetcodeController>();
        if (netcodeController == null)
        {
            _mls.LogError("Netcode Controller is null, aborting spawn.");
            Destroy(gameObject);
        }
        
        netcodeController.SyncGhostIdentifierClientRpc(_ghostId);
        
        Random.InitState(StartOfRound.Instance.randomMapSeed + _ghostId.GetHashCode() - thisEnemyIndex);
        GhostUtils.AssignCorrectAINodesType(this);
        InitializeConfigValues();
        
        netcodeController.SpawnHarpServerRpc(_ghostId);
        netcodeController.GrabHarpClientRpc(_ghostId);
        StartCoroutine(DelayedHarpMusicActivate());
        
        LogDebug("Harp Ghost Spawned");
    }

    public override void Update()
    {
        base.Update();
        if (!IsServer) return;
        
        _timeSinceHittingLocalPlayer += Time.deltaTime;
        _hearNoiseCooldown -= Time.deltaTime;
        
        CalculateAgentSpeed();

        if (stunNormalizedTimer <= 0.0 && _inStunAnimation && !isEnemyDead)
        {
            _inStunAnimation = false;
            netcodeController.AnimationParamStunned.Value = false;
        }
        
        switch (currentBehaviourStateIndex)
        {
            case (int)States.PlayingMusic:
            {
                if (annoyanceLevel > 0)
                {
                    annoyanceLevel -= annoyanceDecayRate * Time.deltaTime;
                    annoyanceLevel = Mathf.Clamp(annoyanceLevel, 0, Mathf.Infinity);
                }

                if (annoyanceLevel >= annoyanceThreshold)
                {
                    TurnGhostEyesRed();
                    netcodeController.PlayAudioClipTypeServerRpc(
                        _ghostId, 
                        HarpGhostClient.AudioClipTypes.Upset);
                    
                    if (_targetPosition != default) SwitchBehaviourStateLocally((int)States.InvestigatingTargetPosition);
                    else SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
                }

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
            case (int)States.PlayingMusic:
            {
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                if (!roamMap.inProgress) StartSearch(transform.position, roamMap);
                break;
            }
            
            case (int)States.SearchingForPlayers:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                
                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(viewWidth, viewRange, Mathf.Clamp(proximityAwareness, -1, int.MaxValue));
                if (tempTargetPlayer != null)
                {
                    SwitchBehaviourStateLocally((int)States.ChasingTargetPlayer);
                    break;
                }
                
                if (!searchForPlayers.inProgress)
                {
                    if (_targetPosition != default)
                    {
                        if (CheckForPath(_targetPosition))
                        {
                            searchForPlayers.searchWidth = 30f;
                            StartSearch(_targetPosition, searchForPlayers);
                            break;
                        }
                    }
                    
                    // If there is no target player last seen position, just search from where the ghost is currently at
                    searchForPlayers.searchWidth = 100f;
                    StartSearch(transform.position, searchForPlayers);
                }
                
                break;
            }
            
            case (int)States.InvestigatingTargetPosition:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);

                // Check for player in LOS
                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(viewWidth, viewRange, proximityAwareness);
                if (tempTargetPlayer != null)
                {
                    SwitchBehaviourStateLocally((int)States.ChasingTargetPlayer);
                    break;
                }
                
                // begin investigating if not already
                if (!_hasBegunInvestigating) 
                {
                    if (_targetPosition == default) SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
                    else
                    {
                        if (!SetDestinationToPosition(_targetPosition, true))
                        {
                            SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
                            break;
                        }
                        _hasBegunInvestigating = true;
                    }
                }

                // If player isn't in LOS and ghost has reached the player's last known position, then switch to state 1
                if (Vector3.Distance(transform.position, _targetPosition) <= 1)
                {
                    SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
                }
                
                break;
            }

            case (int)States.ChasingTargetPlayer:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                
                // Check for players in LOS
                PlayerControllerB[] playersInLineOfSight = GetAllPlayersInLineOfSight(viewWidth, viewRange, eye, proximityAwareness,
                    layerMask: StartOfRound.Instance.collidersAndRoomMaskAndDefault);

                // Check if our target is in LOS
                bool ourTargetFound;
                if (playersInLineOfSight is { Length: > 0 })
                {
                    ourTargetFound = actualTargetPlayer.IsNotNull && playersInLineOfSight.Any(playerControllerB => 
                        playerControllerB == actualTargetPlayer.Value && playerControllerB != null);
                }
                
                // If no players were found, switch to state 2
                else
                {
                    SwitchBehaviourStateLocally((int)States.InvestigatingTargetPosition);
                    break;
                }
                
                // If our target wasn't found, switch target
                if (!ourTargetFound)
                {
                    // Extra check done to make sure a player is still in LOS
                    PlayerControllerB playerControllerB = CheckLineOfSightForClosestPlayer(viewWidth, viewRange, proximityAwareness);
                    if (playerControllerB == null)
                    {
                        SwitchBehaviourStateLocally((int)States.InvestigatingTargetPosition);
                        break;
                    }
                    
                    BeginChasingPlayer(playerControllerB.playerClientId);
                }
                
                _targetPosition = actualTargetPlayer.Value.transform.position;
                netcodeController.IncreaseTargetPlayerFearLevelClientRpc(_ghostId);
                
                // Check if a player is in attack area and attack
                if (Vector3.Distance(transform.position, actualTargetPlayer.Value.transform.position) < 8) AttackPlayerIfClose();
                break;
            }
            

            case (int)States.Dead: // dead state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                break;
            }
        }
    }

    private void SwitchBehaviourStateLocally(int state)
    {
        if (!IsServer) return;
        switch (state)
        {
            case (int)States.PlayingMusic: // playing music state
            {
                LogDebug($"Switched to behaviour state {(int)States.PlayingMusic}!");
                
                agentMaxSpeed = 0.3f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = false;
                _targetPosition = default;
                _hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 6;
                
                GhostUtils.ChangeNetworkVar(netcodeController.TargetPlayerClientId, (ulong)69420);
                break; 
            }

            case (int)States.SearchingForPlayers: // searching for player state
            {
                LogDebug($"Switched to behaviour state {(int)States.SearchingForPlayers}!");
                
                agentMaxSpeed = 3f; 
                agentMaxAcceleration = 100f;
                movingTowardsTargetPlayer = false;
                _hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 2;
                _targetPosition = default;
                
                netcodeController.DropHarpClientRpc(_ghostId, transform.position);
                
                break;
            }

            case (int)States.InvestigatingTargetPosition:
            {
                LogDebug($"Switched to behaviour state {(int)States.InvestigatingTargetPosition}!");
                
                agentMaxSpeed = HarpGhostConfig.Instance.HarpGhostMaxSpeedInChaseMode.Value;
                agentMaxAcceleration = HarpGhostConfig.Instance.HarpGhostMaxAccelerationInChaseMode.Value;
                movingTowardsTargetPlayer = false;
                _hasBegunInvestigating = false;
                openDoorSpeedMultiplier = HarpGhostConfig.Instance.HarpGhostDoorSpeedMultiplierInChaseMode.Value;
                
                netcodeController.DropHarpClientRpc(_ghostId, transform.position);
                
                break;
            }

            case (int)States.ChasingTargetPlayer:
            {
                LogDebug($"Switched to behaviour state {(int)States.ChasingTargetPlayer}!");
                
                agentMaxSpeed = HarpGhostConfig.Instance.HarpGhostMaxSpeedInChaseMode.Value;
                agentMaxAcceleration = HarpGhostConfig.Instance.HarpGhostMaxAccelerationInChaseMode.Value;
                movingTowardsTargetPlayer = true;
                _hasBegunInvestigating = false;
                _targetPosition = default;
                openDoorSpeedMultiplier = HarpGhostConfig.Instance.HarpGhostDoorSpeedMultiplierInChaseMode.Value;
                
                netcodeController.DropHarpClientRpc(_ghostId, transform.position);
                
                break;
            }

            case (int)States.Dead:
            {
                LogDebug($"Switched to behaviour state {(int)States.Dead}!");
                
                agent.speed = 0;
                agent.acceleration = 900;
                agentMaxSpeed = 0;
                agentMaxAcceleration = 900;
                movingTowardsTargetPlayer = false;
                moveTowardsDestination = false;
                isEnemyDead = true;
                _hasBegunInvestigating = false;
                _targetPosition = default;
                agent.enabled = false;
                
                GhostUtils.ChangeNetworkVar(netcodeController.TargetPlayerClientId, (ulong)69420);
                netcodeController.DropHarpClientRpc(_ghostId, transform.position);
                netcodeController.AnimationParamDead.Value = true;
                
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
        return agent.CalculatePath(position, path1) && !(Vector3.Distance(path1.corners[path1.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 2.7f)) > 1.5499999523162842);
    }

    private void CalculateAgentSpeed()
    {
        if (!IsServer) return;
        
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;
        
        if (stunNormalizedTimer > 0)
        {
            agent.speed = 0;
            agent.acceleration = agentMaxAcceleration;
            return;
        }

        if (currentBehaviourStateIndex != (int)States.Dead)
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

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitId = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitId);
        if (!IsServer) return;
        if (isEnemyDead) return;
        if (!friendlyFire || playerWhoHit == null) return;
        
        enemyHP -= force;
        if (enemyHP > 0)
        {
            TurnGhostEyesRed();
            netcodeController.PlayAudioClipTypeServerRpc(_ghostId, HarpGhostClient.AudioClipTypes.Damage, true);
            if (playerWhoHit != null)
            {
                GhostUtils.ChangeNetworkVar(netcodeController.TargetPlayerClientId, playerWhoHit.actualClientId);
                SwitchBehaviourStateLocally((int)States.ChasingTargetPlayer);
            }
            else
            {
                if (currentBehaviourStateIndex == (int)States.PlayingMusic) SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
            }
            
            return;
        }
        
        // Ghost is dead
        KillEnemyClientRpc(false);
        SwitchBehaviourStateLocally((int)States.Dead);
    }

    public override void SetEnemyStunned(
        bool setToStunned, 
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer) return;
        
        TurnGhostEyesRed();
        netcodeController.PlayAudioClipTypeServerRpc(_ghostId, HarpGhostClient.AudioClipTypes.Stun);
        netcodeController.DropHarpClientRpc(_ghostId, transform.position);
        netcodeController.AnimationParamStunned.Value = true;
        _inStunAnimation = true;

        if (setStunnedByPlayer != null)
        {
            GhostUtils.ChangeNetworkVar(netcodeController.TargetPlayerClientId, setStunnedByPlayer.actualClientId);
            SwitchBehaviourStateLocally((int)States.ChasingTargetPlayer);
        }

        else
        {
            if (currentBehaviourStateIndex == (int)States.PlayingMusic) SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
        }
    }

    private void TurnGhostEyesRed()
    {
        if (!IsServer) return;
        if (_hasTransitionedMaterial) return;
        
        _hasTransitionedMaterial = true;
        netcodeController.TurnGhostEyesRedClientRpc(_ghostId);
    }

    private void AttackPlayerIfClose() // Checks if the player is in the ghost's attack area and if so, attacks
    {
        if (!IsServer) return;
        if (currentBehaviourStateIndex != (int)States.ChasingTargetPlayer || 
            _timeSinceHittingLocalPlayer < attackCooldown || 
            _inStunAnimation) return;
        
        Collider[] hitColliders = Physics.OverlapBox(attackArea.transform.position, attackArea.size * 0.5f, Quaternion.identity, 1 << 3);
        
        if (hitColliders.Length <= 0) return;
        foreach (Collider player in hitColliders)
        {
            PlayerControllerB attackingPlayer = PlayerMeetsStandardCollisionConditions(player);
            if (attackingPlayer == null) continue;
            
            GhostUtils.ChangeNetworkVar(netcodeController.TargetPlayerClientId, attackingPlayer.actualClientId);
            _timeSinceHittingLocalPlayer = 0f;
            netcodeController.SetAnimationTriggerClientRpc(_ghostId, HarpGhostClient.Attack);
            break;
        }
    }
    
    private IEnumerator DelayedHarpMusicActivate() // Needed to mitigate race conditions
    {
        yield return new WaitForSeconds(0.5f);
        netcodeController.PlayHarpMusicClientRpc(_ghostId);
    }

    private void HandleChangeAgentMaxSpeed(string receivedGhostId, float newMaxSpeed, float newMaxSpeed2)
    {
        if (_ghostId != receivedGhostId) return;
        agent.speed = newMaxSpeed;
        agentMaxSpeed = newMaxSpeed2;
    }

    private void HandleFixAgentSpeedAfterAttack(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        float newMaxSpeed, newMaxSpeed2;
        switch (currentBehaviourStateIndex)
        {
            case (int)States.PlayingMusic:
                newMaxSpeed = 0.3f;
                newMaxSpeed2 = 0.3f;
                break;
            case (int)States.SearchingForPlayers:
                newMaxSpeed = 3f;
                newMaxSpeed2 = 1f;
                break;
            case (int)States.InvestigatingTargetPosition:
                newMaxSpeed = 6f;
                newMaxSpeed2 = 1f;
                break;
            case (int)States.ChasingTargetPlayer:
                newMaxSpeed = 8f;
                newMaxSpeed2 = 1f;
                break;
            case (int)States.Dead:
                newMaxSpeed = 0f;
                newMaxSpeed2 = 0f;
                break;
            default:
                newMaxSpeed = 3f;
                newMaxSpeed2 = 1f;
                break;
        }

        agent.speed = newMaxSpeed;
        agentMaxSpeed = newMaxSpeed2;
    }
    
    private void BeginChasingPlayer(ulong targetPlayerClientId)
    {
        if (!IsServer) return;
        GhostUtils.ChangeNetworkVar(netcodeController.TargetPlayerClientId, targetPlayerClientId);
        movingTowardsTargetPlayer = true;
    }

    // Using custom player collision checker because the default one checks if the player is the owner of the enemy, and thats a no no for me
    private PlayerControllerB PlayerMeetsStandardCollisionConditions(Component collider)
    {
        if (!IsServer) return null;
        if (isEnemyDead || currentBehaviourStateIndex == (int)States.Dead) return null;
        PlayerControllerB playerControllerB = collider.gameObject.GetComponent<PlayerControllerB>();
        if (playerControllerB == null) return null;
        return GhostUtils.IsPlayerTargetable(playerControllerB) ? playerControllerB : null;
    }
    
    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        if (!IsServer) return;
        if (currentBehaviourStateIndex is (int)States.PlayingMusic or (int)States.Dead 
            || _timeSinceHittingLocalPlayer < 2f || _inStunAnimation) return;
        
        PlayerControllerB playerCollidedWith = PlayerMeetsStandardCollisionConditions(other);
        if (playerCollidedWith == null) return;

        _timeSinceHittingLocalPlayer = 0f;
        if (currentBehaviourStateIndex != (int)States.ChasingTargetPlayer) SwitchBehaviourStateLocally((int)States.ChasingTargetPlayer);

        GhostUtils.ChangeNetworkVar(netcodeController.TargetPlayerClientId, playerCollidedWith.actualClientId);
        netcodeController.SetAnimationTriggerClientRpc(_ghostId, HarpGhostClient.Attack);
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
        
        if ((double)stunNormalizedTimer > 0 || _hearNoiseCooldown > 0.0 || Enum.IsDefined(typeof(NoiseIDsToIgnore), noiseID)) return;
        switch (currentBehaviourStateIndex)
        {
            case (int)States.PlayingMusic:
            {
                _hearNoiseCooldown = 0.01f;
                float distanceToNoise = Vector3.Distance(transform.position, noisePosition);
                float noiseThreshold = 15f * noiseLoudness;
                LogDebug($"Harp Ghost '{gameObject.name}': Heard Noise | Distance: {distanceToNoise} meters away | Noise loudness: {noiseLoudness}");

                if (Physics.Linecast(transform.position, noisePosition, 256))
                {
                    noiseLoudness /= 1.5f;
                    noiseThreshold /= 1.5f;
                }

                if (noiseLoudness < 0.25 || distanceToNoise >= noiseThreshold) return;
                if (Enum.IsDefined(typeof(NoiseIDsToAnnoy), noiseID))
                    noiseLoudness *= 2;
                annoyanceLevel += noiseLoudness;
                _targetPosition = noisePosition;
        
                LogDebug($"Harp Ghost annoyance level: {annoyanceLevel}");
                break;
            }
            
            case (int)States.SearchingForPlayers:
            {
                if (timesNoisePlayedInOneSpot > 5 || !canHearPlayers) return;
                _hearNoiseCooldown = 0.1f;
                float distanceToNoise = Vector3.Distance(transform.position, noisePosition);
                float noiseThreshold = 8f * noiseLoudness;
                LogDebug($"Harp Ghost '{gameObject.name}': Heard Noise | Distance: {distanceToNoise} meters away | Noise loudness: {noiseLoudness}");

                if (Physics.Linecast(transform.position, noisePosition, 256))
                {
                    noiseLoudness /= 2f;
                    noiseThreshold /= 2f;
                }

                if (noiseLoudness < 0.25 || distanceToNoise >= noiseThreshold) return;
                
                float adjustedRadius = Mathf.Clamp(distanceToNoise * (1f - hearingPrecision / 100f), 0.01f, 50f);
                LogDebug($"Adjusted radius {adjustedRadius}");
                _targetPosition = RoundManager.Instance.GetRandomNavMeshPositionInRadius(noisePosition, adjustedRadius);
                SwitchBehaviourStateLocally((int)States.InvestigatingTargetPosition);
                break;
            }
        }
    }

    private void ExtendAttackAreaCollider()
    {
        float extensionLength = Mathf.Abs(HarpGhostConfig.Instance.HarpGhostAttackAreaLength.Value - 0.91f);
        
        Vector3 newSize = attackArea.size;
        newSize.z += extensionLength;

        Vector3 newCenter = attackArea.center;
        newCenter.z += extensionLength / 2;

        attackArea.size = newSize;
        attackArea.center = newCenter;
    }
    
    private void HandleTargetPlayerChanged(ulong oldValue, ulong newValue)
    {
        actualTargetPlayer.Value = newValue == 69420 ? null : StartOfRound.Instance.allPlayerScripts[newValue];
        targetPlayer = actualTargetPlayer.Value;
        LogDebug(actualTargetPlayer.IsNotNull
            ? $"Changed target player to {actualTargetPlayer.Value?.playerUsername}."
            : "Changed target player to null.");
    }
    
    private void SubscribeToNetworkEvents()
    {
        if (!IsServer || _networkEventsSubscribed) return;
        
        netcodeController.OnChangeAgentMaxSpeed += HandleChangeAgentMaxSpeed;
        netcodeController.OnFixAgentSpeedAfterAttack += HandleFixAgentSpeedAfterAttack;

        netcodeController.TargetPlayerClientId.OnValueChanged += HandleTargetPlayerChanged;

        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!IsServer || !_networkEventsSubscribed) return;
        
        netcodeController.OnChangeAgentMaxSpeed -= HandleChangeAgentMaxSpeed;
        netcodeController.OnFixAgentSpeedAfterAttack -= HandleFixAgentSpeedAfterAttack;
        
        netcodeController.TargetPlayerClientId.OnValueChanged -= HandleTargetPlayerChanged;

        _networkEventsSubscribed = false;
    }

    private void InitializeConfigValues()
    {
        if (!IsServer) return;

        enemyHP = Mathf.Max(HarpGhostConfig.Instance.HarpGhostInitialHealth.Value, 1);
        annoyanceDecayRate = Mathf.Max(HarpGhostConfig.Instance.HarpGhostAnnoyanceLevelDecayRate.Value, 0f);
        annoyanceThreshold = Mathf.Max(HarpGhostConfig.Instance.HarpGhostAnnoyanceThreshold.Value, 0f);
        maxSearchRadius = Mathf.Max(HarpGhostConfig.Instance.HarpGhostMaxSearchRadius.Value, 30f);
        attackCooldown = Mathf.Max(HarpGhostConfig.Instance.HarpGhostAttackCooldown.Value, 0f);
        canHearPlayers = HarpGhostConfig.Instance.HarpGhostCanHearPlayersWhenAngry.Value;
        viewWidth = Mathf.Max(HarpGhostConfig.Instance.HarpGhostViewWidth.Value, 1f);
        viewRange = Mathf.Max(HarpGhostConfig.Instance.HarpGhostViewRange.Value, 1);
        proximityAwareness = Mathf.Max(HarpGhostConfig.Instance.HarpGhostProximityAwareness.Value, -1);
        friendlyFire = HarpGhostConfig.Instance.HarpGhostFriendlyFire.Value;
        hearingPrecision = Mathf.Clamp(HarpGhostConfig.Instance.HarpGhostHearingPrecision.Value, 1f, 100f);
        
        ExtendAttackAreaCollider();
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}