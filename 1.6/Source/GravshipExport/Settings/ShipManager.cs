using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace GravshipExport
{
    public static class ShipManager
    {
        private static readonly Dictionary<string, ShipLayoutDefV2> loadedShips = new Dictionary<string, ShipLayoutDefV2>();

        private static string ExportFolder =>
            Path.Combine(GenFilePaths.ConfigFolderPath, "GravshipExport");

        public static IReadOnlyDictionary<string, ShipLayoutDefV2> LoadedShips => loadedShips;

        /// <summary>
        /// Clears the in-memory ship cache.
        /// </summary>
        public static void Clear()
        {
            loadedShips.Clear();
            GravshipLogger.Message("Cleared loaded ship cache.");
        }

        /// <summary>
        /// Reloads all exported ships from disk into memory.
        /// </summary>
        public static void Refresh()
        {
            Clear();

            if (!Directory.Exists(ExportFolder))
            {
                GravshipLogger.Warning($"Export folder does not exist: {ExportFolder}");
                return;
            }

            int loadedCount = 0;
            foreach (var file in Directory.GetFiles(ExportFolder, "*.xml"))
            {
                try
                {
                    ShipLayoutDefV2 ship = DirectXmlLoader.ItemFromXmlFile<ShipLayoutDefV2>(file);
                    if (ship != null)
                    {
                        loadedShips[Path.GetFileName(file)] = ship;
                        loadedCount++;
                        GravshipLogger.Message($"Loaded ship from {file} (defName={ship.defName})");
                    }
                    else
                    {
                        GravshipLogger.Warning($"File {file} did not contain a valid ship definition.");
                    }
                }
                catch (Exception ex)
                {
                    GravshipLogger.Error($"Failed to load ship from {file}: {ex}");
                }
            }

            GravshipLogger.Message($"Refresh complete. {loadedCount} ships loaded.");
        }
    }
}
