using HarmonyLib;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace LethalCompanyHarpGhost.EnforcerGhost;

[HarmonyPatch(typeof(ShotgunItem))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class ShotgunPatches
{
    internal static RuntimeAnimatorController DefaultShotgunAnimationController;

    [HarmonyPatch(nameof(ShotgunItem.Start))]
    [HarmonyPostfix]
    private static void GetDefaultShotgunAnimator(ShotgunItem __instance)
    {
        if (!DefaultShotgunAnimationController)
            DefaultShotgunAnimationController = __instance.gunAnimator.runtimeAnimatorController;
    }
}

[HarmonyPatch(typeof(GrabbableObject))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class GrabbableObjectPatches
{
    [HarmonyPatch(nameof(ShotgunItem.LateUpdate))]
    [HarmonyPostfix]
    private static void UpdateItemOffsets(GrabbableObject __instance)
    {
        if (__instance is not ShotgunItem shotgun) return;
        if (!shotgun.isHeldByEnemy || shotgun.heldByEnemy is not EnforcerGhostAIServer || !shotgun.parentObject) return;

        Vector3 positionOffset = new(0, 0, 0);
        Vector3 rotationOffset = new(-180f, 180f, -90f);

        shotgun.transform.rotation = shotgun.parentObject.rotation;
        shotgun.transform.Rotate(rotationOffset);
        shotgun.transform.position = shotgun.parentObject.position;
        shotgun.transform.position += shotgun.parentObject.rotation * positionOffset;

        if (!shotgun.radarIcon) return;
        shotgun.radarIcon.position = shotgun.transform.position;
    }
}