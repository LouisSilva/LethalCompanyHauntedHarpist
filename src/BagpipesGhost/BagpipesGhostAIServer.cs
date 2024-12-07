using BepInEx.Logging;
using GameNetcodeStuff;
using LethalCompanyHarpGhost.EnforcerGhost;
using LethalCompanyHarpGhost.HarpGhost;
using LethalCompanyHarpGhost.Items;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost.BagpipesGhost;

public class BagpipesGhostAIServer : EnemyAI, IEscortee
{
    private ManualLogSource _mls;
    private string _ghostId;

    private List<EnforcerGhostAIServer> _escorts = [];
    
#pragma warning disable 0649
    [Header("Controllers and Managers")] [Space(5f)]
    [SerializeField] private BagpipesGhostNetcodeController netcodeController;
    [SerializeField] private Animator animator;
#pragma warning restore 0649

    [Header("AI and Pathfinding")] [Space(5f)]
    [SerializeField] private float agentMaxAcceleration = 200f;
    [SerializeField] private float agentMaxSpeed = 0.5f;
    [SerializeField] private float agentMaxAccelerationInEscapeMode = 30f;
    [SerializeField] private float agentMaxSpeedInEscapeMode = 10f;
    [SerializeField] private float openDoorSpeedMultiplierInEscapeMode = 6f;
    
    [SerializeField] private int numberOfEscorts = 3;
    
    private enum States
    {
        PlayingMusicWhileEscorted,
        RunningToEscapeDoor,
        RunningToEdgeOfOutsideMap,
        TeleportingOutOfMap,
        Dead
    }
    
    private Vector3 _agentLastPosition;
    private readonly List<Vector3> _escortAgentPathPoints = [];

    private EntranceTeleport _escapeDoor;

    private readonly NullableObject<InstrumentBehaviour> _heldBagpipes = new();
    
    private NetworkObjectReference _instrumentObjectRef;

    private Coroutine _teleportCoroutine;
    
    private float _agentCurrentSpeed;
    private float _openDoorTimer;
    private float _takeDamageCooldown;
    private const float EscortHorizontalBaseLength = 5f;
    private const float EscortRowSpacing = 3f;
    
    private bool _agentIsMoving;
    private bool _chosenEscapeDoor;
    private bool _isNarrowPath;
    private bool _inStunAnimation;
    private bool _openDoorTimerActivated;
    private bool _currentlyRetiringAllEscorts;
    private bool _agroSfxPlayed;
    private bool _tauntSfxPlayed;

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        _ghostId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Bagpipes Ghost AI {_ghostId} | Server");
        
