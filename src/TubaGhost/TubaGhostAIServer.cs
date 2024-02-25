using System;
using BepInEx.Logging;
using LethalCompanyHarpGhost.HarpGhost;
using UnityEngine;
using UnityEngine.AI;

namespace LethalCompanyHarpGhost.TubaGhost;

public class TubaGhostAIServer : EnemyAI
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
    
    private RoundManager _roundManager;
    
    [Header("Controllers and Managers")]
    [Space(5f)]
#pragma warning disable 0649
    [SerializeField] private HarpGhostAudioManager audioManager;
    [SerializeField] private TubaGhostNetcodeController netcodeController;
    [SerializeField] private HarpGhostAnimationController animationController;
#pragma warning restore 0649

    private enum States
    {
        GoingTowardsPlayer,
        FollowingPlayerAndPlayingMusic,
        RunningFromPlayer,
        Dead
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;

        _ghostId = Guid.NewGuid().ToString();
        netcodeController.SyncGhostIdentifierClientRpc(_ghostId);
        
        _mls = BepInEx.Logging.Logger.CreateLogSource($"{HarpGhostPlugin.ModGuid} | Tuba Ghost AI {_ghostId} | Server");
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) _mls.LogError("NavMeshAgent component not found on " + name);
        agent.enabled = true;

        audioManager = GetComponent<HarpGhostAudioManager>();
        if (audioManager == null) _mls.LogError("Audio Manger is null");

        netcodeController = GetComponent<TubaGhostNetcodeController>();
        if (netcodeController == null) _mls.LogError("Netcode Controller is null");

        animationController = GetComponent<HarpGhostAnimationController>();
        if (animationController == null) _mls.LogError("Animation Controller is null");
        
        _roundManager = FindObjectOfType<RoundManager>();
        
        UnityEngine.Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsDead, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsStunned, false);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_ghostId, HarpGhostAnimationController.IsRunning, false);
        
        //InitializeConfigValues();
        _mls.LogInfo("Tuba Ghost Spawned");
    }
}