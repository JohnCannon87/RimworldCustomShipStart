using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace GravshipExport
{
    internal sealed class ShipListView
    {
        private readonly Func<List<ShipListItem>> rowsProvider;
        private readonly ShipRowDrawer rowDrawer;
        private Vector2 scrollPosition;

        public ShipListView(Func<List<ShipListItem>> rowsProvider, ShipRowDrawer rowDrawer)
        {
            this.rowsProvider = rowsProvider;
            this.rowDrawer = rowDrawer;
        }

        public void Draw(Rect outRect, string searchText, string currentSelectionKey, ShipListCallbacks callbacks)
        {
            var rows = rowsProvider?.Invoke() ?? new List<ShipListItem>();
            if (rows.Count == 0)
            {
                Widgets.Label(outRect, "No ships found.\n\n• Export a ship in-game\n• Load a mod with ShipLayoutDefV2.");
                return;
            }

            string filter = string.IsNullOrWhiteSpace(searchText) ? null : searchText.ToLowerInvariant();
            if (!string.IsNullOrEmpty(filter))
            {
                rows = rows
                    .Where(r => MatchesFilter(r, filter))
                    .ToList();

                if (rows.Count == 0)
                {
                    Widgets.Label(outRect, "No ships match your search.");
                    return;
                }
            }

            float viewHeight = rows.Count * ShipRowDrawer.RowHeight;
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            for (int i = 0; i < rows.Count; i++)
            {
                var item = rows[i];
                var rowRect = new Rect(0f, i * ShipRowDrawer.RowHeight, viewRect.width, ShipRowDrawer.RowHeight);

                string rowKey = item.IsExported ? item.ExportFilename : item.Ship?.defName;
                bool isCurrent = !string.IsNullOrEmpty(currentSelectionKey) &&
                                 !string.IsNullOrEmpty(rowKey) &&
                                 currentSelectionKey.Equals(rowKey, StringComparison.OrdinalIgnoreCase);

                var result = rowDrawer.DrawRow(rowRect, item, isCurrent, i % 2 == 1);
                if (result.DeleteRequested)
                {
                    callbacks?.DeleteRequested?.Invoke(item);
                }

                if (result.ApplyRequested)
                {
                    callbacks?.ApplyRequested?.Invoke(item);
                }

                if (result.ExportRequested)
                {
                    callbacks?.ExportRequested?.Invoke(item);
                }
            }

            Widgets.EndScrollView();
        }

        private static bool MatchesFilter(ShipListItem item, string filter)
        {
            string label = item.Ship?.label?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(label) && label.Contains(filter))
            {
                return true;
            }

            string defName = item.Ship?.defName?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(defName) && defName.Contains(filter))
            {
                return true;
            }

            if (item.IsExported)
            {
                string filename = item.ExportFilename?.ToLowerInvariant();
                if (!string.IsNullOrEmpty(filename) && filename.Contains(filter))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
