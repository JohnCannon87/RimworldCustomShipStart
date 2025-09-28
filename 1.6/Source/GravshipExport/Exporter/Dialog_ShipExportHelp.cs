using System.Diagnostics;
using System.IO;
using UnityEngine;
using Verse;
using RimWorld;

namespace GravshipExport
{
    public class Dialog_ShipExportHelp : Window
    {
        private readonly string configPath;
        private readonly string layoutName;

        public override Vector2 InitialSize => new Vector2(640f, 500f);

        public Dialog_ShipExportHelp(string configPath, string layoutName)
        {
            this.configPath = configPath;
            this.layoutName = layoutName;

            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseButton = false;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = 0f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, inRect.width, 32f), "Ship Export Complete!");
            y += 40f;

            Text.Font = GameFont.Small;

            string text =
                $"✅ Your ship \"{layoutName}\" was exported successfully!\n\n" +
                "📸 To add a preview image:\n\n" +
                "1. Open the following folder on your system:\n" +
                $"   {configPath}\n\n" +
                "2. Place a PNG file with the **exact same name** as your exported XML file in that folder.\n\n" +
                "   i.e.:\n" +
                $"   {layoutName}.xml\n" +
                $"   {layoutName}.png\n\n" +
                "💡 Recommended size: ~512×512 or larger.\n\n" +
                "⚠️ We’re sorry this isn’t automated yet — we’re actively working on an update that will capture ship previews automatically.";

            Widgets.Label(new Rect(0f, y, inRect.width, Text.CalcHeight(text, inRect.width)), text);
            y += Text.CalcHeight(text, inRect.width) + 20f;

            // --- Buttons ---
            float buttonWidth = (inRect.width - 20f) / 2f;

            if (Widgets.ButtonText(new Rect(0f, y, buttonWidth, 40f), "Open Config Folder"))
            {
                TryOpenFolder(configPath);
            }

            if (Widgets.ButtonText(new Rect(buttonWidth + 20f, y, buttonWidth, 40f), "Close"))
            {
                Close();
            }
        }

        private void TryOpenFolder(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
#if UNITY_STANDALONE_OSX
                    Process.Start("open", $"\"{path}\"");
#elif UNITY_STANDALONE_WIN
                    Process.Start("explorer.exe", $"\"{path}\"");
#else
                    Process.Start(path);
#endif
                }
                else
                {
                    Messages.Message("Folder not found: " + path, MessageTypeDefOf.RejectInput, false);
                }
            }
            catch
            {
                Messages.Message("Could not open folder on this platform.", MessageTypeDefOf.RejectInput, false);
            }
        }
    }
}
