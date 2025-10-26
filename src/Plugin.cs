using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using LethalCompanyHarpGhost.BagpipesGhost;
using LethalCompanyHarpGhost.EnforcerGhost;
using LethalCompanyHarpGhost.HarpGhost;
using LethalCompanyHarpGhost.Items;
using LethalLib;
using LethalLib.Modules;
using LobbyCompatibility.Enums;
using LobbyCompatibility.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using static LethalLib.Modules.Levels;
using static LethalLib.Modules.Enemies;
using static LethalLib.Modules.Items;
using NetworkPrefabs = LethalLib.Modules.NetworkPrefabs;

namespace LethalCompanyHarpGhost;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(Plugin.ModGUID)]
[BepInDependency("linkoid-DissonanceLagFix-1.0.0", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("BMX.LobbyCompatibility", BepInDependency.DependencyFlags.SoftDependency)]
public class HarpGhostPlugin : BaseUnityPlugin
{
    public const string ModGuid = $"HauntedHarpist|{MyPluginInfo.PLUGIN_VERSION}";
    private const string ModName = "Lethal Company Haunted Harpist Mod";

    private readonly Harmony _harmony = new(ModGuid);

    private static readonly ManualLogSource Mls = BepInEx.Logging.Logger.CreateLogSource(ModGuid);

    private static HarpGhostPlugin _instance;

    private static readonly Dictionary<string, List<AudioClip>> InstrumentAudioClips = new();

    public static HarpGhostConfig HarpGhostConfig { get; internal set; }
    public static BagpipeGhostConfig BagpipeGhostConfig { get; internal set; }
    public static EnforcerGhostConfig EnforcerGhostConfig { get; internal set; }

    private static EnemyType _harpGhostEnemyType;
    private static EnemyType _bagpipesGhostEnemyType;
    internal static EnemyType EnforcerGhostEnemyType;

    public static Item HarpItem;
    public static Item BagpipesItem;
    private static Item TubaItem;
    private static Item _plushieItem;

    public static GameObject ShotgunPrefab;
    public static RuntimeAnimatorController CustomShotgunAnimator;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        if (LobbyCompatibilityChecker.Enabled) LobbyCompatibilityChecker.Init();

        InitializeNetworkStuff();

        Assets.LoadAssetBundle("harpghostbundle");
        if (!Assets.MainAssetBundle)
        {
            Mls.LogError("MainAssetBundle is null");
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
        // SetupTuba();
        SetupPlushie();

        _harmony.PatchAll();
        _harmony.PatchAll(typeof(HarpGhostPlugin));
        Mls.LogInfo($"Plugin {ModName} is loaded!");
    }

    // private void OnDisable()
    // {
    //     Assets.MainAssetBundle.Unload(false);
    //     Mls.LogDebug($"Unloaded assetbundles.");
    // }

    private void SetupHarpGhost()
    {
        _harpGhostEnemyType = Assets.MainAssetBundle.LoadAsset<EnemyType>("HarpGhost");
        _harpGhostEnemyType.canDie = HarpGhostConfig.Instance.HarpGhostIsKillable.Value;
        _harpGhostEnemyType.PowerLevel = HarpGhostConfig.Instance.HarpGhostPowerLevel.Value;
        _harpGhostEnemyType.canBeStunned = HarpGhostConfig.Instance.HarpGhostIsStunnable.Value;
        _harpGhostEnemyType.MaxCount = HarpGhostConfig.Instance.MaxAmountOfHarpGhosts.Value;
        _harpGhostEnemyType.stunTimeMultiplier = HarpGhostConfig.Instance.HarpGhostStunTimeMultiplier.Value;
        _harpGhostEnemyType.stunGameDifficultyMultiplier =
            HarpGhostConfig.Instance.HarpGhostStunGameDifficultyMultiplier.Value;
        _harpGhostEnemyType.canSeeThroughFog = HarpGhostConfig.Instance.HarpGhostCanSeeThroughFog.Value;

        TerminalNode harpGhostTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("HarpGhostTN");
        TerminalKeyword harpGhostTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("HarpGhostTK");

        NetworkPrefabs.RegisterNetworkPrefab(_harpGhostEnemyType.enemyPrefab);
        Utilities.FixMixerGroups(_harpGhostEnemyType.enemyPrefab);
        RegisterEnemyWithConfig(HarpGhostConfig.Instance.HarpGhostEnabled.Value,
            HarpGhostConfig.Instance.HarpGhostSpawnRarity.Value, _harpGhostEnemyType, harpGhostTerminalNode,
            harpGhostTerminalKeyword);
    }

    private void SetupBagpipesGhost()
    {
        _bagpipesGhostEnemyType = Assets.MainAssetBundle.LoadAsset<EnemyType>("BagpipesGhost");
        _bagpipesGhostEnemyType.canDie = BagpipeGhostConfig.Instance.BagpipeGhostIsKillable.Value;
        _bagpipesGhostEnemyType.PowerLevel = BagpipeGhostConfig.Instance.BagpipeGhostPowerLevel.Value;
        _bagpipesGhostEnemyType.canBeStunned = BagpipeGhostConfig.Instance.BagpipeGhostIsStunnable.Value;
        _bagpipesGhostEnemyType.MaxCount = BagpipeGhostConfig.Instance.MaxAmountOfBagpipeGhosts.Value;
        _bagpipesGhostEnemyType.stunTimeMultiplier = BagpipeGhostConfig.Instance.BagpipeGhostStunTimeMultiplier.Value;
        _bagpipesGhostEnemyType.stunGameDifficultyMultiplier =
            BagpipeGhostConfig.Instance.BagpipeGhostStunGameDifficultyMultiplier.Value;

        TerminalNode bagpipeGhostTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("BagpipeGhostTN");
        TerminalKeyword bagpipeGhostTerminalKeyword =
            Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("BagpipeGhostTK");

        NetworkPrefabs.RegisterNetworkPrefab(_bagpipesGhostEnemyType.enemyPrefab);
        Utilities.FixMixerGroups(_bagpipesGhostEnemyType.enemyPrefab);
        RegisterEnemyWithConfig(BagpipeGhostConfig.Instance.BagpipeGhostEnabled.Value,
            BagpipeGhostConfig.Instance.BagpipeGhostSpawnRarity.Value, _bagpipesGhostEnemyType,
            bagpipeGhostTerminalNode, bagpipeGhostTerminalKeyword);
    }

    private void SetupEnforcerGhost()
    {
        EnforcerGhostEnemyType = Assets.MainAssetBundle.LoadAsset<EnemyType>("EnforcerGhost");
        EnforcerGhostEnemyType.canDie = EnforcerGhostConfig.Instance.EnforcerGhostIsKillable.Value;
        EnforcerGhostEnemyType.PowerLevel = EnforcerGhostConfig.Instance.EnforcerGhostPowerLevel.Value;
        EnforcerGhostEnemyType.canBeStunned = EnforcerGhostConfig.Instance.EnforcerGhostIsStunnable.Value;
        EnforcerGhostEnemyType.MaxCount = EnforcerGhostConfig.Instance.MaxAmountOfEnforcerGhosts.Value;
        EnforcerGhostEnemyType.stunTimeMultiplier = EnforcerGhostConfig.Instance.EnforcerGhostStunTimeMultiplier.Value;
        EnforcerGhostEnemyType.stunGameDifficultyMultiplier =
            EnforcerGhostConfig.Instance.EnforcerGhostStunGameDifficultyMultiplier.Value;

        TerminalNode enforcerGhostTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("EnforcerGhostTN");
        TerminalKeyword enforcerGhostTerminalKeyword =
            Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("EnforcerGhostTK");

        NetworkPrefabs.RegisterNetworkPrefab(EnforcerGhostEnemyType.enemyPrefab);
        Utilities.FixMixerGroups(EnforcerGhostEnemyType.enemyPrefab);
        RegisterEnemyWithConfig(EnforcerGhostConfig.Instance.EnforcerGhostEnabled.Value,
            EnforcerGhostConfig.Instance.EnforcerGhostSpawnRarity.Value, EnforcerGhostEnemyType,
            enforcerGhostTerminalNode, enforcerGhostTerminalKeyword);

        CustomShotgunAnimator = Assets.MainAssetBundle.LoadAsset<RuntimeAnimatorController>("AnimatorShotgun");
        if (CustomShotgunAnimator == null) Mls.LogError("custom shotgun animator is null");
    }

    private void SetupHarp()
    {
        HarpItem = Assets.MainAssetBundle.LoadAsset<Item>("HarpItemData");
        if (!HarpItem)
        {
            Mls.LogError("Failed to load HarpItemData from AssetBundle.");
            return;
        }

        if (!HarpItem.spawnPrefab)
        {
            Mls.LogError("Failed to load spawnPrefab from HarpItemData.");
            return;
        }

        NetworkPrefabs.RegisterNetworkPrefab(HarpItem.spawnPrefab);
        Utilities.FixMixerGroups(HarpItem.spawnPrefab);

        // Async load audio clips to get rid of lag spike
        InstrumentBehaviour harpBehaviour = HarpItem.spawnPrefab.GetComponent<InstrumentBehaviour>();
        if (!harpBehaviour)
        {
            Mls.LogError("Failed to load harp item behaviour script from harp spawnPrefab.");
            return;
        }

        AudioClip[] audioClips = harpBehaviour.instrumentAudioClips;
        List<string> audioClipNames = [];
        audioClipNames.AddRange(audioClips.Select(curAudioClip => curAudioClip.name));
        LoadInstrumentAudioClipsAsync(HarpItem.itemName, audioClipNames);

        RegisterScrap(HarpItem, 0, LevelTypes.All);
    }

    private void SetupBagpipes()
    {
        BagpipesItem = Assets.MainAssetBundle.LoadAsset<Item>("BagpipesItemData");
        if (!BagpipesItem)
        {
            Mls.LogError("Failed to load BagpipesItemData from AssetBundle");
            return;
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
        if (!TubaItem)
        {
            Mls.LogError("Failed to load TubaItemData from AssetBundle");
            return;
        }

        NetworkPrefabs.RegisterNetworkPrefab(TubaItem.spawnPrefab);
        Utilities.FixMixerGroups(TubaItem.spawnPrefab);
        RegisterScrap(TubaItem, 0, LevelTypes.All);
    }

    private static void SetupPlushie()
    {
        _plushieItem = Assets.MainAssetBundle.LoadAsset<Item>("GhostPlushieItemData");
        if (!_plushieItem)
        {
            Mls.LogError("Failed to load GhostPlushieItemData from AssetBundle");
            return;
        }

        _plushieItem.minValue = Mathf.Clamp(HarpGhostConfig.Instance.PlushieMinValue.Value, 0, int.MaxValue);
        _plushieItem.maxValue = Mathf.Clamp(HarpGhostConfig.Instance.PlushieMaxValue.Value, 0, int.MaxValue);

        NetworkPrefabs.RegisterNetworkPrefab(_plushieItem.spawnPrefab);
        Utilities.FixMixerGroups(_plushieItem.spawnPrefab);
        RegisterScrap(_plushieItem, Mathf.Clamp(HarpGhostConfig.Instance.PlushieSpawnRate.Value, 0, int.MaxValue),
            HarpGhostConfig.Instance.PlushieSpawnLevel.Value);
    }

    [HarmonyPatch(typeof(Terminal), "Start")]
    [HarmonyPostfix]
    private static void GetShotgunPrefab()
    {
        for (int i = 0; i < StartOfRound.Instance.allItemsList.itemsList.Count; i++)
        {
            Item item = StartOfRound.Instance.allItemsList.itemsList[i];
            if (item.name.ToLower() != "shotgun") continue;

            ShotgunPrefab = item.spawnPrefab;
            break;
        }
    }

    private void LoadInstrumentAudioClipsAsync(string instrumentName, List<string> audioClipNames)
    {
        for (int i = 0; i < audioClipNames.Count; i++)
        {
            string audioClipName = audioClipNames[i];
            StartCoroutine(Assets.LoadAudioClipAsync(audioClipName, clip =>
            {
                if (!InstrumentAudioClips.ContainsKey(instrumentName))
                    InstrumentAudioClips[instrumentName] = [];
                InstrumentAudioClips[instrumentName].Add(clip);
                Mls.LogDebug($"{instrumentName} audio clip '{audioClipName}' loaded asynchronously");
            }));
        }
    }

    public static AudioClip GetInstrumentAudioClip(string instrumentName, int index)
    {
        if (InstrumentAudioClips.ContainsKey(instrumentName) && index < InstrumentAudioClips[instrumentName].Count)
            return InstrumentAudioClips[instrumentName][index];
        return null;
    }

    private static void RegisterEnemyWithConfig(bool enemyEnabled, string configMoonRarity, EnemyType enemy,
        TerminalNode terminalNode, TerminalKeyword terminalKeyword)
    {
        if (enemyEnabled)
        {
            (Dictionary<LevelTypes, int> spawnRateByLevelType, Dictionary<string, int> spawnRateByCustomLevelType) =
                ConfigParsing(configMoonRarity);
            RegisterEnemy(enemy, spawnRateByLevelType, spawnRateByCustomLevelType, terminalNode, terminalKeyword);
        }
        else
        {
            RegisterEnemy(enemy, 0, LevelTypes.All, terminalNode, terminalKeyword);
        }
    }

    private static (Dictionary<LevelTypes, int> spawnRateByLevelType, Dictionary<string, int> spawnRateByCustomLevelType)
        ConfigParsing(string configMoonRarity)
    {
        Dictionary<LevelTypes, int> spawnRateByLevelType = new();
        Dictionary<string, int> spawnRateByCustomLevelType = new();

        string[] ses = configMoonRarity.Split(',');
        for (int i = 0; i < ses.Length; i++)
        {
            string s = ses[i];
            string entry = s.Trim();
            string[] entryParts = entry.Split(':');

            if (entryParts.Length != 2)
                continue;
            string name = entryParts[0];

            if (!int.TryParse(entryParts[1], out int spawnrate))
                continue;

            if (Enum.TryParse(name, true, out LevelTypes levelType))
            {
                spawnRateByLevelType[levelType] = spawnrate;
            }
            else
            {
                // Try appending "Level" to the name and re-attempt parsing
                string modifiedName = name + "Level";
                if (Enum.TryParse(modifiedName, true, out levelType))
                {
                    spawnRateByLevelType[levelType] = spawnrate;
                }
                else
                {
                    spawnRateByCustomLevelType[name] = spawnrate;
                }
            }
        }

        return (spawnRateByLevelType, spawnRateByCustomLevelType);
    }

    private static void InitializeNetworkStuff()
    {
        IEnumerable<Type> types;
        try
        {
            types = Assembly.GetExecutingAssembly().GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(t => t != null);
        }

        foreach (Type type in types)
        {
            MethodInfo[] methods =
                type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                object[] attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0)
                {
                    method.Invoke(null, null);
                }
            }
        }
    }
}

internal static class LobbyCompatibilityChecker
{
    internal static bool Enabled => Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility");

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    internal static void Init()
    {
        PluginHelper.RegisterPlugin(MyPluginInfo.PLUGIN_GUID, Version.Parse(MyPluginInfo.PLUGIN_VERSION),
            CompatibilityLevel.Everyone, VersionStrictness.Patch);
    }
}