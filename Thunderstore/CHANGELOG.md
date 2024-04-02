# 1.3.0 Phantom Piper & Ethereal Enforcer BETA
* See readme for more details

## 1.2.9
* Actually fixed the bug where only the host player would see the ghost's eyes turn red this time

* Added the following configs:
  * Harp Ghost's view width
  * Harp Ghost's view range
  * Harp Ghost's proximity awareness
  * Whether the Harp Ghost can take damage from non-player entities
  * An insane amount of audio setting configs for the harp. I'm not going to write them all down because there are loads (don't mess with them if you don't know what you are doing though)

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
