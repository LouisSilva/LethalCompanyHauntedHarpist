using HarmonyLib;
using UnityEngine;

namespace LethalCompanyHarpGhost.EnforcerGhost;

[HarmonyPatch(typeof(ShotgunItem))]
internal static class ShotgunPatches
{
    internal static RuntimeAnimatorController DefaultShotgunAnimationController;

    [HarmonyPatch(nameof(ShotgunItem.Start))]
    [HarmonyPostfix]
    private static void GetDefaultShotgunAnimator(ShotgunItem __instance)
    {
        if (DefaultShotgunAnimationController == null) DefaultShotgunAnimationController = __instance.gunAnimator.runtimeAnimatorController;
    }
}

[HarmonyPatch(typeof(GrabbableObject))]
internal static class GrabbableObjectPatches
{
    [HarmonyPatch(nameof(ShotgunItem.LateUpdate))]
    [HarmonyPostfix]
    private static void UpdateItemOffsets(GrabbableObject __instance)
    {
        if (__instance is not ShotgunItem shotgun) return;
        if (shotgun.heldByEnemy is not EnforcerGhostAIServer) return;
        if (shotgun.parentObject != null)
        {
            Vector3 rotationOffset;
            Vector3 positionOffset;
            if (shotgun.heldByEnemy is EnforcerGhostAIServer && shotgun.isHeldByEnemy)
            {
                positionOffset = new Vector3(0, 0, 0);
                rotationOffset = new Vector3(-180f, 180f, -90f);
            }
            else
            {
                rotationOffset = new Vector3(0, 0.39f, 0);
                positionOffset = new Vector3(-90.89f, -1.5f, 0f);
            }

            shotgun.transform.rotation = shotgun.parentObject.rotation;
            shotgun.transform.Rotate(rotationOffset);
            shotgun.transform.position = shotgun.parentObject.position;
            shotgun.transform.position += shotgun.parentObject.rotation * positionOffset;
        }
        
        if (!(shotgun.radarIcon != null)) return;
        shotgun.radarIcon.position = shotgun.transform.position;
    }
}