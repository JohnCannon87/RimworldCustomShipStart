using System.Diagnostics;
using System.IO;
using UnityEngine;
using Verse;
using RimWorld;

namespace GravshipExport
{
    public class Dialog_ModExportSuccess : Window
    {
        private readonly string modFolder;
        private readonly string modName;
        private readonly bool hasPreview;

        public override Vector2 InitialSize => new Vector2(640f, 480f);

        public Dialog_ModExportSuccess(string modFolder, string modName, bool hasPreview)
        {
            this.modFolder = modFolder;
            this.modName = modName;
            this.hasPreview = hasPreview;

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
            Widgets.Label(new Rect(0f, y, inRect.width, 32f), "✅ Mod Export Complete!");
            y += 40f;

            Text.Font = GameFont.Small;

            string text =
                $"Your ship has been successfully exported as a mod!\n\n" +
                $"📦 **Mod name:** {modName}\n" +
                $"📁 **Location:**\n{modFolder}\n\n" +
                "You can now enable this mod in the RimWorld mod list just like any other.\n\n";

            if (!hasPreview)
            {
                text +=
                    "⚠️ **No preview image was found!**\n" +
                    "For a polished mod, add a PNG preview image in both of these locations:\n\n" +
                    $"   {Path.Combine(modFolder, "About", "Preview.png")}\n" +
                    $"   {Path.Combine(modFolder, "Textures", "Previews", $"{modName}.png")}\n\n" +
                    "💡 Recommended size: ~512×512 or larger.\n\n";
            }

            Widgets.Label(new Rect(0f, y, inRect.width, Text.CalcHeight(text, inRect.width)), text);
            y += Text.CalcHeight(text, inRect.width) + 20f;

            float buttonWidth = (inRect.width - 20f) / 2f;

            if (Widgets.ButtonText(new Rect(0f, y, buttonWidth, 40f), "Open Mod Folder"))
            {
                TryOpenFolder(modFolder);
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
