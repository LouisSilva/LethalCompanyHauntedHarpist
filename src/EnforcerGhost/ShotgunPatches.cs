using HarmonyLib;
using UnityEngine;

namespace LethalCompanyHarpGhost.EnforcerGhost;

[HarmonyPatch(typeof(ShotgunItem))]
internal static class ShotgunPatches
{
    private static RuntimeAnimatorController DefaultShotgunAnimationController;

    [HarmonyPatch(nameof(ShotgunItem.Start))]
    [HarmonyPostfix]
    private static void GetDefaultShotgunAnimator(ShotgunItem __instance)
    {
        DefaultShotgunAnimationController = __instance.gunAnimator.runtimeAnimatorController;
    }
    
    [HarmonyPatch(nameof(ShotgunItem.GrabItemFromEnemy))]
    [HarmonyPostfix]
    private static void AddCustomAnimationController(ShotgunItem __instance)
    {
        if (__instance.heldByEnemy is not EnforcerGhostAIServer) return;
        __instance.gunAnimator.runtimeAnimatorController = HarpGhostPlugin.CustomShotgunAnimator;
    }

    [HarmonyPatch(nameof(ShotgunItem.DiscardItemFromEnemy))]
    [HarmonyPostfix]
    private static void RemoveCustomAnimationController(ShotgunItem __instance)
    {
        __instance.gunAnimator.runtimeAnimatorController = DefaultShotgunAnimationController;
    }
}