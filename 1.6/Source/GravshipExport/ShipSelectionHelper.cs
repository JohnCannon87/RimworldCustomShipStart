// ShipSelectionHelper.cs
using System;
using System.Collections.Generic;

namespace GravshipExport
{
    internal static class ShipSelectionHelper
    {
        /// <summary>
        /// Always return the canonical selection key: the ship's defName.
        /// </summary>
        public static string GetCurrentSelectionKey(GravshipExportModSettings settings, IReadOnlyDictionary<string, ShipLayoutDefV2> _)
        {
            var defName = settings?.lastUsedShip?.defName;
            return string.IsNullOrEmpty(defName) ? null : defName;
        }

        public static bool IsSelected(string currentSelectionKey, ShipListItem item)
        {
            if (string.IsNullOrEmpty(currentSelectionKey)) return false;
            var defName = item?.Ship?.defName;
            return !string.IsNullOrEmpty(defName)
                   && string.Equals(defName, currentSelectionKey, StringComparison.OrdinalIgnoreCase);
        }
    }
}
