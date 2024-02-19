using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
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
        private const string ModVersion = "1.1.3";

        private readonly Harmony _harmony = new Harmony(ModGuid);
        
        private static ManualLogSource _mls;

        private static HarpGhostPlugin _instance;
        
        public static HarpGhostConfig HarpGhostConfig { get; internal set; }

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

            // HarpGhostConfig = new HarpGhostConfig(Config);
            
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
            RegisterEnemy(_harpGhost, SpawnType.Default, new Dictionary<LevelTypes, int>{[LevelTypes.DineLevel] = 40, [LevelTypes.RendLevel] = 40}, infoNode: harpGhostTerminalNode, infoKeyword:harpGhostTerminalKeyword);
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

    public class HarpGhostConfig
    {
        public static ConfigEntry<int> ConfigInitialHealth;
        public static ConfigEntry<int> ConfigAttackDamage;
        
        public static ConfigEntry<float> ConfigAnnoyanceLevelDecayRate;
        public static ConfigEntry<float> ConfigAnnoyanceThreshold;
        public static ConfigEntry<float> ConfigMaxSearchRadius;

        public static ConfigEntry<float> ConfigVoiceSfxVolume;

        public HarpGhostConfig(ConfigFile cfg)
        {
            ConfigInitialHealth = cfg.Bind(
                "General",
                "Health",
                3,
                "The health when spawned"
                );
            
            ConfigAttackDamage = cfg.Bind(
                "General",
                "Attack Damage",
                35,
                "The attack damage of the ghost"
            );
            
            ConfigAnnoyanceLevelDecayRate = cfg.Bind(
                "General",
                "Annoyance Level Decay Rate",
                0.3f,
                "The decay rate of the ghost's annoyance level (due to noises) over time"
            );
            
            ConfigAnnoyanceThreshold = cfg.Bind(
                "General",
                "Annoyance Level Threshold",
                8f,
                "The threshold of how annoyed the ghost has to be (from noise) to get angry"
            );
            
            ConfigMaxSearchRadius = cfg.Bind(
                "General",
                "Max Search Radius",
                100f,
                "The maximum distance the ghost will go to search for a player"
            );
            
            ConfigVoiceSfxVolume= cfg.Bind(
                "General",
                "Voice Sound Effects Volume",
                1f,
                "The volume of the ghost's voice. Values are from 0-1"
            );
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