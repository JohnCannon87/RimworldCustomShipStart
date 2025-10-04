using System;
using System.IO;
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

        // NEW: remember per-row description scroll
        private readonly Dictionary<int, Vector2> _descScrollByRow = new Dictionary<int, Vector2>();

        // 📏 Slightly reduced sizes for better fit
        private const float ThumbnailSize = 300f;
        private const float ThumbnailPadding = 10f;
        private const float ButtonAreaHeight = 34f;

        public ShipListView(Func<List<ShipListItem>> rowsProvider, ShipRowDrawer rowDrawer)
        {
            this.rowsProvider = rowsProvider;
            this.rowDrawer = rowDrawer;
        }

        public void Draw(Rect outRect, string searchText, string currentSelectionKey, ShipListCallbacks callbacks)
        {
            outRect.width = Mathf.Max(outRect.width, 880f);

            var rows = rowsProvider?.Invoke() ?? new List<ShipListItem>();
            if (rows.Count == 0)
            {
                Widgets.Label(outRect, "No ships found.\n\n• Export a ship in-game\n• Load a mod with ShipLayoutDefV2.");
                return;
            }

            string filter = string.IsNullOrWhiteSpace(searchText) ? null : searchText.ToLowerInvariant();
            if (!string.IsNullOrEmpty(filter))
            {
                rows = rows.Where(r => MatchesFilter(r, filter)).ToList();
                if (rows.Count == 0)
                {
                    Widgets.Label(outRect, "No ships match your search.");
                    return;
                }
            }

            var settings = LoadedModManager.GetMod<GravshipExportMod>()?.GetSettings<GravshipExportModSettings>();
            HashSet<string> randomPool = (settings?.randomShipPool != null)
                ? new HashSet<string>(settings.randomShipPool)
                : new HashSet<string>();

            float rowHeight = ThumbnailSize + (ThumbnailPadding * 2) + ButtonAreaHeight;
            float viewHeight = rows.Count * rowHeight;

            var viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            for (int i = 0; i < rows.Count; i++)
            {
                var item = rows[i];
                viewRect = DrawRow(currentSelectionKey, callbacks, rows, settings, randomPool, rowHeight, viewRect, i, item);
            }

            Widgets.EndScrollView();
        }

        private Rect DrawRow(string currentSelectionKey, ShipListCallbacks callbacks, List<ShipListItem> rows, GravshipExportModSettings settings, HashSet<string> randomPool, float rowHeight, Rect viewRect, int i, ShipListItem item)
        {
            var rowRect = new Rect(0f, i * rowHeight, viewRect.width, rowHeight);

            bool isSelected = ShipSelectionHelper.IsSelected(currentSelectionKey, item);
            bool inRandomPool = item.Ship?.defName != null && randomPool.Contains(item.Ship.defName);

            // Highlight
            if (settings?.randomSelectionEnabled == true)
            {
                if (inRandomPool)
                    Widgets.DrawBoxSolid(rowRect, new Color(0.85f, 0.75f, 0.2f, 0.2f));
            }
            else if (isSelected)
            {
                Widgets.DrawBoxSolid(rowRect, new Color(0.3f, 0.55f, 0.3f, 0.25f));
            }

            // 📸 Ship preview
            Rect previewRect = new Rect(
                rowRect.x + ThumbnailPadding,
                rowRect.y + ThumbnailPadding,
                ThumbnailSize,
                ThumbnailSize
            );

            Rect innerRect = previewRect.ContractedBy(4f);
            Widgets.DrawBoxSolid(previewRect, new Color(0.08f, 0.08f, 0.08f, 0.95f));

            Color borderColor = new Color(0.35f, 0.5f, 0.35f, 0.7f);
            float thickness = 2f;
            Widgets.DrawBoxSolid(new Rect(previewRect.x, previewRect.y, previewRect.width, thickness), borderColor);
            Widgets.DrawBoxSolid(new Rect(previewRect.x, previewRect.yMax - thickness, previewRect.width, thickness), borderColor);
            Widgets.DrawBoxSolid(new Rect(previewRect.x, previewRect.y, thickness, previewRect.height), borderColor);
            Widgets.DrawBoxSolid(new Rect(previewRect.xMax - thickness, previewRect.y, thickness, previewRect.height), borderColor);

            Texture2D preview = ShipPreviewUtility.GetPreviewFor(item);
            if (preview != null)
            {
                float texAspect = (float)preview.width / preview.height;
                float rectAspect = innerRect.width / innerRect.height;
                Rect drawRect = innerRect;

                if (texAspect > rectAspect)
                {
                    float scaledHeight = innerRect.width / texAspect;
                    drawRect.y += (innerRect.height - scaledHeight) / 2f;
                    drawRect.height = scaledHeight;
                }
                else
                {
                    float scaledWidth = innerRect.height * texAspect;
                    drawRect.x += (innerRect.width - scaledWidth) / 2f;
                    drawRect.width = scaledWidth;
                }

                GUI.DrawTexture(drawRect, preview, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.Label(innerRect.ContractedBy(10f), "No Preview");
            }

            // 📝 Metadata (leave room for buttons)
            float textStartX = previewRect.xMax + 16f;
            Rect metaRect = new Rect(
                textStartX,
                rowRect.y + ThumbnailPadding,
                rowRect.width - textStartX - 20f,
                rowHeight - (ThumbnailPadding * 2) - ButtonAreaHeight
            );

            DrawShipMetadata(metaRect, item, i);

            // --- Buttons ---
            float buttonY = rowRect.yMax - ButtonAreaHeight + 4f;
            float buttonWidth = 130f;
            float buttonHeight = 26f;
            float spacing = 8f;

            Rect exportRect = new Rect(rowRect.xMax - buttonWidth - 12f, buttonY, buttonWidth, buttonHeight);
            Rect deleteRect = new Rect(exportRect.x - buttonWidth - spacing, buttonY, buttonWidth, buttonHeight);

            if (settings?.randomSelectionEnabled == true && item.Ship?.defName != null)
            {
                bool inPoolNow = randomPool.Contains(item.Ship.defName);
                string label = inPoolNow ? "❌ Exclude from Pool" : "✅ Include in Pool";
                Rect toggleRect = new Rect(deleteRect.x - (buttonWidth * 1.2f) - spacing, buttonY, buttonWidth * 1.2f, buttonHeight);

                if (Widgets.ButtonText(toggleRect, label))
                {
                    if (inPoolNow) randomPool.Remove(item.Ship.defName);
                    else randomPool.Add(item.Ship.defName);

                    var s = LoadedModManager.GetMod<GravshipExportMod>()?.GetSettings<GravshipExportModSettings>();
                    if (s != null)
                    {
                        s.randomShipPool = randomPool.ToList();
                        s.Write();
                    }
                }
            }
            else
            {
                Rect applyRect = new Rect(deleteRect.x - buttonWidth - spacing, buttonY, buttonWidth, buttonHeight);
                if (Widgets.ButtonText(applyRect, "Apply"))
                    callbacks?.ApplyRequested?.Invoke(item);
            }

            if (item.IsExported && Widgets.ButtonText(exportRect, "Export as Mod"))
                callbacks?.ExportRequested?.Invoke(item);

            if (item.IsExported && Widgets.ButtonText(deleteRect, "Delete"))
                callbacks?.DeleteRequested?.Invoke(item);

            // --- Divider between rows (draw last so it sits on top) ---
            if (i < rows.Count - 1)
            {
                // Slight inset from edges; thin and subtle
                float inset = 8f;
                float yLine = rowRect.yMax - 1f;

                // Single crisp line
                Widgets.DrawLineHorizontal(rowRect.x + inset, yLine, rowRect.width - inset * 2f, new Color(1f, 1f, 1f, 0.10f));

                // Optional soft shadow line just below for a tiny bit of depth:
                // Widgets.DrawLineHorizontal(rowRect.x + inset, yLine + 1f, rowRect.width - inset * 2f, new Color(0f, 0f, 0f, 0.06f));
            }

            return viewRect;
        }

        private void DrawShipMetadata(Rect rect, ShipListItem item, int rowIndex)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            string shipName = item.Ship?.label
                ?? (item.ExportFilename != null ? Path.GetFileNameWithoutExtension(item.ExportFilename) : "Unnamed Ship");

            int width = item.Ship?.width ?? 0;
            int height = item.Ship?.height ?? 0;
            string size = $"{width}×{height}";

            int thingCount = 0;
            float totalWealth = 0f;

            // 🔍 Count all placed things and calculate wealth
            if (item.Ship?.rows != null)
            {
                foreach (var row in item.Ship.rows)
                {
                    if (row == null) continue;
                    foreach (var cell in row)
                    {
                        if (cell?.things == null) continue;
                        foreach (var entry in cell.things)
                        {
                            thingCount++;

                            if (!string.IsNullOrEmpty(entry.defName))
                            {
                                var def = DefDatabase<ThingDef>.GetNamedSilentFail(entry.defName);
                                if (def != null)
                                {
                                    totalWealth += def.BaseMarketValue;
                                }
                            }
                        }
                    }
                }
            }

            string description = !string.IsNullOrEmpty(item.Ship?.description)
                ? item.Ship.description
                : "No description provided.";

            // 🧭 Draw metadata
            Text.Font = GameFont.Medium;
            listing.Label($"🛠 Ship Name: {shipName}");
            Text.Font = GameFont.Small;
            listing.Label($"📏 Size: {size}");
            listing.Label($"🧱 Things: {thingCount}");
            listing.Label($"💰 Wealth: {totalWealth.ToStringMoney()}");
            listing.Label($"📦 Source: {item.SourceLabel ?? "Unknown"}");
            listing.GapLine();

            // 📜 Scrollable description
            float maxDescHeight = 80f;
            Rect descRect = listing.GetRect(maxDescHeight);

            if (!_descScrollByRow.TryGetValue(rowIndex, out var descScroll))
                descScroll = Vector2.zero;

            Widgets.LabelScrollable(descRect, description, ref descScroll);
            _descScrollByRow[rowIndex] = descScroll;

            listing.End();
        }


        private static bool MatchesFilter(ShipListItem item, string filter)
        {
            string label = item.Ship?.label?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(label) && label.Contains(filter))
                return true;

            string defName = item.Ship?.defName?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(defName) && defName.Contains(filter))
                return true;

            if (item.IsExported)
            {
                string filename = item.ExportFilename?.ToLowerInvariant();
                if (!string.IsNullOrEmpty(filename) && filename.Contains(filter))
                    return true;
            }

            return false;
        }
    }
}
