using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace GravshipExport.GravshipExport.Importer
{
    [HarmonyPatch(typeof(Sketch), nameof(Sketch.Spawn))]
    public static class Patch_Sketch_Spawn_Log
    {
        static void Prefix(Sketch __instance, Map map)
        {
            GravshipLogger.Message("[GravshipExport] Sketch.Spawn called\n" + Environment.StackTrace);
        }
    }

}
