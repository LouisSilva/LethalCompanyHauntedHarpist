using System;
using System.Collections.Generic;
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
        private const string modGUID = "Bob123.LCM_HarpGhost";
        private const string modName = "Bob123 Lethal Company Harp Ghost Mod";
        private const string modVersion = "1.0.2";

        private readonly Harmony harmony = new Harmony(modGUID);
        
        private static ManualLogSource mls;

        private static HarpGhostPlugin Instance;

        private static EnemyType harpGhost;

        public static Item harpItem;

        public static List<AudioClip> harpAudioClips;
        
        private static readonly bool debug = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            
            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

            Assets.PopulateAssets();
            if (debug)
            {
                if (Assets.MainAssetBundle == null)
                {
                    mls.LogError("MainAssetBundle is null");
                    return;
                }
            }
            
            SetupHarpGhost();
            SetupHarp();
            
            harmony.PatchAll(typeof(Patches));
            harmony.PatchAll(typeof(HarpGhostPlugin));
            
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
            
            mls.LogInfo($"Plugin {modName} is loaded!");
        }

        private static void SetupHarpGhost()
        {
            harpGhost = Assets.MainAssetBundle.LoadAsset<EnemyType>("HarpGhost");
            TerminalNode harpGhostTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("HarpGhostTN");
            TerminalKeyword harpGhostTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("HarpGhostTK");

            HarpGhostAI harpGhostAI = harpGhost.enemyPrefab.GetComponent<HarpGhostAI>();
            
            // Setting these in the unity inspector doesn't work for some reason, so have to do it here
            
            harpGhostAI.dieSfx = Assets.MainAssetBundle.LoadAsset<AudioClip>("assets/harpghostasset/audio/die3.mp3");
            AudioClip damageClip1 = Assets.MainAssetBundle.LoadAsset<AudioClip>("assets/harpghostasset/audio/damage1.mp3");
            AudioClip damageClip2 = Assets.MainAssetBundle.LoadAsset<AudioClip>("assets/harpghostasset/audio/damage2.mp3");

            AudioClip laughClip1 = Assets.MainAssetBundle.LoadAsset<AudioClip>("assets/harpghostasset/audio/laugh1.mp3");
            AudioClip laughClip2 = Assets.MainAssetBundle.LoadAsset<AudioClip>("assets/harpghostasset/audio/laugh2.mp3");
            AudioClip laughClip3 = Assets.MainAssetBundle.LoadAsset<AudioClip>("assets/harpghostasset/audio/laugh3.mp3");

            AudioClip upsetClip1 = Assets.MainAssetBundle.LoadAsset<AudioClip>("assets/harpghostasset/audio/random_upset1.mp3");
            AudioClip upsetClip2 = Assets.MainAssetBundle.LoadAsset<AudioClip>("assets/harpghostasset/audio/random_upset2.mp3");

            AudioClip stunClip1 = Assets.MainAssetBundle.LoadAsset<AudioClip>("assets/harpghostasset/audio/stun1.mp3");
            AudioClip stunClip2 = Assets.MainAssetBundle.LoadAsset<AudioClip>("assets/harpghostasset/audio/stun2.mp3");
            
            if (damageClip1 == null) mls.LogError("Failed to load damage1 AudioClip from AssetBundle - Asset is null");

            if (damageClip2 == null) mls.LogError("Failed to load damage2 AudioClip from AssetBundle - Asset is null");

            if (laughClip1 == null) mls.LogError("Failed to load laugh1 AudioClip from AssetBundle - Asset is null");

            if (laughClip2 == null) mls.LogError("Failed to load laugh2 AudioClip from AssetBundle - Asset is null");

            if (laughClip3 == null) mls.LogError("Failed to load laugh3 AudioClip from AssetBundle - Asset is null");

            if (upsetClip1 == null) mls.LogError("Failed to load random_upset1 AudioClip from AssetBundle - Asset is null");

            if (upsetClip2 == null) mls.LogError("Failed to load random_upset2 AudioClip from AssetBundle - Asset is null");

            if (stunClip1 == null) mls.LogError("Failed to load stun1 AudioClip from AssetBundle - Asset is null");

            if (stunClip2 == null) mls.LogError("Failed to load stun2 AudioClip from AssetBundle - Asset is null");
            
            harpGhostAI.damageSfx = new AudioClip[]
            {
                damageClip1,
                damageClip2
            };

            harpGhostAI.laughSfx = new AudioClip[]
            {
                laughClip1,
                laughClip2,
                laughClip3
            };

            harpGhostAI.upsetSfx = new AudioClip[]
            {
                upsetClip1,
                upsetClip2
            };

            harpGhostAI.stunSfx = new AudioClip[]
            {
                stunClip1,
                stunClip2
            };
            
            NetworkPrefabs.RegisterNetworkPrefab(harpGhost.enemyPrefab);
            Utilities.FixMixerGroups(harpGhost.enemyPrefab);
            RegisterEnemy(harpGhost, 40, LevelTypes.DineLevel, SpawnType.Daytime, harpGhostTerminalNode, harpGhostTerminalKeyword);
            
        }

        private static void SetupHarp()
        {
            string[] assetNames = Assets.MainAssetBundle.GetAllAssetNames();
            foreach (string assetName in assetNames)
            {
                mls.LogInfo("Asset in bundle: " + assetName);
            }

            // Load the HarpItemData from the AssetBundle
            harpItem = Assets.MainAssetBundle.LoadAsset<Item>("HarpItemData");
            if (harpItem == null)
            {
                mls.LogError("Failed to load HarpItemData from AssetBundle.");
                return;
            }

            // Add HarpBehaviour component to the harp item spawn prefab
            HarpBehaviour harpBehaviour = harpItem.spawnPrefab.AddComponent<HarpBehaviour>();
            if (harpBehaviour == null)
            {
                mls.LogError("Failed to add HarpBehaviour to the spawnPrefab.");
                return;
            }

            // Ensure the AudioSource is attached to the prefab
            harpBehaviour.harpAudioSource = harpItem.spawnPrefab.GetComponent<AudioSource>();
            if (harpBehaviour.harpAudioSource == null)
            {
                mls.LogError("AudioSource component is missing from the spawnPrefab.");
                return;
            }

            // Initialize the harpAudioClips list to avoid NullReferenceException
            harpBehaviour.harpAudioClips = new List<AudioClip>();

            try
            {
                AudioClip harpClip = Assets.MainAssetBundle.LoadAsset<AudioClip>("assets/harpghostasset/audio/harpmusic1.mp3");
                harpAudioClips = new List<AudioClip>();
                if (harpClip != null)
                {
                    harpBehaviour.harpAudioClips.Add(harpClip);
                    harpAudioClips.Add(harpClip);
                    mls.LogInfo("Successfully added HarpMusic1 AudioClip to the list.");
                }
                else
                {
                    mls.LogError("Failed to load HarpMusic1 AudioClip from AssetBundle - Asset is null");
                }
            }
            catch (Exception ex)
            {
                mls.LogError($"Exception while attempting to load AudioClip: {ex}");
            }

            // Set other relevant properties for the HarpBehaviour
            harpBehaviour.grabbable = true;
            harpBehaviour.grabbableToEnemies = true;
            harpBehaviour.itemProperties = harpItem;
            
            NetworkPrefabs.RegisterNetworkPrefab(harpItem.spawnPrefab);
            Utilities.FixMixerGroups(harpItem.spawnPrefab);
            RegisterItem(harpItem);
        }
    }

    public static class Assets
    {
        private const string mainAssetBundleName = "Assets.harpghostbundle";
        public static AssetBundle MainAssetBundle = null;

        private static string GetAssemblyName() => Assembly.GetExecutingAssembly().FullName.Split(',')[0];
        public static void PopulateAssets()
        {
            if (MainAssetBundle != null) return;
            using Stream assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetAssemblyName() + "." + mainAssetBundleName);
            MainAssetBundle = AssetBundle.LoadFromStream(assetStream);
        }
    }
}