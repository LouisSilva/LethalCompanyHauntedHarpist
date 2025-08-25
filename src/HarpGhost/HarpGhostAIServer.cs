using BepInEx.Logging;
using GameNetcodeStuff;
using LethalCompanyHarpGhost.Types;
using System;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost.HarpGhost;

public class HarpGhostAIServer : MusicalGhost
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

    [Header("AI and Pathfinding")] [Space(5f)] 
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
    [SerializeField] private bool canHearPlayers = true;
    [SerializeField] private bool friendlyFire = true;

#pragma warning disable 0649
    [Header("Transforms")] [Space(5f)] 
    [SerializeField] private BoxCollider attackArea;

    [Header("Controllers and Managers")] [Space(5f)] 
    [SerializeField] private HarpGhostAudioManager audioManager;
    [SerializeField] private HarpGhostNetcodeController netcodeController;
    [SerializeField] private HarpGhostAnimationController animationController;
#pragma warning restore 0649

    private Collider[] attackHitBuffer;
    
    private Vector3 _targetPosition;
    private Vector3 _agentLastPosition;
    
    private float _agentCurrentSpeed;
    private float _timeSinceHittingLocalPlayer;
    private float _hearNoiseCooldown;

    private bool _hasBegunInvestigating;
    private bool _inStunAnimation;
    private bool _hasTransitionedMaterial;
    private bool _areNetworkEventsSubscribed;
    
    public void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    public void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;

        _ghostId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Harp Ghost AI {_ghostId} | Server");

        netcodeController = GetComponent<HarpGhostNetcodeController>();
        if (!netcodeController) _mls.LogError("Netcode Controller is null");

        agent = GetComponent<NavMeshAgent>();
        if (!agent) _mls.LogError("NavMeshAgent component not found on " + name);
        agent.enabled = true;

        audioManager = GetComponent<HarpGhostAudioManager>();
        if (!audioManager) _mls.LogError("Audio Manger is null");

        animationController = GetComponent<HarpGhostAnimationController>();
        if (!animationController) _mls.LogError("Animation Controller is null");
        
        SubscribeToNetworkEvents();
        netcodeController.SyncGhostIdentifierClientRpc(_ghostId);

        Random.InitState(StartOfRound.Instance.randomMapSeed + _ghostId.GetHashCode() - thisEnemyIndex);
        InitializeConfigValues();

        attackHitBuffer = new Collider[20];
        
        PlayerTargetableConditions.AddCondition(player => !GhostUtils.IsPlayerDead(player));
        PlayerTargetableConditions.AddCondition(player => !player.isInHangarShipRoom);
        PlayerTargetableConditions.AddCondition(player => player.sinkingValue < 0.7300000190734863);

        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsDead, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsStunned, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsRunning, false);
        
        netcodeController.SpawnHarpServerRpc(_ghostId);
        netcodeController.GrabHarpClientRpc(_ghostId);
        StartCoroutine(DelayedHarpMusicActivate());

        LogDebug("Harp Ghost Spawned");
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime,
            0.75f);
        _agentLastPosition = position;
    }

    public override void Update()
    {
        base.Update();
        if (!IsServer) return;
        
        CalculateAgentSpeed();
        
        if (stunNormalizedTimer <= 0.0 && _inStunAnimation && !isEnemyDead)
        {
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsStunned, false);
            _inStunAnimation = false;
        }

        if (StartOfRound.Instance.allPlayersDead)
        {
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsRunning, false);
            return;
        }

        _timeSinceHittingLocalPlayer += Time.deltaTime;
        _hearNoiseCooldown -= Time.deltaTime;

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
                    netcodeController.PlayCreatureVoiceClientRpc(_ghostId,
                        (int)HarpGhostAudioManager.AudioClipTypes.Upset, audioManager.upsetSfx.Length);

                    if (_targetPosition != default) SwitchBehaviourStateLocally((int)States.InvestigatingTargetPosition);
                    else SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
                }

                break;
            }

            case (int)States.SearchingForPlayers:
            case (int)States.InvestigatingTargetPosition:
            case (int)States.ChasingTargetPlayer:
            {
                RunAnimation();
                break;
            }

            case (int)States.Dead:
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
            case (int)States.PlayingMusic:
            {
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                if (!roamMap.inProgress) StartSearch(transform.position, roamMap);
                break;
            }

            case (int)States.SearchingForPlayers:
            {
                if (roamMap.inProgress) StopSearch(roamMap);

                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(viewWidth, viewRange, proximityAwareness);
                if (tempTargetPlayer)
                {
                    BeginChasingPlayer(GhostUtils.GetClientIdFromPlayer(tempTargetPlayer));
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
                    LogDebug("Started search");
                }

                break;
            }

            case (int)States.InvestigatingTargetPosition:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);

                // Check for player in LOS
                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(viewWidth, viewRange, proximityAwareness);
                if (tempTargetPlayer)
                {
                    BeginChasingPlayer(GhostUtils.GetClientIdFromPlayer(tempTargetPlayer));
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

                PlayerControllerB visiblePlayer = GetClosestVisiblePlayer(eye, viewWidth, viewRange, targetPlayer,
                    proximityAwareness: proximityAwareness);

                // If no players were found, switch to state 2
                if (!visiblePlayer)
                {
                    SwitchBehaviourStateLocally((int)States.InvestigatingTargetPosition);
                    break;
                }

                // If our target has changed, then tell the ghost to chase the new target
                if (visiblePlayer != targetPlayer)
                {
                    BeginChasingPlayer(GhostUtils.GetClientIdFromPlayer(visiblePlayer));
                }
                
                _targetPosition = targetPlayer.transform.position;
                
                netcodeController.IncreaseTargetPlayerFearLevelClientRpc(_ghostId);

                // Check if a player is in attack area and if so, attack
                if (Vector3.Distance(transform.position, targetPlayer.transform.position) < 8) AttackPlayerIfClose();
                break;
            }
            
            case (int)States.Dead:
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
            case (int)States.PlayingMusic:
            {
                LogDebug($"Switched to behaviour state {(int)States.PlayingMusic}!");

                agentMaxSpeed = 0.3f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = false;
                _targetPosition = default;
                _hasBegunInvestigating = false;
                openDoorSpeedMultiplier = 6;

                netcodeController.ChangeTargetPlayerClientRpc(_ghostId, MusicalGhost.NullPlayerId);
                break;
            }

            case (int)States.SearchingForPlayers:
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

                agentMaxSpeed = 0;
                agentMaxAcceleration = 0;
                movingTowardsTargetPlayer = false;
                agent.speed = 0;
                agent.enabled = false;
                isEnemyDead = true;
                _hasBegunInvestigating = false;
                _targetPosition = default;

                netcodeController.ChangeTargetPlayerClientRpc(_ghostId, MusicalGhost.NullPlayerId);
                netcodeController.DropHarpClientRpc(_ghostId, transform.position);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsDead,
                    true);

                break;
            }
        }

        if (currentBehaviourStateIndex == state) return;
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false,
        int hitId = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitId);
        if (!IsServer || isEnemyDead) return;

        // If friendly fire is disabled, and we weren't hit by a player, then ignore the hit
        bool isPlayerWhoHitNull = !playerWhoHit;
        if (!friendlyFire && isPlayerWhoHitNull) return;

        enemyHP -= force;
        if (enemyHP <= 0)
        {
            KillEnemyClientRpc(false);
            return;
        }
        
        if (isPlayerWhoHitNull) return;
        
        TurnGhostEyesRed();
        netcodeController.PlayCreatureVoiceClientRpc(_ghostId, (int)HarpGhostAudioManager.AudioClipTypes.Damage, 
            audioManager.damageSfx.Length);
        netcodeController.ChangeTargetPlayerClientRpc(_ghostId, playerWhoHit!.playerClientId);
        SwitchBehaviourStateLocally((int)States.ChasingTargetPlayer);
    }

    public override void KillEnemy(bool destroy = false)
    {
        base.KillEnemy(destroy);
        if (!IsServer) return;
        
        netcodeController.EnterDeathStateClientRpc(_ghostId);
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
        netcodeController.PlayCreatureVoiceClientRpc(_ghostId, (int)HarpGhostAudioManager.AudioClipTypes.Stun,
            audioManager.stunSfx.Length);
        netcodeController.DropHarpClientRpc(_ghostId, transform.position);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsStunned, true);
        netcodeController.DoAnimationClientRpc(_ghostId, HarpGhostAnimationController.IsStunned);
        _inStunAnimation = true;

        if (setStunnedByPlayer)
        {
            netcodeController.ChangeTargetPlayerClientRpc(_ghostId, setStunnedByPlayer.playerClientId);
            SwitchBehaviourStateLocally((int)States.ChasingTargetPlayer);
        }
        else
        {
            if (currentBehaviourStateIndex == (int)States.PlayingMusic)
                SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
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
        
        int numColldiersHit = Physics.OverlapBoxNonAlloc(attackArea.transform.position, attackArea.size * 0.5f, 
            attackHitBuffer, Quaternion.identity, 1 << 3, QueryTriggerInteraction.Ignore);

        if (numColldiersHit <= 0) return;
        for (int i = 0; i < attackHitBuffer.Length; i++)
        {
            Collider player = attackHitBuffer[i];
            PlayerControllerB playerControllerB = PlayerMeetsStandardCollisionConditions(player);
            if (!playerControllerB) continue;

            netcodeController.ChangeTargetPlayerClientRpc(_ghostId, playerControllerB.playerClientId);
            _timeSinceHittingLocalPlayer = 0f;
            netcodeController.DoAnimationClientRpc(_ghostId, HarpGhostAnimationController.Attack);
            break;
        }
    }

    private IEnumerator DelayedHarpMusicActivate() // Needed to mitigate race conditions
    {
        yield return new WaitForSeconds(0.5f);
        netcodeController.PlayHarpMusicClientRpc(_ghostId);
    }

    private void HandleChangeTargetPlayer(string receivedGhostId, ulong targetPlayerObjectId)
    {
        if (!IsServer || _ghostId != receivedGhostId) return;
        if (targetPlayerObjectId == NullPlayerId)
        {
            targetPlayer = null;
            return;
        }

        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        targetPlayer = player;
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

    private void BeginChasingPlayer(ulong playerClientId)
    {
        if (!IsServer) return;
        netcodeController.ChangeTargetPlayerClientRpc(_ghostId, playerClientId);
        PlayerControllerB player = GhostUtils.GetPlayerFromClientId(playerClientId);
        SetMovingTowardsTargetPlayer(player);
    }

    // Using custom player collision checker because the default one checks if the player is the owner of the enemy
    private PlayerControllerB PlayerMeetsStandardCollisionConditions(Collider collider)
    {
        PlayerControllerB player = collider.GetComponent<PlayerControllerB>();
        return PlayerTargetableConditions.IsPlayerTargetable(player) ? player : null;
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        if (!IsServer) return;
        if (currentBehaviourStateIndex is (int)States.PlayingMusic or (int)States.Dead ||
            _timeSinceHittingLocalPlayer < 2f || _inStunAnimation) return;

        PlayerControllerB playerControllerB = PlayerMeetsStandardCollisionConditions(other);
        if (!playerControllerB) return;

        _timeSinceHittingLocalPlayer = 0f;
        if (currentBehaviourStateIndex != (int)States.ChasingTargetPlayer)
            SwitchBehaviourStateLocally((int)States.ChasingTargetPlayer);

        netcodeController.ChangeTargetPlayerClientRpc(_ghostId, playerControllerB.playerClientId);
        netcodeController.DoAnimationClientRpc(_ghostId, HarpGhostAnimationController.Attack);
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

        if ((double)stunNormalizedTimer > 0 || _hearNoiseCooldown > 0.0 ||
            Enum.IsDefined(typeof(HarpGhostAudioManager.NoiseIDToIgnore), noiseID)) return;
        
        switch (currentBehaviourStateIndex)
        {
            case (int)States.PlayingMusic:
            {
                _hearNoiseCooldown = 0.01f;
                float distanceToNoise = Vector3.Distance(transform.position, noisePosition);
                float noiseThreshold = 15f * noiseLoudness;
                LogDebug(
                    $"Harp Ghost '{gameObject.name}': Heard Noise | Distance: {distanceToNoise} meters away | Noise loudness: {noiseLoudness}");

                if (Physics.Linecast(transform.position, noisePosition, 256))
                {
                    noiseLoudness /= 1.5f;
                    noiseThreshold /= 1.5f;
                }

                if (noiseLoudness < 0.25 || distanceToNoise >= noiseThreshold) return;
                if (noiseID is (int)HarpGhostAudioManager.NoiseIds.Boombox
                    or (int)HarpGhostAudioManager.NoiseIds.PlayersTalking
                    or (int)HarpGhostAudioManager.NoiseIds.RadarBoosterPing)
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
                LogDebug(
                    $"Harp Ghost '{gameObject.name}': Heard Noise | Distance: {distanceToNoise} meters away | Noise loudness: {noiseLoudness}");

                if (Physics.Linecast(transform.position, noisePosition, 256))
                {
                    noiseLoudness /= 2f;
                    noiseThreshold /= 2f;
                }

                if (noiseLoudness < 0.25 || distanceToNoise >= noiseThreshold) return;
                _targetPosition = RoundManager.Instance.GetRandomNavMeshPositionInRadius(noisePosition, distanceToNoise / 14f);
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

    private void RunAnimation()
    {
        bool isRunning = _agentCurrentSpeed >= 3f;
        if (animationController.GetBool(HarpGhostAnimationController.IsRunning) != isRunning && !_inStunAnimation)
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsRunning,
                isRunning);
    }
    
    private bool CheckForPath(Vector3 position)
    {
        position = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 1.75f);

        // ReSharper disable once UseIndexFromEndExpression
        return agent.CalculatePath(position, path1) && !(Vector3.Distance(path1.corners[path1.corners.Length - 1],
                                                             RoundManager.Instance.GetNavMeshPosition(position,
                                                                 RoundManager.Instance.navHit, 2.7f)) >
                                                         1.5499999523162842);
    }

    private void CalculateAgentSpeed()
    {
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
    
    private void InitializeConfigValues()
    {
        if (!IsServer) return;
        netcodeController.InitializeConfigValuesClientRpc(_ghostId);

        enemyHP = HarpGhostConfig.Instance.HarpGhostInitialHealth.Value;
        annoyanceDecayRate = HarpGhostConfig.Instance.HarpGhostAnnoyanceLevelDecayRate.Value;
        annoyanceThreshold = HarpGhostConfig.Instance.HarpGhostAnnoyanceThreshold.Value;
        maxSearchRadius = HarpGhostConfig.Instance.HarpGhostMaxSearchRadius.Value;
        attackCooldown = HarpGhostConfig.Instance.HarpGhostAttackCooldown.Value;
        canHearPlayers = HarpGhostConfig.Instance.HarpGhostCanHearPlayersWhenAngry.Value;
        viewWidth = HarpGhostConfig.Instance.HarpGhostViewWidth.Value;
        viewRange = HarpGhostConfig.Instance.HarpGhostViewRange.Value;
        proximityAwareness = Mathf.Clamp(HarpGhostConfig.Instance.HarpGhostProximityAwareness.Value, -1, int.MaxValue);
        friendlyFire = HarpGhostConfig.Instance.HarpGhostFriendlyFire.Value;

        ExtendAttackAreaCollider();
    }

    /// <summary>
    /// Makes the agent move by using <see cref="Mathf.Lerp"/> to make the movement smooth
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MoveWithAcceleration()
    {
        float speedAdjustment = Time.deltaTime / 2f;
        agent.speed = Mathf.Lerp(agent.speed, agentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        agent.acceleration = Mathf.Lerp(agent.acceleration, agentMaxAcceleration, accelerationAdjustment);
    }
    
    /// <summary>
    /// Subscribe to the needed network events.
    /// </summary>
    private void SubscribeToNetworkEvents()
    {
        if (!IsServer || _areNetworkEventsSubscribed) return;
        
        netcodeController.OnChangeTargetPlayer += HandleChangeTargetPlayer;
        netcodeController.OnChangeAgentMaxSpeed += HandleChangeAgentMaxSpeed;
        netcodeController.OnFixAgentSpeedAfterAttack += HandleFixAgentSpeedAfterAttack;

        _areNetworkEventsSubscribed = true;
    }

    /// <summary>
    /// Unsubscribe to the network events.
    /// </summary>
    private void UnsubscribeFromNetworkEvents()
    {
        if (!IsServer || !_areNetworkEventsSubscribed) return;
        
        netcodeController.OnChangeTargetPlayer -= HandleChangeTargetPlayer;
        netcodeController.OnChangeAgentMaxSpeed -= HandleChangeAgentMaxSpeed;
        netcodeController.OnFixAgentSpeedAfterAttack -= HandleFixAgentSpeedAfterAttack;
        
        _areNetworkEventsSubscribed = false;
    }

    private void LogDebug(string msg)
    {
#if DEBUG
        if (!IsServer) return;
        _mls?.LogInfo(msg);
#endif
    }
}