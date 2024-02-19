using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using LethalLib.Modules;
using static LethalLib.Modules.Levels;
using static LethalLib.Modules.Enemies;
using static LethalLib.Modules.Items;

namespace LethalCompanyHarpGhost
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class HarpGhostPlugin : BaseUnityPlugin
    {
        private const string ModGuid = $"LCM_HarpGhost|{ModVersion}";
        private const string ModName = "Lethal Company Harp Ghost Mod";
        private const string ModVersion = "1.1.2";

        private readonly Harmony _harmony = new Harmony(ModGuid);
        
        private static ManualLogSource _mls;

        private static HarpGhostPlugin _instance;

        private static EnemyType _harpGhost;

        public static Item HarpItem;

        private void Awake()
        {
            if (_instance == null) _instance = this;
            
            _mls = BepInEx.Logging.Logger.CreateLogSource(ModGuid);

            Assets.PopulateAssets();
            if (Assets.MainAssetBundle == null)
            {
                _mls.LogError("MainAssetBundle is null");
                return;
            }
            
            
            SetupHarpGhost();
            SetupHarp();
            
            _harmony.PatchAll(typeof(Patches));
            _harmony.PatchAll(typeof(HarpGhostPlugin));
            
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (Type type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (MethodInfo method in methods)
                {
                    object[] attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
            
            _mls.LogInfo($"Plugin {ModName} is loaded!");
        }

        private static void SetupHarpGhost()
        {
            _harpGhost = Assets.MainAssetBundle.LoadAsset<EnemyType>("HarpGhost");
            TerminalNode harpGhostTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("HarpGhostTN");
            TerminalKeyword harpGhostTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("HarpGhostTK");
            
            NetworkPrefabs.RegisterNetworkPrefab(_harpGhost.enemyPrefab);
            Utilities.FixMixerGroups(_harpGhost.enemyPrefab);
            RegisterEnemy(_harpGhost, 40, LevelTypes.DineLevel, SpawnType.Daytime, harpGhostTerminalNode, harpGhostTerminalKeyword);
        }

        private static void SetupHarp()
        {
            // string[] assetNames = Assets.MainAssetBundle.GetAllAssetNames();
            // foreach (string assetName in assetNames)
            // {
            //     mls.LogInfo("Asset in bundle: " + assetName);
            // }
            
            HarpItem = Assets.MainAssetBundle.LoadAsset<Item>("HarpItemData");
            if (HarpItem == null)
            {
                _mls.LogError("Failed to load HarpItemData from AssetBundle.");
                return;
            }
            
            NetworkPrefabs.RegisterNetworkPrefab(HarpItem.spawnPrefab);
            Utilities.FixMixerGroups(HarpItem.spawnPrefab);
            RegisterScrap(HarpItem, 0, LevelTypes.All);
        }
    }

    public static class Assets
    {
        private const string MainAssetBundleName = "Assets.harpghostbundle";
        public static AssetBundle MainAssetBundle = null;

        private static string GetAssemblyName() => Assembly.GetExecutingAssembly().FullName.Split(',')[0];
        public static void PopulateAssets()
        {
            if (MainAssetBundle != null) return;
            using Stream assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetAssemblyName() + "." + MainAssetBundleName);
            MainAssetBundle = AssetBundle.LoadFromStream(assetStream);
        }
    }
}