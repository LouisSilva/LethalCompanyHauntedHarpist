using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using LethalCompanyHarpGhost.BagpipesGhost;

namespace LethalCompanyHarpGhost.EnforcerGhost;

[HarmonyPatch(typeof(ShotgunItem))]
public static class ShotgunPatches
{
    [HarmonyPatch("ShootGun")]
    [SuppressMessage("ReSharper", "SuggestVarOrType_Elsewhere")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        int startIndex = -1;
        Label returnLabel = new(); // This will be used for 'ret' jump

        // Find the instruction index for 'mainScript' and set up the return label from existing 'ret'
        for (int i = 0; i < codes.Count; i++)
        {
            // This condition locates the 'ret' at the end of the method which is the target for a 'break'
            if (codes[i].opcode == OpCodes.Ret && i > 0 && codes[i - 1].opcode == OpCodes.Add)
            {
                returnLabel = codes[i].labels[0]; // Assuming there's a label, otherwise, you need to create a new one.
                continue; // Continue since we just want to assign label without breaking the loop.
            }

            if (codes[i].opcode == OpCodes.Stloc_S && codes[i].operand.ToString().Contains("mainScript"))
            {
                startIndex = i;
                break; // Once we find our target, no need to continue the loop
            }
        }

        if (startIndex != -1 && returnLabel != null)
        {
            var injectedCodes = new List<CodeInstruction>
            {
                // Check if mainScript is BagpipesGhostAIServer and jump out of loop if true
                new CodeInstruction(OpCodes.Ldloc_S, codes[startIndex].operand), // Load mainScript local variable
                new CodeInstruction(OpCodes.Isinst, typeof(BagpipesGhostAIServer)), // Check type
                new CodeInstruction(OpCodes.Brtrue, returnLabel), // Break from loop

                // Check if mainScript is EnforcerGhostAIServer and jump out of loop if true
                new CodeInstruction(OpCodes.Ldloc_S, codes[startIndex].operand), // Repeat load for next check
                new CodeInstruction(OpCodes.Isinst, typeof(EnforcerGhostAIServer)), // Check type
                new CodeInstruction(OpCodes.Brtrue, returnLabel), // Break from loop
            };

            // Insert our new instructions right after setting 'mainScript'
            codes.InsertRange(startIndex + 1, injectedCodes);
        }
        else
        {
            // Log or handle the error if necessary parts were not found
            UnityEngine.Debug.LogError("Transpiler failed: Unable to find target index or return label.");
        }

        return codes;
    }
}