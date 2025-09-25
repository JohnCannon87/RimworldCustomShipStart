using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using System.Security;

namespace RimworldCustomShipStart
{
    public class CustomShipMod : Mod
    {
        private CustomShipModSettings settings;

        private Vector2 scrollPos;
        private string searchText = string.Empty;
        private bool didInitialRefresh;

        // Inline export prompt state
        private bool exportPromptOpen;
        private string exportNameBuffer = string.Empty;
        private ShipItem exportTarget;

        // Load/save logs only (no draw-loop spam)
        private const bool DebugLogs = true;

        private sealed class ShipItem
        {
            public ShipLayoutDefV2 Ship;
            public bool IsExported;
            public string ExportFilename; // e.g. "MyShip.xml"
            public string SourceLabel;    // "Exported" | "Mod: X" | "Built-in"
        }

        public CustomShipMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<CustomShipModSettings>();
            didInitialRefresh = false;
        }

        public override string SettingsCategory() => "Custom Ship Start";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Refresh once when opening settings
            if (!didInitialRefresh)
            {
                if (DebugLogs) Log.Message("[CustomShipStart/UI] Opening settings — performing initial refresh…");
                ShipManager.Refresh();
                didInitialRefresh = true;

                if (DebugLogs)
                {
                    Log.Message($"[CustomShipStart/UI] After refresh: LoadedShips.Count={ShipManager.LoadedShips.Count}");
                    if (ShipManager.LoadedShips.Count > 0)
                    {
                        var keys = string.Join(", ", ShipManager.LoadedShips.Keys.Take(10));
                        Log.Message($"[CustomShipStart/UI] First keys (up to 10): {keys}");
                    }
                    Log.Message($"[CustomShipStart/UI] settings.lastUsedShip={(settings.lastUsedShip != null ? settings.lastUsedShip.defName : "null")}");
                }

                TryRestoreLastUsedShip();
            }

            // Header
            var headerRect = new Rect(inRect.x, inRect.y, inRect.width, 112f);
            DrawHeader(headerRect);

            // Inline export prompt (shown just under the header)
            float promptHeight = exportPromptOpen ? 64f : 0f;
            if (exportPromptOpen)
            {
                var promptRect = new Rect(inRect.x, headerRect.yMax + 4f, inRect.width, promptHeight);
                DrawExportPrompt(promptRect);
            }

