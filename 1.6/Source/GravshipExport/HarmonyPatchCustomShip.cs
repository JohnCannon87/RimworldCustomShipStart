using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.SketchGen;
using Verse;

namespace GravshipExport
{
    [HarmonyPatch(typeof(ScenPart_PlayerPawnsArriveMethod), "DoGravship")]
    public static class HarmonyPatchCustomShip
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            var gravshipField = AccessTools.Field(typeof(SketchResolverDefOf), "Gravship");
            var getNamed = AccessTools.Method(
                typeof(DefDatabase<SketchResolverDef>),
                "GetNamed",
                new[] { typeof(string), typeof(bool) }
            );

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldsfld && codes[i].operand == gravshipField)
                {
                    // Replace SketchResolverDefOf.Gravship with DefDatabase.GetNamed("CustomGravship", true)
                    yield return new CodeInstruction(OpCodes.Ldstr, "CustomGravship");
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1); // true
                    yield return new CodeInstruction(OpCodes.Call, getNamed);
                }
                else
                {
                    yield return codes[i];
                }
            }
        }
    }

}
