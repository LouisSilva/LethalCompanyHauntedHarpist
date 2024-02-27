using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using LethalCompanyHarpGhost.HarpGhost;
using UnityEngine;
using UnityEngine.AI;
using PlayerControllerB = GameNetcodeStuff.PlayerControllerB;

namespace LethalCompanyHarpGhost.SitarGhost;

public class SitarGhostAIServer : EnemyAI
{
    private ManualLogSource _mls;
    private string _ghostId;
    
    [Header("AI and Pathfinding")]
    [Space(5f)]
    [SerializeField] private float agentMaxAcceleration = 200f;
    [SerializeField] private float agentMaxSpeed = 20f;
    private float _agentCurrentSpeed = 0f;
    private float _timeSinceHittingLocalPlayer = 0f;

    private bool _inStunAnimation = false;
    
    private Vector3 _agentLastPosition = default;

    // private PlayerControllerB _targetPlayer;
    
    private RoundManager _roundManager;
    
    [Header("Controllers and Managers")]
    [Space(5f)]
#pragma warning disable 0649
    [SerializeField] private HarpGhostAudioManager audioManager;
    [SerializeField] private SitarGhostNetcodeController netcodeController;
    [SerializeField] private HarpGhostAnimationController animationController;
#pragma warning restore 0649

    private enum States
    {
        PlayingMusic,
        Dead
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;

        _ghostId = Guid.NewGuid().ToString();
        netcodeController.SyncGhostIdentifierClientRpc(_ghostId);
        
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Sitar Ghost AI {_ghostId} | Server");
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) _mls.LogError("NavMeshAgent component not found on " + name);
        agent.enabled = true;

        audioManager = GetComponent<HarpGhostAudioManager>();
        if (audioManager == null) _mls.LogError("Audio Manger is null");

        netcodeController = GetComponent<SitarGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("Netcode Controller is null");

        animationController = GetComponent<HarpGhostAnimationController>();
        if (animationController == null) _mls.LogError("Animation Controller is null");
        
        _roundManager = FindObjectOfType<RoundManager>();
        
        UnityEngine.Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsDead, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsStunned, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsRunning, false);
        
        //InitializeConfigValues();
        _mls.LogInfo("Sitar Ghost Spawned");
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
            LogDebug("Doing stun recover animation");
            netcodeController.DoAnimationClientRpc(_ghostId, HarpGhostAnimationController.Recover);
            _inStunAnimation = false;
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsStunned, false);
        }
        
        if (StartOfRound.Instance.allPlayersDead)
        {
            netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsRunning, false);
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
            case (int)States.PlayingMusic:
            {
                break;
            }
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