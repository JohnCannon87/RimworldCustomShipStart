using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace RimworldCustomShipStart
{
    public static class ShipManager
    {
        private static Dictionary<string, ShipLayoutDefV2> loadedShips = new Dictionary<string, ShipLayoutDefV2>();

        private static string ExportFolder =>
            Path.Combine(GenFilePaths.ConfigFolderPath, "CustomShipStart");

        public static IReadOnlyDictionary<string, ShipLayoutDefV2> LoadedShips => loadedShips;

        public static void Clear()
        {
            loadedShips.Clear();
            Log.Message("[CustomShipStart] Cleared loaded ship cache.");
        }

        public static void Refresh()
        {
            Clear();

            if (!Directory.Exists(ExportFolder))
            {
                Log.Warning($"[CustomShipStart] Export folder does not exist: {ExportFolder}");
                return;
            }

            foreach (var file in Directory.GetFiles(ExportFolder, "*.xml"))
            {
                try
                {
                    ShipLayoutDefV2 ship = DirectXmlLoader.ItemFromXmlFile<ShipLayoutDefV2>(file);
                    if (ship != null)
                    {
                        loadedShips[Path.GetFileName(file)] = ship;
                        Log.Message($"[CustomShipStart] Loaded ship from {file} (defName={ship.defName})");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CustomShipStart] Failed to load ship from {file}: {ex}");
                }
            }

            Log.Message($"[CustomShipStart] Refresh complete. {loadedShips.Count} ships loaded.");
        }

    }
    
}
