using GameNetcodeStuff;
using System.Collections;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace LethalCompanyHarpGhost.EnforcerGhost;

public class EnforcerGhostAIServer : MusicalGhost
{
    internal enum States
    {
        Escorting = 0,
        SearchingForPlayers = 1,
        InvestigatingTargetPosition = 2,
        ShootingTargetPlayer = 3,
        Dead = 4
    }

    [Header("AI and Pathfinding")]
    [Space(5f)]
    public AISearchRoutine searchForPlayers;

    [SerializeField] private float agentMaxAcceleration = 50f;
    [SerializeField] private float agentMaxSpeed = 0.8f;
    [SerializeField] private float maxSearchRadius = 60f;
    [SerializeField] private float shootDelay = 3f;
    [SerializeField] private float reloadTime = 5f;
    [SerializeField] private float shieldRegenerateTime = 25f;
    [SerializeField] private bool shieldBehaviourEnabled = true;

#pragma warning disable 0649
    [Header("Controllers")] [Space(5f)]
    [SerializeField] private EnforcerGhostNetcodeController netcodeController;

    internal IEscortee Escortee { private get; set; }
#pragma warning restore 0649

    internal Vector3 TargetPosition;
    private Vector3 _agentLastPosition;

    private ShotgunItem _heldShotgun;
    private NetworkObjectReference _shotgunObjectRef;

    private float _agentCurrentSpeed;
    private float _shootTimer;
    private float _takeDamageCooldown;
    private float _shieldRecoverTimer = 25f;
    public float escorteePingTimer = 5f;

    private bool _hasBegunInvestigating;
    private bool _inStunAnimation;
    private bool _isReloading;
    private bool _isShieldEnabled;
    private bool _isSubscribedToNetworkEvents;
    internal bool FullySpawned;

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;

        netcodeController = GetComponent<EnforcerGhostNetcodeController>();
        if (!netcodeController) LogError("Netcode Controller is null");

        agent = GetComponent<NavMeshAgent>();
        if (!agent) LogError("NavMeshAgent component not found on " + name);
        agent.enabled = true;

