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
    public class HarpGhostPlugin : BaseUnityPlugin
    {
        public const string ModGuid = $"LCM_HarpGhost|{ModVersion}";
        private const string ModName = "Lethal Company Harp Ghost Mod";
        private const string ModVersion = "1.2.9";

        private readonly Harmony _harmony = new(ModGuid);
        
        // ReSharper disable once InconsistentNaming
        private static readonly ManualLogSource _mls = BepInEx.Logging.Logger.CreateLogSource(ModGuid);

        private static HarpGhostPlugin _instance;

        // ReSharper disable once InconsistentNaming
        private static readonly Dictionary<string, List<AudioClip>> _instrumentAudioClips = new();
        
        public static HarpGhostConfig HarpGhostConfig { get; internal set; }

        private static EnemyType _harpGhostEnemyType;
        private static EnemyType _bagpipesGhostEnemyType;
        internal static EnemyType EnforcerGhostEnemyType;

        public static Item HarpItem;
        public static Item BagpipesItem;
        private static Item TubaItem;
        
        public static GameObject ShotgunPrefab;
        public static RuntimeAnimatorController CustomShotgunAnimator;

        public static ParticleSystem BeamOutParticle;

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

        private void SetupHarpGhost()
        {
            _harpGhostEnemyType = Assets.MainAssetBundle.LoadAsset<EnemyType>("HarpGhost");
            _harpGhostEnemyType.canDie = HarpGhostConfig.Instance.HarpGhostIsKillable.Value;
            _harpGhostEnemyType.PowerLevel = HarpGhostConfig.Instance.HarpGhostPowerLevel.Value;
            _harpGhostEnemyType.canBeStunned = HarpGhostConfig.Instance.HarpGhostIsStunnable.Value;
            _harpGhostEnemyType.MaxCount = HarpGhostConfig.Instance.MaxAmountOfHarpGhosts.Value;
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
        
        private void SetupBagpipesGhost()
        {
            _bagpipesGhostEnemyType = Assets.MainAssetBundle.LoadAsset<EnemyType>("BagpipesGhost");
            TerminalNode harpGhostTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("HarpGhostTN");
            TerminalKeyword harpGhostTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("HarpGhostTK");
            
            NetworkPrefabs.RegisterNetworkPrefab(_bagpipesGhostEnemyType.enemyPrefab);
            Utilities.FixMixerGroups(_bagpipesGhostEnemyType.enemyPrefab);
            RegisterEnemy(_bagpipesGhostEnemyType, Mathf.Clamp(HarpGhostConfig.Instance.HarpGhostSpawnRate.Value, 0, 999), HarpGhostConfig.Instance.HarpGhostSpawnLevel.Value, SpawnType.Default, harpGhostTerminalNode, harpGhostTerminalKeyword);
        }
        
        private void SetupEnforcerGhost()
        {
            EnforcerGhostEnemyType = Assets.MainAssetBundle.LoadAsset<EnemyType>("EnforcerGhost");
            TerminalNode harpGhostTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("HarpGhostTN");
            TerminalKeyword harpGhostTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("HarpGhostTK");
            
            NetworkPrefabs.RegisterNetworkPrefab(EnforcerGhostEnemyType.enemyPrefab);
            Utilities.FixMixerGroups(EnforcerGhostEnemyType.enemyPrefab);
            RegisterEnemy(EnforcerGhostEnemyType, 0, HarpGhostConfig.Instance.HarpGhostSpawnLevel.Value, SpawnType.Default, harpGhostTerminalNode, harpGhostTerminalKeyword);

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

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.Start))]
        [HarmonyPostfix]
        private static void GetBeamOutParticleSystem(PlayerControllerB __instance)
        {
            if (BeamOutParticle != null) return;
            
            BeamOutParticle = __instance.beamOutParticle;
            _mls.LogInfo($"emission module enabled: {BeamOutParticle.emission.enabled}");
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
                "General",
                "Health",
                3,
                "The health when spawned"
                );
            
            HarpGhostIsKillable = cfg.Bind(
                "General",
                "Killable",
                true,
                "Whether a Harp Ghost can be killed or not"
            );
            
            HarpGhostFriendlyFire = cfg.Bind(
                "General",
                "Friendly Fire",
                true,
                "Whether a Harp Ghost can be killed by something other than a player e.g. an eyeless dog, mine etc"
            );
            
            HarpGhostIsStunnable= cfg.Bind(
                "General",
                "Stunnable",
                true,
                "Whether a Harp Ghost can be stunned or not"
            );
            
            HarpGhostCanHearPlayersWhenAngry = cfg.Bind(
                "General",
                "Can Hear Players When Angry",
                true,
                "Whether a Harp Ghost can hear players to aid its search when angry"
            );
            
            HarpGhostCanSeeThroughFog = cfg.Bind(
                "General",
                "Can See Through Fog",
                false,
                "Whether a Harp Ghost can see through fog"
            );
            
            HarpGhostAttackDamage = cfg.Bind(
                "General",
                "Attack Damage",
                45,
                "The attack damage of the Harp Ghost"
            );
            
            HarpGhostAttackCooldown = cfg.Bind(
                "General",
                "Attack Cooldown",
                2f,
                "The max speed of the Harp Ghost in chase mode. Note that new attacks interrupt the audio and animation of the previous attack, therefore putting this value too low will make the attacks look and sound very jagged."
            );
            
            HarpGhostAttackAreaLength = cfg.Bind(
                "General",
                "Attack Area Length",
                1f,
                "The length of the Harp Ghost's attack area in the Z dimension (in in-game meters)"
            );
            
            HarpGhostMaxSpeedInChaseMode = cfg.Bind(
                "General",
                "Max Speed In Chase Mode",
                8f,
                "The max speed of the Harp Ghost in chase mode"
            );
            
            HarpGhostMaxAccelerationInChaseMode = cfg.Bind(
                "General",
                "Max Acceleration In Chase Mode",
                50f,
                "The max acceleration of the Harp Ghost in chase mode"
            );
            
            HarpGhostViewWidth = cfg.Bind(
                "General",
                "View Width",
                135f,
                "The width in degrees of the Harp Ghost's view"
            );
            
            HarpGhostViewRange = cfg.Bind(
                "General",
                "View Range",
                80,
                "The range in in-game units (a meter kind of) of the Harp Ghost's view"
            );
            
            HarpGhostProximityAwareness = cfg.Bind(
                "General",
                "Proximity Awareness",
                3,
                "The area around the ghost in in-game units where it can detect players, regardless if the Harp Ghost has line of sight to the player. Set it to -1 to completely disable it. I recommend you do not touch this."
            );
            
            HarpGhostStunTimeMultiplier = cfg.Bind(
                "General",
                "Stun Time Multiplier",
                1f,
                "The multiplier for how long a Harp Ghost can be stunned"
            );
            
            HarpGhostDoorSpeedMultiplierInChaseMode = cfg.Bind(
                "General",
                "Max Door Speed Multiplier",
                6f,
                "The MAXIMUM multiplier for how long it takes a Harp Ghost to open a door (maximum because the value changes depending on how angry the ghost is, there is no global value)"
            );
            
            HarpGhostStunTimeMultiplier = cfg.Bind(
                "General",
                "Stun Time Multiplier",
                1f,
                "The multiplier for how long a Harp Ghost can be stunned"
            );
            
            HarpGhostStunGameDifficultyMultiplier = cfg.Bind(
                "General",
                "Stun Game Difficulty Multiplier",
                0f,
                "Not sure what this does"
            );
            
            HarpGhostAnnoyanceLevelDecayRate = cfg.Bind(
                "General",
                "Annoyance Level Decay Rate",
                0.3f,
                "The decay rate of the ghost's annoyance level (due to noises) over time"
            );
            
            HarpGhostAnnoyanceThreshold = cfg.Bind(
                "General",
                "Annoyance Level Threshold",
                8f,
                "The threshold of how annoyed the ghost has to be (from noise) to get angry"
            );
            
            HarpGhostMaxSearchRadius = cfg.Bind(
                "General",
                "Max Search Radius",
                100f,
                "The maximum distance the ghost will go to search for a player"
            );
            
            HarpGhostVoiceSfxVolume = cfg.Bind(
                "Audio",
                "Voice Sound Effects Volume",
                0.8f,
                "The volume of the ghost's voice. Values are from 0-1"
            );

            HarpVolume = cfg.Bind(
                "Audio",
                "Harp Volume",
                0.8f,
                "The volume of the music played from the harp. Values are from 0-1"
            );
            
            HarpPitch = cfg.Bind(
                "Audio",
                "Harp Pitch",
                1f,
                "The pitch of the music played from the harp. Values are from -3 to 3"
            );
            
            HarpBypassReverbZones = cfg.Bind(
                "Audio",
                "Harp Bypass Reverb Zones",
                false,
                "For the following audio configs, if you don't know what you are doing then DO NOT touch them."
            );
            
            HarpReverbZoneMix = cfg.Bind(
                "Audio",
                "Harp Reverb Zone Mix",
                1f,
                "Values are from 0 to 1.1"
            );
            
            HarpDopplerLevel = cfg.Bind(
                "Audio",
                "Harp Doppler Level",
                0.3f,
                "Values are from 0 to 5"
            );
            
            HarpSoundSpread = cfg.Bind(
                "Audio",
                "Harp Sound Spread",
                80,
                "Values are from 0 to 360"
            );
            
            HarpSoundMaxDistance = cfg.Bind(
                "Audio",
                "Harp Sound Max Distance",
                45,
                "Values are from 0 to Infinity"
            );
            
            HarpAudioLowPassFilterEnabled = cfg.Bind(
                "Audio",
                "Harp Audio Low Pass Filter Enabled",
                false,
                ""
            );
            
            HarpAudioLowPassFilterCutoffFrequency = cfg.Bind(
                "Audio",
                "Harp Audio Low Pass Filter Cutoff Frequency",
                1000,
                "Values are from 10 to 22000"
            );
            
            HarpAudioLowPassFilterLowpassResonanceQ = cfg.Bind(
                "Audio",
                "Harp Audio Low Pass Filter Lowpass Resonance Q",
                1f,
                ""
            );
            
            HarpAudioHighPassFilterEnabled = cfg.Bind(
                "Audio",
                "Harp Audio High Pass Filter Enabled",
                false,
                ""
            );
            
            HarpAudioHighPassFilterCutoffFrequency = cfg.Bind(
                "Audio",
                "Harp Audio High Pass Filter Cutoff Frequency",
                600,
                "Values are from 10 to 22000"
            );
            
            HarpAudioHighPassFilterHighpassResonanceQ = cfg.Bind(
                "Audio",
                "Harp Audio High Pass Highpass Resonance Q",
                1f,
                ""
            );
            
            HarpAudioEchoFilterEnabled = cfg.Bind(
                "Audio",
                "Harp Audio Echo Filter Enabled",
                false,
                ""
            );
            
            HarpAudioEchoFilterDelay = cfg.Bind(
                "Audio",
                "Harp Audio Echo Filter Delay",
                200f,
                ""
            );
            
            HarpAudioEchoFilterDecayRatio = cfg.Bind(
                "Audio",
                "Harp Audio Echo Filter Decay Ratio",
                0.5f,
                "Values are from 0 to 1"
            );
            
            HarpAudioEchoFilterDryMix = cfg.Bind(
                "Audio",
                "Harp Audio Echo Filter Dry Mix",
                1f,
                ""
            );
            
            HarpAudioEchoFilterWetMix = cfg.Bind(
                "Audio",
                "Harp Audio Echo Filter Wet Mix",
                1f,
                ""
            );
            
            HarpAudioChorusFilterEnabled = cfg.Bind(
                "Audio",
                "Harp Audio Chorus Filter Enabled",
                false,
                ""
            );
            
            HarpAudioChorusFilterDryMix = cfg.Bind(
                "Audio",
                "Harp Audio Chorus Filter Dry Mix",
                0.5f,
                ""
            );
            
            HarpAudioChorusFilterWetMix1 = cfg.Bind(
                "Audio",
                "Harp Audio Chorus Filter Wet Mix 1",
                0.5f,
                ""
            );
            
            HarpAudioChorusFilterWetMix2 = cfg.Bind(
                "Audio",
                "Harp Audio Chorus Filter Wet Mix 2",
                0.5f,
                ""
            );
            
            HarpAudioChorusFilterWetMix3 = cfg.Bind(
                "Audio",
                "Harp Audio Chorus Filter Wet Mix 3",
                0.5f,
                ""
            );
            
            HarpAudioChorusFilterDelay = cfg.Bind(
                "Audio",
                "Harp Audio Chorus Filter Delay",
                40f,
                ""
            );
            
            HarpAudioChorusFilterRate = cfg.Bind(
                "Audio",
                "Harp Audio Chorus Filter Rate",
                0.5f,
                ""
            );
            
            HarpAudioChorusFilterDepth = cfg.Bind(
                "Audio",
                "Harp Audio Chorus Filter Depth",
                0.2f,
                ""
            );
            
            HarpOccludeAudioEnabled = cfg.Bind(
                "Audio",
                "Harp Occlude Audio Enabled",
                true,
                ""
            );
            
            HarpOccludeAudioUseReverbEnabled = cfg.Bind(
                "Audio",
                "Harp Occlude Audio Use Reverb Enabled",
                false,
                ""
            );
            
            HarpOccludeAudioOverridingLowPassEnabled = cfg.Bind(
                "Audio",
                "Harp Occlude Audio Overriding Low Pass Enabled",
                false,
                ""
            );
            
            HarpOccludeAudioLowPassOverride = cfg.Bind(
                "Audio",
                "Harp Occlude Audio Low Pass Override",
                20000,
                ""
            );
            
            HarpGhostSpawnRate = cfg.Bind(
                "Spawn Values",
                "Spawn Value",
                40,
                "The weighted spawn rarity of the harp ghost"
            );
            
            MaxAmountOfHarpGhosts = cfg.Bind(
                "Spawn Values",
                "Max Amount of Harp Ghosts",
                2,
                "The maximum amount of harp ghosts that can spawn in a game"
            );
            
            HarpGhostPowerLevel = cfg.Bind(
                "Spawn Values",
                "Power Level",
                1,
                "The power level of a Harp Ghost"
            );
            
            HarpGhostSpawnLevel = cfg.Bind(
                "Spawn Values",
                "Spawn Level",
                LevelTypes.DineLevel,
                "The LevelTypes that the ghost spawns in"
            );
            
            HarpMinValue = cfg.Bind(
                "Spawn Values",
                "Harp Minimum value",
                150,
                "The minimum value that the harp can be set to"
            );
            
            HarpMaxValue = cfg.Bind(
                "Spawn Values",
                "Harp Maximum value",
                300,
                "The maximum value that the harp can be set to"
            );

            HarpGhostAngryEyesEnabled = cfg.Bind(
                "Cosmetics",
                "Angry Eyes Enabled",
                true,
                "Whether the Harp Ghost's eyes turn red when angry"
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

    // public class BagpipeGhostConfig : SyncedInstance<BagpipeGhostConfig>
    // {
    //     public readonly ConfigEntry<bool> EnableBagpipePlayerBuffs;
    // }

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