            // List
            var listRect = new Rect(inRect.x, headerRect.yMax + 8f + promptHeight, inRect.width, inRect.height - headerRect.height - 8f - promptHeight);
            DrawShipList(listRect);
        }

        // ──────────────────────────────────────────────
        // Restore lastUsedShip by matching loaded ships
        // ──────────────────────────────────────────────
        private void TryRestoreLastUsedShip()
        {
            if (settings.lastUsedShip == null)
            {
                if (DebugLogs) Log.Message("[CustomShipStart/Restore] No lastUsedShip stored. Nothing to restore.");
                return;
            }

            string defName = settings.lastUsedShip.defName;
            if (string.IsNullOrEmpty(defName))
            {
                if (DebugLogs) Log.Warning("[CustomShipStart/Restore] lastUsedShip has no defName — cannot restore.");
                return;
            }

            var exported = ShipManager.LoadedShips.Values.FirstOrDefault(s => s.defName == defName);
            if (exported != null)
            {
                settings.lastUsedShip = exported;
                if (DebugLogs) Log.Message($"[CustomShipStart/Restore] Matched lastUsedShip to exported ship by defName='{defName}'");
                return;
            }

            var modDef = DefDatabase<ShipLayoutDefV2>.AllDefsListForReading.FirstOrDefault(s => s.defName == defName);
            if (modDef != null)
            {
                settings.lastUsedShip = modDef;
                if (DebugLogs) Log.Message($"[CustomShipStart/Restore] Matched lastUsedShip to mod def by defName='{defName}'");
                return;
            }

            Log.Warning($"[CustomShipStart/Restore] Could not find a ship with defName='{defName}'. Highlight may fail until reapplied.");
        }

        // ──────────────────────────────────────────────
        // Header with current ship + search
        // ──────────────────────────────────────────────
        private void DrawHeader(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(8f);

            string currentLabel = settings.lastUsedShip != null
                ? settings.lastUsedShip.label
                : "None";

            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), $"Current ship: {currentLabel}");

            var searchLabelRect = new Rect(inner.x, inner.y + 32f, 100f, 24f);
            Widgets.Label(searchLabelRect, "Search:");
            var searchRect = new Rect(searchLabelRect.xMax + 8f, searchLabelRect.y, inner.width - searchLabelRect.width - 8f, 24f);
            searchText = Widgets.TextField(searchRect, searchText);
        }

        // ──────────────────────────────────────────────
        // Inline export prompt UI
        // ──────────────────────────────────────────────
        private void DrawExportPrompt(Rect rect)
        {
            Widgets.DrawLightHighlight(rect);
            var inner = rect.ContractedBy(8f);

            Widgets.Label(new Rect(inner.x, inner.y, 180f, 24f), "Export as Mod:");
            var inputRect = new Rect(inner.x + 180f + 8f, inner.y, inner.width - 180f - 8f - 180f, 24f);

            exportNameBuffer = Widgets.TextField(inputRect, exportNameBuffer);

            var createRect = new Rect(inner.xMax - 170f, inner.y, 80f, 24f);
            var cancelRect = new Rect(inner.xMax - 85f, inner.y, 80f, 24f);

            if (Widgets.ButtonText(createRect, "Create"))
            {
                if (exportTarget?.Ship != null && !string.IsNullOrWhiteSpace(exportNameBuffer))
                {
                    TryExportShipAsMod(exportTarget, exportNameBuffer);
                }
            }
            if (Widgets.ButtonText(cancelRect, "Cancel"))
            {
                exportPromptOpen = false;
                exportTarget = null;
                exportNameBuffer = string.Empty;
            }
        }

        // ──────────────────────────────────────────────
        // Scrollable list of ships (single-row layout, truncation+tooltips)
        // ──────────────────────────────────────────────
        private void DrawShipList(Rect outRect)
        {
            var rows = BuildShipRows();
            if (rows.Count == 0)
            {
                Widgets.Label(outRect, "No ships found.\n\n• Export a ship in-game\n• Load a mod with ShipLayoutDefV2.");
                return;
            }

            if (!string.IsNullOrEmpty(searchText))
            {
                string f = searchText.ToLowerInvariant();
                rows = rows.Where(r =>
                    (r.Ship.label?.ToLowerInvariant().Contains(f) ?? false) ||
                    (r.Ship.defName?.ToLowerInvariant().Contains(f) ?? false) ||
                    (r.IsExported && (r.ExportFilename?.ToLowerInvariant().Contains(f) ?? false))
                ).ToList();
            }

            const float rowHeight = 42f;
            const float rowPad = 4f;
            const float labelWidth = 300f;
            const float sourceWidth = 200f;
            const float infoWidth = 300f;
            const float applyBtnWidth = 50f;
            const float exportBtnWidth = 50f;

            const int labelMaxChars = 30;
            const int sourceMaxChars = 30;
            const int infoMaxChars = 40;

            var viewRect = new Rect(0f, 0f, outRect.width - 16f, rows.Count * rowHeight);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);

            string currentKey = CurrentSelectionKey();

            for (int i = 0; i < rows.Count; i++)
            {
                var item = rows[i];
                var ship = item.Ship;
                var rowRect = new Rect(0f, i * rowHeight, viewRect.width, rowHeight);

                string rowKey = item.IsExported ? item.ExportFilename : ship.defName;
                bool isCurrent = !string.IsNullOrEmpty(currentKey) &&
                                 currentKey.Equals(rowKey, StringComparison.OrdinalIgnoreCase);

                if (isCurrent)
                    Widgets.DrawBoxSolid(rowRect, new Color(0f, 1f, 0f, 0.15f));
                else if (i % 2 == 1)
                    Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.05f));

                float rightX = rowRect.xMax - rowPad;

                var applyRect = new Rect(rightX - applyBtnWidth, rowRect.y + rowPad, applyBtnWidth, rowHeight - 2 * rowPad);
                rightX -= applyBtnWidth + rowPad;

                var exportRect = new Rect(rightX - exportBtnWidth, rowRect.y + rowPad, exportBtnWidth, rowHeight - 2 * rowPad);
                rightX -= exportBtnWidth + rowPad;

                var infoRect = new Rect(rightX - infoWidth, rowRect.y + rowPad, infoWidth, rowHeight - 2 * rowPad);
                rightX -= infoWidth + rowPad;

                var sourceRect = new Rect(rightX - sourceWidth, rowRect.y + rowPad, sourceWidth, rowHeight - 2 * rowPad);
                rightX -= sourceWidth + rowPad;

                var labelRect = new Rect(rowRect.x + rowPad, rowRect.y + rowPad, rightX - (rowRect.x + rowPad), rowHeight - 2 * rowPad);

                string fullLabel = ship.label ?? ship.defName ?? "Unnamed Ship";
                string drawLabel = HardCut(fullLabel, labelMaxChars);
                Widgets.Label(labelRect, drawLabel);
                if (!string.Equals(fullLabel, drawLabel, StringComparison.Ordinal))
                    TooltipHandler.TipRegion(labelRect, fullLabel);

                string fullSource = item.SourceLabel ?? "";
                string drawSource = HardCut(fullSource, sourceMaxChars);
                GUI.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                Widgets.Label(sourceRect, drawSource);
                GUI.color = Color.white;
                if (!string.Equals(fullSource, drawSource, StringComparison.Ordinal))
                    TooltipHandler.TipRegion(sourceRect, fullSource);

                string fullInfo = GetShipInfo(ship);
                string drawInfo = HardCut(fullInfo, infoMaxChars);
                Widgets.Label(infoRect, drawInfo);
                if (!string.Equals(fullInfo, drawInfo, StringComparison.Ordinal))
                    TooltipHandler.TipRegion(infoRect, fullInfo);

                if (item.IsExported)
                {
                    if (Widgets.ButtonText(exportRect, "Export"))
                    {
                        exportTarget = item;
                        exportNameBuffer = $"CustomShip_{(ship.label ?? ship.defName ?? "NewShip")}";
                        exportPromptOpen = true;
                    }
                }

                // ✅ Fixed: Replace useless "Applied" button with non-interactive label
                if (isCurrent)
                {
                    GUI.color = Color.green;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(applyRect, "Active");
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                else
                {
                    if (Widgets.ButtonText(applyRect, "Apply"))
                    {
                        settings.lastUsedShip = ship;
                        WriteSettings();
                        Messages.Message($"[CustomShipStart] Ship '{ship.label}' set as default.", MessageTypeDefOf.PositiveEvent, false);
                    }
                }
            }

            Widgets.EndScrollView();
        }

        private List<ShipItem> BuildShipRows()
        {
            var rows = new List<ShipItem>();

            foreach (var kvp in ShipManager.LoadedShips)
            {
                rows.Add(new ShipItem
                {
                    Ship = kvp.Value,
                    IsExported = true,
                    ExportFilename = kvp.Key,
                    SourceLabel = "Exported"
                });
            }

            var exportedDefNames = new HashSet<string>(rows.Select(r => r.Ship?.defName).Where(d => !string.IsNullOrEmpty(d)));
            var modDefs = DefDatabase<ShipLayoutDefV2>.AllDefsListForReading;

            foreach (var ship in modDefs)
            {
                if (ship == null || string.IsNullOrEmpty(ship.defName)) continue;
                if (exportedDefNames.Contains(ship.defName)) continue;

                string source = "Built-in";
                var pack = ship.modContentPack;
                if (pack != null && !string.IsNullOrEmpty(pack.Name))
                    source = $"Mod: {pack.Name}";

                rows.Add(new ShipItem
                {
                    Ship = ship,
                    IsExported = false,
                    ExportFilename = null,
                    SourceLabel = source
                });
            }

            return rows.OrderBy(r => r.Ship?.label).ToList();
        }

        private string CurrentSelectionKey()
        {
            if (settings.lastUsedShip == null)
                return null;

            string defName = settings.lastUsedShip.defName;

            foreach (var kvp in ShipManager.LoadedShips)
            {
                if (ReferenceEquals(kvp.Value, settings.lastUsedShip))
                    return kvp.Key;
            }

            foreach (var kvp in ShipManager.LoadedShips)
            {
                if (kvp.Value?.defName == defName)
                    return kvp.Key;
            }

            return defName;
        }

        private void TryExportShipAsMod(ShipItem item, string userModName)
        {
            if (item == null || item.Ship == null) return;

            string modsRoot = GenFilePaths.ModsFolderPath;
            string folderName = SanitizeFolderName(userModName);
            string modFolder = Path.Combine(modsRoot, folderName);

            if (Directory.Exists(modFolder))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    $"A mod folder named '{folderName}' already exists.\n\nOverwrite the ship definition file?\n\n(About.xml will be preserved.)",
                    () => DoExportShipAsMod(item.Ship, modFolder, userModName),
                    true));
            }
            else
            {
                DoExportShipAsMod(item.Ship, modFolder, userModName);
            }
        }

        private void DoExportShipAsMod(ShipLayoutDefV2 ship, string modFolder, string modName)
        {
            try
            {
                string aboutDir = Path.Combine(modFolder, "About");
                string defsDir = Path.Combine(modFolder, "Defs", "ShipLayoutDefs");
                Directory.CreateDirectory(aboutDir);
                Directory.CreateDirectory(defsDir);

                string author = "Unknown";
                try
                {
                    author = SteamUtility.SteamPersonaName ?? "Unknown";
                }
                catch { }

                string safeAuthor = SanitizeIdPart(author);
                string safeModName = SanitizeIdPart(modName);
                string packageId = $"{safeAuthor}.{safeModName}";

                string aboutPath = Path.Combine(aboutDir, "About.xml");
                if (!File.Exists(aboutPath))
                {
                    string aboutXml = BuildAboutXml(modName, author, packageId);
                    File.WriteAllText(aboutPath, aboutXml);
                }

                string temp = Path.GetTempFileName();
                DirectXmlSaver.SaveDataObject(ship, temp);
                string raw = File.ReadAllText(temp);
                File.Delete(temp);

                string wrapped = WrapInDefs(raw);
                string defFileName = $"{(string.IsNullOrEmpty(ship.defName) ? "ShipLayout" : ship.defName)}.xml";
                string defPath = Path.Combine(defsDir, defFileName);
                File.WriteAllText(defPath, wrapped);

                Messages.Message($"[CustomShipStart] Exported '{ship.label ?? ship.defName}' as mod:\n{modFolder}", MessageTypeDefOf.PositiveEvent, false);
                if (DebugLogs) Log.Message($"[CustomShipStart/Export] Wrote About.xml to {aboutPath} (created only if absent)");
                if (DebugLogs) Log.Message($"[CustomShipStart/Export] Wrote Def to {defPath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[CustomShipStart/Export] Failed to export mod: {ex}");
                Messages.Message("[CustomShipStart] Failed to export mod. See log.", MessageTypeDefOf.RejectInput, false);
            }
            finally
            {
                exportPromptOpen = false;
                exportTarget = null;
                exportNameBuffer = string.Empty;
            }
        }

        private static string BuildAboutXml(string modName, string author, string packageId)
        {
            return
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<ModMetaData>
  <name>{SecurityElement.Escape(modName)}</name>
  <author>{SecurityElement.Escape(author)}</author>
  <packageId>{SecurityElement.Escape(packageId)}</packageId>
  <supportedVersions>
    <li>1.6</li>
  </supportedVersions>
  <description><![CDATA[Modifies the starting ship for the Gravship scenario. Exported using Custom Ship Start.]]></description>
  <modDependencies>
    <li>
      <packageId>Arcjc007.CustomShipStart</packageId>
      <displayName>Custom Ship Start</displayName>
      <steamWorkshopUrl>steam://url/CommunityFilePage/3573188050</steamWorkshopUrl>
      <downloadUrl>https://steamcommunity.com/sharedfiles/filedetails/?id=3573188050</downloadUrl>
    </li>
  </modDependencies>
  <loadAfter>
    <li>Arcjc007.CustomShipStart</li>
  </loadAfter>
</ModMetaData>";
        }

        private static string WrapInDefs(string inner)
        {
            string trimmed = inner.Trim();
            if (trimmed.StartsWith("<?xml", StringComparison.Ordinal))
            {
                int idx = trimmed.IndexOf("?>", StringComparison.Ordinal);
                if (idx >= 0) trimmed = trimmed.Substring(idx + 2).Trim();
            }

            return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Defs>\n{trimmed}\n</Defs>\n";
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "CustomShip_Mod";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private static string SanitizeIdPart(string s)
        {
            if (string.IsNullOrEmpty(s)) return "User";
            var chars = s.Where(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '_').ToArray();
            string cleaned = new string(chars);
            if (string.IsNullOrEmpty(cleaned)) cleaned = "User";
            if (!char.IsLetter(cleaned[0])) cleaned = "U" + cleaned;
            return cleaned;
        }

        private static string GetShipInfo(ShipLayoutDefV2 ship)
        {
            if (ship == null) return "";
            int width = ship.width;
            int height = ship.height;

            int thingCount = 0;
            int terrainCells = 0;

            if (ship.rows != null)
            {
                foreach (var row in ship.rows)
                {
                    if (row == null) continue;
                    foreach (var cell in row)
                    {
                        if (cell == null) continue;
                        if (cell.things != null) thingCount += cell.things.Count;
                        if (!string.IsNullOrEmpty(cell.foundationDef) || !string.IsNullOrEmpty(cell.terrainDef))
                            terrainCells++;
                    }
                }
            }

            return $"{width}×{height} | {thingCount} things | {terrainCells} terrain cells";
        }

        private static string HardCut(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Length <= maxChars) return text;
            return text.Substring(0, maxChars);
        }
    }
}
