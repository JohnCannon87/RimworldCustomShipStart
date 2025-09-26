using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace GravshipExport
{
    [HarmonyPatch(typeof(SketchThing), "Spawn")]
    public static class Patch_SketchThing_Spawn
    {
        // The method actually returns bool
        public static bool Prefix(
            SketchThing __instance,
            IntVec3 at,
            Map map,
            Faction faction,
            Sketch.SpawnMode spawnMode,
            bool wipeIfCollides,
            bool forceTerrainAffordance,
            List<Thing> spawnedThings,
            bool dormant,
            TerrainDef defaultAffordanceTerrain,
            ref bool __result)
        {
            try
            {
                // Let vanilla try first
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[GravshipExport] Suppressed spawn exception for {__instance?.def?.defName ?? "unknown"} at {at}. Forcing spawn. Exception: {ex}");

                try
                {
                    // Manually spawn the thing
                    Thing forcedThing = ThingMaker.MakeThing(__instance.def, __instance.stuff);
                    forcedThing.SetFaction(faction);
                    GenSpawn.Spawn(forcedThing, at, map, __instance.rot, wipeIfCollides ? WipeMode.Vanish : WipeMode.VanishOrMoveAside);

                    spawnedThings?.Add(forcedThing);

                    __result = true; // success
                }
                catch (System.Exception innerEx)
                {
                    Log.Error($"[GravshipExport] Failed to force-spawn {__instance?.def?.defName}: {innerEx}");
                    __result = false; // failed
                }

                return false; // skip vanilla
            }
        }
    }
}
