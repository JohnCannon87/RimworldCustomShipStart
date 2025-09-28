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

                if (DebugLogs)
                {
                    Log.Message($"[GravshipExport/UI] After refresh: LoadedShips.Count={ShipManager.LoadedShips.Count}");
                    if (ShipManager.LoadedShips.Count > 0)
                    {
                        var keys = string.Join(", ", ShipManager.LoadedShips.Keys.Take(10));
                        Log.Message($"[GravshipExport/UI] First keys (up to 10): {keys}");
                    }
                    Log.Message($"[GravshipExport/UI] settings.lastUsedShip={(settings.lastUsedShip != null ? settings.lastUsedShip.defName : "null")}");
                }

                TryRestoreLastUsedShip();
            }

            var headerRect = new Rect(inRect.x, inRect.y, inRect.width, 112f);
            ModHeaderView.Draw(headerRect, settings.lastUsedShip, ref searchText);

            var listRect = new Rect(inRect.x, headerRect.yMax + 8f, inRect.width, inRect.height - headerRect.height - 8f);
            DrawShipList(listRect);
        }

        private void TryRestoreLastUsedShip()
        {
            if (settings.lastUsedShip == null)
            {
                if (DebugLogs) Log.Message("[GravshipExport/Restore] No lastUsedShip stored. Nothing to restore.");
                return;
            }

            string defName = settings.lastUsedShip.defName;
            if (string.IsNullOrEmpty(defName))
            {
                if (DebugLogs) Log.Warning("[GravshipExport/Restore] lastUsedShip has no defName — cannot restore.");
                return;
            }

            var exported = ShipManager.LoadedShips.Values.FirstOrDefault(s => s.defName == defName);
            if (exported != null)
            {
                settings.lastUsedShip = exported;
                if (DebugLogs) Log.Message($"[GravshipExport/Restore] Matched lastUsedShip to exported ship by defName='{defName}'");
                return;
            }

            var modDef = DefDatabase<ShipLayoutDefV2>.AllDefsListForReading.FirstOrDefault(s => s.defName == defName);
            if (modDef != null)
            {
                settings.lastUsedShip = modDef;
                if (DebugLogs) Log.Message($"[GravshipExport/Restore] Matched lastUsedShip to mod def by defName='{defName}'");
                return;
            }

            Log.Warning($"[GravshipExport/Restore] Could not find a ship with defName='{defName}'. Highlight may fail until reapplied.");
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
