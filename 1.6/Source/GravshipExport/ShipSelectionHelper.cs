using System.Collections.Generic;
using Verse;

namespace GravshipExport
{
    internal static class ShipSelectionHelper
    {
        public static string GetCurrentSelectionKey(GravshipExportModSettings settings, Dictionary<string, ShipLayoutDefV2> loadedShips)
        {
            if (settings?.lastUsedShip == null)
            {
                return null;
            }

            string defName = settings.lastUsedShip.defName;
            if (loadedShips != null)
            {
                foreach (var kvp in loadedShips)
                {
                    if (ReferenceEquals(kvp.Value, settings.lastUsedShip))
                    {
                        return kvp.Key;
                    }
                }

                foreach (var kvp in loadedShips)
                {
                    if (kvp.Value?.defName == defName)
                    {
                        return kvp.Key;
                    }
                }
            }

            return defName;
        }
    }
}
