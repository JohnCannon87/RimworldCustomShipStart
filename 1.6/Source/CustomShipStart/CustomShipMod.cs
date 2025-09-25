using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimworldCustomShipStart
{
    public class CustomShipMod : Mod
    {
        private CustomShipModSettings settings;

        public CustomShipMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<CustomShipModSettings>();
        }

        public override string SettingsCategory() => "Custom Ship Start";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            if (list.ButtonText("Refresh Ships"))
            {
                ShipManager.Refresh();
            }

            if (ShipManager.LoadedShips.Count > 0)
            {
                List<string> keys = new List<string>(ShipManager.LoadedShips.Keys);
                int index = Mathf.Max(0, keys.IndexOf(settings.selectedFileName));
                string current = index >= 0 ? keys[index] : keys[0];

                if (list.ButtonText($"Selected Ship: {current}"))
                {
                    FloatMenu menu = new FloatMenu(keys.ConvertAll(k =>
                        new FloatMenuOption(k, () => settings.selectedFileName = k)));
                    Find.WindowStack.Add(menu);
                }

                if (list.ButtonText("Apply Selected Ship"))
                {
                    if (!string.IsNullOrEmpty(settings.selectedFileName))
                    {
                        ShipManager.ApplySelection(settings.selectedFileName);
                        WriteSettings();
                    }
                    else
                    {
                        Messages.Message("No ship selected.", MessageTypeDefOf.RejectInput, false);
                    }
                }
            }
            else
            {
                list.Label("No ships loaded. Click 'Refresh Ships' to scan exports.");
            }

            list.End();
        }
    }
}
