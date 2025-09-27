using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace GravshipExport
{
    internal static class ShipListBuilder
    {
        public static List<ShipListItem> Build(ModContentPack content)
        {
            var rows = new List<ShipListItem>();

            foreach (var kvp in ShipManager.LoadedShips)
            {
                rows.Add(new ShipListItem
                {
                    Ship = kvp.Value,
                    IsExported = true,
                    ExportFilename = kvp.Key,
                    SourceLabel = "User Created"
                });
            }

            var exportedDefNames = new HashSet<string>(rows
                .Select(r => r.Ship?.defName)
                .Where(d => !string.IsNullOrEmpty(d)));

            var modDefs = DefDatabase<ShipLayoutDefV2>.AllDefsListForReading;
            foreach (var ship in modDefs)
            {
                if (ship == null || string.IsNullOrEmpty(ship.defName))
                {
                    continue;
                }

                if (exportedDefNames.Contains(ship.defName))
                {
                    continue;
                }

                string source = "Built-in";
                var pack = ship.modContentPack;
                if (pack != null)
                {
                    if (pack.PackageId.Equals(content.PackageId, StringComparison.OrdinalIgnoreCase))
                    {
                        source = "Built-in Example";
                    }
                    else
                    {
                        source = $"Mod: {pack.Name}";
                    }
                }

                rows.Add(new ShipListItem
                {
                    Ship = ship,
                    IsExported = false,
                    ExportFilename = null,
                    SourceLabel = source
                });
            }

            return rows
                .OrderBy(r => r.Ship?.label)
                .ToList();
        }
    }
}
