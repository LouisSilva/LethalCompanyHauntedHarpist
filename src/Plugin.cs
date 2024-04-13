using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using LethalLib.Modules;
using Unity.Collections;
using Unity.Netcode;
using static LethalLib.Modules.Levels;
using static LethalLib.Modules.Enemies;
using static LethalLib.Modules.Items;
using NetworkPrefabs = LethalLib.Modules.NetworkPrefabs;

namespace LethalCompanyHarpGhost
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(LethalLib.Plugin.ModGUID)]
    [BepInDependency("linkoid-DissonanceLagFix-1.0.0", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("mattymatty-AsyncLoggers-1.6.1", BepInDependency.DependencyFlags.SoftDependency)]
    public class HarpGhostPlugin : BaseUnityPlugin
    {
        public const string ModGuid = $"LCM_HauntedHarpist|{ModVersion}";
        private const string ModName = "Lethal Company Haunted Harpist Mod";
        private const string ModVersion = "1.3.9";

        private readonly Harmony _harmony = new(ModGuid);
        
        // ReSharper disable once InconsistentNaming
        private static readonly ManualLogSource _mls = BepInEx.Logging.Logger.CreateLogSource(ModGuid);

        private static HarpGhostPlugin _instance;

        // ReSharper disable once InconsistentNaming
        private static readonly Dictionary<string, List<AudioClip>> _instrumentAudioClips = new();
        
        public static HarpGhostConfig HarpGhostConfig { get; internal set; }
        public static BagpipeGhostConfig BagpipeGhostConfig { get; internal set; }
        public static EnforcerGhostConfig EnforcerGhostConfig { get; internal set; }

        private static EnemyType _harpGhostEnemyType;
        private static EnemyType _bagpipesGhostEnemyType;
        internal static EnemyType EnforcerGhostEnemyType;

        public static Item HarpItem;
        public static Item BagpipesItem;
        private static Item TubaItem;
        
        public static GameObject ShotgunPrefab;
        public static RuntimeAnimatorController CustomShotgunAnimator;

        private void Awake()
        {
            if (_instance == null) _instance = this;
            
            InitializeNetworkStuff();

            Assets.PopulateAssets();
            if (Assets.MainAssetBundle == null)
            {
                _mls.LogError("MainAssetBundle is null");
                return;
            }
            
            _harmony.PatchAll();
            HarpGhostConfig = new HarpGhostConfig(Config);
            BagpipeGhostConfig = new BagpipeGhostConfig(Config);
            EnforcerGhostConfig = new EnforcerGhostConfig(Config);
            
            SetupHarpGhost();
            SetupBagpipesGhost();
            SetupEnforcerGhost();
            
            SetupHarp();
            SetupBagpipes();
            //SetupTuba();
            
            _harmony.PatchAll();
            _harmony.PatchAll(typeof(HarpGhostPlugin));
            _mls.LogInfo($"Plugin {ModName} is loaded!");
        }

        private static void SetupHarpGhost()
        {
            _harpGhostEnemyType = Assets.MainAssetBundle.LoadAsset<EnemyType>("HarpGhost");
            _harpGhostEnemyType.canDie = HarpGhostConfig.Instance.HarpGhostIsKillable.Value;
            //_harpGhostEnemyType.PowerLevel = HarpGhostConfig.Instance.HarpGhostPowerLevel.Value;
            _harpGhostEnemyType.canBeStunned = HarpGhostConfig.Instance.HarpGhostIsStunnable.Value;
            //_harpGhostEnemyType.MaxCount = HarpGhostConfig.Instance.MaxAmountOfHarpGhosts.Value;
            _harpGhostEnemyType.stunTimeMultiplier = HarpGhostConfig.Instance.HarpGhostStunTimeMultiplier.Value;
            _harpGhostEnemyType.stunGameDifficultyMultiplier = HarpGhostConfig.Instance.HarpGhostStunGameDifficultyMultiplier.Value;
            _harpGhostEnemyType.canSeeThroughFog = HarpGhostConfig.Instance.HarpGhostCanSeeThroughFog.Value;
            
            TerminalNode harpGhostTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("HarpGhostTN");
            TerminalKeyword harpGhostTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("HarpGhostTK");
            
            NetworkPrefabs.RegisterNetworkPrefab(_harpGhostEnemyType.enemyPrefab);
            Utilities.FixMixerGroups(_harpGhostEnemyType.enemyPrefab);
            RegisterEnemy(
                _harpGhostEnemyType, 
                Mathf.Clamp(HarpGhostConfig.Instance.HarpGhostSpawnRate.Value, 0, 999), 
                HarpGhostConfig.Instance.HarpGhostSpawnLevel.Value, 
                SpawnType.Default, 
                harpGhostTerminalNode,
                harpGhostTerminalKeyword
                );
        }
        
        private static void SetupBagpipesGhost()
        {
            _bagpipesGhostEnemyType = Assets.MainAssetBundle.LoadAsset<EnemyType>("BagpipesGhost");
            _bagpipesGhostEnemyType.canDie = BagpipeGhostConfig.Instance.BagpipeGhostIsKillable.Value;
            //_bagpipesGhostEnemyType.PowerLevel = BagpipeGhostConfig.Instance.BagpipeGhostPowerLevel.Value;
            _bagpipesGhostEnemyType.canBeStunned = BagpipeGhostConfig.Instance.BagpipeGhostIsStunnable.Value;
            //_bagpipesGhostEnemyType.MaxCount = BagpipeGhostConfig.Instance.MaxAmountOfBagpipeGhosts.Value;
            _bagpipesGhostEnemyType.stunTimeMultiplier = BagpipeGhostConfig.Instance.BagpipeGhostStunTimeMultiplier.Value;
            _bagpipesGhostEnemyType.stunGameDifficultyMultiplier = BagpipeGhostConfig.Instance.BagpipeGhostStunGameDifficultyMultiplier.Value;
                
            TerminalNode bagpipeGhostTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("BagpipeGhostTN");
            TerminalKeyword bagpipeGhostTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("BagpipeGhostTK");
            
            NetworkPrefabs.RegisterNetworkPrefab(_bagpipesGhostEnemyType.enemyPrefab);
            Utilities.FixMixerGroups(_bagpipesGhostEnemyType.enemyPrefab);
            RegisterEnemy(
                _bagpipesGhostEnemyType, 
                Mathf.Clamp(BagpipeGhostConfig.Instance.BagpipeGhostSpawnRate.Value, 0, 999), 
                BagpipeGhostConfig.Instance.BagpipeGhostSpawnLevel.Value, 
                SpawnType.Default, 
                bagpipeGhostTerminalNode, 
                bagpipeGhostTerminalKeyword);
        }
        
        private static void SetupEnforcerGhost()
        {
            EnforcerGhostEnemyType = Assets.MainAssetBundle.LoadAsset<EnemyType>("EnforcerGhost");
            EnforcerGhostEnemyType.canDie = EnforcerGhostConfig.Instance.EnforcerGhostIsKillable.Value;
           // EnforcerGhostEnemyType.PowerLevel = EnforcerGhostConfig.Instance.EnforcerGhostPowerLevel.Value;
            EnforcerGhostEnemyType.canBeStunned = EnforcerGhostConfig.Instance.EnforcerGhostIsStunnable.Value; 
            // EnforcerGhostEnemyType.MaxCount = EnforcerGhostConfig.Instance.MaxAmountOfEnforcerGhosts.Value;
            EnforcerGhostEnemyType.stunTimeMultiplier = EnforcerGhostConfig.Instance.EnforcerGhostStunTimeMultiplier.Value;
            EnforcerGhostEnemyType.stunGameDifficultyMultiplier = EnforcerGhostConfig.Instance.EnforcerGhostStunGameDifficultyMultiplier.Value;
            
            TerminalNode enforcerGhostTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("EnforcerGhostTN");
            TerminalKeyword enforcerGhostTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("EnforcerGhostTK");
            
            NetworkPrefabs.RegisterNetworkPrefab(EnforcerGhostEnemyType.enemyPrefab);
            Utilities.FixMixerGroups(EnforcerGhostEnemyType.enemyPrefab);
            RegisterEnemy(
                EnforcerGhostEnemyType, 
                Mathf.Clamp(EnforcerGhostConfig.Instance.EnforcerGhostSpawnRate.Value, 0, 999), 
                EnforcerGhostConfig.Instance.EnforcerGhostSpawnLevel.Value, 
                SpawnType.Default, 
                enforcerGhostTerminalNode, 
                enforcerGhostTerminalKeyword);

            CustomShotgunAnimator = Assets.MainAssetBundle.LoadAsset<RuntimeAnimatorController>("AnimatorShotgun");
            if (CustomShotgunAnimator == null) _mls.LogError("custom shotgun animator is null");
        }

        private void SetupHarp()
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

            // Async load audio clips to get rid of lag spike
            AudioClip[] audioClips = HarpItem.spawnPrefab.GetComponent<InstrumentBehaviour>().instrumentAudioClips;
            List<string> audioClipNames = [];
            audioClipNames.AddRange(audioClips.Select(curAudioClip => curAudioClip.name));
            LoadInstrumentAudioClipsAsync(HarpItem.itemName, audioClipNames);
            
            RegisterScrap(HarpItem, 0, LevelTypes.All);
        }

        private void SetupBagpipes()
        {
            BagpipesItem = Assets.MainAssetBundle.LoadAsset<Item>("BagpipesItemData");
            if (BagpipesItem == null)
            {
                _mls.LogError("Failed to load BagpipesItemData from AssetBundle");
            }
            
            NetworkPrefabs.RegisterNetworkPrefab(BagpipesItem.spawnPrefab);
            Utilities.FixMixerGroups(BagpipesItem.spawnPrefab);

            AudioClip[] audioClips = BagpipesItem.spawnPrefab.GetComponent<InstrumentBehaviour>().instrumentAudioClips;
            List<string> audioClipNames = [];
            audioClipNames.AddRange(audioClips.Select(curAudioClip => curAudioClip.name));
            LoadInstrumentAudioClipsAsync(BagpipesItem.itemName, audioClipNames);
            
            RegisterScrap(BagpipesItem, 0, LevelTypes.All);
        }
        
        private static void SetupTuba()
        {
            TubaItem = Assets.MainAssetBundle.LoadAsset<Item>("TubaItemData");
            if (TubaItem == null)
            {
                _mls.LogError("Failed to load TubaItemData from AssetBundle");
            }
            
            NetworkPrefabs.RegisterNetworkPrefab(TubaItem.spawnPrefab);
            Utilities.FixMixerGroups(TubaItem.spawnPrefab);
            RegisterScrap(TubaItem, 0, LevelTypes.All);
        }

        [HarmonyPatch(typeof(Terminal), "Start")]
        [HarmonyPostfix]
        private static void GetShotgunPrefab()
        {
            foreach (Item item in StartOfRound.Instance.allItemsList.itemsList.Where(item => item.name.ToLower() == "shotgun"))
            {
                ShotgunPrefab = item.spawnPrefab;
                break;
            }
        }

        private void LoadInstrumentAudioClipsAsync(string instrumentName, List<string> audioClipNames)
        {
            foreach (string audioClipName in audioClipNames)
            {
                StartCoroutine(Assets.LoadAudioClipAsync(audioClipName, clip =>
                {
                    if (!_instrumentAudioClips.ContainsKey(instrumentName))
                        _instrumentAudioClips[instrumentName] = [];
                    _instrumentAudioClips[instrumentName].Add(clip);
                    _mls.LogDebug($"{instrumentName} audio clip '{audioClipName}' loaded asynchronously");
                }));
            }
        }

        public static AudioClip GetInstrumentAudioClip(string instrumentName, int index)
        {
            if (_instrumentAudioClips.ContainsKey(instrumentName) && index < _instrumentAudioClips[instrumentName].Count)
                return _instrumentAudioClips[instrumentName][index];
            return null;
        }

        private static Dictionary<string, int> CustomLevelRaritiesStringToDictionary(string raritiesString)
        {
            string[] raritiesArray = raritiesString.Trim().Split(",");
            Dictionary<string, int> raritiesDictionary = new();
            foreach (string rarityEntry in raritiesArray)
            {
                string[] rarity = rarityEntry.Trim().Split(":");
                raritiesDictionary[rarity[0]] = int.Parse(rarity[1]);
            }

            return raritiesDictionary;
        }

        private static void InitializeNetworkStuff()
        {
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (Type type in types)
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (MethodInfo method in methods)
                {
                    object[] attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }
    }

    public class HarpGhostConfig : SyncedInstance<HarpGhostConfig>
    {
        public readonly ConfigEntry<int> HarpGhostInitialHealth;
        public readonly ConfigEntry<int> HarpGhostAttackDamage;
        public readonly ConfigEntry<int> HarpGhostViewRange;
        public readonly ConfigEntry<int> HarpGhostProximityAwareness;
        public readonly ConfigEntry<float> HarpGhostAttackCooldown;
        public readonly ConfigEntry<float> HarpGhostAttackAreaLength;
        public readonly ConfigEntry<float> HarpGhostStunTimeMultiplier;
        public readonly ConfigEntry<float> HarpGhostDoorSpeedMultiplierInChaseMode;
        public readonly ConfigEntry<float> HarpGhostMaxAccelerationInChaseMode;
        public readonly ConfigEntry<float> HarpGhostMaxSpeedInChaseMode;
        public readonly ConfigEntry<float> HarpGhostStunGameDifficultyMultiplier;
        public readonly ConfigEntry<float> HarpGhostAnnoyanceLevelDecayRate;
        public readonly ConfigEntry<float> HarpGhostAnnoyanceThreshold;
        public readonly ConfigEntry<float> HarpGhostMaxSearchRadius;
        public readonly ConfigEntry<float> HarpGhostViewWidth;
        public readonly ConfigEntry<bool> HarpGhostIsStunnable;
        public readonly ConfigEntry<bool> HarpGhostIsKillable;
        public readonly ConfigEntry<bool> HarpGhostCanHearPlayersWhenAngry;
        public readonly ConfigEntry<bool> HarpGhostCanSeeThroughFog;
        public readonly ConfigEntry<bool> HarpGhostFriendlyFire;

        public readonly ConfigEntry<float> HarpGhostVoiceSfxVolume;
        public readonly ConfigEntry<float> HarpVolume;
        public readonly ConfigEntry<bool> HarpBypassReverbZones;
        public readonly ConfigEntry<float> HarpPitch;
        public readonly ConfigEntry<float> HarpReverbZoneMix;
        public readonly ConfigEntry<float> HarpDopplerLevel;
        public readonly ConfigEntry<int> HarpSoundSpread;
        public readonly ConfigEntry<int> HarpSoundMaxDistance;
        public readonly ConfigEntry<bool> HarpAudioLowPassFilterEnabled;
        public readonly ConfigEntry<int> HarpAudioLowPassFilterCutoffFrequency;
        public readonly ConfigEntry<float> HarpAudioLowPassFilterLowpassResonanceQ;
        public readonly ConfigEntry<bool> HarpAudioHighPassFilterEnabled;
        public readonly ConfigEntry<int> HarpAudioHighPassFilterCutoffFrequency;
        public readonly ConfigEntry<float> HarpAudioHighPassFilterHighpassResonanceQ;
        public readonly ConfigEntry<bool> HarpAudioEchoFilterEnabled;
        public readonly ConfigEntry<float> HarpAudioEchoFilterDelay;
        public readonly ConfigEntry<float> HarpAudioEchoFilterDecayRatio;
        public readonly ConfigEntry<float> HarpAudioEchoFilterDryMix;
        public readonly ConfigEntry<float> HarpAudioEchoFilterWetMix;
        public readonly ConfigEntry<bool> HarpAudioChorusFilterEnabled;
        public readonly ConfigEntry<float> HarpAudioChorusFilterDryMix;
        public readonly ConfigEntry<float> HarpAudioChorusFilterWetMix1;
        public readonly ConfigEntry<float> HarpAudioChorusFilterWetMix2;
        public readonly ConfigEntry<float> HarpAudioChorusFilterWetMix3;
        public readonly ConfigEntry<float> HarpAudioChorusFilterDelay;
        public readonly ConfigEntry<float> HarpAudioChorusFilterRate;
        public readonly ConfigEntry<float> HarpAudioChorusFilterDepth;
        public readonly ConfigEntry<bool> HarpOccludeAudioEnabled;
        public readonly ConfigEntry<bool> HarpOccludeAudioUseReverbEnabled;
        public readonly ConfigEntry<bool> HarpOccludeAudioOverridingLowPassEnabled;
        public readonly ConfigEntry<int> HarpOccludeAudioLowPassOverride;
        
        public readonly ConfigEntry<int> HarpGhostSpawnRate;
        public readonly ConfigEntry<int> MaxAmountOfHarpGhosts;
        public readonly ConfigEntry<int> HarpGhostPowerLevel;
        public readonly ConfigEntry<LevelTypes> HarpGhostSpawnLevel;

        public readonly ConfigEntry<bool> HarpGhostAngryEyesEnabled;
        
        public readonly ConfigEntry<int> HarpMinValue;
        public readonly ConfigEntry<int> HarpMaxValue;

        public HarpGhostConfig(ConfigFile cfg)
        {
            InitInstance(this);
            
            HarpGhostInitialHealth = cfg.Bind(
                "Haunted Harpist General",
                "Health",
                4,
                "The health when spawned"
                );
            
            HarpGhostIsKillable = cfg.Bind(
                "Haunted Harpist General",
                "Killable",
                true,
                "Whether the Haunted Harpist can be killed or not"
            );
            
            HarpGhostFriendlyFire = cfg.Bind(
                "Haunted Harpist General",
                "Friendly Fire",
                true,
                "Whether the Haunted Harpist can be killed by something other than a player e.g. an eyeless dog, mine etc"
            );
            
            HarpGhostIsStunnable= cfg.Bind(
                "Haunted Harpist General",
                "Stunnable",
                true,
                "Whether the Haunted Harpist can be stunned or not"
            );
            
            HarpGhostCanHearPlayersWhenAngry = cfg.Bind(
                "Haunted Harpist General",
                "Can Hear Players When Angry",
                true,
                "Whether the Haunted Harpist can hear players to aid its search when angry"
            );
            
            HarpGhostCanSeeThroughFog = cfg.Bind(
                "Haunted Harpist General",
                "Can See Through Fog",
                false,
                "Whether the Haunted Harpist can see through fog"
            );
            
            HarpGhostAttackDamage = cfg.Bind(
                "Haunted Harpist General",
                "Attack Damage",
                45,
                "The attack damage of the Haunted Harpist"
            );
            
            HarpGhostAttackCooldown = cfg.Bind(
                "Haunted Harpist General",
                "Attack Cooldown",
                2f,
                "The max speed of the Haunted Harpist in chase mode. Note that new attacks interrupt the audio and animation of the previous attack, therefore putting this value too low will make the attacks look and sound very jagged."
            );
            
            HarpGhostAttackAreaLength = cfg.Bind(
                "Haunted Harpist General",
                "Attack Area Length",
                1f,
                "The length of the Haunted Harpist's attack area in the Z dimension (in in-game meters)"
            );
            
            HarpGhostMaxSpeedInChaseMode = cfg.Bind(
                "Haunted Harpist General",
                "Max Speed In Chase Mode",
                8f,
                "The max speed of the Haunted Harpist in chase mode"
            );
            
            HarpGhostMaxAccelerationInChaseMode = cfg.Bind(
                "Haunted Harpist General",
                "Max Acceleration In Chase Mode",
                50f,
                "The max acceleration of the Haunted Harpist in chase mode"
            );
            
            HarpGhostViewWidth = cfg.Bind(
                "Haunted Harpist General",
                "View Width",
                135f,
                "The width in degrees of the Haunted Harpist's view"
            );
            
            HarpGhostViewRange = cfg.Bind(
                "Haunted Harpist General",
                "View Range",
                80,
                "The range in in-game units (a meter kind of) of the Haunted Harpist's view"
            );
            
            HarpGhostProximityAwareness = cfg.Bind(
                "Haunted Harpist General",
                "Proximity Awareness",
                3,
                "The area around the Haunted Harpist in in-game units where it can detect players, regardless if the ghost has line of sight to the player. Set it to -1 to completely disable it. I recommend you do not touch this."
            );
            
            HarpGhostDoorSpeedMultiplierInChaseMode = cfg.Bind(
                "Haunted Harpist General",
                "Max Door Speed Multiplier",
                6f,
                "The MAXIMUM multiplier for how long it takes the Haunted Harpist to open a door (maximum because the value changes depending on how angry the ghost is, there is no global value)"
            );
            
            HarpGhostStunTimeMultiplier = cfg.Bind(
                "Haunted Harpist General",
                "Stun Time Multiplier",
                1f,
                "The multiplier for how long a Haunted Harpist can be stunned"
            );
            
            HarpGhostStunGameDifficultyMultiplier = cfg.Bind(
                "Haunted Harpist General",
                "Stun Game Difficulty Multiplier",
                0f,
                "Not sure what this does"
            );
            
            HarpGhostAnnoyanceLevelDecayRate = cfg.Bind(
                "Haunted Harpist General",
                "Annoyance Level Decay Rate",
                0.3f,
                "The decay rate of the Haunted Harpist's annoyance level (due to noises) over time"
            );
            
            HarpGhostAnnoyanceThreshold = cfg.Bind(
                "Haunted Harpist General",
                "Annoyance Level Threshold",
                8f,
                "The threshold of how annoyed the Haunted Harpist has to be (from noise) to get angry"
            );
            
            HarpGhostMaxSearchRadius = cfg.Bind(
                "Haunted Harpist General",
                "Max Search Radius",
                100f,
                "The maximum distance the Haunted Harpist will go to search for a player"
            );
            
            HarpGhostVoiceSfxVolume = cfg.Bind(
                "Ghost Audio",
                "Haunted Harpist Voice Sound Effects Volume",
                0.8f,
                "The volume of the Haunted Harpist's voice. Values are from 0-1"
            );

            HarpVolume = cfg.Bind(
                "Instrument Audio",
                "Harp Volume",
                0.8f,
                "The volume of the music played from the harp. Values are from 0-1"
            );
            
            HarpPitch = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Pitch",
                1f,
                "The pitch of the music played from the harp. Values are from -3 to 3"
            );
            
            HarpBypassReverbZones = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Bypass Reverb Zones",
                false,
                "For the following audio configs, if you don't know what you are doing then DO NOT touch them."
            );
            
            HarpReverbZoneMix = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Reverb Zone Mix",
                1f,
                "Values are from 0 to 1.1"
            );
            
            HarpDopplerLevel = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Doppler Level",
                0.3f,
                "Values are from 0 to 5"
            );
            
            HarpSoundSpread = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Sound Spread",
                80,
                "Values are from 0 to 360"
            );
            
            HarpSoundMaxDistance = cfg.Bind(
                "Instrument Audio",
                "Harp Sound Max Distance",
                45,
                "Values are from 0 to Infinity"
            );
            
            HarpAudioLowPassFilterEnabled = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Low Pass Filter Enabled",
                false,
                ""
            );
            
            HarpAudioLowPassFilterCutoffFrequency = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Low Pass Filter Cutoff Frequency",
                1000,
                "Values are from 10 to 22000"
            );
            
            HarpAudioLowPassFilterLowpassResonanceQ = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Low Pass Filter Lowpass Resonance Q",
                1f,
                ""
            );
            
            HarpAudioHighPassFilterEnabled = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio High Pass Filter Enabled",
                false,
                ""
            );
            
            HarpAudioHighPassFilterCutoffFrequency = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio High Pass Filter Cutoff Frequency",
                600,
                "Values are from 10 to 22000"
            );
            
            HarpAudioHighPassFilterHighpassResonanceQ = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio High Pass Highpass Resonance Q",
                1f,
                ""
            );
            
            HarpAudioEchoFilterEnabled = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Echo Filter Enabled",
                false,
                ""
            );
            
            HarpAudioEchoFilterDelay = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Echo Filter Delay",
                200f,
                ""
            );
            
            HarpAudioEchoFilterDecayRatio = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Echo Filter Decay Ratio",
                0.5f,
                "Values are from 0 to 1"
            );
            
            HarpAudioEchoFilterDryMix = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Echo Filter Dry Mix",
                1f,
                ""
            );
            
            HarpAudioEchoFilterWetMix = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Echo Filter Wet Mix",
                1f,
                ""
            );
            
            HarpAudioChorusFilterEnabled = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Chorus Filter Enabled",
                false,
                ""
            );
            
            HarpAudioChorusFilterDryMix = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Chorus Filter Dry Mix",
                0.5f,
                ""
            );
            
            HarpAudioChorusFilterWetMix1 = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Chorus Filter Wet Mix 1",
                0.5f,
                ""
            );
            
            HarpAudioChorusFilterWetMix2 = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Chorus Filter Wet Mix 2",
                0.5f,
                ""
            );
            
            HarpAudioChorusFilterWetMix3 = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Chorus Filter Wet Mix 3",
                0.5f,
                ""
            );
            
            HarpAudioChorusFilterDelay = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Chorus Filter Delay",
                40f,
                ""
            );
            
            HarpAudioChorusFilterRate = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Chorus Filter Rate",
                0.5f,
                ""
            );
            
            HarpAudioChorusFilterDepth = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Audio Chorus Filter Depth",
                0.2f,
                ""
            );
            
            HarpOccludeAudioEnabled = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Occlude Audio Enabled",
                true,
                ""
            );
            
            HarpOccludeAudioUseReverbEnabled = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Occlude Audio Use Reverb Enabled",
                false,
                ""
            );
            
            HarpOccludeAudioOverridingLowPassEnabled = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Occlude Audio Overriding Low Pass Enabled",
                false,
                ""
            );
            
            HarpOccludeAudioLowPassOverride = cfg.Bind(
                "Harp - Advanced Audio Settings",
                "Harp Occlude Audio Low Pass Override",
                20000,
                ""
            );
            
            HarpGhostSpawnRate = cfg.Bind(
                "Ghost Spawn Values",
                "Haunted Harpist Spawn Value",
                6,
                "The weighted spawn rarity of the Haunted Harpist"
            );
            
            MaxAmountOfHarpGhosts = cfg.Bind(
                "Ghost Spawn Values",
                "Max Amount of Haunted Harpists",
                2,
                "The maximum amount of Haunted Harpist's that can spawn in a game"
            );
            
            HarpGhostPowerLevel = cfg.Bind(
                "Haunted Harpist General",
                "Power Level",
                1,
                "The power level of the Haunted Harpist"
            );
            
            HarpGhostSpawnLevel = cfg.Bind(
                "Ghost Spawn Values",
                "Haunted Harpist Spawn Level",
                LevelTypes.DineLevel,
                "The LevelTypes that the Haunted Harpist spawns in"
            );
            
            HarpMinValue = cfg.Bind(
                "Item Spawn Values",
                "Harp Minimum value",
                150,
                "The minimum value that the harp can be set to"
            );
            
            HarpMaxValue = cfg.Bind(
                "Item Spawn Values",
                "Harp Maximum value",
                300,
                "The maximum value that the harp can be set to"
            );

            HarpGhostAngryEyesEnabled = cfg.Bind(
                "Haunted Harpist General",
                "Angry Eyes Enabled",
                true,
                "Whether the Haunted Harpist's eyes turn red when angry"
            );
        }

        private static void RequestSync() {
            if (!IsClient) return;

            using FastBufferWriter stream = new(IntSize, Allocator.Temp);
            MessageManager.SendNamedMessage($"{HarpGhostPlugin.ModGuid}_OnRequestConfigSync", 0uL, stream);
        }

        private static void OnRequestSync(ulong clientId, FastBufferReader _) {
            if (!IsHost) return;

            Debug.Log($"Config sync request received from client: {clientId}");

            byte[] array = SerializeToBytes(Instance);
            int value = array.Length;

            using FastBufferWriter stream = new(value + IntSize, Allocator.Temp);

            try {
                stream.WriteValueSafe(in value);
                stream.WriteBytesSafe(array);

                MessageManager.SendNamedMessage($"{HarpGhostPlugin.ModGuid}_OnReceiveConfigSync", clientId, stream);
            } catch(Exception e) {
                Debug.Log($"Error occurred syncing config with client: {clientId}\n{e}");
            }
        }

        private static void OnReceiveSync(ulong _, FastBufferReader reader) {
            if (!reader.TryBeginRead(IntSize)) {
                Debug.LogError("Config sync error: Could not begin reading buffer.");
                return;
            }

            reader.ReadValueSafe(out int val);
            if (!reader.TryBeginRead(val)) {
                Debug.LogError("Config sync error: Host could not sync.");
                return;
            }

            byte[] data = new byte[val];
            reader.ReadBytesSafe(ref data, val);

            SyncInstance(data);

            Debug.Log("Successfully synced config with host.");
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        public static void InitializeLocalPlayer() {
            if (IsHost) {
                MessageManager.RegisterNamedMessageHandler($"{HarpGhostPlugin.ModGuid}_OnRequestConfigSync", OnRequestSync);
                Synced = true;

                return;
            }

            Synced = false;
            MessageManager.RegisterNamedMessageHandler($"{HarpGhostPlugin.ModGuid}_OnReceiveConfigSync", OnReceiveSync);
            RequestSync();
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
        public static void PlayerLeave() {
            RevertSync();
        }
    }

    public class BagpipeGhostConfig : SyncedInstance<BagpipeGhostConfig>
    {
        public readonly ConfigEntry<int>BagpipeGhostInitialHealth;
        public readonly ConfigEntry<float> BagpipeGhostStunTimeMultiplier;
        public readonly ConfigEntry<float> BagpipeGhostDoorSpeedMultiplierInEscapeMode;
        public readonly ConfigEntry<float> BagpipeGhostMaxAccelerationInEscapeMode;
        public readonly ConfigEntry<float> BagpipeGhostMaxSpeedInEscapeMode;
        public readonly ConfigEntry<float> BagpipeGhostStunGameDifficultyMultiplier;
        public readonly ConfigEntry<bool> BagpipeGhostIsStunnable;
        public readonly ConfigEntry<bool> BagpipeGhostIsKillable;

        public readonly ConfigEntry<float> BagpipeGhostVoiceSfxVolume;
        public readonly ConfigEntry<float> BagpipesVolume;
        public readonly ConfigEntry<int> BagpipesSoundMaxDistance;
        
        public readonly ConfigEntry<int> BagpipeGhostSpawnRate;
        public readonly ConfigEntry<int> MaxAmountOfBagpipeGhosts;
        public readonly ConfigEntry<int> BagpipeGhostPowerLevel;
        public readonly ConfigEntry<int> BagpipeGhostNumberOfEscortsToSpawn;
        public readonly ConfigEntry<LevelTypes> BagpipeGhostSpawnLevel;
        
        public readonly ConfigEntry<int> BagpipesMinValue;
        public readonly ConfigEntry<int> BagpipesMaxValue;

        public BagpipeGhostConfig(ConfigFile cfg)
        {
            InitInstance(this);
            
            BagpipeGhostInitialHealth = cfg.Bind(
                "Phantom Piper General",
                "Health",
                6,
                "The health of the Phantom Piper when spawned"
                );
            
            BagpipeGhostIsKillable = cfg.Bind(
                "Phantom Piper General",
                "Killable",
                true,
                "Whether a Phantom Piper can be killed or not"
            );
            
            BagpipeGhostIsStunnable= cfg.Bind(
                "Phantom Piper General",
                "Stunnable",
                true,
                "Whether a Phantom Piper can be stunned or not"
            );
            
            BagpipeGhostMaxSpeedInEscapeMode = cfg.Bind(
                "Phantom Piper General",
                "Max Speed In Escape Mode",
                10f,
                "The max speed of the Phantom Piper in escape mode"
            );
            
            BagpipeGhostMaxAccelerationInEscapeMode = cfg.Bind(
                "Phantom Piper General",
                "Max Acceleration In Escape Mode",
                30f,
                "The max acceleration of the Phantom Piper in escape mode"
            );
            
            BagpipeGhostStunTimeMultiplier = cfg.Bind(
                "Phantom Piper General",
                "Stun Time Multiplier",
                1f,
                "The multiplier for how long a Phantom Piper can be stunned"
            );
            
            BagpipeGhostDoorSpeedMultiplierInEscapeMode = cfg.Bind(
                "Phantom Piper General",
                "Door Speed Multiplier In Escape Mode",
                6f,
                "The door speed multiplier when the Phantom Piper is in escape mode"
            );
            
            BagpipeGhostStunGameDifficultyMultiplier = cfg.Bind(
                "Phantom Piper General",
                "Stun Game Difficulty Multiplier",
                0f,
                "Not sure what this does"
            );
            
            BagpipeGhostVoiceSfxVolume = cfg.Bind(
                "Ghost Audio",
                "Phantom Piper Voice Sound Effects Volume",
                0.8f,
                "The volume of the Phantom Piper's voice. Values are from 0-1"
            );
            
            BagpipesVolume = cfg.Bind(
                "Instrument Audio",
                "Bagpipes Volume",
                0.8f,
                "The volume of the music played from the Bagpipes. Values are from 0-1"
            );
            
            BagpipesSoundMaxDistance = cfg.Bind(
                "Instrument Audio",
                "Bagpipes Sound Max Distance",
                65,
                "Values are from 0 to Infinity"
            );
            
            BagpipeGhostSpawnRate = cfg.Bind(
                "Ghost Spawn Values",
                "Phantom Piper Spawn Value",
                1,
                "The weighted spawn rarity of the Phantom Piper."
            );
            
            MaxAmountOfBagpipeGhosts = cfg.Bind(
                "Ghost Spawn Values",
                "Max Amount of Phantom Piper Ghosts",
                1,
                "The maximum amount of Phantom Piper that can spawn in a game"
            );

            BagpipeGhostNumberOfEscortsToSpawn = cfg.Bind(
                "Phantom Piper General",
                "Number of Escorts to Spawn",
                3,
                "The number of escorts to spawn when the Phantom Piper spawns"
            );
            
            BagpipeGhostPowerLevel = cfg.Bind(
                "Phantom Piper General",
                "Power Level",
                1,
                "The power level of a Phantom Piper"
            );
            
            BagpipeGhostSpawnLevel = cfg.Bind(
                "Ghost Spawn Values",
                "Phantom Piper Spawn Level",
                LevelTypes.DineLevel,
                "The LevelTypes that the Phantom Piper spawns in"
            );
            
            BagpipesMinValue = cfg.Bind(
                "Item Spawn Values",
                "Bagpipes Minimum value",
                225,
                "The minimum value that the bagpipes can be set to"
            );
            
            BagpipesMaxValue = cfg.Bind(
                "Item Spawn Values",
                "Bagpipes Maximum value",
                380,
                "The maximum value that the bagpipes can be set to"
            );
        }
        
        private static void RequestSync() {
            if (!IsClient) return;

            using FastBufferWriter stream = new(IntSize, Allocator.Temp);
            MessageManager.SendNamedMessage($"{HarpGhostPlugin.ModGuid}_OnRequestConfigSync", 0uL, stream);
        }

        private static void OnRequestSync(ulong clientId, FastBufferReader _) {
            if (!IsHost) return;

            Debug.Log($"Config sync request received from client: {clientId}");

            byte[] array = SerializeToBytes(Instance);
            int value = array.Length;

            using FastBufferWriter stream = new(value + IntSize, Allocator.Temp);

            try {
                stream.WriteValueSafe(in value);
                stream.WriteBytesSafe(array);

                MessageManager.SendNamedMessage($"{HarpGhostPlugin.ModGuid}_OnReceiveConfigSync", clientId, stream);
            } catch(Exception e) {
                Debug.Log($"Error occurred syncing config with client: {clientId}\n{e}");
            }
        }

        private static void OnReceiveSync(ulong _, FastBufferReader reader) {
            if (!reader.TryBeginRead(IntSize)) {
                Debug.LogError("Config sync error: Could not begin reading buffer.");
                return;
            }

            reader.ReadValueSafe(out int val);
            if (!reader.TryBeginRead(val)) {
                Debug.LogError("Config sync error: Host could not sync.");
                return;
            }

            byte[] data = new byte[val];
            reader.ReadBytesSafe(ref data, val);

            SyncInstance(data);

            Debug.Log("Successfully synced config with host.");
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        public static void InitializeLocalPlayer() {
            if (IsHost) {
                MessageManager.RegisterNamedMessageHandler($"{HarpGhostPlugin.ModGuid}_OnRequestConfigSync", OnRequestSync);
                Synced = true;

                return;
            }

            Synced = false;
            MessageManager.RegisterNamedMessageHandler($"{HarpGhostPlugin.ModGuid}_OnReceiveConfigSync", OnReceiveSync);
            RequestSync();
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
        public static void PlayerLeave() {
            RevertSync();
        }
    }
    
    public class EnforcerGhostConfig : SyncedInstance<EnforcerGhostConfig>
    {
        public readonly ConfigEntry<int> EnforcerGhostInitialHealth;
        public readonly ConfigEntry<float> EnforcerGhostStunTimeMultiplier;
        public readonly ConfigEntry<float> EnforcerGhostDoorSpeedMultiplierInChaseMode;
        public readonly ConfigEntry<float> EnforcerGhostMaxAccelerationInChaseMode;
        public readonly ConfigEntry<float> EnforcerGhostMaxSpeedInChaseMode;
        public readonly ConfigEntry<float> EnforcerGhostStunGameDifficultyMultiplier;
        public readonly ConfigEntry<bool> EnforcerGhostIsStunnable;
        public readonly ConfigEntry<bool> EnforcerGhostIsKillable;
        public readonly ConfigEntry<float> EnforcerGhostTurnSpeed;
        public readonly ConfigEntry<float> EnforcerGhostShootDelay;
        public readonly ConfigEntry<bool> EnforcerGhostShieldEnabled;
        public readonly ConfigEntry<float> EnforcerGhostShieldRegenTime;

        public readonly ConfigEntry<float> EnforcerGhostVoiceSfxVolume;
        public readonly ConfigEntry<float> EnforcerGhostSfxVolume;
        
        public readonly ConfigEntry<int> EnforcerGhostSpawnRate;
        public readonly ConfigEntry<int> MaxAmountOfEnforcerGhosts;
        public readonly ConfigEntry<int> EnforcerGhostPowerLevel;
        public readonly ConfigEntry<LevelTypes> EnforcerGhostSpawnLevel;
        
        public readonly ConfigEntry<int> ShotgunMinValue;
        public readonly ConfigEntry<int> ShotgunMaxValue;

        public EnforcerGhostConfig(ConfigFile cfg)
        {
            InitInstance(this);
            
            EnforcerGhostInitialHealth = cfg.Bind(
                "Ethereal Enforcer General",
                "Health",
                6,
                "The health of an Enforcer ghost when spawned"
                );
            
            EnforcerGhostIsKillable = cfg.Bind(
                "Ethereal Enforcer General",
                "Killable",
                true,
                "Whether an Enforcer Ghost can be killed or not"
            );
            
            EnforcerGhostIsStunnable= cfg.Bind(
                "Ethereal Enforcer General",
                "Stunnable",
                true,
                "Whether an Enforcer Ghost can be stunned or not"
            );
            
            EnforcerGhostMaxSpeedInChaseMode = cfg.Bind(
                "Ethereal Enforcer General",
                "Max Speed In Chase Mode",
                1.5f,
                "The max speed of the Enforcer Ghost in chase mode"
            );
            
            EnforcerGhostMaxAccelerationInChaseMode = cfg.Bind(
                "Ethereal Enforcer General",
                "Max Acceleration In Chase Mode",
                15f,
                "The max acceleration of the Enforcer Ghost in chase mode"
            );

            EnforcerGhostTurnSpeed = cfg.Bind(
                "Ethereal Enforcer General",
                "Turn Speed",
                75f,
                "The turn speed of the Enforcer Ghost"
            );
            
            EnforcerGhostShootDelay = cfg.Bind(
                "Ethereal Enforcer General",
                "Shoot Delay",
                2f,
                "The delay which dictates how long it takes for an Enforcer Ghost to shoot you after it notices you"
            );
            
            EnforcerGhostShieldEnabled = cfg.Bind(
                "Ethereal Enforcer General",
                "Is Shield Enabled",
                true,
                "Whether or not the Enforcer ghost has a shield which can withstand 1 hit (regardless of the damage). When damaged it breaks, and regens after a specified time"
            );
            
            EnforcerGhostShieldRegenTime = cfg.Bind(
                "Ethereal Enforcer General",
                "Shield Regeneration TIme",
                25f,
                "The time it takes for the shield to regenerate after being hit, the ghost being stunned or the ghost disabling the shield to shoot"
            );
            
            EnforcerGhostStunTimeMultiplier = cfg.Bind(
                "Ethereal Enforcer General",
                "Stun Time Multiplier",
                3f,
                "The multiplier for how long a Enforcer Ghost can be stunned"
            );
            
            EnforcerGhostDoorSpeedMultiplierInChaseMode = cfg.Bind(
                "Ethereal Enforcer General",
                "Door Speed Multiplier In Chase Mode",
                1f,
                "The door speed multiplier when the Enforcer ghost is in chase mode"
            );
            
            EnforcerGhostStunGameDifficultyMultiplier = cfg.Bind(
                "Ethereal Enforcer General",
                "Stun Game Difficulty Multiplier",
                0f,
                "Not sure what this does"
            );
            
            EnforcerGhostVoiceSfxVolume = cfg.Bind(
                "Ghost Audio",
                "Enforcer Ghost Voice Sound Effects Volume",
                0.8f,
                "The volume of the Enforcer ghost's voice. Values are from 0-1"
            );
            
            EnforcerGhostSfxVolume = cfg.Bind(
                "Ghost Audio",
                "Enforcer Ghost Sound Effects Volume",
                0.5f,
                "The volume of the Enforcer ghost's sound effects (e.g. shotgun noises, teleport noises etc). Values are from 0-1"
            );
            
            EnforcerGhostSpawnRate = cfg.Bind(
                "Ghost Spawn Values",
                "Ethereal Enforcer Spawn Value",
                0,
                "The weighted spawn rarity of the Enforcer ghost (can be changed, but isn't supposed to spawn by itself)"
            );
            
            MaxAmountOfEnforcerGhosts = cfg.Bind(
                "Ghost Spawn Values",
                "Max Amount of Enforcer Ghosts",
                3,
                "The maximum amount of Enforcer ghosts that can spawn in a game"
            );
            
            EnforcerGhostPowerLevel = cfg.Bind(
                "Ethereal Enforcer General",
                "Power Level",
                1,
                "The power level of a Enforcer Ghost"
            );
            
            EnforcerGhostSpawnLevel = cfg.Bind(
                "Ghost Spawn Values",
                "Spawn Level",
                LevelTypes.DineLevel,
                "The LevelTypes that the Enforcer ghost spawns in"
            );
            
            ShotgunMinValue = cfg.Bind(
                "Item Spawn Values",
                "Shotgun Minimum value",
                60,
                "The minimum value that the shotgun spawned by an enforcer ghost can be set to"
            );
            
            ShotgunMaxValue = cfg.Bind(
                "Item Spawn Values",
                "Shotgun Maximum value",
                90,
                "The maximum value that the shotgun spawned by an enforcer ghost can be set to"
            );
        }
        
        private static void RequestSync() {
            if (!IsClient) return;

            using FastBufferWriter stream = new(IntSize, Allocator.Temp);
            MessageManager.SendNamedMessage($"{HarpGhostPlugin.ModGuid}_OnRequestConfigSync", 0uL, stream);
        }

        private static void OnRequestSync(ulong clientId, FastBufferReader _) {
            if (!IsHost) return;

            Debug.Log($"Config sync request received from client: {clientId}");

            byte[] array = SerializeToBytes(Instance);
            int value = array.Length;

            using FastBufferWriter stream = new(value + IntSize, Allocator.Temp);

            try {
                stream.WriteValueSafe(in value);
                stream.WriteBytesSafe(array);

                MessageManager.SendNamedMessage($"{HarpGhostPlugin.ModGuid}_OnReceiveConfigSync", clientId, stream);
            } catch(Exception e) {
                Debug.Log($"Error occurred syncing config with client: {clientId}\n{e}");
            }
        }

        private static void OnReceiveSync(ulong _, FastBufferReader reader) {
            if (!reader.TryBeginRead(IntSize)) {
                Debug.LogError("Config sync error: Could not begin reading buffer.");
                return;
            }

            reader.ReadValueSafe(out int val);
            if (!reader.TryBeginRead(val)) {
                Debug.LogError("Config sync error: Host could not sync.");
                return;
            }

            byte[] data = new byte[val];
            reader.ReadBytesSafe(ref data, val);

            SyncInstance(data);

            Debug.Log("Successfully synced config with host.");
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        public static void InitializeLocalPlayer() {
            if (IsHost) {
                MessageManager.RegisterNamedMessageHandler($"{HarpGhostPlugin.ModGuid}_OnRequestConfigSync", OnRequestSync);
                Synced = true;

                return;
            }

            Synced = false;
            MessageManager.RegisterNamedMessageHandler($"{HarpGhostPlugin.ModGuid}_OnReceiveConfigSync", OnReceiveSync);
            RequestSync();
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
        public static void PlayerLeave() {
            RevertSync();
        }
    }

    // Got from https://lethal.wiki/dev/intermediate/custom-config-syncing
    [Serializable]
    public class SyncedInstance<T>
    {
        internal static CustomMessagingManager MessageManager => NetworkManager.Singleton.CustomMessagingManager;
        internal static bool IsClient => NetworkManager.Singleton.IsClient;
        internal static bool IsHost => NetworkManager.Singleton.IsHost;
        
        [NonSerialized]
        protected static int IntSize = 4;

        public static T Default { get; private set; }
        public static T Instance { get; private set; }

        public static bool Synced { get; internal set; }

        protected void InitInstance(T instance) {
            Default = instance;
            Instance = instance;
            
            IntSize = sizeof(int);
        }

        internal static void SyncInstance(byte[] data) {
            Instance = DeserializeFromBytes(data);
            Synced = true;
        }

        internal static void RevertSync() {
            Instance = Default;
            Synced = false;
        }

        public static byte[] SerializeToBytes(T val) {
            BinaryFormatter bf = new();
            using MemoryStream stream = new();

            try {
                bf.Serialize(stream, val);
                return stream.ToArray();
            }
            catch (Exception e) {
                Debug.LogError($"Error serializing instance: {e}");
                return null;
            }
        }

        public static T DeserializeFromBytes(byte[] data) {
            BinaryFormatter bf = new();
            using MemoryStream stream = new(data);

            try {
                return (T) bf.Deserialize(stream);
            } catch (Exception e) {
                Debug.LogError($"Error deserializing instance: {e}");
                return default;
            }
        }
    }
    
    internal static class Assets
    {
        private const string MainAssetBundleName = "Assets.harpghostbundle";
        public static AssetBundle MainAssetBundle;

        private static string GetAssemblyName() => Assembly.GetExecutingAssembly().FullName.Split(',')[0];
        public static void PopulateAssets()
        {
            if (MainAssetBundle != null) return;
            using Stream assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetAssemblyName() + "." + MainAssetBundleName);
            MainAssetBundle = AssetBundle.LoadFromStream(assetStream);
        }

        public static IEnumerator LoadAudioClipAsync(string clipName, Action<AudioClip> callback)
        {
            if (MainAssetBundle == null) yield break;

            AssetBundleRequest request = MainAssetBundle.LoadAssetAsync<AudioClip>(clipName);
            yield return request;
            
            AudioClip clip = request.asset as AudioClip;
            callback?.Invoke(clip);
        }
    }
}