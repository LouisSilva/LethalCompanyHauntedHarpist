# Haunted Harpist
**Adds to the game a new enemy called the Haunted Harpist.**
<p>The Haunted Harpist is an enemy that initially roams the map while playing its haunting melody on the harp. If provoked during its performance—whether by excessive noise, stunning, or direct attack—the Haunted Harpist will cease its serene music, dropping its harp to engage aggressively with anyone it perceives.</p>

<p>Dealing with the angry ghost may prove difficult, but it is worth it to obtain the harp.</p>

# The Phantom Piper & Ethereal Enforcer
The Phantom Piper plays the bagpipes.

Unlike the Haunted Harpist, the Phantom Piper is scared of players and will run away, bagpipes in hand, when provoked.

Although luckily for him, he is always escorted by his own Ethereal Enforcers.

# FAQ

**I never see the ghosts, where do they spawn?**

By default, they all spawn only on Dine, with the Haunted Harpist having an "uncommon" spawn rate and the Phantom Piper having a "rare" spawn rate.

<br></br>
**Can I configure the spawn values?**

With the config provided, you can change the **Spawn Level** config value to one of the following:

    - LevelTypes.All
    - LevelTypes.Vanilla
    - LevelTypes.None
    - LevelTypes.Modded
    - LevelTypes.<name of moon>Level -> e.g. LevelTypes.TitanLevel

With this Spawn Level, you can set a spawn rarity to it. The default one for the Haunted Harpist is 6.

This config is quite limited, so I suggest you use another mod to fine tune your spawn values if needed.

<br></br>
**Why are the Ethereal Enforcers so overpowered?**

The Phantom Piper and his escorts are supposed to be a rare enemy in the game, with a high risk - high reward for trying to steal the bagpipes.

Even though they have a shield and 6 HP (configurable), they have big weaknesses which can be easily exploited (some are obvious, some of them aren't so).

<br></br>
**Does this mod support v50?**

Yes

<br></br>
**I found a bug, where can I report it?**

I really appreciate bug reports, so feel free to report them in the Lethal Company Modding Discord or create a new issue on my GitHub page

# Recommended Mods to Install

## [DissonanceLagFix](https://thunderstore.io/c/lethal-company/p/linkoid/DissonanceLagFix/) 
This mod reduces any potential lag that occurs when an instrument is played for the first time in a game, and generally helps improve performance for all mods.

## [AsyncLoggers](https://thunderstore.io/c/lethal-company/p/mattymatty/AsyncLoggers/)
This mod moves logs to their own thread, which significantly increases performance for modpacks.

The Haunted Harpist mod does not log anything unless it's an error, but you should still install this mod because many other mods do spam the console, which reduces performance.

## Acknowledgements

- [Evaisa](https://github.com/EvaisaDev) for [LethalLib](https://github.com/EvaisaDev/LethalLib), the [Unity Template](https://github.com/EvaisaDev/LethalCompanyUnityTemplate) and the [Netcode Patcher](https://github.com/EvaisaDev/UnityNetcodePatcher)
- [Hamunii](https://github.com/Hamunii) for the [Example Enemy](https://lethal.wiki/dev/apis/lethallib/custom-enemies/overview) enemy modding tutorial
- [Bagpipes Mesh](https://sketchfab.com/3d-models/bagpipes-mesh-free-to-download-48214a76a8694f64be9fe75225a51a57) by [torquemod](https://sketchfab.com/torquemod) -- License: [Attribution 4.0](https://creativecommons.org/licenses/by/4.0/)