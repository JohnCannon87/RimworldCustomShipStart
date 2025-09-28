using UnityEngine;
using Verse;
using RimWorld;

namespace GravshipExport
{
    public class Dialog_ExportModName : Window
    {
        private readonly ShipLayoutDefV2 ship;
        private readonly System.Action<string> onConfirm;
        private string modName;

        public override Vector2 InitialSize => new Vector2(480f, 180f);

        public Dialog_ExportModName(ShipLayoutDefV2 ship, string suggestedName, System.Action<string> onConfirm)
        {
            this.ship = ship;
            this.modName = suggestedName;
            this.onConfirm = onConfirm;

            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30f), "Export Ship as Mod");

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 40, inRect.width, 24f), "Mod Name:");

            modName = Widgets.TextField(new Rect(0, 70, inRect.width, 30f), modName);

            if (Widgets.ButtonText(new Rect(0, 120, inRect.width / 2f - 5f, 30f), "Cancel"))
            {
                Close();
            }

            if (Widgets.ButtonText(new Rect(inRect.width / 2f + 5f, 120, inRect.width / 2f - 5f, 30f), "Export"))
            {
                if (!string.IsNullOrWhiteSpace(modName))
                {
                    onConfirm?.Invoke(modName.Trim());
                    Close();
                }
                else
                {
                    Messages.Message("Please enter a valid mod name.", MessageTypeDefOf.RejectInput, false);
                }
            }
        }
    }
}
