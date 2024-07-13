using System.Collections.Generic;
using UnityEngine;

namespace LethalCompanyHarpGhost;

public static class GhostUtils
{
    public enum PathStatus
    {
        Invalid, // Path is invalid or incomplete
        ValidButInLos, // Path is valid but obstructed by line of sight
        Valid, // Path is valid and unobstructed
        Unknown,
    }

    public static void AssignCorrectAINodesType(EnemyAI enemyAI)
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
    public static IEnumerable<Vector3> FindOutsideAINodePositions(GameObject[] outsideAINodes = null)
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
    public static IEnumerable<Vector3> FindInsideAINodePositions(GameObject[] insideAINodes = null)
    {
        insideAINodes ??= GameObject.FindGameObjectsWithTag("AINode");
        Vector3[] insideNodePositions = new Vector3[insideAINodes.Length];
                
        for (int i = 0; i < insideAINodes.Length; i++)
        {
            insideNodePositions[i] = insideAINodes[i].transform.position;
        }
        
        return insideNodePositions;
    }
}