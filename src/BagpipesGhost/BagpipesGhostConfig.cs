using BepInEx.Configuration;

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
    public readonly ConfigEntry<float> BagpipesPitch;
    
    public readonly ConfigEntry<bool> BagpipeGhostEnabled;
    public readonly ConfigEntry<string> BagpipeGhostSpawnRarity;
    public readonly ConfigEntry<float> BagpipeGhostPowerLevel;
    public readonly ConfigEntry<int> MaxAmountOfBagpipeGhosts;
    public readonly ConfigEntry<int> BagpipeGhostNumberOfEscortsToSpawn;
    
    public readonly ConfigEntry<int> BagpipesMinValue;
    public readonly ConfigEntry<int> BagpipesMaxValue;

    public BagpipeGhostConfig(ConfigFile cfg)
    {
        InitInstance(this);
        
        BagpipeGhostEnabled = cfg.Bind(
            "Ghost Spawn Values",
            "Phantom Piper Enabled",
            true,
            "Whether the Phantom Piper is enabled (will spawn in games)."
        );
        
        BagpipeGhostSpawnRarity = cfg.Bind(
            "Ghost Spawn Values", 
            "Phantom Piper Spawn Rarity",
            "Modded:5,ExperimentationLevel:0,AssuranceLevel:0,VowLevel:2,OffenseLevel:1,MarchLevel:2,RendLevel:75,DineLevel:75,TitanLevel:25,Adamance:10,Embrion:0,Artifice:10,Auralis:0,Atlantica:0,Acidir:100,Cosmocos:5,Asteroid:2,Desolation:5,Etern:0,Gloom:5,Gratar:0,Infernis:0,Junic:2,Oldred:0,Polarus:0,Seichi:4,Bozoros:5",
            "Spawn weight of the Phantom Piper on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config)."
        );
        
        BagpipeGhostPowerLevel = cfg.Bind(
            "Ghost Spawn Values",
            "Phantom Piper Power Level",
            1f,
            "The power level of the Phantom Piper."
        );
        
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
        
        BagpipesPitch = cfg.Bind(
            "Instrument Audio",
            "Bagpipes Pitch",
            0.8f,
            "The pitch of the music played from the bagpipes. Values are from -3 to 3"
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