        netcodeController = GetComponent<BagpipesGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("Netcode Controller is null");
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) _mls.LogError("NavMeshAgent component not found on " + name);
        agent.enabled = true;
        
        netcodeController.SyncGhostIdentifierClientRpc(_ghostId);

        Random.InitState(StartOfRound.Instance.randomMapSeed + _ghostId.GetHashCode() - thisEnemyIndex);
        InitializeConfigValues();
        EnableEnemyMesh(true);
        
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAIClient.IsDead, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAIClient.IsStunned, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAIClient.IsRunning, false);
        
        netcodeController.SpawnBagpipesServerRpc(_ghostId);
        netcodeController.GrabBagpipesClientRpc(_ghostId);

        if (HarpGhostPlugin.EnforcerGhostEnemyType.enemyPrefab == null)
        {
            _mls.LogError("Enforcer ghost prefab is null, making bagpipe ghost run away from players because he is not escorted");
            SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor);
        }
        else StartCoroutine(SpawnEscorts(() => { netcodeController.PlayBagpipesMusicClientRpc(_ghostId); }));
        
        // The ghost is already in this state, but calling the method will choose the ghost's first node to go to
        SwitchBehaviourStateLocally((int)States.PlayingMusicWhileEscorted);
        LogDebug("Bagpipe Ghost Spawned");
    }

    public void OnEnable()
    {
        if (!IsServer) return;
        if (netcodeController == null) return;
        netcodeController.OnGrabBagpipes += HandleGrabBagpipes;
    }

    public void OnDisable()
    {
        if (!IsServer) return;
        if (netcodeController == null) return;
        netcodeController.OnGrabBagpipes -= HandleGrabBagpipes;
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

        _takeDamageCooldown -= Time.deltaTime;
        CalculateAgentSpeed();
        
        if (stunNormalizedTimer <= 0.0 && _inStunAnimation && !isEnemyDead)
        {
            netcodeController.DoAnimationClientRpc(_ghostId, BagpipesGhostAIClient.Recover);
            _inStunAnimation = false;
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAIClient.IsStunned, false);
        }
        
        if (StartOfRound.Instance.allPlayersDead)
        {
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAIClient.IsRunning, false);
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
                // Check if the ghost has reached its destination
                if (Vector3.Distance(transform.position, targetNode.position) <= 5)
                {
                    // Pick next node to go to
                    int maxOffset = Mathf.Max(1, Mathf.FloorToInt(allAINodes.Length * 0.3f));
                    Transform farAwayTransform = ChooseFarthestNodeFromPosition(transform.position, offset: Random.Range(0, maxOffset));
                    targetNode = farAwayTransform;
                    if (!SetDestinationToPosition(farAwayTransform.position, true))
                    {
                        _mls.LogWarning("Bagpipe ghost pathfinding has failed, as a fail-safe the ghost is now running away. This should not happen, contact the mod developer if you see this");
                        SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor);
                        break;
                    }
                }
                
                UpdateEscortAgentPathPoints();
                UpdateAgentFormation();
                
                break;
            }

            case (int)States.RunningToEscapeDoor:
            {
                if (isOutside) SwitchBehaviourStateLocally((int)States.RunningToEdgeOfOutsideMap);
                if (!_chosenEscapeDoor) ChooseEscapeDoor();

                if (Vector3.Distance(transform.position, _escapeDoor.exitPoint.position) <= 3 &&
                    !_openDoorTimerActivated)
                {
                    if (!_tauntSfxPlayed)
                    {
                        _tauntSfxPlayed = true;
                        netcodeController.PlayCreatureVoiceClientRpc(_ghostId, (int)BagpipesGhostAIClient.AudioClipTypes.Taunt, 3, false);
                    }
                    
                    // Check if the ghost has reached the exit door
                    if (Vector3.Distance(transform.position, _escapeDoor.exitPoint.position) <= 1 && !_openDoorTimerActivated)
                    {
                        LogDebug("Reached escape door");
                        moveTowardsDestination = false;
                        _openDoorTimerActivated = true;
                        _openDoorTimer = 0f;
                    }
                }
                
                break;
            }

            case (int)States.RunningToEdgeOfOutsideMap:
            {
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
        
        LogDebug("Going through escape door!");
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
        StartCoroutine(PlaySfxAfterTime(0.3f, (int)BagpipesGhostAIClient.AudioClipTypes.LongLaugh,
            2, false));
        yield return new WaitForSeconds(0.5f);
        netcodeController.DespawnHeldBagpipesClientRpc(_ghostId);
        EnableEnemyMesh(false);
        netcodeController.SetMeshEnabledClientRpc(_ghostId, false);
        
        yield return new WaitForSeconds(5);
        KillEnemyClientRpc(true);
        _teleportCoroutine = null;
        Destroy(this);
    }

    private void ChooseEscapeDoor()
    {
        if (!IsServer) return;
        
        LogDebug("Choosing escape door");
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
    private void RetireAllEscorts(PlayerControllerB targetPlayerToSet = null)
    {
        if (!IsServer) return;
        if (_currentlyRetiringAllEscorts) return;

        _currentlyRetiringAllEscorts = true;
        for (int i = _escorts.Count - 1; i >= 0; i--)
        {
            LogDebug($"Retiring escort {_escorts[i].GhostId}");
            if (targetPlayerToSet != null)
            {
                _escorts[i].targetPlayer = targetPlayerToSet;
                _escorts[i].TargetPosition = targetPlayerToSet.transform.position;
                _escorts[i].SwitchToBehaviourStateOnLocalClient((int)EnforcerGhostAIServer.States.InvestigatingTargetPosition);
            }
            else
            {
                _escorts[i].SwitchToBehaviourStateOnLocalClient((int)EnforcerGhostAIServer.States.SearchingForPlayers);
            }
            
            RemoveEscort(i);
        }
        
        _currentlyRetiringAllEscorts = false;
    }

    private void UpdateAgentFormation()
    {
        _isNarrowPath = CheckForNarrowPassages();
        for (int i = _escorts.Count - 1; i >= 0; i--)
        {
            // Check if escort is dead and if so remove them from the escort list
            if (_escorts[i].isEnemyDead || _escorts[i] == null)
            {
                _escorts.RemoveAt(i);
                continue;
            }

            _escorts[i].escorteePingTimer = 5f;
            
            // Check if the escort has finished its spawn animation before giving it orders
            if (!_escorts[i].FullySpawned) continue;
            
            // Check if the bagpipe ghost is moving, if not then don't bother making the escorts do anything
            if (!_agentIsMoving && (transform.position - _escorts[i].transform.position).magnitude < EscortRowSpacing) continue;

            Vector3 targetPosition;
            if (!_isNarrowPath && _escorts.Count > 1) // triangle formation
            {
                float lateralOffset = (i % 2 * 2 - 1) * EscortHorizontalBaseLength / 2;
                int row = i / 2;
                Vector3 directionOffset = transform.right * lateralOffset - transform.forward * (EscortRowSpacing * row);
                targetPosition = transform.position + directionOffset;
            }
            else // single file line
            {
                const float closenessToEscorteeFactor = 0.5f;
                const float increasedGapBetweenEscortersFactor = 2f;
                
                int targetIndex = Mathf.Max(0, Mathf.RoundToInt(_escortAgentPathPoints.Count - 1 - i * increasedGapBetweenEscortersFactor));
                targetPosition = _escortAgentPathPoints[Mathf.Clamp(targetIndex, 0, _escortAgentPathPoints.Count - 1)];
                targetPosition = Vector3.Lerp(targetPosition, transform.position, closenessToEscorteeFactor);
            }

            if (_agentIsMoving || (_escorts[i].transform.position - targetPosition).magnitude > 1f)
            {
                _escorts[i].SetDestinationToPosition(targetPosition);
            }
        }
    }

    private void UpdateEscortAgentPathPoints()
    {
        if (_escorts.Count == 0) return;
        if (_escortAgentPathPoints.Count == 0) _escortAgentPathPoints.Add(transform.position);
        if (Vector3.Distance(_escortAgentPathPoints[^1], transform.position) > 0.25f)
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

    internal void RemoveEscort(int indexOfEscort)
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
            RoundManager.Instance.currentEnemyPower += HarpGhostPlugin.EnforcerGhostEnemyType.PowerLevel;
            ++RoundManager.Instance.currentLevel.Enemies.FirstOrDefault(enemy => enemy.enemyType.name == "EnforcerGhost")!.enemyType.numberSpawned;

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
    
    private void HandleGrabBagpipes(string receivedGhostId)
    {
        if (_ghostId != receivedGhostId) return;
        if (_heldBagpipes.IsNotNull) return;
        if (!_instrumentObjectRef.TryGet(out NetworkObject networkObject)) return;
        _heldBagpipes.Value = networkObject.gameObject.GetComponent<InstrumentBehaviour>();
    }

    public override void SetEnemyStunned(
        bool setToStunned,
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer) return;
        if (isEnemyDead || currentBehaviourStateIndex is (int)States.TeleportingOutOfMap or (int)States.Dead) return;
        
        netcodeController.PlayCreatureVoiceClientRpc(_ghostId, (int)HarpGhostAudioManager.AudioClipTypes.Stun, 2);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsStunned, true);
        netcodeController.DoAnimationClientRpc(_ghostId, BagpipesGhostAIClient.IsStunned);
        _inStunAnimation = true;

        if (currentBehaviourStateIndex != (int)States.PlayingMusicWhileEscorted) return;
        if (!_agroSfxPlayed)
        {
            _agroSfxPlayed = true;
            StartCoroutine(PlaySfxAfterTime(setToStunTime + 1f, (int)BagpipesGhostAIClient.AudioClipTypes.Shocked, 3, false));
        }
        SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor);
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitId = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitId);
        if (!IsServer || isEnemyDead || _takeDamageCooldown > 0 || currentBehaviourStateIndex is (int)States.TeleportingOutOfMap or (int)States.Dead) return;
        if (playerWhoHit == null) return;
        
        enemyHP -= force;
        _takeDamageCooldown = 0.03f;
        
        if (enemyHP > 0)
        {
            netcodeController.PlayCreatureVoiceClientRpc(_ghostId, (int)BagpipesGhostAIClient.AudioClipTypes.Damage, 2);
            if (!_agroSfxPlayed)
            {
                _agroSfxPlayed = true;
                StartCoroutine(PlaySfxAfterTime(1f, (int)BagpipesGhostAIClient.AudioClipTypes.Shocked, 3, false));
            }
            
            if (currentBehaviourStateIndex == (int)States.PlayingMusicWhileEscorted) SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor, playerWhoHit);
            return;
        }
        
        KillEnemyClientRpc(false);
    }

    public override void KillEnemy(bool destroy = false)
    {
        base.KillEnemy(destroy);
        if (!IsServer) return;
        SwitchBehaviourStateLocally((int)States.Dead);
    }

    private IEnumerator PlaySfxAfterTime(float time, int typeIndex, int clipArrayLength, bool interrupt = true)
    {
        yield return new WaitForSeconds(time);
        netcodeController.PlayCreatureVoiceClientRpc(_ghostId, typeIndex, clipArrayLength, interrupt);
    }
    
    private void SwitchBehaviourStateLocally(int state, PlayerControllerB targetPlayerToSet = null)
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
                
                // Pick first node to go to
                int maxOffset = Mathf.Max(1, Mathf.FloorToInt(allAINodes.Length * 0.1f));
                Transform farAwayTransform = ChooseFarthestNodeFromPosition(transform.position, offset: Random.Range(0, maxOffset));
                targetNode = farAwayTransform;
                if (!SetDestinationToPosition(farAwayTransform.position, true))
                {
                    _mls.LogWarning("Bagpipe ghost pathfinding has failed, as a fail-safe the ghost is now running away. This should not happen, contact the mod developer if you see this");
                }

                break;
            }

            case (int)States.RunningToEscapeDoor:
            {
                LogDebug($"Switched to behaviour state {(int)States.RunningToEscapeDoor}!");
                
                agentMaxSpeed = agentMaxSpeedInEscapeMode;
                agentMaxAcceleration = agentMaxAccelerationInEscapeMode;
                openDoorSpeedMultiplier = openDoorSpeedMultiplierInEscapeMode;
                movingTowardsTargetPlayer = false;

                RetireAllEscorts(targetPlayerToSet);
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
                
                netcodeController.EnterDeathStateClientRpc(_ghostId);
                RetireAllEscorts();
                break;
            }
        }
        
        if (currentBehaviourStateIndex == state) return;
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
    }

    private void RunAnimation()
    {
        if (!IsServer) return;
        
        bool isRunning = _agentCurrentSpeed >= 3f;
        if (animator.GetBool(BagpipesGhostAIClient.IsRunning) != isRunning && !_inStunAnimation)
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAIClient.IsRunning, isRunning);
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

    public void EscorteeBreakoff(PlayerControllerB targetPlayerReceived = null)
    {
        RetireAllEscorts(targetPlayerReceived);
        if (!_agroSfxPlayed)
        {
            _agroSfxPlayed = true;
            StartCoroutine(PlaySfxAfterTime(0.1f, (int)BagpipesGhostAIClient.AudioClipTypes.Shocked, 3, false));
        }
        SwitchBehaviourStateLocally((int)States.RunningToEscapeDoor);
    }
    
    private void LogDebug(string logMessage)
    {
        #if DEBUG
        _mls?.LogInfo(logMessage);
        #endif
    }
}