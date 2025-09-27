using System;
using System.IO;
using System.Linq;
using System.Security;
using RimWorld;
using Verse;

namespace GravshipExport
{
    internal sealed class ShipExportController
    {
        private readonly bool debugLogs;

        public ShipExportController(ModContentPack content, bool debugLogs)
        {
            _ = content ?? throw new ArgumentNullException(nameof(content));
            this.debugLogs = debugLogs;
        }

        public void DeleteShipFile(string filename)
        {
            try
            {
                string path = Path.Combine(GenFilePaths.ConfigFolderPath, "GravshipExport", filename);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Messages.Message($"[GravshipExport] Deleted ship: {filename}", MessageTypeDefOf.PositiveEvent, false);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[GravshipExport] Failed to delete ship file {filename}: {ex}");
                Messages.Message("[GravshipExport] Failed to delete ship file. See log.", MessageTypeDefOf.RejectInput, false);
            }
        }

        public void ExportShipAsMod(ShipLayoutDefV2 ship, string userModName, Action onComplete)
        {
            if (ship == null)
            {
                return;
            }

            string modsRoot = GenFilePaths.ModsFolderPath;
            string folderName = SanitizeFolderName(userModName);
            string modFolder = Path.Combine(modsRoot, folderName);

            void PerformExport()
            {
                try
                {
                    DoExportShipAsMod(ship, modFolder, userModName);
                }
                finally
                {
                    onComplete?.Invoke();
                }
            }

            if (Directory.Exists(modFolder))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    $"A mod folder named '{folderName}' already exists.\n\nOverwrite the ship definition file?\n\n(About.xml will be preserved.)",
                    PerformExport,
                    true));
            }
            else
            {
                PerformExport();
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
                catch
                {
                    // ignored
                }

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

                Messages.Message($"[GravshipExport] Exported '{ship.label ?? ship.defName}' as mod:\n{modFolder}", MessageTypeDefOf.PositiveEvent, false);
                if (debugLogs) Log.Message($"[GravshipExport/Export] Wrote About.xml to {aboutPath} (created only if absent)");
                if (debugLogs) Log.Message($"[GravshipExport/Export] Wrote Def to {defPath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[GravshipExport/Export] Failed to export mod: {ex}");
                Messages.Message("[GravshipExport] Failed to export mod. See log.", MessageTypeDefOf.RejectInput, false);
            }
        }

        private static string BuildAboutXml(string modName, string author, string packageId)
        {
            return
@$"<?xml version=\"1.0\" encoding=\"utf-8\"?>
<ModMetaData>
  <name>{SecurityElement.Escape(modName)}</name>
  <author>{SecurityElement.Escape(author)}</author>
  <packageId>{SecurityElement.Escape(packageId)}</packageId>
  <supportedVersions>
    <li>1.6</li>
  </supportedVersions>
  <description><![CDATA[Modifies the starting ship for the Gravship scenario. Exported using Gravship Export.]]></description>
  <modDependencies>
    <li>
      <packageId>Arcjc007.GravshipExporter</packageId>
      <displayName>Gravship Exporter</displayName>
      <steamWorkshopUrl>steam://url/CommunityFilePage/3573188050</steamWorkshopUrl>
      <downloadUrl>https://steamcommunity.com/sharedfiles/filedetails/?id=3573188050</downloadUrl>
    </li>
  </modDependencies>
  <loadAfter>
    <li>Arcjc007.GravshipExporter</li>
  </loadAfter>
</ModMetaData>";
        }

        private static string WrapInDefs(string inner)
        {
            string trimmed = inner.Trim();
            if (trimmed.StartsWith("<?xml", StringComparison.Ordinal))
            {
                int idx = trimmed.IndexOf("?>", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    trimmed = trimmed.Substring(idx + 2).Trim();
                }
            }

            trimmed = trimmed
                .Replace("<ShipLayoutDefV2>", "<GravshipExport.ShipLayoutDefV2>")
                .Replace("</ShipLayoutDefV2>", "</GravshipExport.ShipLayoutDefV2>");

            return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Defs>\n{trimmed}\n</Defs>\n";
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "GravshipExport_Mod";
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name.Trim();
        }

        private static string SanitizeIdPart(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "User";
            }

            var chars = value.Where(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '_').ToArray();
            string cleaned = new string(chars);
            if (string.IsNullOrEmpty(cleaned))
            {
                cleaned = "User";
            }

            if (!char.IsLetter(cleaned[0]))
            {
                cleaned = "U" + cleaned;
            }

            return cleaned;
        }
    }
}
