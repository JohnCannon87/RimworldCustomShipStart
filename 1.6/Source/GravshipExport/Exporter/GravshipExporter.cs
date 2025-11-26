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
            GravshipLogger.Message("ExportV2: Entered Export method.");

            try
            {
                if (engine == null)
                {
                    GravshipLogger.Error("ExportV2: engine was null. Aborting export.");
                    return;
                }

                GravshipLogger.Message(
                    string.Format("ExportV2: Starting BuildLayout for engine at {0}.",
                        engine.Position));

                var layout = BuildLayout(engine);

                if (layout == null)
                {
                    GravshipLogger.Warning("ExportV2: BuildLayout returned null layout. Aborting export.");
                    return;
                }

                try
                {
                    GravshipLogger.Message(
                        string.Format("ExportV2: Layout built. defName={0}, label={1}, rows={2}.",
                            layout.defName ?? "null",
                            layout.label ?? "null",
                            layout.rows != null ? layout.rows.Count.ToString() : "rows=null"));
                }
                catch (Exception exInfo)
                {
                    GravshipLogger.Warning("ExportV2: Failed to log layout summary: " + exInfo);
                }

                // ✅ Use provided name if available
                GravshipLogger.Message(
                    string.Format("ExportV2: Applying custom name/description. customName='{0}', customDescription length={1}.",
                        customName ?? "null",
                        customDescription != null ? customDescription.Length.ToString() : "null"));

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

                GravshipLogger.Message(
                    string.Format("ExportV2: Final layout metadata: defName={0}, label={1}.",
                        layout.defName ?? "null",
                        layout.label ?? "null"));

                string folder = Path.Combine(GenFilePaths.ConfigFolderPath, "GravshipExport");
                GravshipLogger.Message("ExportV2: Target folder resolved to: " + folder);

                if (!Directory.Exists(folder))
                {
                    GravshipLogger.Message("ExportV2: Target folder does not exist. Creating directory...");
                    Directory.CreateDirectory(folder);
                    GravshipLogger.Message("ExportV2: Directory created.");
                }

                string file = Path.Combine(folder, layout.defName + ".xml");
                string previewFile = Path.Combine(folder, layout.defName + ".png");

                GravshipLogger.Message(
                    string.Format("ExportV2: File paths set. xml='{0}', preview='{1}'.",
                        file, previewFile));

                GravshipLogger.Message("ExportV2: About to save XML via DirectXmlSaver.SaveDataObject.");
                try
                {
                    DirectXmlSaver.SaveDataObject(layout, file);
                    GravshipLogger.Message("ExportV2: XML saved successfully.");
                }
                catch (Exception exSave)
                {
                    GravshipLogger.Error("ExportV2: DirectXmlSaver.SaveDataObject threw: " + exSave);
                    // Rethrow so RimWorld gives a red error with full stack trace
                    throw;
                }

                GravshipLogger.Message("ExportV2: About to capture preview via ShipPreviewRenderer.Capture.");
                ShipPreviewRenderer.Capture(engine, layout, previewFile);
                GravshipLogger.Message("ExportV2: Preview capture completed.");

                // ✅ Show helpful popup guide after successful export
                string configPath = Path.Combine(GenFilePaths.ConfigFolderPath, "GravshipExport");
                GravshipLogger.Message(
                    string.Format("ExportV2: About to show Dialog_ShipExportHelp. configPath='{0}', defName='{1}'.",
                        configPath, layout.defName ?? "null"));

                Find.WindowStack.Add(new Dialog_ShipExportHelp(configPath, layout.defName));
                GravshipLogger.Message("ExportV2: Dialog_ShipExportHelp added to WindowStack.");

                // ✅ Immediately refresh the loaded ship list so new export is visible in UI
                GravshipLogger.Message("ExportV2: About to call ShipManager.Refresh().");
                ShipManager.Refresh();
                GravshipLogger.Message("ExportV2: ShipManager.Refresh() complete.");

                GravshipLogger.Message("ExportV2 complete! Saved to " + file);
            }
            catch (Exception ex)
            {
                GravshipLogger.Error("ExportV2 crashed: " + ex);
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
                        GravshipLogger.Message($"Exporting thing {thing.def.defName} at {cell}");
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
                        ShipThingEntry entry = CreateNewThingEntry(thing, stuffName);

                        if (thing.TryGetComp<CompQuality>() is CompQuality q)
                            entry.quality = q.Quality.ToString();

                        shipCell.things.Add(entry);
                        GravshipLogger.Message($"Exported thing {thing.def.defName} at {cell}");
                    }
                    GravshipLogger.Message($"Exported everything at {cell}");

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
            GravshipLogger.Message($"Exported Ship Grid, now exporting attachments");

            // 🟩 Handle external attachments
            foreach (var pos in attachmentCells)
            {
                foreach (var thing in pos.GetThingList(map))
                {
                    var def = thing.def;
                    if (def?.building?.isAttachment != true)
                        continue;
                    GravshipLogger.Message($"Exporting thing {def.defName} at {pos}");

                    string stuffName = def.MadeFromStuff && thing.Stuff != null ? thing.Stuff.defName : null;

                    ShipThingEntry entry = CreateNewThingEntry(thing, stuffName);

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
            GravshipLogger.Message($"Exported Ship Attachments, now exporting Ship");

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

        private static ShipThingEntry CreateNewThingEntry(Thing thing, string stuffName)
        {
            var entry = new ShipThingEntry
            {
                defName = thing.def.defName,
                stuffDef = stuffName,
                rotInteger = thing.Rotation.AsInt
            };

            CompQuality qualityComp = thing.TryGetComp<CompQuality>();
            if (qualityComp != null)
            {
                entry.quality = qualityComp.Quality.ToString();
            }

            // 1) StorageSettings – copy whole object
            IStoreSettingsParent ssp = thing as IStoreSettingsParent;
            if (ssp != null)
            {
                entry.exportedStorageSettings = ThingSettingsExportHelper.SnapshotStorageSettings(thing);
            }
            else
            {
                entry.exportedStorageSettings = null;
            }

            // 2) Generic comp snapshot for primitive values (optional but “smart”)
            entry.compSettings = ThingSettingsExportHelper.SnapshotThingComps(thing);

            // new: capture plant selection for hydroponics / grow buildings.
            ThingDef plantDef = ThingSettingsExportHelper.GetPlantToGrowDef(thing);
            if (plantDef != null)
                entry.plantToGrowDef = plantDef.defName;

            return entry;
        }
    }
}
