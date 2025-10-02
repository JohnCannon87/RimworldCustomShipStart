using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace GravshipExport
{
    public class GravshipExportMod : Mod
    {
        private const bool DebugLogs = false;

        private readonly ShipListView shipListView;
        private readonly ShipExportController exportController;

        private GravshipExportModSettings settings;
        private string searchText = string.Empty;
        private bool didInitialRefresh;

        public GravshipExportMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<GravshipExportModSettings>();
            shipListView = new ShipListView(
                () => ShipListBuilder.Build(Content),
                new ShipRowDrawer());
            exportController = new ShipExportController(Content, DebugLogs);
        }

        public override string SettingsCategory() => "Gravship Export";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (!didInitialRefresh)
            {
                if (DebugLogs) Log.Message("[GravshipExport/UI] Opening settings — performing initial refresh…");
                ShipManager.Refresh();
                didInitialRefresh = true;
                TryRestoreLastUsedShip();
            }

            float y = inRect.y;

            // 🎲 Random mode toggle
            Rect randomToggleRect = new Rect(inRect.x, y, inRect.width, 28f);
            Widgets.CheckboxLabeled(randomToggleRect, "🎲 Enable Random Ship Selection", ref settings.randomSelectionEnabled);
            y += 32f;

            // 📊 Dynamic info line (depends on mode)
            string infoLine = settings.randomSelectionEnabled
                ? (settings.randomShipPool?.Count > 0
                    ? $"Ships in random pool: {settings.randomShipPool.Count}"
                    : "⚠️ No ships selected for the random pool!")
                : (settings.lastUsedShip != null
                    ? $"Current ship: {settings.lastUsedShip.label ?? settings.lastUsedShip.defName}"
                    : "No default ship selected.");

            Rect infoRect = new Rect(inRect.x + 6f, y, inRect.width - 12f, 22f);
            Widgets.Label(infoRect, infoLine);
            y += 28f;

            // --- Search header (much smaller now) ---
            ShipLayoutDefV2 headerShip = settings.randomSelectionEnabled ? null : settings.lastUsedShip;
            float headerHeight = 38f; // shrunk from ~90px
            var headerRect = new Rect(inRect.x, y, inRect.width, headerHeight);
            ModHeaderView.Draw(headerRect, headerShip, ref searchText);
            y += headerHeight + 6f;

            // --- Ship list fills all remaining space dynamically ---
            var listRect = new Rect(inRect.x, y, inRect.width, inRect.height - y - 6f);
            DrawShipList(listRect);
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
                ApplyRequested = HandleApplyRequest,
                ExportRequested = HandleExportRequest
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
                    exportController.DeleteShipFile(item.ExportFilename);
                    ShipManager.Refresh();
                }));
        }

        private void HandleApplyRequest(ShipListItem item)
        {
            if (item?.Ship == null)
                return;

            settings.lastUsedShip = item.Ship;

            if (settings.randomSelectionEnabled)
            {
                settings.randomSelectionEnabled = false;
                Messages.Message("[GravshipExport] Random ship selection disabled (manual ship chosen).", MessageTypeDefOf.NeutralEvent);
            }

            WriteSettings();
            Messages.Message($"[GravshipExport] Ship '{item.Ship.label}' set as default.", MessageTypeDefOf.PositiveEvent, false);
        }

        private void HandleExportRequest(ShipListItem item)
        {
            if (item?.Ship == null)
                return;

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
