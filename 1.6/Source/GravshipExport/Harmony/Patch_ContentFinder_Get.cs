using GravshipExport;
using HarmonyLib;
using RimWorld;
using System;

[HarmonyPatch(typeof(Sketch), nameof(Sketch.Spawn))]
public static class Patch_Sketch_Spawn_Finalizer
{
    static void Finalizer(Exception __exception)
    {
        if (__exception != null)
        {
            GravshipLogger.Error($"[GravshipExport] Suppressed exception during Sketch.Spawn: {__exception}");
            // Swallowing here is safe for a diagnostic build; remove once you’re confident.
        }
    }
}
