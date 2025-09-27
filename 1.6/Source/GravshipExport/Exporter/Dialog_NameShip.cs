using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipExport
{
    public class Dialog_NameShip : Window
    {
        private readonly Building_GravEngine engine;
        private string shipName = "MyShip";

        // keep text field focused so typing works immediately
        private bool focusedNameField;
        private const string NameFieldControlName = "ShipNameField";

        public override Vector2 InitialSize => new Vector2(400f, 150f);

        public Dialog_NameShip(Building_GravEngine engine)
        {
            this.engine = engine;

            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;

            // let us handle Enter/Escape ourselves
            closeOnAccept = false;
            closeOnCancel = false;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "Name Your Ship:");

            Text.Font = GameFont.Small;

            var nameRect = new Rect(0f, 40f, inRect.width, 30f);
            GUI.SetNextControlName(NameFieldControlName);
            shipName = Widgets.TextField(nameRect, shipName);

            if (!focusedNameField)
            {
                UI.FocusControl(NameFieldControlName, this);
                focusedNameField = true;
            }

            if (Widgets.ButtonText(new Rect(0f, 90f, inRect.width / 2f - 5f, 30f), "Cancel"))
                OnCancelKeyPressed();

            if (Widgets.ButtonText(new Rect(inRect.width / 2f + 5f, 90f, inRect.width / 2f - 5f, 30f), "Export"))
                OnAcceptKeyPressed();
        }

        // Pressing Enter (or KeypadEnter) triggers this automatically via WindowStack
        public override void OnAcceptKeyPressed()
        {
            ConfirmAndExport();
        }

        // Pressing Escape triggers this automatically via WindowStack
        public override void OnCancelKeyPressed()
        {
            Close();
        }

        private void ConfirmAndExport()
        {
            var trimmed = shipName?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                Messages.Message("Please enter a valid ship name.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            GravshipExporter.Export(engine, trimmed);
            Close();
        }
    }
}
