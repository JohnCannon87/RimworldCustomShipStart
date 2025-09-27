using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.SketchGen;
using Verse;

namespace GravshipExport
{
    /// <summary>
    /// Builds a RimWorld Sketch from a ShipLayoutDefV2 (cell-based export).
    /// Includes strict validation to prevent crashes during Sketch.Spawn().
    /// </summary>
    public static class ShipSketchBuilder
    {
        private const bool LogInfo = false;
        private const bool LogWarn = false;

        /// <summary>
        /// Convert a ShipLayoutDefV2 into a Sketch.
        /// </summary>
        public static Sketch BuildFromLayout(ShipLayoutDefV2 layout)
        {
            if (layout == null)
            {
                Log.Error("[GravshipExport] BuildFromLayout: layout is null.");
                return new Sketch();
            }
            if (layout.rows == null || layout.rows.Count == 0)
            {
                Log.Error($"[GravshipExport] BuildFromLayout: layout '{layout.defName}' has no rows.");
                return new Sketch();
            }

            if (LogInfo)
            {
                Log.Message($"[GravshipExport] BuildFromLayout: '{layout.defName}' size {layout.width}x{layout.height}, rows={layout.rows.Count}.");
                Log.Message($"[GravshipExport] Engine offset: ({layout.gravEngineX}, {layout.gravEngineZ}).");
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
                    if (LogWarn) Log.Warning($"[GravshipExport] BuildFromLayout: row {z} is null, skipping.");
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

                    // Terrain - Don't add terrain for now, we capture it but adding it breaks the foundation appearing which is obviously bad
                    /*var terrain = ResolveTerrain(cell.terrainDef, terrainCache);
                    if (terrain != null) sketch.AddTerrain(terrain, pos, false);*/

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
                                    Log.Warning($"[GravshipExport] Skipping invalid thing '{t.defName}' at {pos}");
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
                                Log.Message($"[GravshipExport] Adding {thingDef.defName} " +
                                            $"stuff={(stuffDef != null ? stuffDef.defName : "null")} " +
                                            $"at {pos} rot={rot}");
                        }
                    }

                    if (cell.HasAnyData) cellCount++;
                }
            }

            if (LogInfo)
                Log.Message($"[GravshipExport] BuildFromLayout: finished. Structural cells={cellCount}, things added={thingCount}.");

            // ─────────────────────────────────────────────
            // Add orbital platform perimeter following ship shape
            // ─────────────────────────────────────────────
            var mechanoidPlatform = DefDatabase<TerrainDef>.GetNamedSilentFail("MechanoidPlatform");
            if (mechanoidPlatform != null)
            {
                // Iterate a slightly larger region to include perimeter
                for (int z = -1; z <= layout.height; z++)
                {
                    for (int x = -1; x <= layout.width; x++)
                    {
                        var pos = new IntVec3(x, 0, z);

                        // Skip if already part of ship
                        bool isShipCell = sketch.TerrainAt(pos) != null || sketch.Things.Exists(t => t.pos == pos);
                        if (isShipCell) continue;

                        // Check 4 neighbors for a ship cell
                        bool adjacentToShip =
                            (sketch.TerrainAt(new IntVec3(x + 1, 0, z)) != null) ||
                            (sketch.TerrainAt(new IntVec3(x - 1, 0, z)) != null) ||
                            (sketch.TerrainAt(new IntVec3(x, 0, z + 1)) != null) ||
                            (sketch.TerrainAt(new IntVec3(x, 0, z - 1)) != null) ||
                            (sketch.Things.Exists(t => t.pos == new IntVec3(x + 1, 0, z))) ||
                            (sketch.Things.Exists(t => t.pos == new IntVec3(x - 1, 0, z))) ||
                            (sketch.Things.Exists(t => t.pos == new IntVec3(x, 0, z + 1))) ||
                            (sketch.Things.Exists(t => t.pos == new IntVec3(x, 0, z - 1)));

                        if (adjacentToShip)
                        {
                            sketch.AddTerrain(mechanoidPlatform, pos);
                            if (LogInfo)
                                Log.Message($"[GravshipExport] Added MechanoidPlatform perimeter at {pos}");
                        }
                    }
                }
            }
            else
            {
                Log.Warning("[GravshipExport] Could not find TerrainDef 'MechanoidPlatform' — perimeter not added.");
            }



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
                Log.Error($"[GravshipExport] Failed to place GravEngine at {pos}: {ex}");
            }
        }

        private static TerrainDef ResolveTerrain(string defName, Dictionary<string, TerrainDef> cache)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            if (cache.TryGetValue(defName, out var td)) return td;

            td = DefDatabase<TerrainDef>.GetNamedSilentFail(defName);
            if (td == null && LogWarn)
                Log.Warning($"[GravshipExport] ResolveTerrain: unknown TerrainDef '{defName}'.");
            cache[defName] = td;
            return td;
        }

        private static ThingDef ResolveThing(string defName, Dictionary<string, ThingDef> cache)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            if (cache.TryGetValue(defName, out var td)) return td;

            td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (td == null && LogWarn)
                Log.Warning($"[GravshipExport] ResolveThing: unknown ThingDef '{defName}'.");
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
                /*if (def.terrainAffordanceNeeded == null)
                {
                    Log.Warning($"[GravshipExport] Def {def.defName} has no terrainAffordanceNeeded; skipping.");
                    return false;
                }*/
            }

            return true;
        }
    }
}
