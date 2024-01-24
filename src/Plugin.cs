using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using LethalLib.Modules;
using static LethalLib.Modules.Levels;
using static LethalLib.Modules.Enemies;

namespace LethalCompanyHarpGhost
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class HarpGhostPlugin : BaseUnityPlugin
    {
        private const string modGUID = "DarthFigo.LCM_HarpGhost";
        private const string modName = "DarthFigo Lethal Company Harp Ghost Mod";
        private const string modVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);
        private ManualLogSource mls;

        public static EnemyType harpGhost;

        private void Awake()
        {
            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

            Assets.PopulateAssets();
            if (Assets.MainAssetBundle == null)
            {
                mls.LogError("MainAssetBundle is null");
                return; 
            }
            
            harpGhost = Assets.MainAssetBundle.LoadAsset<EnemyType>("HarpGhost");
            var harpGhostTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("HarpGhostTN");
            var harpGhostTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("HarpGhostTK");
            
            NetworkPrefabs.RegisterNetworkPrefab(harpGhost.enemyPrefab);
            RegisterEnemy(harpGhost, 100, LevelTypes.All, SpawnType.Daytime, harpGhostTerminalNode, harpGhostTerminalKeyword);
            
            harmony.PatchAll(typeof(Patches));
            harmony.PatchAll(typeof(HarpGhostPlugin));
            mls.LogInfo($"Plugin {modName} is loaded!");

        }
    }

    public static class Assets
    {
        private static string mainAssetBundleName = "Assets.harpghostbundle";
        public static AssetBundle MainAssetBundle = null;

        private static string GetAssemblyName() => Assembly.GetExecutingAssembly().FullName.Split(',')[0];
        public static void PopulateAssets()
        {
            if (MainAssetBundle != null) return;
            using (var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetAssemblyName() + "." + mainAssetBundleName)) {
                MainAssetBundle = AssetBundle.LoadFromStream(assetStream);
            }
        }
    }
}