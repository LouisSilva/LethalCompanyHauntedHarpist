using BepInEx.Logging;
using GameNetcodeStuff;
using UnityEngine;

namespace LethalCompanyHarpGhost;

public class HarpGhostAI : EnemyAI
{
    private ManualLogSource mls;

    public AISearchRoutine roamMap;

    public Transform turnCompass;
    
    private float timeSinceHittingLocalPlayer;
    private float timeSinceNewRandPos;

    private Vector3 positionRandomness;
    private Vector3 stalkPos;

    private bool isSearching;

    private System.Random enemyRandom;

    public override void Start()
    {
        base.Start();
        mls = BepInEx.Logging.Logger.CreateLogSource("Harp Ghost");
        mls.LogInfo("Harp Ghost Spawned");
        
        timeSinceHittingLocalPlayer = 0;
        timeSinceNewRandPos = 0;
        positionRandomness = new Vector3(0, 0, 0);
        isSearching = false;
        enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
    }
    
    public override void Update()
    {
        base.Update();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;
        
        timeSinceHittingLocalPlayer += Time.deltaTime;
        timeSinceNewRandPos += Time.deltaTime;

        if (targetPlayer != null && PlayerIsTargetable(targetPlayer) && !isSearching)
        {
            turnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, turnCompass.eulerAngles.y, 0f)), 3f * Time.deltaTime);
        }

        if (stunNormalizedTimer > 0f)
        {
            agent.speed = 0f;
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;

        if (TargetClosestPlayer(4f) && Vector3.Distance(transform.position, targetPlayer.transform.position) < 25)
        {
            if (isSearching)
            {
                mls.LogInfo($"Harp Ghost is targeting player {targetPlayer.playerUsername}");
                StopSearch(roamMap);
                isSearching = false;
                movingTowardsTargetPlayer = true;
                moveTowardsDestination = false;
            }
        }

        else
        {
            if (!isSearching)
            {
                mls.LogInfo($"Harp Ghost has stopped targeting player {targetPlayer.playerUsername}");
                StartSearch(transform.position, roamMap);
                isSearching = true;
                movingTowardsTargetPlayer = false;
                moveTowardsDestination = true;
            }
        }

        if (targetPlayer != null && PlayerIsTargetable(targetPlayer) && isSearching)
        {
            if (timeSinceNewRandPos > 0.7f && IsOwner)
            {
                timeSinceNewRandPos = 0;
                if (enemyRandom.Next(0, 5) == 0)
                {
                    mls.LogInfo("Harp Ghost attack");
                }

                else
                {
                    positionRandomness = new Vector3(enemyRandom.Next(-2, 2), 0, enemyRandom.Next(-2, 2));
                    stalkPos = targetPlayer.transform.position -
                        Vector3.Scale(new Vector3(-5, 0, -5), targetPlayer.transform.forward) + positionRandomness;
                }

                SetDestinationToPosition(stalkPos);
            }

            agent.speed = 5f;
        }
        
        else
        {
            agent.speed = 3f;
        }
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        if (timeSinceHittingLocalPlayer < 1f) return;

        PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
        if (playerControllerB != null)
        {
            mls.LogInfo($"Harp Ghost collision with player {playerControllerB.playerUsername}");
            timeSinceHittingLocalPlayer = 0f;
            playerControllerB.DamagePlayer(20);
        }
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