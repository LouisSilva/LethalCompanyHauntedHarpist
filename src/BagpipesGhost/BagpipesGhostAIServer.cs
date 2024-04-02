using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using LethalCompanyHarpGhost.EnforcerGhost;
using LethalCompanyHarpGhost.HarpGhost;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace LethalCompanyHarpGhost.BagpipesGhost;

[SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
public class BagpipesGhostAIServer : EnemyAI, IEscortee
{
    private ManualLogSource _mls;
    private string _ghostId;

    private List<EnforcerGhostAIServer> _escorts = [];

    [Header("AI and Pathfinding")]
    [Space(5f)]
    public AISearchRoutine roamMap;
    
    private float _agentCurrentSpeed;
    private float _openDoorTimer = 0f;
    private const float EscortHorizontalBaseLength = 5f;
    private const float EscortRowSpacing = 3f;
    [SerializeField] private float agentMaxAcceleration = 200f;
    [SerializeField] private float agentMaxSpeed = 0.5f;

    [SerializeField] private float agentMaxAccelerationInEscapeMode = 30f;
    [SerializeField] private float agentMaxSpeedInEscapeMode = 10f;
    [SerializeField] private float openDoorSpeedMultiplierInEscapeMode = 6f;
    
    private bool _agentIsMoving = false;
    private bool _chosenEscapeDoor = false;
    private bool _isNarrowPath = false;
    private bool _inStunAnimation = false;
    private bool _openDoorTimerActivated = false;
    private bool _currentlyRetiringAllEscorts = false;
    
    [SerializeField] private int numberOfEscorts = 3;
    
    private Vector3 _agentLastPosition;
    private readonly List<Vector3> _escortAgentPathPoints = [];

    private EntranceTeleport _escapeDoor;

    private RoundManager _roundManager;

    private InstrumentBehaviour _heldBagpipes;
    private NetworkObjectReference _instrumentObjectRef;

    private Coroutine _teleportCoroutine = null;
    
    #pragma warning disable 0649
    [Header("Visual Effects")]
    [Space(5f)]
    private LineRenderer _lineRenderer;
    
    [Header("Controllers and Managers")]
    [Space(5f)]
    [SerializeField] private BagpipesGhostAudioManager audioManager;
    [SerializeField] private BagpipesGhostNetcodeController netcodeController;
    [SerializeField] private BagpipesGhostAnimationController animationController;
    #pragma warning restore 0649

    private enum States
    {
        PlayingMusicWhileEscorted,
        RunningToEscapeDoor,
        RunningToEdgeOfOutsideMap,
        TeleportingOutOfMap,
        Dead
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Bagpipes Ghost AI {_ghostId} | Server");
        
        netcodeController = GetComponent<BagpipesGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("Netcode Controller is null");
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) _mls.LogError("NavMeshAgent component not found on " + name);
        agent.enabled = true;
        
        audioManager = GetComponent<BagpipesGhostAudioManager>();
        if (audioManager == null) _mls.LogError("Audio Manger is null");

        animationController = GetComponent<BagpipesGhostAnimationController>();
        if (animationController == null) _mls.LogError("Animation Controller is null");
        
        _roundManager = FindObjectOfType<RoundManager>();
        
        _ghostId = Guid.NewGuid().ToString();
        netcodeController.SyncGhostIdentifierClientRpc(_ghostId);
        
        UnityEngine.Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        InitializeConfigValues();
        EnableEnemyMesh(true);
        
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsDead, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsStunned, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsRunning, false);
        
        netcodeController.SpawnBagpipesServerRpc(_ghostId);
        netcodeController.GrabBagpipesClientRpc(_ghostId);
        netcodeController.OnGrabBagpipes += HandleGrabBagpipes;

        if (HarpGhostPlugin.EnforcerGhostEnemyType.enemyPrefab == null)
        {
            _mls.LogError("Enforcer ghost prefab is null, making bagpipe ghost run away from players because he is not escorted");
            SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor);
        }
        else StartCoroutine(SpawnEscorts(() => { netcodeController.PlayBagpipesMusicClientRpc(_ghostId); }));
        
        _mls.LogInfo("Bagpipes Ghost Spawned");
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        netcodeController.OnGrabBagpipes += HandleGrabBagpipes;
    }

    private void InitializeConfigValues()
    {
        if (!IsServer) return;

        enemyHP = BagpipeGhostConfig.Instance.BagpipeGhostInitialHealth.Value;
        agentMaxSpeedInEscapeMode = BagpipeGhostConfig.Instance.BagpipeGhostMaxSpeedInEscapeMode.Value;
        agentMaxAccelerationInEscapeMode = BagpipeGhostConfig.Instance.BagpipeGhostMaxAccelerationInEscapeMode.Value;
        openDoorSpeedMultiplierInEscapeMode = BagpipeGhostConfig.Instance.BagpipeGhostDoorSpeedMultiplierInEscapeMode.Value;
        numberOfEscorts = (int)Mathf.Clamp(BagpipeGhostConfig.Instance.BagpipeGhostNumberOfEscortsToSpawn.Value, 0, Mathf.Infinity);
        
        netcodeController.InitializeConfigValuesClientRpc(_ghostId);
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (currentBehaviourStateIndex is (int)States.PlayingMusicWhileEscorted or (int)States.Dead) return;
        
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;
        _agentIsMoving = _agentCurrentSpeed > 0f;
    }

    public override void Update()
    {
        base.Update();
        if (!IsServer) return;
        
        CalculateAgentSpeed();
        
        if (stunNormalizedTimer <= 0.0 && _inStunAnimation && !isEnemyDead)
        {
            netcodeController.DoAnimationClientRpc(_ghostId, BagpipesGhostAnimationController.Recover);
            _inStunAnimation = false;
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsStunned, false);
        }
        
        if (StartOfRound.Instance.allPlayersDead)
        {
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsRunning, false);
            return;
        }

        switch (currentBehaviourStateIndex)
        {
            case (int)States.PlayingMusicWhileEscorted:
            {
                break;
            }

            case (int)States.RunningToEscapeDoor:
            {
                RunAnimation();
                
                if (_openDoorTimerActivated)
                {
                    _openDoorTimer += Time.deltaTime;
                    if (_openDoorTimer > 1f && stunNormalizedTimer <= 0)
                    {
                        _openDoorTimerActivated = false;
                        GoThroughEscapeDoor();
                    }
                }

                break;
            }

            case (int)States.RunningToEdgeOfOutsideMap:
            {
               RunAnimation();

               break;
            }

            case (int)States.TeleportingOutOfMap:
            {
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
            case (int)States.PlayingMusicWhileEscorted:
            {
                if (!moveTowardsDestination)
                {
                    SetDestinationToPosition(ChooseFarthestNodeFromPosition(transform.position).position);
                    moveTowardsDestination = true;
                }

                // Check if the ghost has reached its destination
                if (Vector3.Distance(transform.position, destination) <= 1)
                {
                    moveTowardsDestination = false; 
                }
                
                _isNarrowPath = CheckForNarrowPassages();
                UpdateEscortAgentPathPoints();
                UpdateAgentFormation();
                
                DrawDebugCircleAtTargetNode();
                
                break;
            }

            case (int)States.RunningToEscapeDoor:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (isOutside) SwitchBehaviourStateLocally((int)States.RunningToEdgeOfOutsideMap);
                if (!_chosenEscapeDoor) ChooseEscapeDoor();

                // Check if the ghost has reached the exit door
                if (Vector3.Distance(transform.position, _escapeDoor.exitPoint.position) <= 1 && !_openDoorTimerActivated)
                {
                    LogDebug("Reached escape door");
                    moveTowardsDestination = false;
                    _openDoorTimerActivated = true;
                    _openDoorTimer = 0f;
                }

                break;
            }

            case (int)States.RunningToEdgeOfOutsideMap:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                if (Vector3.Distance(transform.position, destination) <= 1)
                {
                    moveTowardsDestination = false;
                    if (_teleportCoroutine == null) 
                        StartCoroutine(ExitTheGameThroughOutsideNode());
                }
                
                break;
            }

            case (int)States.TeleportingOutOfMap:
            {
                if (roamMap.inProgress) StopSearch(roamMap);

                break;
            }
        }
    }

    private void GoThroughEscapeDoor()
    {
        if (!IsServer) return;

        isOutside = true;
        serverPosition = _escapeDoor.entrancePoint.position;
        transform.position = serverPosition;
        
        agent.Warp(serverPosition);
        SyncPositionToClients();
        SwitchBehaviourStateLocally((int)States.RunningToEdgeOfOutsideMap);
    }

    private IEnumerator ExitTheGameThroughOutsideNode()
    {
        if(!IsServer) yield break;
        LogDebug("Reached outside escape node");
        
        SwitchBehaviourStateLocally((int)States.TeleportingOutOfMap);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsRunning, false);
        netcodeController.PlayTeleportVfxClientRpc(_ghostId);
        yield return new WaitForSeconds(0.5f);
        EnableEnemyMesh(false);
        netcodeController.SetMeshEnabledClientRpc(_ghostId, false);
        if (_heldBagpipes != null) _heldBagpipes.NetworkObject.Despawn();
        
        yield return new WaitForSeconds(5);
        KillEnemyClientRpc(true);
        _teleportCoroutine = null;
        Destroy(this);
    }

    private void ChooseEscapeDoor()
    {
        if (!IsServer) return;
        
        _chosenEscapeDoor = true;
        EntranceTeleport[] exits = FindObjectsOfType<EntranceTeleport>().Where(exit => exit != null && exit.exitPoint != null).ToArray();
        Dictionary<EntranceTeleport, float> exitDistances = exits.ToDictionary(exit => exit, exit => Vector3.Distance(transform.position, exit.exitPoint.position));
        
        EntranceTeleport closestExit = exitDistances.Aggregate((l, r) => l.Value < r.Value ? l : r).Key;
        _escapeDoor = closestExit;
        
        // Avoid the escape door being the main door if possible
        if (closestExit.entranceId == 0)
        {
            if (Vector3.Distance(transform.position, closestExit.exitPoint.position) > 30f)
            {
                exitDistances.Remove(closestExit);
                _escapeDoor = exitDistances.Aggregate((l, r) => l.Value < r.Value ? l : r).Key;
            }
        }
        
        SetDestinationToPosition(_escapeDoor.exitPoint.position);
    }

    // Makes the escorts enter rambo state and removes them from the escort list
    private void RetireAllEscorts(PlayerControllerB targetPlayer = null)
    {
        if (!IsServer) return;
        if (_currentlyRetiringAllEscorts) return;

        _currentlyRetiringAllEscorts = true;
        for (int i = _escorts.Count - 1; i >= 0; i--)
        {
            LogDebug($"Retiring escort {_escorts[i].ghostId}");
            if (targetPlayer != null)
            {
                _escorts[i].targetPlayer = targetPlayer;
                _escorts[i].SwitchToBehaviourStateOnLocalClient(3);
            }
            RemoveEscort(i);
        }
        _currentlyRetiringAllEscorts = false;
    }

    private void UpdateAgentFormation()
    {
        _isNarrowPath = CheckForNarrowPassages();
        for (int i = 0; i < _escorts.Count; i++)
        {
            // Check if escort is dead and if so remove them from the escort list
            if (_escorts[i].isEnemyDead || _escorts[i] == null)
            {
                _escorts.RemoveAt(i);
                continue;
            }
            
            // Check if the escort has finished its spawn animation before giving it orders
            if (!_escorts[i].fullySpawned) continue;
            
            // Check if the bagpipe ghost is moving, if not then dont bother making the escorts do anything
            if (!_agentIsMoving && (transform.position - _escorts[i].transform.position).magnitude < EscortRowSpacing) continue;
            
            NavMeshAgent curEscortNavMeshAgent = _escorts[i].agent;
            if (curEscortNavMeshAgent == null) continue;

            Vector3 targetPosition;
            if (!_isNarrowPath && _escorts.Count > 1) // triangle formation
            {
                float lateralOffset = ((i % 2) * 2 - 1) * EscortHorizontalBaseLength / 2;
                int row = i / 2;
                Vector3 directionOffset = transform.right * lateralOffset - transform.forward * (EscortRowSpacing * row);
                targetPosition = transform.position + directionOffset;
            }
            else // single file line
            {
                int targetIndex = Mathf.Max(0, _escortAgentPathPoints.Count - 1 - i);
                targetPosition = _escortAgentPathPoints[Mathf.Clamp(targetIndex, 0, _escortAgentPathPoints.Count - 1)];
            }
            
            if (_agentIsMoving || (curEscortNavMeshAgent.destination - targetPosition).magnitude > 0.1f)
                curEscortNavMeshAgent.SetDestination(targetPosition);
        }
    }

    private void UpdateEscortAgentPathPoints()
    {
        if (_escorts.Count == 0) return;
        if (_escortAgentPathPoints.Count == 0) _escortAgentPathPoints.Add(transform.position);
        if (Vector3.Distance(_escortAgentPathPoints[^1], transform.position) > 1f)
            _escortAgentPathPoints.Add(transform.position);

        if (_escortAgentPathPoints.Count > 50) _escortAgentPathPoints.RemoveAt(0);
    }

    private bool CheckForNarrowPassages()
    {
        float sideLength = EscortHorizontalBaseLength * Mathf.Sqrt(2);
        bool leftBlocked = Physics.Raycast(transform.position, -transform.right, out RaycastHit _, sideLength / 2);
        bool rightBlocked = Physics.Raycast(transform.position, transform.right, out RaycastHit _, sideLength / 2);
        return leftBlocked || rightBlocked;
    }

    private void AddEscort(EnforcerGhostAIServer escort)
    {
        if (_escorts == null || _escorts.Contains(escort)) return;
        escort.Escortee = this;
        _escorts.Add(escort);
    }

    public void RemoveEscort(int indexOfEscort)
    {
        if (_escorts[indexOfEscort] == null) return;
        _escorts[indexOfEscort].Escortee = null;
        _escorts.RemoveAt(indexOfEscort);
    }

    private IEnumerator SpawnEscorts(Action callback = null)
    {
        yield return new WaitForSeconds(0.5f); // wait a little bit to allow the ghost to move a bit away from the vent where it spawned at (away from the wall)
        
        _escorts = new List<EnforcerGhostAIServer>(numberOfEscorts);
        for (int i = 0; i < numberOfEscorts; i++)
        {
            GameObject escort = Instantiate(HarpGhostPlugin.EnforcerGhostEnemyType.enemyPrefab, transform.position, Quaternion.identity);
            escort.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);

            EnforcerGhostAIServer escortScript = escort.GetComponent<EnforcerGhostAIServer>();
            escortScript.agent.avoidancePriority = Mathf.Min(i + 1, 99); // Give the escort agents a different, descending priority
            AddEscort(escortScript);
            yield return new WaitForSeconds(1f);
        }

        // This callback can technically be used for anything, but its only use is going to be for playing music after the escorts are spawned
        if (callback == null) yield break;
        yield return new WaitForSeconds(0.5f);
        callback.Invoke();
    }
    
    private void HandleGrabBagpipes(string recievedGhostId)
    {
        if (_ghostId != recievedGhostId) return;
        if (_heldBagpipes != null) return;
        if (!_instrumentObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldBagpipes = networkObject.gameObject.GetComponent<InstrumentBehaviour>();
    }

    public override void SetEnemyStunned(
        bool setToStunned,
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer) return;
        if (currentBehaviourStateIndex is (int)States.TeleportingOutOfMap or (int)States.Dead) return;
        
        netcodeController.PlayCreatureVoiceClientRpc(_ghostId, (int)HarpGhostAudioManager.AudioClipTypes.Stun, audioManager.stunSfx.Length);
        // netcodeController.DropBagpipesClientRpc(_ghostId, transform.position);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsStunned, true);
        netcodeController.DoAnimationClientRpc(_ghostId, BagpipesGhostAnimationController.Stunned);
        _inStunAnimation = true;

        if (currentBehaviourStateIndex == (int)States.PlayingMusicWhileEscorted)
        {
            SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor);
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX);
        if (!IsServer) return;
        if (isEnemyDead) return;
        if (currentBehaviourStateIndex is (int)States.TeleportingOutOfMap or (int)States.Dead) return;
        if (playerWhoHit == null) return;

        enemyHP -= force;
        if (enemyHP > 0)
        {
            netcodeController.PlayCreatureVoiceClientRpc(_ghostId, (int)HarpGhostAudioManager.AudioClipTypes.Damage, audioManager.damageSfx.Length);
            if (currentBehaviourStateIndex != (int)States.RunningToEscapeDoor) SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor);
            return;
        }
        
        // Ghost is dead
        netcodeController.EnterDeathStateClientRpc(_ghostId);
        KillEnemyClientRpc(false);
        SwitchBehaviourStateLocally((int)States.Dead);
    }
    
    private void SwitchBehaviourStateLocally(int state)
    {
        if (!IsServer) return;
        switch (state)
        {
            case (int)States.PlayingMusicWhileEscorted:
            {
                LogDebug($"Switched to behaviour state {(int)States.PlayingMusicWhileEscorted}!");
                
                agentMaxSpeed = 0.5f;
                agentMaxAcceleration = 50f;
                movingTowardsTargetPlayer = false;
                openDoorSpeedMultiplier = 6;

                break;
            }

            case (int)States.RunningToEscapeDoor:
            {
                LogDebug($"Switched to behaviour state {(int)States.RunningToEscapeDoor}!");
                
                agentMaxSpeed = agentMaxSpeedInEscapeMode;
                agentMaxAcceleration = agentMaxAccelerationInEscapeMode;
                openDoorSpeedMultiplier = openDoorSpeedMultiplierInEscapeMode;
                movingTowardsTargetPlayer = false;

                RetireAllEscorts();
                netcodeController.StopBagpipesMusicClientRpc(_ghostId);
                break;
            }

            case (int)States.RunningToEdgeOfOutsideMap:
            {
                LogDebug($"Switched to behaviour state {(int)States.RunningToEdgeOfOutsideMap}!");

                agentMaxSpeed = agentMaxSpeedInEscapeMode;
                agentMaxAcceleration = agentMaxAccelerationInEscapeMode;
                openDoorSpeedMultiplier = openDoorSpeedMultiplierInEscapeMode;
                movingTowardsTargetPlayer = false;
                
                RetireAllEscorts();
                allAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
                SetDestinationToPosition(ChooseFarthestNodeFromPosition(StartOfRound.Instance.middleOfShipNode.position, true).position);
                netcodeController.StopBagpipesMusicClientRpc(_ghostId);
                
                break;
            }

            case (int)States.TeleportingOutOfMap:
            {
                LogDebug($"Switched to behaviour state {(int)States.TeleportingOutOfMap}!");

                agentMaxSpeed = 1f;
                agentMaxAcceleration = 0f;
                movingTowardsTargetPlayer = false;
                
                RetireAllEscorts();
                netcodeController.StopBagpipesMusicClientRpc(_ghostId);
                Destroy(GetComponentInChildren<ScanNodeProperties>().gameObject);

                break;
            }

            case (int)States.Dead:
            {
                LogDebug($"Switched to behaviour state {(int)States.Dead}!");
                
                agentMaxSpeed = 0f;
                agentMaxAcceleration = 0f;
                movingTowardsTargetPlayer = false;
                agent.speed = 0;
                agent.enabled = false;
                isEnemyDead = true;
                
                netcodeController.DropBagpipesClientRpc(_ghostId, transform.position);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsDead, true);
                RetireAllEscorts();
                break;
            }
        }
        
        if (currentBehaviourStateIndex == state) return;
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
    }

    private void DrawDebugCircleAtTargetNode()
    {
        if (!_lineRenderer || !roamMap.inProgress || roamMap.currentTargetNode == null) return;

        Vector3 centerPosition = roamMap.currentTargetNode.transform.position;
        float angle = 20f;
        const float circleRadius = 0.5f;
        
        for (int i = 0; i <= 50; i++)
        {
            float x = centerPosition.x + Mathf.Sin(Mathf.Deg2Rad * angle) * circleRadius;
            float z = centerPosition.z + Mathf.Cos(Mathf.Deg2Rad * angle) * circleRadius;
            float y = centerPosition.y;
            
            _lineRenderer.SetPosition(i, new Vector3(x, y, z));
            angle += 360f / 50;
        }
    }

    private void RunAnimation()
    {
        if (!IsServer) return;
        
        bool isRunning = _agentCurrentSpeed >= 3f;
        if (animationController.GetBool(BagpipesGhostAnimationController.IsRunning) != isRunning && !_inStunAnimation)
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsRunning, isRunning);
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

    public void EscorteeBreakoff()
    {
        EscorteeBreakoff(null);
    }

    public void EscorteeBreakoff(PlayerControllerB targetPlayer)
    {
        RetireAllEscorts(targetPlayer);
        SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor);
    }
    
    private void LogDebug(string logMessage)
    {
        #if DEBUG
        _mls?.LogInfo(logMessage);
        #endif
    }
    
    public Vector3 TransformPosition => transform.position;
    public RoundManager RoundManagerInstance => RoundManager.Instance;
}