using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LethalCompanyHarpGhost.EnforcerGhost;

[HarmonyPatch(typeof(ShotgunItem))]
internal static class ShotgunPatches
{
    [HarmonyPatch(nameof(ShotgunItem.GrabItemFromEnemy))]
    [HarmonyPostfix]
    private static void AddShotgunToRegistry(ShotgunItem __instance, EnemyAI enemy)
    {
        if (enemy is not EnforcerGhostAIServer) return;
        if (!EnforcerGhostShotgunAnimationRegistry.IsShotgunInMap(__instance))
            EnforcerGhostShotgunAnimationRegistry.AddShotgun(__instance);
    }

    [HarmonyPatch(nameof(ShotgunItem.DiscardItemFromEnemy))]
    [HarmonyPostfix]
    private static void RemoveShotgunFromRegistry(ShotgunItem __instance)
    {
        if (EnforcerGhostShotgunAnimationRegistry.IsShotgunInMap(__instance))
            EnforcerGhostShotgunAnimationRegistry.RemoveShotgun(__instance);
    }
}

[HarmonyPatch(typeof(GrabbableObject))]
internal static class GrabbableObjectPatches
{
    [HarmonyPatch(nameof(GrabbableObject.LateUpdate))]
    [HarmonyPostfix]
    private static void DoCustomShotgunAnimation(GrabbableObject __instance)
    {
        if (__instance is not ShotgunItem shotgun) return;
        if (!shotgun.isHeldByEnemy) return;
        if (shotgun.heldByEnemy is not EnforcerGhostAIServer) return;
        
        // If current shotgun is not in the registry, then add it
        if (!EnforcerGhostShotgunAnimationRegistry.IsShotgunInMap(shotgun))
            EnforcerGhostShotgunAnimationRegistry.AddShotgun(shotgun);
        
        (CustomShotgunRotationAnimation, CustomShotgunRotationAnimation) shotgunAnimationTuple =
            EnforcerGhostShotgunAnimationRegistry.GetShotgunRotationAnimation(shotgun);

        if (EnforcerGhostShotgunAnimationRegistry.IsShotgunInAnimation(shotgun))
        {
            shotgun.transform.localRotation *= shotgunAnimationTuple.Item1.CalculateUpdatedRotation();
            EnforcerGhostShotgunAnimationRegistry.GetShotgunBarrelTransform(shotgun).localRotation *=
                shotgunAnimationTuple.Item2.CalculateUpdatedRotation();
        }
        
        else
        {
            shotgunAnimationTuple.Item1.ResetAnimation();
            shotgunAnimationTuple.Item2.ResetAnimation();
        }
    }
}

public class CustomShotgunRotationAnimation
{
    private readonly Quaternion[] _rotations;
    private readonly float[] _keyframeTimes;
    public bool IsInAnimation;
    private float _startTime;
    private int _currentKeyframeIndex;

    public CustomShotgunRotationAnimation(Vector3[] eulerRotations, float[] keyframeTimes)
    {
        _currentKeyframeIndex = 0;
        _keyframeTimes = keyframeTimes;
        
        // Converts the euler rotations into Quaternions
        _rotations = new Quaternion[eulerRotations.Length];
        for (int i = 0; i < eulerRotations.Length; i++)
            _rotations[i] = Quaternion.Euler(eulerRotations[i]);
    }

    public void StartAnimation()
    {
        IsInAnimation = true;
        _startTime = Time.time;
    }

    public void ResetAnimation()
    {
        IsInAnimation = false;
        _startTime = Time.time;
        _currentKeyframeIndex = 0;
    }

    public Quaternion CalculateUpdatedRotation()
    {
        // Update keyframe index
        while (_currentKeyframeIndex < _keyframeTimes.Length - 1 &&
               Time.time - _startTime >= _keyframeTimes[_currentKeyframeIndex + 1])
            _currentKeyframeIndex++;

        if (_currentKeyframeIndex < _rotations.Length - 1)
        {
            float elapsedSinceKeyframe = Time.time - _startTime - _keyframeTimes[_currentKeyframeIndex];
            float timeBetweenKeyframes = _keyframeTimes[_currentKeyframeIndex + 1] - _keyframeTimes[_currentKeyframeIndex];
            float normalizedTime = elapsedSinceKeyframe / timeBetweenKeyframes;

            return Quaternion.Lerp(_rotations[_currentKeyframeIndex], _rotations[_currentKeyframeIndex + 1], normalizedTime);
        }
        
        // If there are no more keyframes or pausing in the current position
        return _rotations[_currentKeyframeIndex];
    }
}

