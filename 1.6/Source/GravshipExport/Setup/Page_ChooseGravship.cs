using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace GravshipExport
{
    public class Page_ChooseGravship : Page
    {
        private Vector2 scrollPos;
        private List<ShipListItem> ships = new List<ShipListItem>();
        private int selectedIndex = -1;
        private string searchText = string.Empty; // new search text box state

        private const float ThumbnailSize = 256f;
        private const float CellPadding = 16f;
        private const float LabelHeight = 36f;
        private const float CellWidth = ThumbnailSize + (CellPadding * 2);
        private const float CellHeight = ThumbnailSize + LabelHeight + (CellPadding * 2);

        private GravshipExportModSettings settings =
            LoadedModManager.GetMod<GravshipExportMod>()?.GetSettings<GravshipExportModSettings>();

        public override string PageTitle => "Choose your Gravship";

        public override void PreOpen()
        {
            base.PreOpen();
            ShipManager.Refresh();

            ships.Clear();
            ships = ShipListBuilder.Build(LoadedModManager.GetMod<GravshipExportMod>().Content);

            if (ships.Count == 0)
            {
                selectedIndex = -1;
                return;
            }

            selectedIndex = 0; // default to first ship

            if (settings != null && settings.lastUsedShip != null)
            {
                string lastDef = settings.lastUsedShip.defName;
                if (!string.IsNullOrEmpty(lastDef))
                {
                    for (int i = 0; i < ships.Count; i++)
                    {
                        var s = ships[i];
                        string defName = (s.Ship != null) ? s.Ship.defName : null;
                        if (defName != null &&
                            string.Equals(defName, lastDef, System.StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = i;
                            GravshipLogger.Message("[GravshipExport/UI] Restored last used ship: " + lastDef);
                            break;
                        }
                    }
                }
            }
            else
            {
                GravshipLogger.Message("[GravshipExport/UI] No previous ship selection found; defaulting to first ship.");
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            // --- Draw title and reserve top space ---
            DrawPageTitle(rect);
            float titleHeight = 50f;
            Rect mainArea = new Rect(rect.x, rect.y + titleHeight, rect.width, rect.height - titleHeight);

            // --- Search bar ---
            float searchBarHeight = 30f;
            Rect searchRect = new Rect(mainArea.x + 10f, mainArea.y, mainArea.width - 20f, searchBarHeight);
            GUI.SetNextControlName("ShipSearchField");
            searchText = Widgets.TextField(searchRect, searchText);
            TooltipHandler.TipRegion(searchRect, "Type to filter ships by name, defName, or description.");
            mainArea.y += searchBarHeight + 8f;
            mainArea.height -= searchBarHeight + 8f;

            // Apply filter
            IEnumerable<ShipListItem> filteredShips = ships;
            if (!string.IsNullOrEmpty(searchText))
            {
                string filter = searchText.ToLowerInvariant();
                filteredShips = ships.Where(s =>
                    (s.Ship?.label != null && s.Ship.label.ToLowerInvariant().Contains(filter)) ||
                    (s.Ship?.defName != null && s.Ship.defName.ToLowerInvariant().Contains(filter)) ||
                    (s.Ship?.description != null && s.Ship.description.ToLowerInvariant().Contains(filter))
                );
            }

            List<ShipListItem> visibleShips = filteredShips.ToList();

            // Reserve space for info box + buttons
            float infoBoxHeight = 120f;
            float bottomReserved = Page.BottomButSize.y + infoBoxHeight + 30f;
            Rect mainRect = new Rect(mainArea.x, mainArea.y, mainArea.width, mainArea.height - bottomReserved);

            if (visibleShips.Count == 0)
            {
                Widgets.Label(mainRect, "No ships match your search.");
                return;
            }

            // --- Draw scroll view of ships ---
            float spacing = 20f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(mainRect.width / (CellWidth + spacing)));
            int totalRows = Mathf.CeilToInt((float)visibleShips.Count / columns);
            float contentHeight = totalRows * (CellHeight + spacing);

            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(mainRect, ref scrollPos, viewRect);

            int index = 0;
            for (int row = 0; row < totalRows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (index >= visibleShips.Count)
                        break;

                    float x = col * (CellWidth + spacing);
                    float y = row * (CellHeight + spacing);
                    Rect cellRect = new Rect(x, y, CellWidth, CellHeight);

                    DrawShipCell(cellRect, ships.IndexOf(visibleShips[index]), visibleShips[index]);
                    index++;
                }
            }

            Widgets.EndScrollView();

            // --- Info box below list ---
            if (selectedIndex >= 0 && selectedIndex < ships.Count)
            {
                ShipListItem selected = ships[selectedIndex];
                ShipLayoutDefV2 ship = selected.Ship;

                if (ship != null)
                {
                    float totalWealth = 0f;
                    if (ship.rows != null)
                    {
                        foreach (var row in ship.rows)
                        {
                            if (row == null) continue;
                            foreach (var cell in row)
                            {
                                if (cell == null || cell.things == null) continue;
                                foreach (var entry in cell.things)
                                {
                                    if (!string.IsNullOrEmpty(entry.defName))
                                    {
                                        ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(entry.defName);
                                        if (def != null)
                                            totalWealth += def.BaseMarketValue;
                                    }
                                }
                            }
                        }
                    }

                    string infoLine = "📏 Size: " + ship.width + "×" + ship.height + "   |   💰 Wealth: " + totalWealth.ToStringMoney();
                    string description = !string.IsNullOrEmpty(ship.description)
                        ? ship.description
                        : "No description provided.";

                    Rect infoRect = new Rect(
                        mainArea.x + 40f,
                        mainArea.yMax - infoBoxHeight - Page.BottomButSize.y - 10f,
                        mainArea.width - 80f,
                        infoBoxHeight
                    );

                    Widgets.DrawBoxSolid(infoRect, new Color(0.08f, 0.08f, 0.08f, 0.9f));
                    Widgets.DrawBox(infoRect);

                    // Centered Size + Wealth
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperCenter;
                    GUI.color = Color.white;
                    Rect headerRect = new Rect(infoRect.x, infoRect.y + 8f, infoRect.width, 24f);
                    Widgets.Label(headerRect, infoLine);

                    // Scrollable description
                    float descTop = headerRect.yMax + 6f;
                    Rect descRect = new Rect(infoRect.x + 10f, descTop, infoRect.width - 20f, infoRect.height - (descTop - infoRect.y) - 8f);
                    Vector2 descScroll = Vector2.zero;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.85f, 0.85f, 0.85f);
                    Widgets.LabelScrollable(descRect, description, ref descScroll);

                    // Reset
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                }
            }

            // --- Bottom buttons ---
            DoBottomButtons(rect, "Next".Translate(), "Back".Translate(), null, selectedIndex >= 0, true);

            // Random button centered
            float buttonWidth = Page.BottomButSize.x;
            float buttonHeight = Page.BottomButSize.y;
            float centerX = rect.center.x - (buttonWidth / 2f);
            Rect randomRect = new Rect(centerX, rect.yMax - buttonHeight, buttonWidth, buttonHeight);
            if (Widgets.ButtonText(randomRect, "🎲 Random"))
            {
                DoRandomSelection();
            }
        }

        private void DrawShipCell(Rect cellRect, int index, ShipListItem item)
        {
            bool isSelected = (index == selectedIndex);

            Color background = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            Color highlight = new Color(0.3f, 0.55f, 0.3f, 0.25f);
            Color border = new Color(0.35f, 0.5f, 0.35f, 0.7f);

            Widgets.DrawBoxSolid(cellRect, background);
            if (isSelected)
                Widgets.DrawBoxSolid(cellRect, highlight);

            float t = 2f;
            Color borderColor = isSelected ? border : new Color(1f, 1f, 1f, 0.25f);
            Widgets.DrawBoxSolid(new Rect(cellRect.x, cellRect.y, cellRect.width, t), borderColor);
            Widgets.DrawBoxSolid(new Rect(cellRect.x, cellRect.yMax - t, cellRect.width, t), borderColor);
            Widgets.DrawBoxSolid(new Rect(cellRect.x, cellRect.y, t, cellRect.height), borderColor);
            Widgets.DrawBoxSolid(new Rect(cellRect.xMax - t, cellRect.y, t, cellRect.height), borderColor);

            if (Mouse.IsOver(cellRect))
                Widgets.DrawBoxSolid(cellRect, new Color(0.6f, 0.9f, 0.6f, 0.1f));

            // --- Ship name ---
            string shipName = item.Ship.label ?? item.Ship.defName ?? "Unnamed Ship";
            Rect labelRect = new Rect(cellRect.x + 6f, cellRect.y + 4f, cellRect.width - 12f, LabelHeight);

            GameFont chosenFont = GameFont.Medium;
            Text.Font = chosenFont;
            Vector2 size = Text.CalcSize(shipName);
            if (size.x > labelRect.width * 0.95f)
            {
                chosenFont = GameFont.Small;
                Text.Font = chosenFont;
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(labelRect, shipName);
            Text.Anchor = TextAnchor.UpperLeft;

            // --- Source info ---
            string sourceLabel = item.SourceLabel ?? "Unknown Source";
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            Rect sourceRect = new Rect(cellRect.x + 6f, labelRect.yMax - 2f, cellRect.width - 12f, 16f);
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(sourceRect, "📦 " + sourceLabel);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // --- Image preview ---
            Rect previewRect = new Rect(
                cellRect.x + CellPadding,
                cellRect.y + LabelHeight + CellPadding + 8f, // offset to fit source label
                ThumbnailSize,
                ThumbnailSize
            );

            Texture2D preview = ShipPreviewUtility.GetPreviewFor(item);
            if (preview != null)
                GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
            else
                Widgets.Label(previewRect.ContractedBy(8f), "No Preview");

            if (Widgets.ButtonInvisible(cellRect))
            {
                selectedIndex = index;
                GravshipLogger.Message("User selected gravship index " + index + ": " + (item.Ship.defName ?? "Unnamed"));
            }
        }


        private void DoRandomSelection()
        {
            if (ships == null || ships.Count == 0)
                return;

            selectedIndex = Rand.RangeInclusive(0, ships.Count - 1);
            ShipListItem selected = ships[selectedIndex];
            if (settings != null)
                settings.lastUsedShip = selected.Ship;

            Messages.Message("[GravshipExport] Random ship selected: " +
                (selected.Ship.label ?? selected.Ship.defName ?? "Unnamed Ship"),
                MessageTypeDefOf.NeutralEvent);

            GravshipLogger.Message("[GravshipExport/UI] Randomly selected gravship index " +
                selectedIndex + ": " + (selected.Ship.defName ?? "Unnamed"));
        }

        protected override void DoNext()
        {
            if (selectedIndex >= 0 && selectedIndex < ships.Count)
                settings.lastUsedShip = ships[selectedIndex].Ship;

            base.DoNext();
        }
    }
}
