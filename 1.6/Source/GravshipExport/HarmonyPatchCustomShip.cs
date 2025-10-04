using System;
using System.Collections.Generic;
using System.Linq;
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
        // 🧹 Prefix: run *before* vanilla code, with map available
        static void Prefix(Map map)
        {
            Log.Message("[GravshipExport] ===== DoGravship Prefix START =====");

            try
            {
                // 1️⃣ Build our custom sketch to get its footprint
                SketchResolveParams parms = default;
                parms.sketch = new Sketch();
                var customResolver = DefDatabase<SketchResolverDef>.GetNamed("CustomGravship", true);
                Sketch preview = RimWorld.SketchGen.SketchGen.Generate(customResolver, parms);

                // 2️⃣ Get where the gravship will spawn
                IntVec3 playerStartSpot = MapGenerator.PlayerStartSpot;
                if (!MapGenerator.PlayerStartSpotValid)
                {
                    Log.Warning("[GravshipExport] PlayerStartSpot is not valid yet, terrain clearing might be partial.");
                }

                // 3️⃣ Translate sketch cells into map coordinates
                IntVec3 centerOffset = preview.OccupiedCenter;
                var cleared = 0;
                foreach (IntVec3 local in preview.OccupiedRect.Cells)
                {
                    IntVec3 mapCell = playerStartSpot + (local - centerOffset);
                    if (!mapCell.InBounds(map)) continue;

                    TerrainDef under = map.terrainGrid.UnderTerrainAt(mapCell);
                    if (under != null)
                    {
                        map.terrainGrid.RemoveTopLayer(mapCell);
                        cleared++;
                    }
                }

                Log.Message($"[GravshipExport] Cleared under-terrain from {cleared} cells before gravship spawn.");
            }
            catch (Exception ex)
            {
                Log.Error($"[GravshipExport] Terrain clearing failed in Prefix: {ex}");
            }

            Log.Message("[GravshipExport] ===== DoGravship Prefix END =====");
        }

        // ✅ Postfix: runs *after* everything has spawned
        static void Postfix()
        {
            Log.Message("[GravshipExport] ===== DoGravship Postfix: Restoring GravEngine range =====");
            ShipSketchBuilder.RestoreGravEngineRange();
        }

        // 🔁 Transpiler: replace default resolver with our custom one
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
                    yield return new CodeInstruction(OpCodes.Ldstr, "CustomGravship");
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
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
