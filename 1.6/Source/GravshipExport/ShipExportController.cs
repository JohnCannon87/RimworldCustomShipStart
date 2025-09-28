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
                string baseDir = Path.Combine(GenFilePaths.ConfigFolderPath, "GravshipExport");

                // Delete the XML
                string xmlPath = Path.Combine(baseDir, filename);
                if (File.Exists(xmlPath))
                {
                    File.Delete(xmlPath);
                    if (debugLogs) Log.Message($"[GravshipExport] Deleted ship XML: {xmlPath}");
                }

                // Try deleting the matching PNG (same name, .png extension)
                string pngName = Path.ChangeExtension(filename, ".png");
                string pngPath = Path.Combine(baseDir, pngName);
                if (File.Exists(pngPath))
                {
                    File.Delete(pngPath);
                    if (debugLogs) Log.Message($"[GravshipExport] Deleted matching preview: {pngPath}");
                }

                Messages.Message($"[GravshipExport] Deleted ship: {Path.GetFileNameWithoutExtension(filename)}", MessageTypeDefOf.PositiveEvent, false);
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

                // --- Handle preview image ---
                string previewSource = Path.Combine(
                    GenFilePaths.ConfigFolderPath,
                    "GravshipExport",
                    $"{ship.defName}.png"
                );

                string texturesDir = Path.Combine(modFolder, "Textures", "Previews");
                Directory.CreateDirectory(texturesDir); // ✅ Always create it, even if empty

                // 👇 Force lowercase for the preview image in Textures/Previews
                string previewTargetMain = Path.Combine(texturesDir, $"{ship.defName.ToLowerInvariant()}.png");
                string previewTargetAbout = Path.Combine(modFolder, "About", "Preview.png");

                bool hasPreview = false;

                try
                {
                    if (File.Exists(previewSource))
                    {
                        File.Copy(previewSource, previewTargetMain, overwrite: true);
                        File.Copy(previewSource, previewTargetAbout, overwrite: true);
                        hasPreview = true;
                        if (debugLogs)
                            Log.Message($"[GravshipExport/Export] Copied preview image to: \n - {previewTargetMain}\n - {previewTargetAbout}");
                    }
                    else
                    {
                        if (debugLogs)
                            Log.Warning("[GravshipExport/Export] No preview image found for ship.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[GravshipExport/Export] Failed to copy preview image: {ex}");
                }


                Find.WindowStack.Add(new Dialog_ModExportSuccess(modFolder, modName, hasPreview));


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
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                   "<ModMetaData>\n" +
                   $"  <name>{SecurityElement.Escape(modName)}</name>\n" +
                   $"  <author>{SecurityElement.Escape(author)}</author>\n" +
                   $"  <packageId>{SecurityElement.Escape(packageId)}</packageId>\n" +
                   "  <supportedVersions>\n" +
                   "    <li>1.6</li>\n" +
                   "  </supportedVersions>\n" +
                   "  <description><![CDATA[Modifies the starting ship for the Gravship scenario. " +
                   "Exported using Gravship Export.]]></description>\n" +
                   "  <modDependencies>\n" +
                   "    <li>\n" +
                   "      <packageId>Arcjc007.GravshipExporter</packageId>\n" +
                   "      <displayName>Gravship Exporter</displayName>\n" +
                   "      <steamWorkshopUrl>steam://url/CommunityFilePage/3573188050</steamWorkshopUrl>\n" +
                   "      <downloadUrl>https://steamcommunity.com/sharedfiles/filedetails/?id=3573188050</downloadUrl>\n" +
                   "    </li>\n" +
                   "  </modDependencies>\n" +
                   "  <loadAfter>\n" +
                   "    <li>Arcjc007.GravshipExporter</li>\n" +
                   "  </loadAfter>\n" +
                   "</ModMetaData>";
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
                return "user";
            }

            // ✅ Lowercase and replace spaces/underscores with nothing
            value = value.ToLowerInvariant()
                         .Replace(" ", "")
                         .Replace("_", "");

            // ✅ Keep only letters, digits, and dots
            var chars = value.Where(ch => char.IsLetterOrDigit(ch) || ch == '.').ToArray();
            string cleaned = new string(chars);

            // ✅ Fallback if cleaned string is empty
            if (string.IsNullOrEmpty(cleaned))
            {
                cleaned = "user";
            }

            // ✅ Must start with a letter
            if (!char.IsLetter(cleaned[0]))
            {
                cleaned = "u" + cleaned;
            }

            return cleaned;
        }

    }
}