        InitializeConfigValues();
        netcodeController.ChangeAnimationParameterBoolClientRpc(EnforcerGhostAIClient.IsDead, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(EnforcerGhostAIClient.IsStunned, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(EnforcerGhostAIClient.IsRunning, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(EnforcerGhostAIClient.IsHoldingShotgun, false);

        StartCoroutine(SpawnAnimation());

        LogVerbose("Enforcer Ghost Spawned");
    }

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
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
        if (!IsServer || isEnemyDead) return;

        CalculateAgentSpeed();

        _shootTimer -= Time.deltaTime;
        _takeDamageCooldown -= Time.deltaTime;
        _shieldRecoverTimer -= Time.deltaTime;

        if (currentBehaviourStateIndex == (int)States.Escorting)
        {
            escorteePingTimer -= Time.deltaTime;
            if (escorteePingTimer <= 0)
            {
                SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
            }
        }

        if (stunNormalizedTimer <= 0.0 && _inStunAnimation)
        {
            //netcodeController.DoAnimationClientRpc(EnforcerGhostAIClient.Recover);
            _inStunAnimation = false;
            netcodeController.ChangeAnimationParameterBoolClientRpc(EnforcerGhostAIClient.IsStunned, false);
        }

        if (StartOfRound.Instance.allPlayersDead)
        {
            netcodeController.ChangeAnimationParameterBoolClientRpc(EnforcerGhostAIClient.IsRunning, false);
            return;
        }

        if (shieldBehaviourEnabled && _shieldRecoverTimer <= 0 && !_isShieldEnabled &&
            currentBehaviourStateIndex is (int)States.Escorting or (int)States.SearchingForPlayers
                or (int)States.InvestigatingTargetPosition)
        {
            LogDebug("Enabling shield through timer");
            _isShieldEnabled = true;
            netcodeController.EnableShieldClientRpc();
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (!IsServer) return;

        switch (currentBehaviourStateIndex)
        {
            case (int)States.Escorting:
            {
                if (Escortee == null) SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
                break;
            }

            case (int)States.SearchingForPlayers: // searching for player state
            {
                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(90f, 40, 2);
                if (tempTargetPlayer)
                {
                    BeginChasingPlayer(GhostUtils.GetClientIdFromPlayer(tempTargetPlayer));
                    SwitchBehaviourStateLocally((int)States.ShootingTargetPlayer);
                    break;
                }

                if (!searchForPlayers.inProgress)
                {
                    if (TargetPosition != default)
                    {
                        if (CheckForPath(TargetPosition))
                        {
                            searchForPlayers.searchWidth = 30f;
                            StartSearch(TargetPosition, searchForPlayers);
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

            case (int)States.InvestigatingTargetPosition: // investigating last seen player position state
            {
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);

                // Check for player in LOS
                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(90f, 40, 2);
                if (tempTargetPlayer)
                {
                    BeginChasingPlayer(GhostUtils.GetClientIdFromPlayer(tempTargetPlayer));
                    SwitchBehaviourStateLocally((int)States.ShootingTargetPlayer);
                    break;
                }

                // begin investigating if not already
                if (!_hasBegunInvestigating)
                {
                    if (TargetPosition == default) SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
                    else
                    {
                        if (!SetDestinationToPosition(TargetPosition, true))
                        {
                            SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
                            break;
                        }

                        _hasBegunInvestigating = true;
                    }
                }

                // If player isn't in LOS and ghost has reached the player's last known position, then switch to state 1
                if ((transform.position - TargetPosition).sqrMagnitude <= 1)
                {
                    SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
                }

                break;
            }

            case (int)States.ShootingTargetPlayer:
            {
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);

                PlayerControllerB playerControllerB = CheckLineOfSightForClosestPlayer(115f, 40, 2);
                if (!playerControllerB)
                {
                    SwitchBehaviourStateLocally((int)States.InvestigatingTargetPosition);
                    break;
                }

                if (shieldBehaviourEnabled && _isShieldEnabled)
                {
                    netcodeController.DisableShieldClientRpc();
                    _isShieldEnabled = false;
                    _shieldRecoverTimer = shieldRegenerateTime;
                }

                if (stunNormalizedTimer > 0) break;

                BeginChasingPlayer(GhostUtils.GetClientIdFromPlayer(playerControllerB));

                // _targetPosition is the last seen position of a player before they went out of view
                TargetPosition = targetPlayer.transform.position;
                netcodeController.IncreaseTargetPlayerFearLevelClientRpc();

                AimAtPosition(targetPlayer.transform.position);

                // Check the distance between the enforcer ghost and the target player, if they are close, then stop moving
                if ((transform.position - targetPlayer.transform.position).sqrMagnitude <= 25f ) // 5f * 5f = 25f
                {
                    movingTowardsTargetPlayer = false;
                    agentMaxSpeed = 0f;
                    agent.speed = 0f;
                }
                else
                {
                    agentMaxSpeed = 1f;
                    movingTowardsTargetPlayer = true;
                }

                if (!_heldShotgun)
                {
                    LogError("Missing shotgun.");
                    return;
                }

                if (_heldShotgun.shellsLoaded <= 0 && !_isReloading)
                {
                    LogDebug("The shotgun has no more bullets! Reloading!");
                    _isReloading = true;
                    StartCoroutine(ReloadShotgun());
                    break;
                }

                // Check if the shoot timer is complete
                if (_shootTimer > 0) break;

                // Check if the enforcer ghost is aiming at the player
                Vector3 directionToPlayer = targetPlayer.transform.position - _heldShotgun.transform.position;
                directionToPlayer.Normalize();
                float dotProduct = Vector3.Dot(_heldShotgun.transform.forward, directionToPlayer);
                float distanceToPlayer =
                    Vector3.Distance(_heldShotgun.transform.position, targetPlayer.transform.position);

                float accuracyThreshold = 0.875f;
                if (distanceToPlayer < 3f)
                    accuracyThreshold = 0.7f;

                // Shoot the gun if the ghost has an accurate enough shot
                if (dotProduct > accuracyThreshold)
                {
                    netcodeController.ShootGunClientRpc();
                    _shootTimer = shootDelay;
                    _heldShotgun.shellsLoaded = Mathf.Clamp(_heldShotgun.shellsLoaded - 1, 0, 2);
                    netcodeController.UpdateShotgunShellsLoadedClientRpc(_heldShotgun.shellsLoaded);
                }

                break;
            }

            case (int)States.Dead:
            {
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);

                break;
            }
        }
    }

    private IEnumerator SpawnAnimation()
    {
        if (!IsServer) yield break;

        FullySpawned = false;
        netcodeController.PlayTeleportVfxClientRpc();
        netcodeController.PlayCreatureVoiceClientRpc((int)EnforcerGhostAIClient.AudioClipTypes.Spawn, 1,
            false);
        yield return new WaitForSeconds(0.75f);
        netcodeController.SpawnShotgunServerRpc();
        netcodeController.GrabShotgunClientRpc();
        yield return new WaitForSeconds(2f);
        FullySpawned = true;

        LogDebug("Enabling shield through spawn animation");
        _isShieldEnabled = true;
        netcodeController.EnableShieldClientRpc();
    }

    private IEnumerator ReloadShotgun()
    {
        if (!IsServer) yield break;

        _isReloading = true;
        float previousSpeed = agentMaxSpeed;
        agentMaxSpeed = 0f;

        LogDebug("In reload coroutine");
        netcodeController.UpdateShotgunShellsLoadedClientRpc(2);
        netcodeController.DoAnimationClientRpc(EnforcerGhostAIClient.ReloadShotgun);

        yield return new WaitForSeconds(reloadTime - 0.3f);
        _heldShotgun.shellsLoaded = 2;
        agentMaxSpeed = previousSpeed;
        yield return new WaitForSeconds(reloadTime);
        _shootTimer = shootDelay;
        _isReloading = false;
    }

    private void AimAtPosition(Vector3 position)
    {
        Vector3 direction = (position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
    }

    private void BeginChasingPlayer(ulong playerClientId)
    {
        if (!IsServer) return;
        netcodeController.ChangeTargetPlayerClientRpc(playerClientId);
        PlayerControllerB player = GhostUtils.GetPlayerFromClientId(playerClientId);
        SetMovingTowardsTargetPlayer(player);
    }

    private void SwitchBehaviourStateLocally(int state)
    {
        if (!IsServer) return;
        switch (state)
        {
            case (int)States.Escorting:
            {
                LogDebug($"Switched to behaviour state {(int)States.Escorting}!");

                agentMaxSpeed = 0.5f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = false;
                openDoorSpeedMultiplier = 4f;
                _isReloading = false;
                _shootTimer = shootDelay;
                _hasBegunInvestigating = false;
                TargetPosition = default;

                break;
            }

            case (int)States.SearchingForPlayers:
            {
                LogDebug($"Switched to behaviour state {(int)States.SearchingForPlayers}!");

                agentMaxSpeed = Mathf.Min(2f, EnforcerGhostConfig.Instance.EnforcerGhostMaxSpeedInChaseMode.Value);
                agentMaxAcceleration = Mathf.Min(15f,
                    EnforcerGhostConfig.Instance.EnforcerGhostMaxAccelerationInChaseMode.Value);
                openDoorSpeedMultiplier = Mathf.Min(1f,
                    EnforcerGhostConfig.Instance.EnforcerGhostDoorSpeedMultiplierInChaseMode.Value);
                movingTowardsTargetPlayer = false;
                _hasBegunInvestigating = false;
                TargetPosition = default;
                _shootTimer = shootDelay;

                Escortee?.EscorteeBreakoff();

                break;
            }

            case (int)States.InvestigatingTargetPosition:
            {
                LogDebug($"Switched to behaviour state {(int)States.InvestigatingTargetPosition}!");

                agentMaxSpeed = Mathf.Min(2f, EnforcerGhostConfig.Instance.EnforcerGhostMaxSpeedInChaseMode.Value);
                agentMaxAcceleration = Mathf.Min(15f,
                    EnforcerGhostConfig.Instance.EnforcerGhostMaxAccelerationInChaseMode.Value);
                openDoorSpeedMultiplier = Mathf.Min(1f,
                    EnforcerGhostConfig.Instance.EnforcerGhostDoorSpeedMultiplierInChaseMode.Value);
                moveTowardsDestination = true;
                movingTowardsTargetPlayer = false;
                _hasBegunInvestigating = false;
                _shootTimer = shootDelay;

                Escortee?.EscorteeBreakoff();

                break;
            }

            case (int)States.ShootingTargetPlayer:
            {
                LogDebug($"Switched to behaviour state {(int)States.ShootingTargetPlayer}!");

                agentMaxSpeed = EnforcerGhostConfig.Instance.EnforcerGhostMaxSpeedInChaseMode.Value;
                agentMaxAcceleration = EnforcerGhostConfig.Instance.EnforcerGhostMaxAccelerationInChaseMode.Value;
                openDoorSpeedMultiplier =
                    EnforcerGhostConfig.Instance.EnforcerGhostDoorSpeedMultiplierInChaseMode.Value;
                movingTowardsTargetPlayer = true;
                _hasBegunInvestigating = false;
                _shootTimer = shootDelay;
                TargetPosition = default;

                Escortee?.EscorteeBreakoff();

                break;
            }

            case (int)States.Dead:
            {
                LogDebug($"Switched to behaviour state {(int)States.Dead}!");

                agentMaxSpeed = 0f;
                agentMaxAcceleration = 0f;
                movingTowardsTargetPlayer = false;
                agent.speed = 0f;
                agent.enabled = false;
                isEnemyDead = true;
                _isReloading = false;
                TargetPosition = default;
                _hasBegunInvestigating = false;
                moveTowardsDestination = false;

                netcodeController.EnterDeathStateClientRpc();
                Escortee?.EscorteeBreakoff();

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
        if (!IsServer || isEnemyDead || _takeDamageCooldown > 0)
            return;

        if (!EnforcerGhostConfig.Instance.EnforcerGhostFriendlyFire.Value && !playerWhoHit)
            return;

        _takeDamageCooldown = 0.03f;
        Escortee?.EscorteeBreakoff(playerWhoHit);
        if (shieldBehaviourEnabled && _isShieldEnabled)
        {
            netcodeController.DisableShieldClientRpc();
            _isShieldEnabled = false;
            _shieldRecoverTimer = shieldRegenerateTime;
        }
        else enemyHP -= force;

        if (enemyHP > 0)
        {
            netcodeController.PlayCreatureVoiceClientRpc((int)EnforcerGhostAIClient.AudioClipTypes.Damage, 4);
        }
        else
        {
            // Ghost is dead
            KillEnemyClientRpc(false);
        }
    }

    public override void KillEnemy(bool destroy = false)
    {
        base.KillEnemy(destroy);
        if (!IsServer) return;
        SwitchBehaviourStateLocally((int)States.Dead);
    }

    public override void SetEnemyStunned(
        bool setToStunned,
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer || isEnemyDead || currentBehaviourStateIndex is (int)States.Dead) return;
        if (shieldBehaviourEnabled && _isShieldEnabled)
        {
            netcodeController.DisableShieldClientRpc();
            _isShieldEnabled = false;
            _shieldRecoverTimer = shieldRegenerateTime;
        }

        netcodeController.PlayCreatureVoiceClientRpc((int)EnforcerGhostAIClient.AudioClipTypes.Stun, 1);
        // netcodeController.DropShotgunForStun(transform.position);
        // netcodeController.ChangeAnimationParameterBoolClientRpc(HarpGhostAnimationController.IsStunned, true);
        // netcodeController.DoAnimationClientRpc(HarpGhostAnimationController.Stunned);
        _inStunAnimation = true;
        Escortee?.EscorteeBreakoff(setStunnedByPlayer ? setStunnedByPlayer : null);
    }

    private void HandleGrabShotgunPhaseTwo()
    {
        if (!IsServer || _heldShotgun) return;
        if (!_shotgunObjectRef.TryGet(out NetworkObject networkObject))
        {
            LogDebug("Could not get shotgun object reference");
            return;
        }

        _heldShotgun = networkObject.gameObject.GetComponent<ShotgunItem>();
    }

    private void HandleSpawnShotgun(NetworkObjectReference shotgunObject, int shotgunScrapValue)
    {
        _shotgunObjectRef = shotgunObject;
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

    public override void FinishedCurrentSearchRoutine()
    {
        base.FinishedCurrentSearchRoutine();
        if (!IsServer) return;
        if (searchForPlayers.inProgress)
            searchForPlayers.searchWidth = Mathf.Clamp(searchForPlayers.searchWidth + 10f, 1f, maxSearchRadius);
    }

    private void InitializeConfigValues()
    {
        if (!IsServer) return;

        enemyHP = EnforcerGhostConfig.Instance.EnforcerGhostInitialHealth.Value;
        shootDelay = EnforcerGhostConfig.Instance.EnforcerGhostShootDelay.Value;
        agent.angularSpeed = EnforcerGhostConfig.Instance.EnforcerGhostTurnSpeed.Value;
        shieldBehaviourEnabled = EnforcerGhostConfig.Instance.EnforcerGhostShieldEnabled.Value;
        shieldRegenerateTime = EnforcerGhostConfig.Instance.EnforcerGhostShieldRegenTime.Value;

        _shootTimer = shootDelay;

        netcodeController.InitializeConfigValuesClientRpc();
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

        if (currentBehaviourStateIndex != (int)States.Dead)
        {
            MoveWithAcceleration();
        }
    }

    /// <summary>
    /// Makes the agent move by using <see cref="Mathf.Lerp"/> to make the movement smooth.
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
        if (!IsServer || _isSubscribedToNetworkEvents) return;

        netcodeController.OnGrabShotgunPhaseTwo += HandleGrabShotgunPhaseTwo;
        netcodeController.OnSpawnShotgun += HandleSpawnShotgun;

        _isSubscribedToNetworkEvents = true;
    }

    /// <summary>
    /// Unsubscribe to the network events.
    /// </summary>
    private void UnsubscribeFromNetworkEvents()
    {
        if (!IsServer || !_isSubscribedToNetworkEvents) return;

        netcodeController.OnGrabShotgunPhaseTwo -= HandleGrabShotgunPhaseTwo;
        netcodeController.OnSpawnShotgun -= HandleSpawnShotgun;

        _isSubscribedToNetworkEvents = false;
    }
}