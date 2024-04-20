## 1.3.11
* Fixed bug that resets people's configs.
* Fixed bug where the plushie spawn values weren't being updated from the config.
* Changed mod icon.

## 1.3.10
* Added scrap item ghost plushies. 4 Different colours.
* Switched the mod to using a seperate asset bundle file along with the dll because that seems to be the general format.
* Fixed animation curve spawn rates. In the future I will make them customizable.
* Changed all ghost's default spawn moon to be Rend instead of Dine (butler annoyes me).
* Added code to delete old config values that aren't used anymore.

## 1.3.9 Phantom Piper & Ethereal Enforcer OUT OF BETA
* Updated the mod to work with the newest v50 update
* A few minor bug fixes
* There is still a small thing I want to fix to do with how the Ethereal Enforcers escort the Phantom Piper, but apart from that I am happy with taking these ghosts out of beta

## 1.3.8
* Fixed bug where the enforcer ghost shield would still show even after disabling it in the config
* Lowered the spawn rates of the ghosts due to v50 changing how they work in some way

## 1.3.7
* Fixed bug stopping the mod to be loaded because v50 doesn't have the enemy power level variable for some reason

## 1.3.6 
* Accidentally uploaded the wrong version. I'm incrementing the version to 1.3.6 for clarity

## 1.3.5
* The Lethallib version has been updated the Haunted Harpist works with v50, but the Phantom Piper is a bit buggy (in v50)

## 1.3.4
* Made the Phantom Piper's exit teleport bigger.
* Added a shield to the Ethereal Enforcer. The shield absorbs a single hit regardless of damage. The shield then takes a specified amount of time (configurable) to regenerate.
* Ethereal Enforcers should hunt separately now.
* Added sound effect to the Phantom Piper's exit teleport.
* Added pick-up and drop sound effects to bagpipes.
* Added drop sound effect to harp.
* Added proper scrap icons for the harp and bagpipes.
* Changed the default spawn rate of the Phantom Piper from 0 to 5. It might mess up the value that you put there if you changed it. I'm confident that the Phantom Piper and Ethereal Enforcers are stable for regular people to use, but ill keep it in "beta" for a few more days.

## 1.3.3
* Ethereal Enforcer now has a delay between seeing a player, and shooting them.
* The Ethereal Enforcer's reload time is now longer.
* The Ethereal Enforcer's turn speed is now slower, and customizable.
* The Phantom Piper and Ethereal Enforcer now have a few extra sound effects.
* The bagpipes now disappear properly when the Phantom Piper manages to escape out the map.

## 1.3.2
* Fixed the bagpipes rotation when a player is holding them.
* Fixed bug where the shotguns dropped by the Ethereal Enforcer didnt work.
* Added AsyncLoggers as a **soft** dependency. Not because this mod needs it but like the DissonanceLagFix mod, its just free fps for no cost.

## 1.3.1
* Fixed bug where when a Phantom Piper or Ethereal Enforcer was provoked, unless the enforcer's could see the player, they would not agro onto them.
* Fixed the messed up config (I **really** suggest you delete your config so it can regenerate).

# 1.3.0 Phantom Piper & Ethereal Enforcer BETA
* See readme for more details.

## 1.2.9
* Actually fixed the bug where only the host player would see the ghost's eyes turn red this time.

* Added the following configs:
  * Harp Ghost's view width.
  * Harp Ghost's view range.
  * Harp Ghost's proximity awareness.
  * Whether the Harp Ghost can take damage from non-player entities.
  * An insane amount of audio setting configs for the harp. I'm not going to write them all down because there are loads (don't mess with them if you don't know what you are doing though).

## 1.2.8
* Added the following configs:
    * Harp Ghost's max speed in chase mode
    * Harp Ghost's max acceleration in chase mode
    * Harp Ghost enable red eyes when angry (its true by default)
    * Harp Ghost's attack cooldown
    * Harp Ghost's attack area length (how far the attack area reaches out pretty much)
    * Harp Ghost's ability to hear players to aid its search when angry (true by default)
  
* Changed the max open door speed config to "open door speed multiplier when in chase mode" because the previous one was a bit useless and didnt really make sense
* Fixed bug where only the host player would see the ghost's eyes turn red

## 1.2.7
* The harp ghost's eyes turn red when angry
* Reduced lag when the harp spawns (hopefully)

## 1.2.6
* Removed DissonanceLagFix as a hard dependency

## 1.2.5
* Thunderstore bugging out, ignore this

## 1.2.4
* Fixed bug that makes the harp play different music for different people
* Changed the harp ghost's default attack damage to 45 from 35 (you can change the value with configs)
* Made the plugin try to load the instruments' audio files on startup to hopefully kill the lag caused when a harp is first played (music) in a game. The DissonanceLagFix mod is also now added as a dependency to help with the lag just in-case the plugin was unable to load the audio files on startup.   

## 1.2.3
* Fixed disappearing harps bug
* Fixed double harps appearing when ghost gets annoyed
* Changed network events to not static (which caused a plethora of other bugs)
* Fixed harp item offset bug

## 1.2.2
* Added LevelTypes config
* Fixed bug where ghost won't spawn, but for now he only spawns on Dine
* Removed delay at the beginning of one of the harp songs 

## 1.2.1
* Added more configs

## 1.2.0 Out of Beta
* Configs added, more will be added upon request 
* Better scrap icon (one that isn't a white square)

## 1.1.3
* Two more harp songs added
* Shotgun now does damage 
* Now spawns on Rend as well as Dine

## 1.1.2
* Fear mechanic works
* Ghost can now hear players if near (when its in search mode)

## 1.1.1
* Updated readme

## 1.1.0 Beta Release
