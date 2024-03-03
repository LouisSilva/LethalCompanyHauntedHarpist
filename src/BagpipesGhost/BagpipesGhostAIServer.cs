using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using LethalCompanyHarpGhost.EnforcerGhost;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace LethalCompanyHarpGhost.BagpipesGhost;

public class BagpipesGhostAIServer : EnemyAI
{
    private ManualLogSource _mls;
    private string _ghostId;

    private List<EnforcerGhostAIServer> _escorts = [];

    [Header("AI and Pathfinding")]
    [Space(5f)]
    public AISearchRoutine roamMap;
    private float _agentCurrentSpeed;
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
        
        _ghostId = Guid.NewGuid().ToString();
        netcodeController.SyncGhostIdentifierClientRpc(_ghostId);
        
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Bagpipes Ghost AI {_ghostId} | Server");
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) _mls.LogError("NavMeshAgent component not found on " + name);
        agent.enabled = true;
        
        audioManager = GetComponent<BagpipesGhostAudioManager>();
        if (audioManager == null) _mls.LogError("Audio Manger is null");

        netcodeController = GetComponent<BagpipesGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("Netcode Controller is null");

        animationController = GetComponent<BagpipesGhostAnimationController>();
        if (animationController == null) _mls.LogError("Animation Controller is null");
        
        _roundManager = FindObjectOfType<RoundManager>();
        
        UnityEngine.Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        // netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsDead, false);
        // netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsStunned, false);
        // netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, BagpipesGhostAnimationController.IsRunning, false);
        
        // netcodeController.SpawnBagpipesServerRpc(_ghostId);
        // netcodeController.GrabBagpipesClientRpc(_ghostId);
        // StartCoroutine(DelayedBagpipesMusicActivate());
        
        if (HarpGhostPlugin.EnforcerGhostEnemyType.enemyPrefab == null) _mls.LogError("Enforcer ghost prefab is null");
        else StartCoroutine(SpawnEscorts());
        
        _mls.LogInfo("Bagpipes Ghost Spawned");
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
                _isNarrowPath = CheckForNarrowPassages();
                UpdateEscortAgentPathPoints();
                UpdateAgentFormation();

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
                if (!roamMap.inProgress) StartSearch(transform.position, roamMap);
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
                openDoorSpeedMultiplier = Mathf.Clamp(6f, 0, HarpGhostConfig.Instance.GhostMaxDoorSpeedMultiplier.Value);

                break;
            }

            case (int)States.RunningAway:
            {
                LogDebug($"Switched to behaviour state {(int)States.RunningAway}!");
                
                agentMaxSpeed = 10f;
                agentMaxAcceleration = 100f;
                openDoorSpeedMultiplier = Mathf.Clamp(2f, 0, HarpGhostConfig.Instance.GhostMaxDoorSpeedMultiplier.Value);

                RetireAllEscorts();
                break;
            }
        }
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
        float currentBaseLength = _isNarrowPath ? 0 : EscortHorizontalBaseLength;
        for (int i = 0; i < _escorts.Count; i++)
        {
            // Check if escort is dead and if so remove them from the escort list
            if (_escorts[i].isEnemyDead)
            {
                _escorts.RemoveAt(i);
                continue;
            }
            
            int pathPointIndex = Mathf.Max(0, _escortAgentPathPoints.Count - 1 - i * 2);
            Vector3 targetPosition =
                _escortAgentPathPoints[Mathf.Clamp(pathPointIndex, 0, _escortAgentPathPoints.Count - 1)];

            float lateralOffset = ((i % 2) * 2 - 1) * currentBaseLength / 2;
            int row = i / 2;
            Vector3 formationOffset = transform.right * lateralOffset - transform.forward * (EscortRowSpacing * row);
            
            NavMeshAgent curEscortNavMeshAgent = _escorts[i].agent;
            if (curEscortNavMeshAgent != null)
            {
                curEscortNavMeshAgent.speed = CalculateEscortSpeed(
                    curEscortNavMeshAgent.transform.position,
                    targetPosition + formationOffset
                    );
                curEscortNavMeshAgent.SetDestination(targetPosition + formationOffset);
            } else LogDebug("Current escort nav mesh agent is null");
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
        bool leftBlocked = Physics.Raycast(transform.position, -transform.right, out RaycastHit _, EscortHorizontalBaseLength / 2);
        bool rightBlocked = Physics.Raycast(transform.position, transform.right, out RaycastHit _, EscortHorizontalBaseLength / 2);
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
    
    private IEnumerator DelayedBagpipesMusicActivate() // Needed to mitigate race conditions
    {
        yield return new WaitForSeconds(0.5f);
        netcodeController.PlayBagpipesMusicClientRpc(_ghostId);
    }

    private IEnumerator SpawnEscorts()
    {
        yield return new WaitForSeconds(0.5f); // wait a little bit to allow the ghost to move a bit away from the vent where it spawned at (away from the wall)
        
        _escorts = new List<EnforcerGhostAIServer>(numberOfEscorts);
        for (int i = 0; i < numberOfEscorts; i++)
        {
            GameObject escort = Instantiate(HarpGhostPlugin.EnforcerGhostEnemyType.enemyPrefab, transform.position, Quaternion.identity);
            AddEscort(escort.GetComponentInChildren<EnforcerGhostAIServer>());
            escort.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
            yield return new WaitForSeconds(1f);
        }
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

// A registry to keep track of all the escorts mapped to their associated bagpipe ghost
// public static class EscortRegistry
// {
//     private static Dictionary<EnforcerGhostAIServer, BagpipesGhostAIServer> _escortToGhostDict = [];
//
//     public static void AddEscort(EnforcerGhostAIServer escort, BagpipesGhostAIServer bagpipeGhost)
//     {
//         if (escort != null && bagpipeGhost != null) _escortToGhostDict[escort] = bagpipeGhost;
//     }
//
//     public static void RemoveEscort(EnforcerGhostAIServer escort)
//     {
//         _escortToGhostDict.Remove(escort);
//     }
//
//     public static BagpipesGhostAIServer GetGhostForEscort(EnforcerGhostAIServer escort)
//     {
//         return _escortToGhostDict.TryGetValue(escort, out BagpipesGhostAIServer bagpipeGhost) ? bagpipeGhost : null;
//     }
//
//     public static void RemoveAllEscorts()
//     {
//         foreach (KeyValuePair<EnforcerGhostAIServer, BagpipesGhostAIServer> t in _escortToGhostDict)
//         {
//             RemoveEscort(t.Key);
//         }
//     }
// }