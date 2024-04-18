using BepInEx.Configuration;
using LethalLib.Modules;

namespace LethalCompanyHarpGhost.BagpipesGhost;

public class BagpipeGhostConfig : SyncedInstance<BagpipeGhostConfig>
{
    public readonly ConfigEntry<int> BagpipeGhostInitialHealth;
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
    public readonly ConfigEntry<int> BagpipeGhostNumberOfEscortsToSpawn;
    public readonly ConfigEntry<Levels.LevelTypes> BagpipeGhostSpawnLevel;
    
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
        
        BagpipeGhostSpawnLevel = cfg.Bind(
            "Ghost Spawn Values",
            "Phantom Piper Spawn Level",
            Levels.LevelTypes.RendLevel,
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
}