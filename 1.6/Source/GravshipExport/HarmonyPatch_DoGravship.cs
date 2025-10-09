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
    public static class HarmonyPatch_DoGravship
    {
        // 🧹 Prefix: clear under-terrain before spawn
        static void Prefix(Map map)
        {
            GravshipLogger.Message("===== DoGravship Prefix START =====");
            try
            {
                SketchResolveParams parms = default;
                parms.sketch = new Sketch();
                var customResolver = DefDatabase<SketchResolverDef>.GetNamed("CustomGravship", true);
                Sketch preview = RimWorld.SketchGen.SketchGen.Generate(customResolver, parms);

                IntVec3 playerStartSpot = MapGenerator.PlayerStartSpot;
                if (!MapGenerator.PlayerStartSpotValid)
                {
                    GravshipLogger.Warning("PlayerStartSpot is not valid yet, terrain clearing might be partial.");
                }

                IntVec3 centerOffset = preview.OccupiedCenter;
                int cleared = 0;
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

                GravshipLogger.Message($"Cleared under-terrain from {cleared} cells before gravship spawn.");
            }
            catch (Exception ex)
            {
                GravshipLogger.Error($"Terrain clearing failed in Prefix: {ex}");
            }

            GravshipLogger.Message("===== DoGravship Prefix END =====");
        }

        // ✅ Postfix: spawn flooring **after gravship sketch is placed**
        static void Postfix(Map map)
        {
            GravshipLogger.Message("===== DoGravship Postfix: Applying terrain layer (accurate rotation + pivot fix) =====");

            var layout = ShipSketchBuilder.CurrentLayout;
            var sketch = ShipSketchBuilder.CurrentSketch;
            ShipSketchBuilder.RestoreGravEngineRange();

            if (map == null)
            {
                GravshipLogger.Error("Postfix: Map is null — cannot spawn terrain.");
                return;
            }
            if (layout == null || sketch == null)
            {
                GravshipLogger.Warning("Postfix: No current layout/sketch found — skipping terrain spawn.");
                return;
            }

            // --- 1️⃣ Determine world transform from known rotation + anchor ---
            // Use the recorded sketch rotation from DoGravship
            Rot4 relRot = SketchRotationRegistry.LastRotation; // ✅ authoritative rotation

            // Find an anchor pair (sketch vs world) to compute translation
            ThingDef gravDef = ThingDefOf.GravEngine;
            SketchThing sketchAnchor = sketch.Things.FirstOrDefault(t => t.def == gravDef);
            Thing worldAnchor = (gravDef != null) ? map.listerThings.ThingsOfDef(gravDef).FirstOrDefault() : null;

            if (worldAnchor == null || sketchAnchor == null)
            {
                foreach (var st in sketch.Things)
                {
                    var candidates = map.listerThings.ThingsOfDef(st.def);
                    if (!candidates.NullOrEmpty())
                    {
                        sketchAnchor = st;
                        worldAnchor = candidates[0];
                        break;
                    }
                }
            }

            if (worldAnchor == null || sketchAnchor == null)
            {
                GravshipLogger.Warning("Postfix: Could not find anchor — skipping terrain spawn.");
                return;
            }

            // RimWorld rotation is always around (0,0)
            // Compute translation: T = anchor_world - Rot(relRot, anchor_local)
            IntVec3 offset = worldAnchor.Position - sketchAnchor.pos.RotatedBy(relRot);

            GravshipLogger.Message($"Using rotation={relRot} and offset={offset}");

            // --- 2️⃣ Apply terrain from layout with same transform ---
            var terrainGrid = map.terrainGrid;
            var terrainCache = new Dictionary<string, TerrainDef>(StringComparer.Ordinal);
            int appliedCount = 0, skippedCount = 0;

            for (int z = 0; z < layout.rows.Count; z++)
            {
                var row = layout.rows[z];
                if (row == null) continue;

                for (int x = 0; x < row.Count; x++)
                {
                    var cell = row[x];
                    if (cell == null || string.IsNullOrEmpty(cell.terrainDef)) continue;

                    // Cache/resolve TerrainDef
                    if (!terrainCache.TryGetValue(cell.terrainDef, out var terrainDef) || terrainDef == null)
                    {
                        terrainDef = DefDatabase<TerrainDef>.GetNamedSilentFail(cell.terrainDef);
                        terrainCache[cell.terrainDef] = terrainDef;
                        if (terrainDef == null)
                        {
                            GravshipLogger.Warning($"Unknown terrainDef '{cell.terrainDef}' at ({x},{z}) — skipping.");
                            continue;
                        }
                    }

                    // Apply same transform the Sketch uses
                    IntVec3 local = new IntVec3(x, 0, z);
                    IntVec3 world = local.RotatedBy(relRot) + offset;

                    if (!world.InBounds(map))
                    {
                        skippedCount++;
                        continue;
                    }

                    var current = terrainGrid.TerrainAt(world);
                    if (!terrainDef.layerable && current != null && current != terrainDef)
                    {
                        skippedCount++;
                        continue;
                    }

                    if (current == terrainDef) continue;

                    terrainGrid.SetTerrain(world, terrainDef);
                    appliedCount++;
                }
            }

            GravshipLogger.Message($"✅ Terrain pass complete — applied={appliedCount}, skipped={skippedCount}, rotation={relRot}.");
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
