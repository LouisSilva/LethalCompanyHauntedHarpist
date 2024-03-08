using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using GameNetcodeStuff;
using LethalCompanyHarpGhost.EnforcerGhost;
using LethalCompanyHarpGhost.HarpGhost;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace LethalCompanyHarpGhost.BagpipesGhost;

public class BagpipesGhostAIServer : EnemyAI, IEscortee
{
    private ManualLogSource _mls;
    private string _ghostId;

    private List<EnforcerGhostAIServer> _escorts = [];

    [Header("AI and Pathfinding")]
    [Space(5f)]
    public AISearchRoutine roamMap;
    
    private bool _agentIsMoving = false;
    private bool _isIdleTimerRunning = false;
    
    private float _agentCurrentSpeed;
    private float _idleTimer;
    private const float EscortHorizontalBaseLength = 5f;
    private const float EscortRowSpacing = 3f;
    [SerializeField] private float agentMaxAcceleration = 200f;
    [SerializeField] private float agentMaxSpeed = 0.5f;
    
    private bool _isNarrowPath = false;
    private bool _inStunAnimation = false;
    
    [SerializeField] private int numberOfEscorts = 3;

    private Vector3 _agentLastPosition;
    private readonly List<Vector3> _escortAgentPathPoints = [];

    private RoundManager _roundManager;
    
    [Header("Controllers and Managers")]
    [Space(5f)]
    #pragma warning disable 0649
    [SerializeField] private BagpipesGhostAudioManager audioManager;
    [SerializeField] private BagpipesGhostNetcodeController netcodeController;
    [SerializeField] private BagpipesGhostAnimationController animationController;
    private LineRenderer _lineRenderer;
    #pragma warning restore 0649

    private enum States
    {
        PlayingMusicWhileEscorted,
        RunningAway,
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

        // GameObject debugCircle = new("Circle");
        // debugCircle.transform.SetParent(transform, false);
        // _lineRenderer = debugCircle.AddComponent<LineRenderer>();
        // _lineRenderer.widthMultiplier = 0.1f;
        // _lineRenderer.positionCount = 51;
        // _lineRenderer.useWorldSpace = true;
        // _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsDead, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsStunned, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsRunning, false);
        
        netcodeController.SpawnBagpipesServerRpc(_ghostId);
        netcodeController.GrabBagpipesClientRpc(_ghostId);

        if (HarpGhostPlugin.EnforcerGhostEnemyType.enemyPrefab == null)
        {
            _mls.LogError("Enforcer ghost prefab is null, making bagpipe ghost run away from players because he is not escorted");
            SwitchBehaviourStateLocally((int)States.RunningAway);
        }
        else StartCoroutine(SpawnEscorts(() => { netcodeController.PlayBagpipesMusicClientRpc(_ghostId); }));
        
        _mls.LogInfo("Bagpipes Ghost Spawned");
    }

    private void InitializeConfigValues()
    {
        if (!IsServer) return;
        netcodeController.InitializeConfigValuesClientRpc(_ghostId);
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (currentBehaviourStateIndex is (int)States.PlayingMusicWhileEscorted or (int)States.Dead) return;
        
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;
    }

    public override void Update()
    {
        base.Update();
        if (!IsServer) return;

        if (_isIdleTimerRunning) _idleTimer += Time.deltaTime;
        
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

            case (int)States.RunningAway:
            {
                bool isRunning = _agentCurrentSpeed >= 3f;
                if (animationController.GetBool(BagpipesGhostAnimationController.IsRunning) != isRunning && !_inStunAnimation)
                    netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsRunning, isRunning);
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
                _agentIsMoving = (transform.position - _agentLastPosition).magnitude > 0.05f;
                if (!roamMap.inProgress) StartSearch(transform.position, roamMap);

                // Starts the idle timer if the ghost isnt moving
                if (!_agentIsMoving)
                {
                    switch (_isIdleTimerRunning)
                    {
                        case true when _idleTimer > 5f:
                            LogDebug("Restarting search because ghost not moving");
                            StopSearch(roamMap);
                            StartSearch(transform.position, roamMap);
                            _isIdleTimerRunning = false;
                            _idleTimer = 0f;
                            break;
                        
                        case false:
                            _isIdleTimerRunning = true;
                            _idleTimer = 0f;
                            break;
                    }
                }
                
                _isNarrowPath = CheckForNarrowPassages();
                UpdateEscortAgentPathPoints();
                UpdateAgentFormation();
                
                DrawDebugCircleAtTargetNode();
                
                break;
            }

