using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace GravshipExport
{
    public class GravshipExportMod : Mod
    {
        private readonly ShipListView shipListView;
        private readonly ShipExportController exportController;

        private GravshipExportModSettings settings;
        private string searchText = string.Empty;
        private bool didInitialRefresh;

        public GravshipExportMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<GravshipExportModSettings>();
            shipListView = new ShipListView(new ShipRowDrawer());
            exportController = new ShipExportController(Content, settings.enableDebugLogging);
        }

        public override string SettingsCategory() => "Gravship Export";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (!didInitialRefresh)
            {
                if (settings.enableDebugLogging)
                    Log.Message("[GravshipExport/UI] Opening settings — performing initial refresh…");

                ShipManager.Refresh();
                didInitialRefresh = true;
                TryRestoreLastUsedShip();
            }

            float y = inRect.y;

            // 🪵 Logging toggle
            Rect logToggleRect = new Rect(inRect.x, y, inRect.width, 28f);
            Widgets.CheckboxLabeled(logToggleRect, "🪵 Enable Debug Logging", ref settings.enableDebugLogging);
            y += 32f;

            // --- Header & Search bar ---
            float headerHeight = 38f;
            var headerRect = new Rect(inRect.x, y, inRect.width, headerHeight);
            ModHeaderView.Draw(headerRect, null, ref searchText);
            y += headerHeight + 6f;

            // --- Ship list ---
            var listRect = new Rect(inRect.x, y, inRect.width, inRect.height - y - 6f);
            DrawShipList(listRect);

            WriteSettings();
        }

        private void TryRestoreLastUsedShip()
        {
            if (settings.lastUsedShip == null)
                return;

            string defName = settings.lastUsedShip.defName;
            if (string.IsNullOrEmpty(defName))
                return;

            var exported = ShipManager.LoadedShips.Values.FirstOrDefault(s => s.defName == defName);
            if (exported != null)
            {
                settings.lastUsedShip = exported;
                return;
            }

            var modDef = DefDatabase<ShipLayoutDefV2>.AllDefsListForReading.FirstOrDefault(s => s.defName == defName);
            if (modDef != null)
            {
                settings.lastUsedShip = modDef;
            }
        }

        private void DrawShipList(Rect outRect)
        {
            var callbacks = new ShipListCallbacks
            {
                DeleteRequested = HandleDeleteRequest,
                ExportRequested = HandleExportRequest
                // ApplyRequested removed — selection now handled in Page_ChooseGravship
            };

            string currentKey = ShipSelectionHelper.GetCurrentSelectionKey(settings, ShipManager.LoadedShips);
            shipListView.Draw(outRect, searchText, currentKey, callbacks);
        }

        private void HandleDeleteRequest(ShipListItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.ExportFilename))
                return;

            string label = item.Ship?.label ?? item.ExportFilename;

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                $"Are you sure you want to delete '{label}'?\n\nThis cannot be undone.",
                () =>
                {
                    if (settings.enableDebugLogging)
                        Log.Message($"[GravshipExport/Delete] Deleting ship file: {item.ExportFilename}");

                    exportController.DeleteShipFile(item.ExportFilename);
                    ShipManager.Refresh();
                }));
        }

        private void HandleExportRequest(ShipListItem item)
        {
            if (item?.Ship == null)
                return;

            if (settings.enableDebugLogging)
                Log.Message($"[GravshipExport/Export] Exporting ship: {item.Ship.label}");

            string suggestedName = $"Gravship_{(item.Ship.label ?? item.Ship.defName ?? "NewShip")}";

            Find.WindowStack.Add(new Dialog_ExportModName(
                item.Ship,
                suggestedName,
                modName =>
                {
                    exportController.ExportShipAsMod(item.Ship, modName, null);
                }));
        }
    }
}
