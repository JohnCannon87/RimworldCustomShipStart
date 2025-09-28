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

        private const float ThumbnailSize = 330f;
        private const float ThumbnailPadding = 12f;

        public ShipListView(Func<List<ShipListItem>> rowsProvider, ShipRowDrawer rowDrawer)
        {
            this.rowsProvider = rowsProvider;
            this.rowDrawer = rowDrawer;
        }

        public void Draw(Rect outRect, string searchText, string currentSelectionKey, ShipListCallbacks callbacks)
        {
            // Make window wider
            outRect.width = Mathf.Max(outRect.width, 900f);

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

            float rowHeight = ThumbnailSize + (ThumbnailPadding * 2);
            float viewHeight = rows.Count * rowHeight;
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            for (int i = 0; i < rows.Count; i++)
            {
                var item = rows[i];
                var rowRect = new Rect(0f, i * rowHeight, viewRect.width, rowHeight);

                // ✅ Highlight if this row is the current selection (canonical: defName)
                if (ShipSelectionHelper.IsSelected(currentSelectionKey, item))
                {
                    Widgets.DrawBoxSolid(rowRect, new Color(0.3f, 0.55f, 0.3f, 0.25f));
                }

                // 🖼️ Preview
                Rect previewRect = new Rect(
                    rowRect.x + ThumbnailPadding,
                    rowRect.y + ThumbnailPadding,
                    ThumbnailSize,
                    ThumbnailSize
                );

                // Slightly inset the area we actually use for the image
                Rect innerRect = previewRect.ContractedBy(4f);

                // Draw background behind preview
                Widgets.DrawBoxSolid(previewRect, new Color(0.08f, 0.08f, 0.08f, 0.95f));

                // Draw subtle custom border (muted grey-green)
                Color borderColor = new Color(0.35f, 0.5f, 0.35f, 0.7f);
                float thickness = 2f;
                Widgets.DrawBoxSolid(new Rect(previewRect.x, previewRect.y, previewRect.width, thickness), borderColor); // top
                Widgets.DrawBoxSolid(new Rect(previewRect.x, previewRect.yMax - thickness, previewRect.width, thickness), borderColor); // bottom
                Widgets.DrawBoxSolid(new Rect(previewRect.x, previewRect.y, thickness, previewRect.height), borderColor); // left
                Widgets.DrawBoxSolid(new Rect(previewRect.xMax - thickness, previewRect.y, thickness, previewRect.height), borderColor); // right

                Texture2D preview = ShipPreviewUtility.GetPreviewFor(item);
                if (preview != null)
                {
                    // Calculate aspect ratio and scale to fit *inside* innerRect
                    float texAspect = (float)preview.width / preview.height;
                    float rectAspect = innerRect.width / innerRect.height;
                    Rect drawRect = innerRect;

                    if (texAspect > rectAspect)
                    {
                        // Texture is wider — match width, adjust height
                        float scaledHeight = innerRect.width / texAspect;
                        drawRect.y += (innerRect.height - scaledHeight) / 2f;
                        drawRect.height = scaledHeight;
                    }
                    else
                    {
                        // Texture is taller — match height, adjust width
                        float scaledWidth = innerRect.height * texAspect;
                        drawRect.x += (innerRect.width - scaledWidth) / 2f;
                        drawRect.width = scaledWidth;
                    }

                    // Draw the texture scaled and centered inside the inset area
                    GUI.DrawTexture(drawRect, preview, ScaleMode.StretchToFill);
                }
                else
                {
                    Widgets.Label(innerRect.ContractedBy(10f), "No Preview");
                }



                // 📊 Metadata area
                float textStartX = previewRect.xMax + 20f;
                Rect metaRect = new Rect(
                    textStartX,
                    rowRect.y + ThumbnailPadding,
                    rowRect.width - textStartX - 20f,
                    rowHeight - (ThumbnailPadding * 2)
                );

                DrawShipMetadata(metaRect, item, callbacks);
            }

            Widgets.EndScrollView();
        }

        private void DrawShipMetadata(Rect rect, ShipListItem item, ShipListCallbacks callbacks)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            // 🛠 Ship name fallback logic
            string shipName = item.Ship?.label
                ?? (item.ExportFilename != null ? Path.GetFileNameWithoutExtension(item.ExportFilename) : "Unnamed Ship");

            // 📐 Size from layout
            int width = item.Ship?.width ?? 0;
            int height = item.Ship?.height ?? 0;
            string size = $"{width}×{height}";

            // 📊 Count things and terrain cells
            int thingCount = 0;
            int terrainCount = 0;

            if (item.Ship?.rows != null)
            {
                foreach (var row in item.Ship.rows)
                {
                    if (row == null) continue;
                    foreach (var cell in row)
                    {
                        if (cell == null) continue;

                        if (!string.IsNullOrEmpty(cell.terrainDef))
                            terrainCount++;

                        if (cell.things != null)
                            thingCount += cell.things.Count;
                    }
                }
            }

            // 📄 Use description from the Ship def if available
            string description = !string.IsNullOrEmpty(item.Ship?.description)
                ? item.Ship.description
                : "No description provided. This ship was likely exported by a player or mod without metadata.";


            // --- Basic info ---
            Text.Font = GameFont.Medium;
            listing.Label($"🛠 Ship Name: {shipName}");
            Text.Font = GameFont.Small;
            listing.Label($"📏 Size: {size}");
            listing.Label($"🧱 Things: {thingCount}");
            listing.GapLine();

            // --- Description section ---
            Text.Font = GameFont.Small;
            listing.Label("📜 Description:");

            float descHeight = Text.CalcHeight(description, rect.width - 20f);
            float maxDescHeight = 100f;
            bool needsScroll = descHeight > maxDescHeight;

            Rect descRect = listing.GetRect(Mathf.Min(descHeight, maxDescHeight));
            if (needsScroll)
            {
                Rect inner = new Rect(0, 0, descRect.width - 16f, descHeight);
                Vector2 descScroll = default;
                Widgets.BeginScrollView(descRect, ref descScroll, inner);
                Widgets.Label(inner, description);
                Widgets.EndScrollView();
            }
            else
            {
                Widgets.Label(descRect, description);
            }

            listing.End();

            // --- Action Buttons ---
            float buttonWidth = 120f;
            float buttonHeight = 30f;
            float spacing = 10f;

            Rect applyRect = new Rect(rect.xMax - buttonWidth, rect.yMax - buttonHeight, buttonWidth, buttonHeight);
            Rect exportRect = new Rect(applyRect.x - buttonWidth - spacing, rect.yMax - buttonHeight, buttonWidth, buttonHeight);
            Rect deleteRect = new Rect(exportRect.x - buttonWidth - spacing, rect.yMax - buttonHeight, buttonWidth, buttonHeight);

            if (Widgets.ButtonText(applyRect, "Apply"))
            {
                callbacks?.ApplyRequested?.Invoke(item);
            }

            if (Widgets.ButtonText(exportRect, "Export as Mod"))
            {
                callbacks?.ExportRequested?.Invoke(item);
            }

            // Only show delete if this is an exported ship
            if (item.IsExported && Widgets.ButtonText(deleteRect, "Delete"))
            {
                callbacks?.DeleteRequested?.Invoke(item);
            }
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
