using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GravshipExport
{
    [HarmonyPatch(typeof(Sketch), nameof(Sketch.Rotate))]
    public static class HarmonyPatch_SketchRotate
    {
        static void Postfix(Sketch __instance, Rot4 rot)
        {
            try
            {
                SketchRotationRegistry.LastRotation = rot; // NEW
                GravshipLogger.Message($"Sketch rotated to {rot}");
            }
            catch (Exception ex)
            {
                GravshipLogger.Error("Failed to store Sketch rotation: " + ex);
            }
        }
    }

}
