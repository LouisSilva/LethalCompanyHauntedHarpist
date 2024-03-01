using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace LethalCompanyHarpGhost.BagpipesGhost;

[HarmonyPatch(typeof(NutcrackerEnemyAI))]
public class NutcrackerPatches
{
  private static readonly int InSpawningAnimation = Animator.StringToHash("inSpawningAnimation");
  private static readonly int IsWalking = Animator.StringToHash("IsWalking");
  private static readonly int State = Animator.StringToHash("State");
  private static readonly int Aiming = Animator.StringToHash("Aiming");

    [HarmonyPatch("KillEnemy")]
    [HarmonyPostfix]
    private static void KillEnemyPatch(NutcrackerEnemyAI __instance)
    {
        if (EscortRegistry.GetGhostForEscort(__instance.gameObject) is not { } ghost) return;
        
        ghost.RemoveEscort(__instance.gameObject);
        //ghost.SwitchToBehaviourStateLocally((int)BagpipesGhostAIServer.States.RunningAway);
        
    }

    [HarmonyPatch("DoAIInterval")]
    [HarmonyPrefix]
    private static bool DoAIIntervalPatch(NutcrackerEnemyAI __instance)
    {
        if (EscortRegistry.GetGhostForEscort(__instance.gameObject) is not { } ghost) return true;
        
        if (__instance.moveTowardsDestination) __instance.agent.SetDestination(__instance.destination);
        __instance.SyncPositionToClients();

        if (__instance.isEnemyDead || __instance.stunNormalizedTimer > 0.0f || __instance.gun == null) return false;
        
        return false;
    }

