using BepInEx.Configuration;
using LethalLib.Modules;

namespace LethalCompanyHarpGhost.HarpGhost;

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

    public readonly ConfigEntry<bool> HarpGhostEnabled;
    public readonly ConfigEntry<string> HarpGhostSpawnRarity;
    public readonly ConfigEntry<float> HarpGhostPowerLevel;
    public readonly ConfigEntry<int> MaxAmountOfHarpGhosts;

    public readonly ConfigEntry<bool> HarpGhostAngryEyesEnabled;
    
    public readonly ConfigEntry<int> HarpMinValue;
    public readonly ConfigEntry<int> HarpMaxValue;
    public readonly ConfigEntry<int> PlushieMinValue;
    public readonly ConfigEntry<int> PlushieMaxValue;
    public readonly ConfigEntry<int> PlushieSpawnRate;
    public readonly ConfigEntry<Levels.LevelTypes> PlushieSpawnLevel;

    public HarpGhostConfig(ConfigFile cfg)
    {
        InitInstance(this);
        
        HarpGhostEnabled = cfg.Bind(
            "Ghost Spawn Values",
            "Haunted Harpist Enabled",
            true,
            "Whether the Haunted Harpist is enabled (will spawn in games)."
        );
        
        HarpGhostSpawnRarity = cfg.Bind(
            "Ghost Spawn Values", 
            "Haunted Harpist Spawn Rarity",
            "Modded:25,ExperimentationLevel:0,AssuranceLevel:0,VowLevel:5,OffenseLevel:0,MarchLevel:5,RendLevel:200,DineLevel:200,TitanLevel:50,Adamance:5,Embrion:1,Artifice:35,Auralis:0,Atlantica:0,Acidir:200,Cosmocos:35,Asteroid:5,Desolation:60,Etern:0,Gloom:60,Gratar:0,Infernis:0,Junic:15,Oldred:1,Polarus:0,Seichi:20,Bozoros:15",
            "Spawn weight of the Haunted Harpist on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config)."
            );
        
        HarpGhostPowerLevel = cfg.Bind(
            "Ghost Spawn Values",
            "Haunted Harpist Power Level",
            1f,
            "The power level of the Haunted Harpist."
        );
        
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
            "Whether the Haunted Harpist can be killed by something other than a player e.g. an eyeless dog"
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
            2f,
            "Determines how difficult it is to use the zap gun on the Harpist."
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
        
        MaxAmountOfHarpGhosts = cfg.Bind(
            "Ghost Spawn Values",
            "Max Amount of Haunted Harpists",
            2,
            "The maximum amount of Haunted Harpist's that can spawn in a game"
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
        
        PlushieMinValue = cfg.Bind(
            "Item Spawn Values",
            "Ghost Plushie Minimum value",
            25,
            "The minimum value that the Ghost Plushie can be set to"
        );
        
        PlushieMaxValue = cfg.Bind(
            "Item Spawn Values",
            "Ghost Plushie Maximum value",
            75,
            "The maximum value that the Ghost Plushie can be set to"
        );
        
        PlushieSpawnRate = cfg.Bind(
            "Item Spawn Values",
            "Ghost Plushie Spawn Value",
            5,
            "The weighted spawn rarity of the Ghost Plushie"
        );
        
        PlushieSpawnLevel = cfg.Bind(
            "Item Spawn Values",
            "Ghost Plushie Spawn Level",
            Levels.LevelTypes.RendLevel,
            "The LevelTypes that the Ghost Plushie spawns in"
        );

        HarpGhostAngryEyesEnabled = cfg.Bind(
            "Haunted Harpist General",
            "Angry Eyes Enabled",
            true,
            "Whether the Haunted Harpist's eyes turn red when angry"
        );
    }
}