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
        public static void Export(Building_GravEngine engine, string customName = null, string customDescription = null)
        {
            try
            {
                GravshipLogger.Message("ExportV2 started.");

                var layout = BuildLayout(engine);
                if (layout == null)
                {
                    GravshipLogger.Warning("ExportV2 failed: layout was null.");
                    return;
                }

                // ✅ Use provided name if available
                if (!string.IsNullOrEmpty(customName))
                {
                    layout.defName = customName.Replace(" ", "_");
                    layout.label = customName;
                }

                // ✅ Set custom description if provided
                if (!string.IsNullOrWhiteSpace(customDescription))
                {
                    layout.description = customDescription.Trim();
                }

                string folder = Path.Combine(GenFilePaths.ConfigFolderPath, "GravshipExport");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string file = Path.Combine(folder, layout.defName + ".xml");
                string previewFile = Path.Combine(folder, layout.defName + ".png");

                DirectXmlSaver.SaveDataObject(layout, file);
                ShipPreviewRenderer.Capture(engine, layout, previewFile);

                // ✅ Show helpful popup guide after successful export
                string configPath = Path.Combine(GenFilePaths.ConfigFolderPath, "GravshipExport");
                Find.WindowStack.Add(new Dialog_ShipExportHelp(configPath, layout.defName));

                // ✅ Immediately refresh the loaded ship list so new export is visible in UI
                ShipManager.Refresh();

                GravshipLogger.Message($"ExportV2 complete! Saved to {file}");
            }
            catch (Exception ex)
            {
                GravshipLogger.Error($"ExportV2 crashed: {ex}");
            }
        }

        public static ShipLayoutDefV2 BuildLayout(Building_GravEngine engine)
        {
            var map = engine.Map;
            var cells = engine.AllConnectedSubstructure.ToList();
            if (cells.Count == 0)
            {
                GravshipLogger.Warning("Tried to export a gravship with no substructure.");
                return null;
            }

            // Expand bounds by one cell around the ship to include external attachments
            int minX = cells.Min(c => c.x) - 1;
            int maxX = cells.Max(c => c.x) + 1;
            int minZ = cells.Min(c => c.z) - 1;
            int maxZ = cells.Max(c => c.z) + 1;

            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;

            GravshipLogger.Message($"ExportV2 bounding box: {width}x{height} (x[{minX}-{maxX}], z[{minZ}-{maxZ}])");

            var rows = new List<List<ShipCell>>();

            // build fast lookup of hull cells
            var hull = new HashSet<IntVec3>(cells);

            // precompute attachment cells: all 8 neighbours of hull
            var attachmentCells = new HashSet<IntVec3>();
            foreach (var c in hull)
            {
                foreach (var d in GenAdj.AdjacentCellsAndInside)
                {
                    var n = c + d;
                    if (!hull.Contains(n) && n.InBounds(map))
                        attachmentCells.Add(n);
                }
            }

            // build the normal ship grid
            for (int z = minZ; z <= maxZ; z++)
            {
                var row = new List<ShipCell>();
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (!hull.Contains(cell))
                    {
                        row.Add(null);
                        continue;
                    }

                    var shipCell = new ShipCell();
                    TerrainDef terrain = cell.GetTerrain(map);
                    if (terrain != null && !terrain.IsSubstructure)
                        shipCell.terrainDef = terrain.defName;

                    bool hasStructures = false;
                    foreach (var thing in cell.GetThingList(map))
                    {
                        if (thing.def == engine.def) continue;
                        if (thing is Pawn) continue;
                        if (thing.def.category != ThingCategory.Building &&
                            thing.def.category != ThingCategory.Item)
                            continue;
                        if (thing.def.category == ThingCategory.Item)
                            continue;
                        if (thing.def.IsFilth || thing.def.IsCorpse || thing.def.IsPlant || thing.def.IsIngestible)
                            continue;
                        if (thing.Position != cell) continue;

                        hasStructures = true;

                        string stuffName = thing.def.MadeFromStuff && thing.Stuff != null
                            ? thing.Stuff.defName
                            : null;

                        var entry = new ShipThingEntry
                        {
                            defName = thing.def.defName,
                            stuffDef = stuffName,
                            rotInteger = thing.Rotation.AsInt
                        };

                        if (thing.TryGetComp<CompQuality>() is CompQuality q)
                            entry.quality = q.Quality.ToString();

                        shipCell.things.Add(entry);
                        GravshipLogger.Message($"Exported thing {thing.def.defName} at {cell}");
                    }

                    // foundations
                    bool shouldHaveFoundation = terrain != null &&
                        (terrain.isFoundation || terrain.IsSubstructure || hasStructures ||
                         (terrain.IsFloor || terrain.IsCarpet) && !terrain.natural && !terrain.IsSoil);
                    if (shouldHaveFoundation)
                        shipCell.foundationDef = "Substructure";

                    row.Add(shipCell.HasAnyData ? shipCell : null);
                }
                rows.Add(row);
            }

            // 🟩 Handle external attachments
            foreach (var pos in attachmentCells)
            {
                foreach (var thing in pos.GetThingList(map))
                {
                    var def = thing.def;
                    if (def?.building?.isAttachment != true)
                        continue;

                    string stuffName = def.MadeFromStuff && thing.Stuff != null ? thing.Stuff.defName : null;

                    var entry = new ShipThingEntry
                    {
                        defName = def.defName,
                        stuffDef = stuffName,
                        rotInteger = thing.Rotation.AsInt
                    };

                    if (thing.TryGetComp<CompQuality>() is CompQuality q)
                        entry.quality = q.Quality.ToString();

                    // Adjust relative grid position
                    int gx = pos.x - minX;
                    int gz = pos.z - minZ;

                    // ✅ SAFETY CHECK
                    if (gz < 0 || gz >= rows.Count || gx < 0 || gx >= rows[gz].Count)
                    {
                        GravshipLogger.Warning($"[AttachmentExport] Skipping {def.defName} at {pos}: out of bounds ({gx},{gz})");
                        continue;
                    }

                    if (rows[gz][gx] == null)
                        rows[gz][gx] = new ShipCell();

                    rows[gz][gx].things.Add(entry);
                    GravshipLogger.Message($"Exported attachment {def.defName} at {pos}");
                }
            }

            return new ShipLayoutDefV2
            {
                defName = "ExportedShip_" + Find.TickManager.TicksGame,
                label = engine.LabelCap + " Export",
                rows = rows,
                width = width,
                height = height,
                gravEngineX = engine.Position.x - minX,
                gravEngineZ = engine.Position.z - minZ
            };
        }

    }
}
