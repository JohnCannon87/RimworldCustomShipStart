using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.SketchGen;
using Verse;

namespace RimworldCustomShipStart
{
    /// <summary>
    /// Builds a RimWorld Sketch from a ShipLayoutDefV2 (cell-based export).
    /// Includes strict validation to prevent crashes during Sketch.Spawn().
    /// </summary>
    public static class ShipSketchBuilder
    {
        private const bool LogInfo = true;
        private const bool LogWarn = true;

        /// <summary>
        /// Convert a ShipLayoutDefV2 into a Sketch.
        /// </summary>
        public static Sketch BuildFromLayout(ShipLayoutDefV2 layout)
        {
            if (layout == null)
            {
                Log.Error("[CustomShipStart] BuildFromLayout: layout is null.");
                return new Sketch();
            }
            if (layout.rows == null || layout.rows.Count == 0)
            {
                Log.Error($"[CustomShipStart] BuildFromLayout: layout '{layout.defName}' has no rows.");
                return new Sketch();
            }

            if (LogInfo)
            {
                Log.Message($"[CustomShipStart] BuildFromLayout: '{layout.defName}' size {layout.width}x{layout.height}, rows={layout.rows.Count}.");
                Log.Message($"[CustomShipStart] Engine offset: ({layout.gravEngineX}, {layout.gravEngineZ}).");
            }

            var sketch = new Sketch();
            TryAddGravEngine(sketch, new IntVec3(layout.gravEngineX, 0, layout.gravEngineZ));

            var terrainCache = new Dictionary<string, TerrainDef>(StringComparer.Ordinal);
            var thingCache = new Dictionary<string, ThingDef>(StringComparer.Ordinal);

            int cellCount = 0;
            int thingCount = 0;

            for (int z = 0; z < layout.rows.Count; z++)
            {
                var row = layout.rows[z];
                if (row == null)
                {
                    if (LogWarn) Log.Warning($"[CustomShipStart] BuildFromLayout: row {z} is null, skipping.");
                    continue;
                }

                for (int x = 0; x < row.Count; x++)
                {
                    var cell = row[x];
                    if (cell == null) continue;

                    var pos = new IntVec3(x, 0, z);

                    // Foundation
                    var foundation = ResolveTerrain(cell.foundationDef, terrainCache);
                    if (foundation != null) sketch.AddTerrain(foundation, pos);

                    // Terrain
                    var terrain = ResolveTerrain(cell.terrainDef, terrainCache);
                    if (terrain != null && terrain != foundation)
                    {
                        if (foundation != null)
                        {
                            if (LogWarn)
                                Log.Warning($"[CustomShipStart] Skipping terrain '{terrain.defName}' at ({x},{z}) " +
                                            $"to avoid overwriting foundation '{foundation.defName}'.");
                        }
                        else
                        {
                            sketch.AddTerrain(terrain, pos);
                        }
                    }

                    // Things
                    if (cell.things != null && cell.things.Count > 0)
                    {
                        foreach (var t in cell.things)
                        {
                            if (t == null || string.IsNullOrEmpty(t.defName))
                                continue;

                            var thingDef = ResolveThing(t.defName, thingCache);
                            if (!IsValidForSketch(thingDef))
                            {
                                if (LogWarn)
                                    Log.Warning($"[CustomShipStart] Skipping invalid thing '{t.defName}' at {pos}");
                                continue;
                            }

                            var stuffDef = ResolveThing(t.stuffDef, thingCache); // may be null
                            var rot = new Rot4(t.rotInteger);

                            if (stuffDef != null && !thingDef.MadeFromStuff)
                            {
                                // Don’t pass stuff for non-stuffable defs
                                stuffDef = null;
                            }

                            if (stuffDef == null)
                            {
                                sketch.AddThing(thingDef, pos, rot, null, 1, null, null, false);;
                            }
                            else
                            {
                                sketch.AddThing(thingDef, pos, rot, stuffDef, 1, null, null, false);
                            }

                            thingCount++;

                            if (LogInfo)
                                Log.Message($"[CustomShipStart] Adding {thingDef.defName} " +
                                            $"stuff={(stuffDef != null ? stuffDef.defName : "null")} " +
                                            $"at {pos} rot={rot}");
                        }
                    }

                    if (cell.HasAnyData) cellCount++;
                }
            }

            if (LogInfo)
                Log.Message($"[CustomShipStart] BuildFromLayout: finished. Structural cells={cellCount}, things added={thingCount}.");

            return sketch;
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private static void TryAddGravEngine(Sketch sketch, IntVec3 pos)
        {
            try
            {
                sketch.AddThing(ThingDefOf.GravEngine, pos, Rot4.North, null, 1, null, null, wipeIfCollides: true, 0.5f);
            }
            catch (Exception ex)
            {
                Log.Error($"[CustomShipStart] Failed to place GravEngine at {pos}: {ex}");
            }
        }

        private static TerrainDef ResolveTerrain(string defName, Dictionary<string, TerrainDef> cache)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            if (cache.TryGetValue(defName, out var td)) return td;

            td = DefDatabase<TerrainDef>.GetNamedSilentFail(defName);
            if (td == null && LogWarn)
                Log.Warning($"[CustomShipStart] ResolveTerrain: unknown TerrainDef '{defName}'.");
            cache[defName] = td;
            return td;
        }

        private static ThingDef ResolveThing(string defName, Dictionary<string, ThingDef> cache)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            if (cache.TryGetValue(defName, out var td)) return td;

            td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (td == null && LogWarn)
                Log.Warning($"[CustomShipStart] ResolveThing: unknown ThingDef '{defName}'.");
            cache[defName] = td;
            return td;
        }

        /// <summary>
        /// Validate that a ThingDef is safe to add to a Sketch.
        /// </summary>
        private static bool IsValidForSketch(ThingDef def)
        {
            if (def == null) return false;

            if (def.IsBlueprint || def.IsFrame) return false;
            if (def.category != ThingCategory.Building && def.category != ThingCategory.Item) return false;

            // Some defs don’t define affordance but still work (like resources)
            if (def.category == ThingCategory.Building && def.building != null)
            {
                if (def.terrainAffordanceNeeded == null)
                {
                    Log.Warning($"[CustomShipStart] Def {def.defName} has no terrainAffordanceNeeded; skipping.");
                    return false;
                }
            }

            return true;
        }
    }
}