// This class is for storing shotguns owned by an enforcer ghost to be able to apply animations to the shotgun
public static class EnforcerGhostShotgunAnimationRegistry
{
    private static readonly Dictionary<ShotgunItem, CustomShotgunAnimationController> ShotgunToCustomAnimationMap = new();

    private class CustomShotgunAnimationController(
        Transform shotgunBarrelTransform, 
        CustomShotgunRotationAnimation customShotgunBarrelRotationAnimation,
        CustomShotgunRotationAnimation customShotgunRootRotationAnimation)
    {
        public Transform ShotgunBarrelTransform { get; set; } = shotgunBarrelTransform;
        public CustomShotgunRotationAnimation ShotgunBarrelRotationAnimation { get; set; } = customShotgunBarrelRotationAnimation;
        public CustomShotgunRotationAnimation ShotgunRootRotationAnimation { get; set; } = customShotgunRootRotationAnimation;
    }

    private static readonly float[] ShotgunRootKeyframeTimes = [0f, 0.3f, 2.11f, 2.3f];
    private static readonly Vector3[] ShotgunRootEulerRotations = [
        new Vector3(-173, 180, -90),
        new Vector3(-140, 180, -90),
        new Vector3(-140, 180, -90),
        new Vector3(-173, 180, -90)
    ];

    private static readonly float[] ShotgunBarrelKeyframeTimes = [0f, 0.45f, 1.10f, 2.22f, 2.31f];
    private static readonly Vector3[] ShotgunBarrelEulerRotations = [
        new Vector3(0, 0, 0),
        new Vector3(0, 0, 0),
        new Vector3(0, -55, 0),
        new Vector3(0, -55, 0),
        new Vector3(0, 0, 0),
    ];
    
    public static void AddShotgun(ShotgunItem shotgun)
    {
        if (shotgun == null) return;
        if (IsShotgunInMap(shotgun)) return;
        
        Transform shotgunBarrelTransform = shotgun.transform.Find("GunBarrel");
        CustomShotgunRotationAnimation customShotgunBarrelRotationAnimation = new(ShotgunBarrelEulerRotations, ShotgunBarrelKeyframeTimes);
        CustomShotgunRotationAnimation customShotgunRootRotationAnimation = new(ShotgunRootEulerRotations, ShotgunRootKeyframeTimes);
        ShotgunToCustomAnimationMap[shotgun] = new CustomShotgunAnimationController(
            shotgunBarrelTransform,
            customShotgunBarrelRotationAnimation,
            customShotgunRootRotationAnimation
            );
    }

    public static void RemoveShotgun(ShotgunItem shotgun)
    {
        if (IsShotgunInMap(shotgun)) ShotgunToCustomAnimationMap.Remove(shotgun);
    }

    public static (CustomShotgunRotationAnimation, CustomShotgunRotationAnimation) GetShotgunRotationAnimation(ShotgunItem shotgun)
    {
        CustomShotgunAnimationController currentShotgun = ShotgunToCustomAnimationMap[shotgun];
        return (currentShotgun.ShotgunRootRotationAnimation, currentShotgun.ShotgunBarrelRotationAnimation);
    }

    public static Transform GetShotgunBarrelTransform(ShotgunItem shotgun)
    {
        return ShotgunToCustomAnimationMap[shotgun].ShotgunBarrelTransform;
    }

    public static void StartShotgunAnimation(ShotgunItem shotgun)
    {
        if (!IsShotgunInMap(shotgun)) return;
        ShotgunToCustomAnimationMap[shotgun].ShotgunRootRotationAnimation.StartAnimation();
        ShotgunToCustomAnimationMap[shotgun].ShotgunBarrelRotationAnimation.StartAnimation();
    }
    
    public static void EndShotgunAnimation(ShotgunItem shotgun)
    {
        if (!IsShotgunInMap(shotgun)) return;
        ShotgunToCustomAnimationMap[shotgun].ShotgunRootRotationAnimation.ResetAnimation();
        ShotgunToCustomAnimationMap[shotgun].ShotgunBarrelRotationAnimation.ResetAnimation();
    }

    public static bool IsShotgunInAnimation(ShotgunItem shotgun)
    {
        CustomShotgunAnimationController currentShotgun = ShotgunToCustomAnimationMap[shotgun];
        return currentShotgun.ShotgunRootRotationAnimation.IsInAnimation || currentShotgun.ShotgunBarrelRotationAnimation.IsInAnimation;
    }

    public static bool IsShotgunInMap(ShotgunItem shotgun)
    {
        return ShotgunToCustomAnimationMap.ContainsKey(shotgun);
    }
}
