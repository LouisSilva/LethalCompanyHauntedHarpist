using HarmonyLib;
using UnityEngine;

namespace LethalCompanyHarpGhost.BagpipesGhost;

[HarmonyPatch(typeof(EnemyAI))]
public class EnemyAIPatches
{
    [HarmonyPatch("ChooseNextNodeInSearchRoutine")]
    [HarmonyPrefix]
    private static bool ChooseNextNodeInSearchRoutinePatch(EnemyAI __instance)
    {
        if (!BagpipesGhostAIServer.Ghosts.Contains(__instance)) return true;
        if (__instance.currentBehaviourStateIndex != (int)BagpipesGhostAIServer.States.PlayingMusicWhileEscorted) return true;
        
        // Add custom search routine logic
        float closestDist = float.MaxValue;
        GameObject chosenNode = null;
        Vector3 currentDirection = __instance.transform.forward;
        const float angleDifferenceThreshold = 90f; // Threshold to consider a node is backward

        foreach (GameObject node in __instance.currentSearch.unsearchedNodes)
        {
            Vector3 directionToNode = (node.transform.position - __instance.transform.position).normalized;
            float angleToNode = Vector3.Angle(currentDirection, directionToNode);

            if (angleToNode < angleDifferenceThreshold) // Prioritize nodes ahead
            {
                float distanceToNode = Vector3.Distance(__instance.transform.position, node.transform.position);
                if (distanceToNode < closestDist)
                {
                    chosenNode = node;
                    closestDist = distanceToNode;
                }
            }
        }

        // If no forward node is chosen, consider all nodes, indicating no other route
        if (chosenNode == null)
        {
            foreach (GameObject node in __instance.currentSearch.unsearchedNodes)
            {
                float distanceToNode = Vector3.Distance(__instance.transform.position, node.transform.position);
                if (distanceToNode < closestDist)
                {
                    chosenNode = node;
                    closestDist = distanceToNode;
                }
            }
        }

        // Apply the chosen node
        if (chosenNode != null)
        {
            __instance.currentSearch.currentTargetNode = chosenNode;
            // Remove the chosen node from unsearchedNodes to avoid revisiting
            __instance.currentSearch.unsearchedNodes.Remove(chosenNode);
        }

        return false;
    }
}