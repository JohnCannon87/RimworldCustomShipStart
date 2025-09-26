using System.IO;
using Verse;

namespace GravshipExport
{
    public static class LayoutFileUtility
    {
        public static string GetSettingsFolder()
        {
            return Path.Combine(GenFilePaths.ConfigFolderPath, "GravshipExport");
        }

        public static void SaveLayout(ShipLayoutDef layout)
        {
            var folder = GetSettingsFolder();
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, $"{layout.defName}.xml");
            Scribe.saver.InitSaving(path, "Defs");
            try
            {
                Scribe_Deep.Look(ref layout, "ShipLayoutDef");
            }
            finally
            {
                Scribe.saver.FinalizeSaving();
            }
        }

        public static ShipLayoutDef LoadLayout(string defName)
        {
            var path = Path.Combine(GetSettingsFolder(), $"{defName}.xml");
            if (!File.Exists(path))
                return null;

            ShipLayoutDef layout = null;
            Scribe.loader.InitLoading(path);
            try
            {
                Scribe_Deep.Look(ref layout, "ShipLayoutDef");
            }
            finally
            {
                Scribe.loader.FinalizeLoading();
            }

            return layout;
        }
    }
}