            case (int)States.RunningAway:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                break;
            }
        }
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
                _idleTimer = 0;
                _isIdleTimerRunning = false;

                break;
            }

            case (int)States.RunningAway:
            {
                LogDebug($"Switched to behaviour state {(int)States.RunningAway}!");
                
                agentMaxSpeed = 10f;
                agentMaxAcceleration = 100f;
                openDoorSpeedMultiplier = 6;
                movingTowardsTargetPlayer = false;
                _idleTimer = 0;
                _isIdleTimerRunning = false;

                RetireAllEscorts();
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
                _idleTimer = 0f;
                _isIdleTimerRunning = false;
                
                netcodeController.DropBagpipesClientRpc(_ghostId, transform.position);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsDead, true);
                RetireAllEscorts();
                break;
            }
        }
    }

    public void EscorteeBreakoff()
    {
        RetireAllEscorts();
    }

    // Makes the escorts enter rambo state and removes them from the escort list
    private void RetireAllEscorts()
    {
        foreach (EnforcerGhostAIServer escort in _escorts)
        {
            escort.EnterRambo();
            RemoveEscort(escort);
        }
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

    private float CalculateEscortSpeed(Vector3 currentPosition, Vector3 targetPosition)
    {
        Vector3 directionToTarget = (targetPosition - currentPosition).normalized;
        float angleDifference = Vector3.Angle(transform.forward, directionToTarget);
        const float turnSpeedAdjustment = 0.5f;
        return Mathf.Max(1, turnSpeedAdjustment * (180 - angleDifference) / 180);
    }

    private void AddEscort(EnforcerGhostAIServer escort)
    {
        if (_escorts == null || _escorts.Contains(escort)) return;
        _escorts.Add(escort);
    }

    public void RemoveEscort(EnforcerGhostAIServer escort)
    {
        if (escort == null || !_escorts.Contains(escort)) return;
        _escorts.Remove(escort);
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

    public override void SetEnemyStunned(
        bool setToStunned,
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer) return;
        
        netcodeController.PlayCreatureVoiceClientRpc(_ghostId, (int)HarpGhostAudioManager.AudioClipTypes.Stun, audioManager.stunSfx.Length);
        netcodeController.DropBagpipesClientRpc(_ghostId, transform.position);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsStunned, true);
        netcodeController.DoAnimationClientRpc(_ghostId, BagpipesGhostAnimationController.Stunned);
        _inStunAnimation = true;

        if (currentBehaviourStateIndex == (int)States.PlayingMusicWhileEscorted)
        {
            SwitchBehaviourStateLocally((int)States.RunningAway);
            RetireAllEscorts();
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
            netcodeController.PlayCreatureVoiceClientRpc(_ghostId, (int)HarpGhostAudioManager.AudioClipTypes.Damage, audioManager.damageSfx.Length);
            if (currentBehaviourStateIndex != (int)States.RunningAway) SwitchBehaviourStateLocally((int)States.RunningAway);
            return;
        }
        
        // Ghost is dead
        netcodeController.EnterDeathStateClientRpc(_ghostId);
        KillEnemyClientRpc(false);
        SwitchBehaviourStateLocally((int)States.Dead);
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
    
    private void LogDebug(string logMessage)
    {
        #if DEBUG
        _mls.LogInfo(logMessage);
        #endif
    }
    
    public Vector3 TransformPosition => transform.position;
    public RoundManager RoundManagerInstance => RoundManager.Instance;
    public InstrumentBehaviour HeldBagpipe { get; set; }
}