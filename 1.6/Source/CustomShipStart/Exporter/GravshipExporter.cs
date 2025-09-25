// GravshipExporterV2.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;

namespace RimworldCustomShipStart
{
    public static class GravshipExporter
    {
        public static void Export(Building_GravEngine engine, string customName = null)
        {
            try
            {
                Log.Message("[CustomShipStart] ExportV2 started.");

                var layout = BuildLayout(engine);
                if (layout == null)
                {
                    Log.Warning("[CustomShipStart] ExportV2 failed: layout was null.");
                    return;
                }

                // Use provided name if available
                if (!string.IsNullOrEmpty(customName))
                {
                    layout.defName = customName.Replace(" ", "_");
                    layout.label = customName;
                }

                string folder = Path.Combine(GenFilePaths.ConfigFolderPath, "CustomShipStart");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string file = Path.Combine(folder, layout.defName + ".xml");
                DirectXmlSaver.SaveDataObject(layout, file);

                Log.Message($"[CustomShipStart] ExportV2 complete! Saved to {file}");
            }
            catch (Exception ex)
            {
                Log.Error($"[CustomShipStart] ExportV2 crashed: {ex}");
            }
        }

        public static ShipLayoutDefV2 BuildLayout(Building_GravEngine engine)
        {
            var map = engine.Map;
            var cells = engine.AllConnectedSubstructure.ToList();
            if (cells.Count == 0)
            {
                Log.Warning("[CustomShipStart] Tried to export a gravship with no substructure.");
                return null;
            }

            int minX = cells.Min(c => c.x);
            int maxX = cells.Max(c => c.x);
            int minZ = cells.Min(c => c.z);
            int maxZ = cells.Max(c => c.z);

            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;

            Log.Message($"[CustomShipStart] ExportV2 bounding box: {width}x{height} (x[{minX}-{maxX}], z[{minZ}-{maxZ}])");

            var rows = new List<List<ShipCell>>();

            // bottom → top (minZ first)
            for (int z = minZ; z <= maxZ; z++)
            {
                var row = new List<ShipCell>();
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (!cells.Contains(cell))
                    {
                        row.Add(null);
                        continue;
                    }

                    var shipCell = new ShipCell();

                    // Foundation terrain
                    var foundation = cell.GetTerrain(map);
                    if (foundation != null)
                        shipCell.foundationDef = foundation.defName;

                    // Things
                    foreach (var thing in cell.GetThingList(map))
                    {
                        if (thing.def == engine.def) continue;
                        if (thing is Pawn) continue;
                        // Skip non-buildings, non-structures, and any loose items
                        if (thing.def.category != ThingCategory.Building && thing.def.category != ThingCategory.Item)
                            continue;

                        // Skip loose items (e.g. meals, steel) — we only care about buildings and installed furniture
                        if (thing.def.category == ThingCategory.Item)
                            continue;

                        // Skip filth, corpses, apparel, projectiles, motes, etc.
                        if (thing.def.IsFilth || thing.def.IsCorpse || thing.def.IsPlant || thing.def.IsIngestible)
                            continue;

                        // Only add once, at root cell
                        if (thing.Position != cell) continue;

                        string stuffName = null;
                        if (thing.def.MadeFromStuff && thing.Stuff != null)
                            stuffName = thing.Stuff.defName;

                        var entry = new ShipThingEntry
                        {
                            defName = thing.def.defName,
                            stuffDef = stuffName,
                            rotInteger = thing.Rotation.AsInt
                        };

                        shipCell.things.Add(entry);

                        Log.Message($"[CustomShipStart] Exported thing {thing.def.defName} " +
                                    $"at world=({thing.Position.x},{thing.Position.z}) " +
                                    $"grid=({x - minX},{z - minZ}) " +
                                    $"size={thing.def.size} rot={thing.Rotation.AsInt} stuff={stuffName ?? "null"}");
                    }

                    row.Add(shipCell.HasAnyData ? shipCell : null);
                }
                rows.Add(row);
            }

            return new ShipLayoutDefV2
            {
                defName = "ExportedShip_" + Find.TickManager.TicksGame,
                label = engine.LabelCap + " Export",
                rows = rows,
                width = width,
                height = height,
                gravEngineX = engine.Position.x - minX,
                gravEngineZ = engine.Position.z - minZ // no flip needed now
            };
        }
    }
}