    [HarmonyPatch("Update")]
    [HarmonyPrefix]
    private static bool UpdatePatch(NutcrackerEnemyAI __instance)
    {
        if (EscortRegistry.GetGhostForEscort(__instance.gameObject) is not { } ghost) return true;
        
        // Cannot call base.Update() in harmony patch so I just have to copy the code here
        if (__instance.enemyType.isDaytimeEnemy && !__instance.daytimeEnemyLeaving)
          __instance.CheckTimeOfDayToLeave();
        if (__instance.stunnedIndefinitely <= 0)
        {
          if (__instance.stunNormalizedTimer >= 0.0)
          {
            __instance.stunNormalizedTimer -= Time.deltaTime / __instance.enemyType.stunTimeMultiplier;
          }
          else
          {
            __instance.stunnedByPlayer = null;
            if (__instance.postStunInvincibilityTimer >= 0.0)
              __instance.postStunInvincibilityTimer -= Time.deltaTime * 5f;
          }
        }
        if (!__instance.ventAnimationFinished && __instance.timeSinceSpawn < __instance.exitVentAnimationTime + 0.004999999888241291 * RoundManager.Instance.numberOfEnemiesInScene)
        {
          __instance.timeSinceSpawn += Time.deltaTime;
          if (!__instance.IsOwner)
          {
            Vector3 serverPosition = __instance.serverPosition;
            if (!(__instance.serverPosition != Vector3.zero))
              return false;
            __instance.transform.position = __instance.serverPosition;
            __instance.transform.eulerAngles = new Vector3(__instance.transform.eulerAngles.x, __instance.targetYRotation, __instance.transform.eulerAngles.z);
          }
          else if (__instance.updateDestinationInterval >= 0.0)
          {
            __instance.updateDestinationInterval -= Time.deltaTime;
          }
          else
          {
            __instance.SyncPositionToClients();
            __instance.updateDestinationInterval = 0.1f;
          }
        }
        else
        {
          if (!__instance.ventAnimationFinished)
          {
            __instance.ventAnimationFinished = true;
            if (__instance.creatureAnimator != null)
              __instance.creatureAnimator.SetBool(InSpawningAnimation, false);
          }
          if (!__instance.IsOwner)
          {
            if (__instance.currentSearch.inProgress)
              __instance.StopSearch(__instance.currentSearch);
            __instance.SetClientCalculatingAI(false);
            if (!__instance.inSpecialAnimation)
            {
              __instance.transform.position = Vector3.SmoothDamp(__instance.transform.position, __instance.serverPosition, ref __instance.tempVelocity, __instance.syncMovementSpeed);
              __instance.transform.eulerAngles = new Vector3(__instance.transform.eulerAngles.x, Mathf.LerpAngle(__instance.transform.eulerAngles.y, __instance.targetYRotation, 15f * Time.deltaTime), __instance.transform.eulerAngles.z);
            }
            __instance.timeSinceSpawn += Time.deltaTime;
          }
          else if (__instance.isEnemyDead)
          {
            __instance.SetClientCalculatingAI(false);
          }
          else
          {
            if (!__instance.inSpecialAnimation)
              __instance.SetClientCalculatingAI(true);
            if (__instance.movingTowardsTargetPlayer && __instance.targetPlayer != null)
            {
              if (__instance.setDestinationToPlayerInterval <= 0.0)
              {
                __instance.setDestinationToPlayerInterval = 0.25f;
                __instance.destination = RoundManager.Instance.GetNavMeshPosition(__instance.targetPlayer.transform.position, RoundManager.Instance.navHit, 2.7f);
              }
              else
              {
                __instance.destination = new Vector3(__instance.targetPlayer.transform.position.x, __instance.destination.y, __instance.targetPlayer.transform.position.z);
                __instance.setDestinationToPlayerInterval -= Time.deltaTime;
              }
              if (__instance.addPlayerVelocityToDestination > 0.0)
              {
                if (__instance.targetPlayer == GameNetworkManager.Instance.localPlayerController)
                  __instance.destination += Vector3.Normalize(__instance.targetPlayer.thisController.velocity * 100f) * __instance.addPlayerVelocityToDestination;
                else if (__instance.targetPlayer.timeSincePlayerMoving < 0.25)
                  __instance.destination += Vector3.Normalize((__instance.targetPlayer.serverPlayerPosition - __instance.targetPlayer.oldPlayerPosition) * 100f) * __instance.addPlayerVelocityToDestination;
              }
            }
            if (__instance.inSpecialAnimation)
              return false;
            if (__instance.updateDestinationInterval >= 0.0)
            {
              __instance.updateDestinationInterval -= Time.deltaTime;
            }
            else
            {
              __instance.DoAIInterval();
              __instance.updateDestinationInterval = __instance.AIIntervalTime;
            }
            if (Mathf.Abs(__instance.previousYRotation - __instance.transform.eulerAngles.y) <= 6.0)
              return false;
            __instance.previousYRotation = __instance.transform.eulerAngles.y;
            __instance.targetYRotation = __instance.previousYRotation;
            if (__instance.IsServer)
              __instance.UpdateEnemyRotationClientRpc((short) __instance.previousYRotation);
            else
              __instance.UpdateEnemyRotationServerRpc((short) __instance.previousYRotation);
          }
        }
        
        // Relevant update code
        __instance.TurnTorsoToTargetDegrees();
        if (__instance.isEnemyDead) __instance.StopInspection();
        
        else
        {
          if (!__instance.isEnemyDead && !__instance.GrabGunIfNotHolding()) return false;

          if (__instance.walkCheckInterval <= 0.0f)
          {
            __instance.walkCheckInterval = 0.1f;
            __instance.creatureAnimator.SetBool(IsWalking,
              (__instance.transform.position - __instance.positionLastCheck).sqrMagnitude > 1.0 / 1000.0);
            __instance.positionLastCheck = __instance.transform.position;
          }
          else __instance.walkCheckInterval -= Time.deltaTime;

          if (__instance.stunNormalizedTimer > 0.0f) __instance.agent.speed = 0.0f;
          else
          {
            __instance.timeSinceSeeingTarget += Time.deltaTime;
            __instance.timeSinceInspecting += Time.deltaTime;
            __instance.timeSinceFiringGun += Time.deltaTime;
            __instance.timeSinceHittingPlayer += Time.deltaTime;
            __instance.creatureAnimator.SetInteger(State, __instance.currentBehaviourStateIndex);
            __instance.creatureAnimator.SetBool(Aiming, __instance.aimingGun);

            __instance.isInspecting = false;
            __instance.lostPlayerInChase = false;
            __instance.creatureVoice.Stop();
            __instance.agent.speed = 5.5f;
            __instance.targetTorsoDegrees = 0;
            __instance.torsoTurnSpeed = 525f;
          }
        }
        
        return false;
    }
}

public static class EscortRegistry
{
    private static Dictionary<GameObject, BagpipesGhostAIServer> _escortToGhostDict = [];

    public static void AddEscort(GameObject escort, BagpipesGhostAIServer ghost)
    {
        if (escort != null && ghost != null) _escortToGhostDict[escort] = ghost;
    }

    public static void RemoveEscort(GameObject escort)
    {
        _escortToGhostDict.Remove(escort);
    }

    public static BagpipesGhostAIServer GetGhostForEscort(GameObject escort)
    {
        return _escortToGhostDict.TryGetValue(escort, out BagpipesGhostAIServer ghost) ? ghost : null;
    }
}