using System;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.AI;

namespace LethalCompanyHarpGhost.EnforcerGhost;

public class EnforcerGhostAIServer : EnemyAI
{
    private ManualLogSource _mls;
    private string _ghostId;
    
    [Header("AI and Pathfinding")]
    [Space(5f)]
    [SerializeField] private float agentMaxAcceleration = 50f;
    [SerializeField] private float agentMaxSpeed = 0.8f;
    private float _agentCurrentSpeed = 0f;
    private float _timeSinceHittingLocalPlayer = 0f;

    private bool _inStunAnimation = false;
    
    private Vector3 _agentLastPosition = default;

    // private PlayerControllerB _targetPlayer;
    
    private RoundManager _roundManager;
    
    [Header("Controllers and Managers")]
    [Space(5f)]
    #pragma warning disable 0649
    [SerializeField] private EnforcerGhostAudioManager audioManager;
    [SerializeField] private EnforcerGhostNetcodeController netcodeController;
    [SerializeField] private EnforcerGhostAnimationController animationController;
    #pragma warning restore 0649

    private enum States
    {
        Escorting,
        Rambo,
        Dead
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;

        _ghostId = Guid.NewGuid().ToString();
        netcodeController.SyncGhostIdentifierClientRpc(_ghostId);
        
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Enforcer Ghost AI {_ghostId} | Server");
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) _mls.LogError("NavMeshAgent component not found on " + name);
        agent.enabled = true;

        audioManager = GetComponent<EnforcerGhostAudioManager>();
        if (audioManager == null) _mls.LogError("Audio Manger is null");

        netcodeController = GetComponent<EnforcerGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("Netcode Controller is null");

        animationController = GetComponent<EnforcerGhostAnimationController>();
        if (animationController == null) _mls.LogError("Animation Controller is null");
        
        _roundManager = FindObjectOfType<RoundManager>();
        
        UnityEngine.Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, EnforcerGhostAnimationController.IsDead, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, EnforcerGhostAnimationController.IsStunned, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, EnforcerGhostAnimationController.IsRunning, false);
        
        //InitializeConfigValues();
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
            netcodeController.DoAnimationClientRpc(_ghostId, EnforcerGhostAnimationController.Recover);
            _inStunAnimation = false;
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, EnforcerGhostAnimationController.IsStunned, false);
        }
        
        if (StartOfRound.Instance.allPlayersDead)
        {
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, EnforcerGhostAnimationController.IsRunning, false);
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
            case 0:
                break;
        }
    }

    // Called by the bagpipe ghost to tell the enforcer to switch to behaviour state 1
    public void EnterRambo()
    {
        // Switchbehavourstate 1
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
    
    private bool CheckForPath(Vector3 position)
    {
        position = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 1.75f);
        path1 = new NavMeshPath();
        
        // ReSharper disable once UseIndexFromEndExpression
        return agent.CalculatePath(position, path1) && !(Vector3.Distance(path1.corners[path1.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 2.7f)) > 1.5499999523162842);
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls.LogInfo(msg);
        #endif
    }
}