using System;
using System.Collections.Generic;
using System.Linq;
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
        private static float originalRange = -1f;

        public static ShipLayoutDefV2 CurrentLayout { get; private set; }
        public static Sketch CurrentSketch { get; private set; }

        /// <summary>
        /// Convert a ShipLayoutDefV2 into a Sketch.
        /// </summary>
        public static Sketch BuildFromLayout(ShipLayoutDefV2 layout)
        {
            if (layout == null)
            {
                GravshipLogger.Error("BuildFromLayout: layout is null.");
                return new Sketch();
            }
            if (layout.rows == null || layout.rows.Count == 0)
            {
                GravshipLogger.Error($"BuildFromLayout: layout '{layout.defName}' has no rows.");
                return new Sketch();
            }

            GravshipLogger.Message($"Building layout '{layout.defName}' size {layout.width}x{layout.height}, rows={layout.rows.Count}.");
            GravshipLogger.Message($"Engine offset: ({layout.gravEngineX}, {layout.gravEngineZ}).");

            var sketch = new Sketch();

            // ⭐ Store for later use by Harmony patches
            CurrentLayout = layout;
            CurrentSketch = sketch;

            // 1️⃣ Place the core grav engine
            TryAddGravEngine(sketch, new IntVec3(layout.gravEngineX, 0, layout.gravEngineZ));

            ExpandGravEngineRange();

            var terrainCache = new Dictionary<string, TerrainDef>(StringComparer.Ordinal);
            var thingCache = new Dictionary<string, ThingDef>(StringComparer.Ordinal);

            int cellCount = 0;
            int thingCount = 0;

            // 3️⃣ Normal placement pass (terrain, other things, etc.)
            for (int z = 0; z < layout.rows.Count; z++)
            {
                var row = layout.rows[z];
                if (row == null)
                {
                    GravshipLogger.Warning($"Row {z} is null, skipping.");
                    continue;
                }

                for (int x = 0; x < row.Count; x++)
                {
                    var cell = row[x];
                    if (cell == null) continue;

                    var pos = new IntVec3(x, 0, z);
                    GravshipLogger.Message($"Processing cell {pos}: {cell}");

                    // Foundation layer (always part of sketch)
                    var foundation = ResolveTerrain(cell.foundationDef, terrainCache);
                    if (foundation != null)
                        sketch.AddTerrain(foundation, pos);

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
                                GravshipLogger.Warning($"Skipping invalid thing '{t.defName}' at {pos}");
                                continue;
                            }

                            var stuffDef = ResolveThing(t.stuffDef, thingCache); // may be null
                            var rot = new Rot4(t.rotInteger);

                            if (stuffDef != null && !thingDef.MadeFromStuff)
                                stuffDef = null;

                            sketch.AddThing(thingDef, pos, rot, stuffDef, 1, null, null, false);
                            thingCount++;

                            GravshipLogger.Message($"Added {thingDef.defName} (stuff={(stuffDef != null ? stuffDef.defName : "null")}) at {pos} rot={rot}");
                        }
                    }

                    if (cell.HasAnyData) cellCount++;
                }
            }

            GravshipLogger.Message($"Finished building sketch. Structural cells={cellCount}, things added={thingCount}.");

            // Perimeter: MechanoidPlatform layer (unchanged)
            var mechanoidPlatform = DefDatabase<TerrainDef>.GetNamedSilentFail("MechanoidPlatform");
            if (mechanoidPlatform != null)
            {
                for (int z = -1; z <= layout.height; z++)
                {
                    for (int x = -1; x <= layout.width; x++)
                    {
                        var pos = new IntVec3(x, 0, z);
                        if (pos.x < 0 || pos.z < 0 || pos.x >= layout.width || pos.z >= layout.height)
                            continue;

                        bool isShipCell = sketch.TerrainAt(pos) != null || sketch.Things.Exists(t => t.pos == pos);
                        if (isShipCell) continue;

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
                            GravshipLogger.Message($"Added MechanoidPlatform perimeter at {pos}");
                        }
                    }
                }
            }
            else
            {
                GravshipLogger.Warning("Could not find TerrainDef 'MechanoidPlatform' — perimeter not added.");
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
                GravshipLogger.Error($"Failed to place GravEngine at {pos}: {ex}");
            }
        }

        private static TerrainDef ResolveTerrain(string defName, Dictionary<string, TerrainDef> cache)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            if (cache.TryGetValue(defName, out var td)) return td;

            td = DefDatabase<TerrainDef>.GetNamedSilentFail(defName);
            if (td == null)
                GravshipLogger.Warning($"ResolveTerrain: unknown TerrainDef '{defName}'.");
            cache[defName] = td;
            return td;
        }

        private static ThingDef ResolveThing(string defName, Dictionary<string, ThingDef> cache)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            if (cache.TryGetValue(defName, out var td)) return td;

            td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (td == null)
                GravshipLogger.Warning($"ResolveThing: unknown ThingDef '{defName}'.");
            cache[defName] = td;
            return td;
        }

        private static bool IsValidForSketch(ThingDef def)
        {
            if (def == null) return false;
            if (def.IsBlueprint || def.IsFrame) return false;
            if (def.category != ThingCategory.Building && def.category != ThingCategory.Item) return false;
            return true;
        }

        private static void ExpandGravEngineRange()
        {
            var comp = ThingDefOf.GravEngine?.comps?.FirstOrDefault(c => c is CompProperties_SubstructureFootprint) as CompProperties_SubstructureFootprint;
            if (comp != null)
            {
                if (originalRange < 0f)
                    originalRange = comp.radius;

                comp.radius = 79;
                GravshipLogger.Message($"Temporarily expanded GravEngine facility range from {originalRange} → {comp.radius}");
            }
            else
            {
                GravshipLogger.Warning("Could not find CompProperties_Facility on GravEngine to expand range.");
            }
        }

        public static void RestoreGravEngineRange()
        {
            var comp = ThingDefOf.GravEngine?.comps?.FirstOrDefault(c => c is CompProperties_SubstructureFootprint) as CompProperties_SubstructureFootprint;
            if (comp != null && originalRange > 0f)
            {
                comp.radius = originalRange;
                GravshipLogger.Message($"Restored GravEngine facility range to {originalRange}");
            }
        }
    }
}
