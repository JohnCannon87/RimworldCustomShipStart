using HarmonyLib;
using RimWorld;
using Verse;

namespace GravshipExport
{
    [HarmonyPatch(typeof(SketchThing), "IsSpawningBlocked")]
    public static class Patch_SketchThing_IsSpawningBlocked
    {
        public static bool Prefix(
            SketchThing __instance,
            IntVec3 at,
            Map map,
            Thing thingToIgnore,
            bool wipeIfCollides,
            ref bool __result)
        {
            // If map is generating (long event thread), suppress graphics calls
            if (Find.TickManager.TicksGame < 1000)
            {
                // Tell RimWorld: "No, it’s not blocked"
                __result = false;
                return false; // Skip vanilla method
            }

            return true; // Allow vanilla during normal play
        }
    }
}
