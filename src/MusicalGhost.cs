using GameNetcodeStuff;
using LethalCompanyHarpGhost.Types;
using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalCompanyHarpGhost;

public class MusicalGhost : EnemyAI
{
    /// <summary>
    /// A unique identifier for the object, stored as a networked fixed-size string.
    /// This ID is generated as a GUID on the server and synchronized to all clients.
    /// </summary>
    private readonly NetworkVariable<FixedString32Bytes> _networkGhostId = new();

    /// <summary>
    /// Gets the unique identifier (GhostId) for this object as a string.
    /// </summary>
    public string GhostId => _networkGhostId.Value.ToString();

    /// <summary>
    /// A constant representing a null or unassigned player ID.
    /// </summary>
    internal const ulong NullPlayerId = 69420;

    internal readonly PlayerTargetableConditions PlayerTargetableConditions = new();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;
        _networkGhostId.Value = new FixedString32Bytes(Guid.NewGuid().ToString("N").Substring(0, 8));
    }

    public override void Start()
    {
        base.Start();
        Random.InitState(StartOfRound.Instance.randomMapSeed + GhostId.GetHashCode() - thisEnemyIndex);
    }

    /// <summary>
    /// Determines if there is an unobstructed line of sight to a position within a specified view cone and range.
    /// </summary>
    /// <param name="targetPosition">The position to check line of sight to.</param>
    /// <param name="eyeTransform">The transform representing the eye's position and forward direction.</param>
    /// <param name="viewWidth">The total angle of the view cone in degrees.</param>
    /// <param name="viewRange">The maximum distance for the check.</param>
    /// <param name="proximityAwareness">The proximity awareness range. If the value is less than zero, then it is assumed that there is no proximity awareness at all.</param>
    /// <returns>Returns true if the AI has line of sight to the given position; otherwise, false.</returns>
    internal bool HasLineOfSight(
        Vector3 targetPosition,
        Transform eyeTransform,
        float viewWidth = 45f,
        float viewRange = 60f,
        float proximityAwareness = -1f)
    {
        // LogVerbose($"In {nameof(HasLineOfSight)}");

        if (!eyeTransform) return false;

        Vector3 eyePosition = eyeTransform.position;
        Vector3 directionToTarget = targetPosition - eyePosition;
        float sqrDistance = directionToTarget.sqrMagnitude;

        // If the target is directly on top of you, then treat them as visible
        if (sqrDistance <= 0.0001f) return true;

        // 1). Get effective range by taking fog into account
        float effectiveRange = viewRange;
        if (isOutside && !enemyType.canSeeThroughFog &&
            TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy)
        {
            effectiveRange = Mathf.Clamp(viewRange, 0f, 30f);
        }

        // 2). Range check
        float effectiveRangeSqr = effectiveRange * effectiveRange;
        if (sqrDistance > effectiveRangeSqr)
        {
            // LogVerbose($"Distance check failed: {Mathf.Sqrt(sqrDistance)} (distance) > {effectiveRange} (effectiveRange)");
            return false;
        }

        // 3). FOV check. The proximity can bypass the FOV check, but not the physics obstruction check.
        float distance = Mathf.Sqrt(sqrDistance);
        if (!(proximityAwareness >= 0f && distance <= proximityAwareness))
        {
            float halfFov = Mathf.Clamp(viewWidth, 0f, 180f) * 0.5f * Mathf.Deg2Rad;
            float cosHalfFov = Mathf.Cos(halfFov);
            float dotProduct = Vector3.Dot(eyeTransform.forward, directionToTarget / distance);
            if (dotProduct < cosHalfFov)
            {
                // LogVerbose($"FOV check failed: {dotProduct} (dotProduct) < {cosHalfFov} (cosHalfFov)");
                return false;
            }
        }

        // 4). Obstruction check
        if (Physics.Linecast(eyePosition, targetPosition, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
        {
            // LogVerbose("Line of sight check failed");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Finds the closest visible and targetable player.
    /// Uses a buffer distance to prevent rapid target switching.
    /// </summary>
    /// <param name="eyeTransform">The transform representing the eye's position and forward direction.</param>
    /// <param name="viewWidth">The total angle of the view cone in degrees.</param>
    /// <param name="viewRange">The maximum distance for the check.</param>
    /// <param name="currentTargetPlayer">The player currently being targeted.</param>
    /// <param name="bufferDistance">The distance buffer to prevent target switching. A new target must be this much closer to be chosen.</param>
    /// <param name="proximityAwareness"></param>
    /// <returns>The "best" (according to the parameters) player target, or null if none are found.</returns>
    internal PlayerControllerB GetClosestVisiblePlayer(
        Transform eyeTransform,
        float viewWidth = 45f,
        float viewRange = 60f,
        PlayerControllerB currentTargetPlayer = null,
        float bufferDistance = 1.5f,
        float proximityAwareness = -1f)
    {
        // LogVerbose($"In {nameof(GetClosestVisiblePlayer)}");
        PlayerControllerB bestTarget = null;
        float bestTargetDistanceSqr = float.MaxValue;

        PlayerControllerB[] allPlayers = StartOfRound.Instance.allPlayerScripts;

        // First, re-validate the current target
        float currentTargetDistanceSqr = float.MaxValue;
        if (currentTargetPlayer && PlayerTargetableConditions.IsPlayerTargetable(currentTargetPlayer))
        {
            if (HasLineOfSight(currentTargetPlayer.gameplayCamera.transform.position, eyeTransform, viewWidth,
                    viewRange, proximityAwareness))
            {
                // The current target player is still valid, and it will be our baseline
                bestTarget = currentTargetPlayer;
                currentTargetDistanceSqr = (currentTargetPlayer.transform.position - eyeTransform.position).sqrMagnitude;
                bestTargetDistanceSqr = currentTargetDistanceSqr;
            }
        }

        for (int i = 0; i < allPlayers.Length; i++)
        {
            PlayerControllerB potentialTarget = allPlayers[i];
            // LogVerbose($"Evaluating player {potentialTarget.playerUsername}");

            // Skip the check if this player is the current target player; they have already been validated
            if (potentialTarget == currentTargetPlayer) continue;
            if (!PlayerTargetableConditions.IsPlayerTargetable(potentialTarget))
            {
                // LogVerbose($"Player {potentialTarget.playerUsername} is not targetable.");
                continue;
            }

            Vector3 targetPosition = potentialTarget.gameplayCamera.transform.position;
            if (!HasLineOfSight(targetPosition, eyeTransform, viewWidth, viewRange, proximityAwareness))
            {
                // LogVerbose($"Player {potentialTarget.playerUsername} is not in LOS.");
                continue;
            }

            float potentialTargetDistanceSqr = (potentialTarget.transform.position - eyeTransform.position).sqrMagnitude;
            if (potentialTargetDistanceSqr < bestTargetDistanceSqr)
            {
                bestTarget = potentialTarget;
                bestTargetDistanceSqr = potentialTargetDistanceSqr;
            }
        }

        // If we switched targets, ensure that the new target is significantly closer
        if (bestTarget && currentTargetPlayer && bestTarget != currentTargetPlayer)
        {
            // If the old target player is still valid and the new one isn't closer by the buffer amount, then revert
            if (bestTargetDistanceSqr > currentTargetDistanceSqr - bufferDistance * bufferDistance)
            {
                return currentTargetPlayer;
            }
        }

        return bestTarget;
    }

    #region Logging
    internal void LogInfo(object message) => HarpGhostPlugin.Logger?.LogInfo($"{GetLogPrefix()} {message}");

    internal void LogVerbose(object message)
    {
        // if (HarpGhostPlugin.Config?.VerboseLoggingEnabled ?? false)
        //     HarpGhostPlugin.Logger.LogDebug($"{GetLogPrefix()} {message}");

        HarpGhostPlugin.Logger.LogDebug($"{GetLogPrefix()} {message}");
    }

    internal void LogDebug(object message) => HarpGhostPlugin.Logger.LogDebug($"{GetLogPrefix()} {message}");

    internal void LogError(object message) => HarpGhostPlugin.Logger.LogError($"{GetLogPrefix()} {message}");

    internal void LogWarning(object message) => HarpGhostPlugin.Logger.LogWarning($"{GetLogPrefix()} {message}");

    protected virtual string GetLogPrefix()
    {
        return $"[{enemyType.enemyName}]";
    }
    #endregion
}