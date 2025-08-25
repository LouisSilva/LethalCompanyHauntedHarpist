using BepInEx.Configuration;
using LethalCompanyHarpGhost.Types;

namespace LethalCompanyHarpGhost.EnforcerGhost;

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
    public readonly ConfigEntry<bool> EnforcerGhostFriendlyFire;

    public readonly ConfigEntry<float> EnforcerGhostVoiceSfxVolume;
    public readonly ConfigEntry<float> EnforcerGhostSfxVolume;
    
    public readonly ConfigEntry<bool> EnforcerGhostEnabled;
    public readonly ConfigEntry<string> EnforcerGhostSpawnRarity;
    public readonly ConfigEntry<int> MaxAmountOfEnforcerGhosts;
    public readonly ConfigEntry<float> EnforcerGhostPowerLevel;
    
    public readonly ConfigEntry<int> ShotgunMinValue;
    public readonly ConfigEntry<int> ShotgunMaxValue;

    public EnforcerGhostConfig(ConfigFile cfg)
    {
        InitInstance(this);
        
        EnforcerGhostEnabled = cfg.Bind(
            "Ghost Spawn Values", 
            "Ethereal Enforcer Loner Enabled",
            false,
            "Whether the Ethereal Enforcer can also spawn by themselves."
        );
        
        EnforcerGhostSpawnRarity = cfg.Bind(
            "Ghost Spawn Values", 
            "Ethereal Enforcer Spawn Rarity",
            "All:0",
            "Spawn weight of a loner Ethereal Enforcer on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config). The default option is having it disabled, because this option is for whether you want the Ethereal Enforcer to also be able to spawn by themselves, aswell as with the Phantom Piper."
        );
        
        EnforcerGhostPowerLevel = cfg.Bind(
            "Ghost Spawn Values",
            "Ethereal Enforcer Power Level",
            2.5f,
            "The power level of the Ethereal Enforcer."
        );
        
        EnforcerGhostInitialHealth = cfg.Bind(
            "Ethereal Enforcer General",
            "Health",
            6,
            "The health of an Enforcer ghost when spawned."
            );
        
        EnforcerGhostIsKillable = cfg.Bind(
            "Ethereal Enforcer General",
            "Killable",
            true,
            "Whether an Enforcer Ghost can be killed or not."
        );
        
        EnforcerGhostFriendlyFire = cfg.Bind(
            "Ethereal Enforcer General",
            "Friendly Fire",
            false,
            "Whether the Ethereal Enforcer can be killed by something other than a player e.g. an eyeless dog. WARNING: May be incompatible with weapons from some mods (if friendly fire is off)."
        );
        
        EnforcerGhostIsStunnable= cfg.Bind(
            "Ethereal Enforcer General",
            "Stunnable",
            true,
            "Whether an Enforcer Ghost can be stunned or not."
        );
        
        EnforcerGhostMaxSpeedInChaseMode = cfg.Bind(
            "Ethereal Enforcer General",
            "Max Speed In Chase Mode",
            1.5f,
            "The max speed of the Enforcer Ghost in chase mode."
        );
        
        EnforcerGhostMaxAccelerationInChaseMode = cfg.Bind(
            "Ethereal Enforcer General",
            "Max Acceleration In Chase Mode",
            15f,
            "The max acceleration of the Enforcer Ghost in chase mode."
        );

        EnforcerGhostTurnSpeed = cfg.Bind(
            "Ethereal Enforcer General",
            "Turn Speed",
            75f,
            "The turn speed of the Enforcer Ghost."
        );
        
        EnforcerGhostShootDelay = cfg.Bind(
            "Ethereal Enforcer General",
            "Shoot Delay",
            2f,
            "The delay which dictates how long it takes for an Enforcer Ghost to shoot you after it notices you."
        );
        
        EnforcerGhostShieldEnabled = cfg.Bind(
            "Ethereal Enforcer General",
            "Is Shield Enabled",
            true,
            "Whether or not the Enforcer ghost has a shield which can withstand 1 hit (regardless of the damage). When damaged it breaks, and regens after a specified time."
        );
        
        EnforcerGhostShieldRegenTime = cfg.Bind(
            "Ethereal Enforcer General",
            "Shield Regeneration TIme",
            25f,
            "The time it takes for the shield to regenerate after being hit, the ghost being stunned or the ghost disabling the shield to shoot."
        );
        
        EnforcerGhostStunTimeMultiplier = cfg.Bind(
            "Ethereal Enforcer General",
            "Stun Time Multiplier",
            3f,
            "The multiplier for how long a Enforcer Ghost can be stunned."
        );
        
        EnforcerGhostDoorSpeedMultiplierInChaseMode = cfg.Bind(
            "Ethereal Enforcer General",
            "Door Speed Multiplier In Chase Mode",
            1f,
            "The door speed multiplier when the Enforcer ghost is in chase mode."
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
            "The volume of the Enforcer ghost's voice. Values are from 0-1."
        );
        
        EnforcerGhostSfxVolume = cfg.Bind(
            "Ghost Audio",
            "Enforcer Ghost Sound Effects Volume",
            0.5f,
            "The volume of the Enforcer ghost's sound effects (e.g. shotgun noises, teleport noises etc). Values are from 0-1."
        );
        
        MaxAmountOfEnforcerGhosts = cfg.Bind(
            "Ghost Spawn Values",
            "Max Amount of Enforcer Ghosts",
            3,
            "The maximum amount of Enforcer ghosts that can spawn in a game."
        );
        
        ShotgunMinValue = cfg.Bind(
            "Item Spawn Values",
            "Shotgun Minimum value",
            60,
            "The minimum value that the shotgun spawned by an enforcer ghost can be set to."
        );
        
        ShotgunMaxValue = cfg.Bind(
            "Item Spawn Values",
            "Shotgun Maximum value",
            90,
            "The maximum value that the shotgun spawned by an enforcer ghost can be set to."
        );
    }
}