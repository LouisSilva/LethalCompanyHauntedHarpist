using BepInEx.Logging;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.AI;

namespace LethalCompanyHarpGhost;

public class HarpGhostAI : EnemyAI
{
    private ManualLogSource mls;

    public AISearchRoutine roamMap;

    public Transform turnCompass;
    
    private float timeSinceHittingLocalPlayer;

    private System.Random enemyRandom;

    public override void Start()
    {
        base.Start();
        mls = BepInEx.Logging.Logger.CreateLogSource("Harp Ghost");
        mls.LogInfo("Harp Ghost Spawned");
        
        timeSinceHittingLocalPlayer = 0;
        enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
        
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) {
            mls.LogError("NavMeshAgent component not found on " + name);
        }
    }
    
    public override void Update()
    {
        base.Update();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;
        
        CalculateAgentSpeed();
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;

        switch (currentBehaviourStateIndex)
        {
            case 0:
                if (!IsServer)
                {
                    ChangeOwnershipOfEnemy(StartOfRound.Instance.allPlayerScripts[0].actualClientId);
                }

                else if (!roamMap.inProgress)
                {
                    StartSearch(transform.position, roamMap);
                }
                
                break;
        }
    }

    private void LogDebug(string logMessage)
    {
        if (DebugEnemy || debugEnemyAI)
        {
            mls.LogDebug(logMessage);
        }
    }

    private void CalculateAgentSpeed()
    {
        if (stunNormalizedTimer >= 0)
        {
            agent.speed = 0;
            agent.acceleration = 200;
            return;
        }

        if (currentBehaviourStateIndex == 0)
        {
            MoveWithAcceleration();
        }
    }

    private void MoveWithAcceleration() {
        
        // If acceleration is not zero, gradually adjust the agent's speed towards the target speed.
        // Time.deltaTime gives the time between the current and previous frame, which ensures
        // smooth transition in speed over time. Dividing by moveAcceleration affects the rate of this change.
        float speedAdjustment = Time.deltaTime / 2f;
        float newSpeed = Mathf.Lerp(agent.speed, 6f, speedAdjustment);
        agent.speed = newSpeed;
        
        // Gradually adjust the agent's acceleration towards a fixed value of 200.
        // This will control how quickly the agent can change its speed.
        float accelerationAdjustment = Time.deltaTime;
        float newAcceleration = Mathf.Lerp(agent.acceleration, 200, accelerationAdjustment);
        agent.acceleration = newAcceleration;
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        if (timeSinceHittingLocalPlayer < 1f) return;

        PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
        if (playerControllerB == null) return;
        
        LogDebug($"Harp Ghost collision with player {playerControllerB.playerUsername}");
        timeSinceHittingLocalPlayer = 0f;
        playerControllerB.DamagePlayer(20);
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX);
        if (isEnemyDead) return;

        enemyHP -= force;
        if (IsOwner && enemyHP <= 0)
        {
            KillEnemyOnOwnerClient();
        }
    }
}