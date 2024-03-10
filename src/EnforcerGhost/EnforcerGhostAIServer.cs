using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

// TODO: Add "Fat Boi" config which allows people to make the enforcer ghost unable to fly across gaps because they are too fat

namespace LethalCompanyHarpGhost.EnforcerGhost;

[SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
public class EnforcerGhostAIServer : EnemyAI
{
    private ManualLogSource _mls;
    public string ghostId;

    [Header("AI and Pathfinding")] 
    [Space(5f)]
    public AISearchRoutine roamMap;
    public AISearchRoutine searchForPlayers;
    
    [SerializeField] private float agentMaxAcceleration = 50f;
    [SerializeField] private float agentMaxSpeed = 0.8f;
    private float _agentCurrentSpeed = 0f;
    private float _timeSinceHittingLocalPlayer = 0f;

    private bool _hasBegunInvestigating = false;
    private bool _inStunAnimation = false;
    private bool _canHearPlayers = false;

    private Vector3 _targetPosition = default;
    private Vector3 _agentLastPosition = default;

    // private PlayerControllerB _targetPlayer;
    
    private RoundManager _roundManager;

    private ShotgunItem _heldShotgun;
    
    [Header("Controllers and Managers")]
    [Space(5f)]
    #pragma warning disable 0649
    [SerializeField] private EnforcerGhostAudioManager audioManager;
    [SerializeField] private EnforcerGhostNetcodeController netcodeController;
    [SerializeField] private EnforcerGhostAnimationController animationController;
    [HideInInspector] public IEscortee Escortee { private get; set; }
    #pragma warning restore 0649

    private enum States
    {
        Escorting,
        SearchingForPlayers,
        InvestigatingTargetPosition,
        ShootingTargetPlayer,
        Dead
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Enforcer Ghost AI {ghostId} | Server");
        
        netcodeController = GetComponent<EnforcerGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("Netcode Controller is null");
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) _mls.LogError("NavMeshAgent component not found on " + name);
        agent.enabled = true;

        audioManager = GetComponent<EnforcerGhostAudioManager>();
        if (audioManager == null) _mls.LogError("Audio Manger is null");
        
        animationController = GetComponent<EnforcerGhostAnimationController>();
        if (animationController == null) _mls.LogError("Animation Controller is null");
        
        _roundManager = FindObjectOfType<RoundManager>();
        
        ghostId = Guid.NewGuid().ToString();
        netcodeController.SyncGhostIdentifierClientRpc(ghostId);
        
        UnityEngine.Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        netcodeController.ChangeAnimationParameterBoolClientRpc(ghostId, EnforcerGhostAnimationController.IsDead, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(ghostId, EnforcerGhostAnimationController.IsStunned, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(ghostId, EnforcerGhostAnimationController.IsRunning, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(ghostId, EnforcerGhostAnimationController.IsHoldingShotgun, false);
        
        InitializeConfigValues();
        
        netcodeController.SpawnShotgunServerRpc(ghostId);
        netcodeController.GrabShotgunClientRpc(ghostId);
        
        _mls.LogInfo("Enforcer Ghost Spawned");
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
        
        if (stunNormalizedTimer <= 0.0 && _inStunAnimation && !isEnemyDead)
        {
            netcodeController.DoAnimationClientRpc(ghostId, EnforcerGhostAnimationController.Recover);
            _inStunAnimation = false;
            netcodeController.ChangeAnimationParameterBoolClientRpc(ghostId, EnforcerGhostAnimationController.IsStunned, false);
        }
        
        if (StartOfRound.Instance.allPlayersDead)
        {
            netcodeController.ChangeAnimationParameterBoolClientRpc(ghostId, EnforcerGhostAnimationController.IsRunning, false);
            return;
        }
        
        _timeSinceHittingLocalPlayer += Time.deltaTime;
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
                if (roamMap.inProgress) StopSearch(roamMap);
                
                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(90f, 40, 2);
                if (tempTargetPlayer != null)
                {
                    SwitchBehaviourStateLocally((int)States.ShootingTargetPlayer);
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
                    break;
                }
                
                break;
            }
            
            case (int)States.InvestigatingTargetPosition: // investigating last seen player position state
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);

                // Check for player in LOS
                PlayerControllerB tempTargetPlayer = CheckLineOfSightForClosestPlayer(90f, 40, 2);
                if (tempTargetPlayer != null)
                {
                    SwitchBehaviourStateLocally((int)States.ShootingTargetPlayer);
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

                // If player isnt in LOS and ghost has reached the player's last known position, then switch to state 1
                if (Vector3.Distance(transform.position, _targetPosition) <= 1)
                {
                    SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
                    break;
                }
                
                break;
            }

            case (int)States.ShootingTargetPlayer:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (searchForPlayers.inProgress) StopSearch(searchForPlayers);
                
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

    private void AimAtPosition(Vector3 position)
    {
        Vector3 direction = (position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
    }

    // Called by the escortee ghost to tell the enforcer to switch to behaviour state 1 and stop escorting
    public void EnterRambo()
    {
        if (!IsServer) return;
        if (currentBehaviourStateIndex != (int)States.Dead && currentBehaviourStateIndex != (int)States.SearchingForPlayers)
        {
            SwitchBehaviourStateLocally((int)States.SearchingForPlayers);
        }
    }

    private void BeginChasingPlayer(int targetPlayerObjectId)
    {
        if (!IsServer) return;
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        targetPlayer = player;
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

                break;
            }

            case (int)States.SearchingForPlayers:
            {
                LogDebug($"Switched to behaviour state {(int)States.SearchingForPlayers}!");

                agentMaxSpeed = 3f;
                agentMaxAcceleration = 15f;
                openDoorSpeedMultiplier = 1f;

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
                
                Escortee?.EscorteeBreakoff();
                netcodeController.DropShotgunClientRpc(ghostId, transform.position);
                netcodeController.ChangeAnimationParameterBoolClientRpc(ghostId, EnforcerGhostAnimationController.IsDead, true);
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
    
    private void InitializeConfigValues()
    {
        if (!IsServer) return;
        netcodeController.InitializeConfigValuesClientRpc(ghostId);
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
    
    private void MoveWithAcceleration() {
        if (!IsServer) return;
        
        float speedAdjustment = Time.deltaTime / 2f;
        agent.speed = Mathf.Lerp(agent.speed, agentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        agent.acceleration = Mathf.Lerp(agent.acceleration, agentMaxAcceleration, accelerationAdjustment);
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls.LogInfo(msg);
        #endif
    }
    
    public Vector3 TransformPosition => transform.position;
    public RoundManager RoundManagerInstance => RoundManager.Instance;
}