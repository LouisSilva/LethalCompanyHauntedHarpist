using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LethalCompanyHarpGhost.EnforcerGhost;

[HarmonyPatch(typeof(ShotgunItem))]
public static class ShotgunPatches
{
    [HarmonyPatch("GrabItemFromEnemy")]
    [HarmonyPostfix]
    private static void AddShotgunToRegistry(ShotgunItem ___instance, EnemyAI enemy)
    {
        if (enemy is not EnforcerGhostAIServer) return;
        if (!EnforcerGhostShotgunAnimationRegistry.IsShotgunInMap(___instance))
            EnforcerGhostShotgunAnimationRegistry.AddShotgun(___instance);
    }

    [HarmonyPatch("DiscardItemFromEnemy")]
    [HarmonyPostfix]
    private static void RemoveShotgunFromRegistry(ShotgunItem ___instance)
    {
        if (EnforcerGhostShotgunAnimationRegistry.IsShotgunInMap(___instance))
            EnforcerGhostShotgunAnimationRegistry.RemoveShotgun(___instance);
    }
    
    [HarmonyPatch("LateUpdate")]
    [HarmonyPostfix]
    private static void DoCustomShotgunAnimation(ShotgunItem ___instance)
    {
        if (!___instance.isHeldByEnemy) return;
        if (___instance.heldByEnemy is not EnforcerGhostAIServer) return;
        
        // If current shotgun is not in the registry, then add it
        if (!EnforcerGhostShotgunAnimationRegistry.IsShotgunInMap(___instance))
            EnforcerGhostShotgunAnimationRegistry.AddShotgun(___instance);
        
        (CustomShotgunRotationAnimation, CustomShotgunRotationAnimation) shotgunAnimationTuple =
            EnforcerGhostShotgunAnimationRegistry.GetShotgunRotationAnimation(___instance);

        if (EnforcerGhostShotgunAnimationRegistry.IsShotgunInAnimation(___instance))
        {
            ___instance.transform.localRotation *= shotgunAnimationTuple.Item1.CalculateUpdatedRotation();
            EnforcerGhostShotgunAnimationRegistry.GetShotgunBarrelTransform(___instance).localRotation *=
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
    public bool IsInAnimation = false;
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

// This basically allows me to "store" a bool variable in the ShotgunItem class which will determine if a shotgun is ongoing a custom animation
public static class EnforcerGhostShotgunAnimationRegistry
{
    private static readonly Dictionary<ShotgunItem, CustomAnimationAndTransformPair> _shotgunToCustomAnimationMap = new();

    private class CustomAnimationAndTransformPair(
        Transform shotgunBarrelTransform, 
        CustomShotgunRotationAnimation customShotgunBarrelRotationAnimation,
        CustomShotgunRotationAnimation customRootShotgunRotationAnimation)
    {
        public Transform ShotgunBarrelTransform { get; set; } = shotgunBarrelTransform;
        public CustomShotgunRotationAnimation ShotgunBarrelRotationAnimation { get; set; } = customShotgunBarrelRotationAnimation;
        public CustomShotgunRotationAnimation RootShotgunRotationAnimation { get; set; } = customRootShotgunRotationAnimation;
    }
    
    public static void AddShotgun(ShotgunItem shotgun)
    {
        if (shotgun == null) return;

        Transform shotgunBarrelTransform = shotgun.transform.Find("GunBarrel");
        CustomShotgunRotationAnimation customShotgunBarrelRotationAnimation = new([new Vector3(0, 0, 0)], []);
        CustomShotgunRotationAnimation customRootShotgunRotationAnimation = new([new Vector3(0, 0, 0)], []);
        _shotgunToCustomAnimationMap[shotgun] = new CustomAnimationAndTransformPair(
            shotgunBarrelTransform, 
            customShotgunBarrelRotationAnimation, 
            customRootShotgunRotationAnimation
            );
    }

    public static void RemoveShotgun(ShotgunItem shotgun)
    {
        if (IsShotgunInMap(shotgun)) _shotgunToCustomAnimationMap.Remove(shotgun);
    }

    public static (CustomShotgunRotationAnimation, CustomShotgunRotationAnimation) GetShotgunRotationAnimation(ShotgunItem shotgun)
    {
        CustomAnimationAndTransformPair currentShotgun = _shotgunToCustomAnimationMap[shotgun];
        return (currentShotgun.RootShotgunRotationAnimation, currentShotgun.ShotgunBarrelRotationAnimation);
    }

    public static Transform GetShotgunBarrelTransform(ShotgunItem shotgun)
    {
        return _shotgunToCustomAnimationMap[shotgun].ShotgunBarrelTransform;
    }

    public static void StartShotgunAnimation(ShotgunItem shotgun)
    {
        if (!IsShotgunInMap(shotgun)) return;
        _shotgunToCustomAnimationMap[shotgun].RootShotgunRotationAnimation.StartAnimation();
        _shotgunToCustomAnimationMap[shotgun].ShotgunBarrelRotationAnimation.StartAnimation();
    }

    public static bool IsShotgunInAnimation(ShotgunItem shotgun)
    {
        CustomAnimationAndTransformPair currentShotgun = _shotgunToCustomAnimationMap[shotgun];
        return currentShotgun.RootShotgunRotationAnimation.IsInAnimation || currentShotgun.ShotgunBarrelRotationAnimation.IsInAnimation;
    }

    public static bool IsShotgunInMap(ShotgunItem shotgun)
    {
        return _shotgunToCustomAnimationMap.ContainsKey(shotgun);
    }
}
