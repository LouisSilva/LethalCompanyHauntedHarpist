using LethalLib;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace LethalCompanyHarpGhost;

internal static class Assets
{
    internal static AssetBundle MainAssetBundle;

    private static string GetAssemblyName() => Assembly.GetExecutingAssembly().FullName.Split(',')[0];

    internal static void LoadAssetBundle(string assetBundleName)
    {
        try
        {
            AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                throw new InvalidOperationException($"Could not find assetbundle: {assetBundleName}"), "AssetBundles",
                assetBundleName));

            MainAssetBundle = bundle;
        }
        catch (Exception e)
        {
            Plugin.logger.LogWarning($"Could not load assetbundle: {e}");
            MainAssetBundle = null;
        }
    }

    internal static IEnumerator LoadAudioClipAsync(string clipName, Action<AudioClip> callback)
    {
        if (MainAssetBundle == null) yield break;

        AssetBundleRequest request = MainAssetBundle.LoadAssetAsync<AudioClip>(clipName);
        yield return request;

        AudioClip clip = request.asset as AudioClip;
        callback?.Invoke(clip);
    }
}