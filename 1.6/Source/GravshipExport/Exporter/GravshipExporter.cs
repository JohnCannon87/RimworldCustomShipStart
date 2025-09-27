// GravshipExporterV2.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;

namespace GravshipExport
{
    public static class GravshipExporter
    {
        public static void Export(Building_GravEngine engine, string customName = null)
        {
            try
            {
                //jcLog.Message("[GravshipExport] ExportV2 started.");

                var layout = BuildLayout(engine);
                if (layout == null)
                {
                    Log.Warning("[GravshipExport] ExportV2 failed: layout was null.");
                    return;
                }

                // Use provided name if available
                if (!string.IsNullOrEmpty(customName))
                {
                    layout.defName = customName.Replace(" ", "_");
                    layout.label = customName;
                }

                string folder = Path.Combine(GenFilePaths.ConfigFolderPath, "GravshipExport");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string file = Path.Combine(folder, layout.defName + ".xml");
                DirectXmlSaver.SaveDataObject(layout, file);

                //jcLog.Message($"[GravshipExport] ExportV2 complete! Saved to {file}");
            }
            catch (Exception ex)
            {
                Log.Error($"[GravshipExport] ExportV2 crashed: {ex}");
            }
        }

        public static ShipLayoutDefV2 BuildLayout(Building_GravEngine engine)
        {
            var map = engine.Map;
            var cells = engine.AllConnectedSubstructure.ToList();
            if (cells.Count == 0)
            {
                Log.Warning("[GravshipExport] Tried to export a gravship with no substructure.");
                return null;
            }

            int minX = cells.Min(c => c.x);
            int maxX = cells.Max(c => c.x);
            int minZ = cells.Min(c => c.z);
            int maxZ = cells.Max(c => c.z);

            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;

            //jcLog.Message($"[GravshipExport] ExportV2 bounding box: {width}x{height} (x[{minX}-{maxX}], z[{minZ}-{maxZ}])");

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

                    // Terrain
                    TerrainDef terrain = cell.GetTerrain(map);
                    if (terrain != null && !terrain.IsSubstructure)
                    {
                        // Save the visible floor/terrain if it's not just substructure
                        shipCell.terrainDef = terrain.defName;
                    }

                    // Things
                    bool hasStructures = false;
                    foreach (var thing in cell.GetThingList(map))
                    {
                        if (thing.def == engine.def) continue;
                        if (thing is Pawn) continue;

                        // Skip non-buildings / non-structures / loose items
                        if (thing.def.category != ThingCategory.Building && thing.def.category != ThingCategory.Item)
                            continue;
                        if (thing.def.category == ThingCategory.Item)
                            continue;
                        if (thing.def.IsFilth || thing.def.IsCorpse || thing.def.IsPlant || thing.def.IsIngestible)
                            continue;
                        if (thing.Position != cell) continue;

                        hasStructures = true;

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

                        /*Log.Message($"[GravshipExport] Exported thing {thing.def.defName} " +
                                    $"at world=({thing.Position.x},{thing.Position.z}) " +
                                    $"grid=({x - minX},{z - minZ}) " +
                                    $"size={thing.def.size} rot={thing.Rotation.AsInt} stuff={stuffName ?? "null"}");*/
                    }

                    // 🔎 Smart foundation inference
                    bool shouldHaveFoundation = false;

                    if (terrain != null && (terrain.isFoundation || terrain.IsSubstructure))
                    {
                        shouldHaveFoundation = true;
                    }
                    else if (hasStructures)
                    {
                        shouldHaveFoundation = true;
                    }
                    else if (terrain != null && (terrain.IsFloor || terrain.IsCarpet) && !terrain.natural && !terrain.IsSoil)
                    {
                        shouldHaveFoundation = true;
                    }

                    if (shouldHaveFoundation)
                    {
                        shipCell.foundationDef = "Substructure";
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
