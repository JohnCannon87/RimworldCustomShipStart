using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipExport
{
    public class Dialog_NameShip : Window
    {
        private readonly Building_GravEngine engine;

        // ✅ These static fields remember the last entered values
        private static string lastShipName = "MyShip";
        private static string lastShipDescription = "";

        private string shipName;
        private string shipDescription;

        private bool focusedNameField;
        private const string NameFieldControlName = "ShipNameField";

        public override Vector2 InitialSize => new Vector2(520f, 360f); // ✅ Taller + wider

        public Dialog_NameShip(Building_GravEngine engine, string initialName = null)
        {
            this.engine = engine;

            // ✅ If a name was provided, use it — otherwise fall back to last typed or default
            shipName = !string.IsNullOrEmpty(initialName) ? initialName : lastShipName;
            shipDescription = lastShipDescription;

            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;

            closeOnAccept = false;
            closeOnCancel = false;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float curY = 0f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, inRect.width, 30f), "Name and Describe Your Ship:");
            curY += 40f;

            Text.Font = GameFont.Small;

            // --- Name Field ---
            GUI.SetNextControlName(NameFieldControlName);
            shipName = Widgets.TextField(new Rect(0f, curY, inRect.width, 30f), shipName);
            curY += 40f;

            if (!focusedNameField)
            {
                UI.FocusControl(NameFieldControlName, this);
                focusedNameField = true;
            }

            // --- Description Field ---
            Widgets.Label(new Rect(0f, curY, inRect.width, 24f), "Description (optional):");
            curY += 30f;

            float descHeight = 150f; // ✅ taller description area
            shipDescription = Widgets.TextArea(new Rect(0f, curY, inRect.width, descHeight), shipDescription);
            curY += descHeight + 20f;

            // --- Buttons ---
            float buttonWidth = (inRect.width / 2f) - 5f;
            Rect cancelRect = new Rect(0f, curY, buttonWidth, 30f);
            Rect exportRect = new Rect(inRect.width / 2f + 5f, curY, buttonWidth, 30f);

            if (Widgets.ButtonText(cancelRect, "Cancel"))
                OnCancelKeyPressed();

            if (Widgets.ButtonText(exportRect, "Export"))
                OnAcceptKeyPressed();
        }

        public override void OnAcceptKeyPressed()
        {
            ConfirmAndExport();
        }

        public override void OnCancelKeyPressed()
        {
            Close();
        }

        private void ConfirmAndExport()
        {
            string trimmedName = shipName?.Trim();
            string trimmedDesc = shipDescription?.Trim();

            if (string.IsNullOrEmpty(trimmedName))
            {
                Messages.Message("Please enter a valid ship name.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            // ✅ Remember last typed values
            lastShipName = trimmedName;
            lastShipDescription = trimmedDesc ?? "";

            // ⚠️ Optional warning if no description
            if (string.IsNullOrWhiteSpace(trimmedDesc))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "No description has been entered for this ship.\n\nAre you sure you want to export without one?",
                    () =>
                    {
                        GravshipExporter.Export(engine, trimmedName, trimmedDesc);
                        Close();
                    },
                    false
                ));
            }
            else
            {
                GravshipExporter.Export(engine, trimmedName, trimmedDesc);
                Close();
            }
        }
    }
}
