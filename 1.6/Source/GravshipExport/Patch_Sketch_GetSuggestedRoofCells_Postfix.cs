using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

[HarmonyPatch(typeof(Sketch), "GetSuggestedRoofCells")]
public static class Patch_Sketch_GetSuggestedRoofCells_Prefix
{
    static bool Prefix(Sketch __instance, ref IEnumerable<IntVec3> __result)
    {
        //jc//jcLog.Message("[CoolerRoofPatch] Prefix called for GetSuggestedRoofCells.");

        if (__instance.Empty)
        {
            __result = new List<IntVec3>();
            return false; // Skip original method
        }

        var occupiedRect = __instance.OccupiedRect;
        var tmpSuggestedRoofCellsVisited = new HashSet<IntVec3>();
        var tmpSuggestedRoofCells = new List<IntVec3>();
        var tmpYieldedSuggestedRoofCells = new HashSet<IntVec3>();

        // Local function: returns true if this cell has a wall or a cooler
        bool AnyRoofHolderAt(IntVec3 c)
        {
            var edifice = __instance.EdificeAt(c);
            bool isRoofHolder = edifice?.def.holdsRoof ?? false;

            foreach (var sketchThing in __instance.ThingsAt(c))
            {
                if (sketchThing.def == ThingDefOf.Cooler)
                {
                    isRoofHolder = true;
                    //jc//jcLog.Message($"[CoolerRoofPatch] Cooler at {c} treated as roof holder.");
                    break;
                }
            }

            return isRoofHolder;
        }

        List<IntVec3> result = new List<IntVec3>();

        foreach (IntVec3 item in occupiedRect)
        {
            if (tmpSuggestedRoofCellsVisited.Contains(item) || AnyRoofHolderAt(item))
                continue;

            tmpSuggestedRoofCells.Clear();
            __instance.FloodFill(item, c => !AnyRoofHolderAt(c), (c, dist) =>
            {
                tmpSuggestedRoofCellsVisited.Add(c);
                tmpSuggestedRoofCells.Add(c);
                return false;
            });

            bool flag = false;
            foreach (var c in tmpSuggestedRoofCells)
            {
                if (occupiedRect.IsOnEdge(c))
                {
                    flag = true;
                    break;
                }
            }
            if (flag)
                continue;

            foreach (var c in tmpSuggestedRoofCells)
            {
                for (int j = 0; j < 9; j++)
                {
                    var intVec = c + GenAdj.AdjacentCellsAndInside[j];
                    if (!tmpYieldedSuggestedRoofCells.Contains(intVec) &&
                        occupiedRect.Contains(intVec) &&
                        (j == 8 || AnyRoofHolderAt(intVec)))
                    {
                        tmpYieldedSuggestedRoofCells.Add(intVec);
                        //jc//jcLog.Message($"[CoolerRoofPatch] Yielding roof cell: {intVec}");
                        result.Add(intVec);
                    }
                }
            }
        }

        __result = result;
        return false; // Skip the original method
    }
}
