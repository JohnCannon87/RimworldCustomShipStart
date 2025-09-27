using UnityEngine;
using Verse;

namespace GravshipExport
{
    internal static class ExportPromptView
    {
        public static void Draw(Rect rect, ShipListItem target, ref string exportNameBuffer, System.Action onCreate, System.Action onCancel)
        {
            Widgets.DrawLightHighlight(rect);
            var inner = rect.ContractedBy(8f);

            Widgets.Label(new Rect(inner.x, inner.y, 180f, 24f), "Export as Mod:");
            var inputRect = new Rect(inner.x + 188f, inner.y, inner.width - 368f, 24f);
            exportNameBuffer = Widgets.TextField(inputRect, exportNameBuffer);

            var createRect = new Rect(inner.xMax - 170f, inner.y, 80f, 24f);
            var cancelRect = new Rect(inner.xMax - 85f, inner.y, 80f, 24f);

            if (Widgets.ButtonText(createRect, "Create"))
            {
                if (target?.Ship != null && !string.IsNullOrWhiteSpace(exportNameBuffer))
                {
                    onCreate?.Invoke();
                }
            }

            if (Widgets.ButtonText(cancelRect, "Cancel"))
            {
                onCancel?.Invoke();
            }
        }
    }
}
