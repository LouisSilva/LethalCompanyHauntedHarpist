using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using LethalCompanyHarpGhost.BagpipesGhost;
using UnityEngine;

namespace LethalCompanyHarpGhost.EnforcerGhost;

/*
[HarmonyPatch(typeof(ShotgunItem))]
public static class ShotgunPatches
{
    [HarmonyPatch("ShootGun")]
    [SuppressMessage("ReSharper", "SuggestVarOrType_Elsewhere")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
    {
        var codes = new List<CodeInstruction>(instructions);
        int startIndex = -1;
        Label labelForNextIteration = ilGenerator.DefineLabel();  // We'll use this for breaking out of the loop.

        // Find the specific IL code lines that lead to mainScript's assignment and set startIndex
        for (int i = 0; i < codes.Count; i++)
        {
            // Check if the opcode is Stloc_S and the operand is not null and is a LocalBuilder
            if (codes[i].opcode == OpCodes.Stloc_S && codes[i].operand is LocalBuilder localBuilder)
            {
                // Now it's safe to check the LocalIndex because we know operand is a LocalBuilder
                if (localBuilder.LocalIndex == 12)
                {
                    startIndex = i; // Found the index where mainScript is stored
                    break;
                }
            }
        }

        int locationForLabel = -1;
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].operand is LocalBuilder localBuilder && localBuilder.LocalIndex == 13 && codes[i].opcode == OpCodes.Ldloc_S)
            {
                locationForLabel = i;
                break;
            }
        }

        if (startIndex != -1 && locationForLabel != -1)
        {
            var injectedCodes = new List<CodeInstruction>
            {
                // Instructions to check if mainScript is an instance of BagpipesGhostAIServer and jump to end if true
                new CodeInstruction(OpCodes.Ldloc_S, (byte)12),  // Load 'mainScript'
                new CodeInstruction(OpCodes.Isinst, typeof(BagpipesGhostAIServer)),  // Check if it's an instance of BagpipesGhostAIServer
                new CodeInstruction(OpCodes.Brtrue_S, labelForNextIteration),  // Break out of loop if true

                // Repeat for EnforcerGhostAIServer
                new CodeInstruction(OpCodes.Ldloc_S, (byte)12),  // Load 'mainScript' again for next check
                new CodeInstruction(OpCodes.Isinst, typeof(EnforcerGhostAIServer)),  // Check if it's an instance of EnforcerGhostAIServer
                new CodeInstruction(OpCodes.Brtrue_S, labelForNextIteration),  // Break out of loop if true
            };

            // Insert our new instructions right after setting 'mainScript'
            codes.InsertRange(startIndex + 1, injectedCodes);
            codes[locationForLabel].labels.Add(labelForNextIteration);
        }
        else
        {
            Debug.LogError("Transpiler failed: Unable to find the insertion point.");
        }

        return codes.AsEnumerable();
    }
}
*/