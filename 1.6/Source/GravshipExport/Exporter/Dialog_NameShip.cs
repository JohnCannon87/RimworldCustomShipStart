using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipExport
{
    public class Dialog_NameShip : Window
    {
        private readonly Building_GravEngine engine;
        private string shipName = "MyShip";

        public override Vector2 InitialSize => new Vector2(400f, 150f);

        public Dialog_NameShip(Building_GravEngine engine)
        {
            this.engine = engine;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30f), "Name Your Ship:");

            Text.Font = GameFont.Small;
            shipName = Widgets.TextField(new Rect(0, 40f, inRect.width, 30f), shipName);

            if (Widgets.ButtonText(new Rect(0, 90f, inRect.width / 2 - 5f, 30f), "Cancel"))
            {
                Close();
            }

            if (Widgets.ButtonText(new Rect(inRect.width / 2 + 5f, 90f, inRect.width / 2 - 5f, 30f), "Export"))
            {
                GravshipExporter.Export(engine, shipName);
                Close();
            }
        }
    }
}
