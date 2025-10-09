using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace GravshipExport
{
    [HarmonyPatch(typeof(SketchThing), "Spawn")]
    public static class Patch_SketchThing_Spawn
    {
        // The original Spawn method returns a bool
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
                // ✅ Let vanilla handle it first. If it throws, we'll catch below.
                return true;
            }
            catch (System.Exception ex)
            {
                GravshipLogger.Warning(
                    $"Suppressed spawn exception for {__instance?.def?.defName ?? "unknown"} at {at}. " +
                    $"Attempting forced spawn instead. Exception: {ex}"
                );

                try
                {
                    // ✅ Fallback: manually spawn the thing
                    Thing forcedThing = ThingMaker.MakeThing(__instance.def, __instance.stuff);
                    forcedThing.SetFaction(faction);

                    GenSpawn.Spawn(
                        forcedThing,
                        at,
                        map,
                        __instance.rot,
                        wipeIfCollides ? WipeMode.Vanish : WipeMode.VanishOrMoveAside
                    );

                    spawnedThings?.Add(forcedThing);

                    __result = true;
                    GravshipLogger.Message($"Successfully force-spawned {forcedThing.def.defName} at {at}.");
                }
                catch (System.Exception innerEx)
                {
                    GravshipLogger.Error(
                        $"Failed to force-spawn {__instance?.def?.defName ?? "unknown"} at {at}: {innerEx}"
                    );
                    __result = false;
                }

                // ✅ Skip vanilla spawn if we handled it here
                return false;
            }
        }
    }
}
