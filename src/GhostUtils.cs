using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyHarpGhost;

internal static class GhostUtils
{
    internal enum PathStatus
    {
        Invalid, // Path is invalid or incomplete
        ValidButInLos, // Path is valid but obstructed by line of sight
        Valid, // Path is valid and unobstructed
        Unknown,
    }

    internal static void AssignCorrectAINodesType(EnemyAI enemyAI)
    {
        Vector3 enemyPos = enemyAI.transform.position;
        Vector3 closestOutsideNode = Vector3.positiveInfinity;
        Vector3 closestInsideNode = Vector3.positiveInfinity;
        
        GameObject[] insideAINodes = GameObject.FindGameObjectsWithTag("AINode");
        GameObject[] outsideAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode");

        IEnumerable<Vector3> insideNodePositions = FindInsideAINodePositions(insideAINodes);
        IEnumerable<Vector3> outsideNodePositions = FindOutsideAINodePositions(outsideAINodes);

        foreach (Vector3 pos in outsideNodePositions)
        {
            if ((pos - enemyPos).sqrMagnitude < (closestOutsideNode - enemyPos).sqrMagnitude)
            {
                closestOutsideNode = pos;
            }
        }
        
        foreach (Vector3 pos in insideNodePositions)
        {
            if ((pos - enemyPos).sqrMagnitude < (closestInsideNode - enemyPos).sqrMagnitude)
            {
                closestInsideNode = pos;
            }
        }

        enemyAI.allAINodes = (closestOutsideNode - enemyPos).sqrMagnitude < (closestInsideNode - enemyPos).sqrMagnitude ? outsideAINodes : insideAINodes;
    }
    
    /// <summary>
    /// Finds and returns the positions of all AI nodes tagged as "OutsideAINode".
    /// </summary>
    /// <returns>An enumerable collection of Vector3 positions for all outside AI nodes.</returns>
    internal static IEnumerable<Vector3> FindOutsideAINodePositions(GameObject[] outsideAINodes = null)
    {
        outsideAINodes ??= GameObject.FindGameObjectsWithTag("OutsideAINode");
        Vector3[] outsideNodePositions = new Vector3[outsideAINodes.Length];
                
        for (int i = 0; i < outsideAINodes.Length; i++)
        {
            outsideNodePositions[i] = outsideAINodes[i].transform.position;
        }
        
        return outsideNodePositions;
    }

    /// <summary>
    /// Finds and returns the positions of all AI nodes tagged as "AINode".
    /// </summary>
    /// <returns>An enumerable collection of Vector3 positions for all inside AI nodes.</returns>
    internal static IEnumerable<Vector3> FindInsideAINodePositions(GameObject[] insideAINodes = null)
    {
        insideAINodes ??= GameObject.FindGameObjectsWithTag("AINode");
        Vector3[] insideNodePositions = new Vector3[insideAINodes.Length];
                
        for (int i = 0; i < insideAINodes.Length; i++)
        {
            insideNodePositions[i] = insideAINodes[i].transform.position;
        }
        
        return insideNodePositions;
    }
    
    /// <summary>
    /// Safely updates a NetworkVariable value if different from the current value.
    /// </summary>
    /// <typeparam name="T">The type implementing IEquatable.</typeparam>
    /// <param name="networkVariable">The NetworkVariable to update.</param>
    /// <param name="newValue">The new value to potentially set.</param>
    /// <remarks> Prevents unnecessary network updates by checking equality before setting.</remarks>
    public static void SafeSet<T>(this NetworkVariable<T> networkVariable, T newValue) 
        where T : IEquatable<T>
    {
        if (!EqualityComparer<T>.Default.Equals(networkVariable.Value, newValue))
            networkVariable.Value = newValue;
    }
    
    /// <summary>
    /// Determines whether the specified player is dead.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>Returns true if the player is dead or not controlled; otherwise, false.</returns>
    internal static bool IsPlayerDead(PlayerControllerB player)
    {
        return player.isPlayerDead || !player.isPlayerControlled;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PlayerControllerB GetPlayerFromClientId(ulong playerClientId) 
    {
        return StartOfRound.Instance.allPlayerScripts[playerClientId];
    }
    
    // Used so I dont mix up `PlayerControllerB.playerClientId` and `PlayerControllerB.actualClientId`
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetClientIdFromPlayer(PlayerControllerB player)
    {
        return player.playerClientId;
    }
}