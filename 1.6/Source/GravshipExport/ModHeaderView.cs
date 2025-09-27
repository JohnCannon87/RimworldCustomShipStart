using UnityEngine;
using Verse;

namespace GravshipExport
{
    internal static class ModHeaderView
    {
        public static void Draw(Rect rect, ShipLayoutDefV2 currentShip, ref string searchText)
        {
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(8f);

            string currentLabel = currentShip != null ? currentShip.label : "None";
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), $"Current ship: {currentLabel}");

            var searchLabelRect = new Rect(inner.x, inner.y + 32f, 100f, 24f);
            Widgets.Label(searchLabelRect, "Search:");

            var searchRect = new Rect(searchLabelRect.xMax + 8f, searchLabelRect.y, inner.width - searchLabelRect.width - 8f, 24f);
            searchText = Widgets.TextField(searchRect, searchText);
        }
    }
}
