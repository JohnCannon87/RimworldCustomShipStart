using UnityEngine;
using Verse;

namespace GravshipExport
{
    internal static class ModHeaderView
    {
        public static void Draw(Rect rect, ShipLayoutDefV2 currentShip, ref string searchText)
        {
            // Draw background section
            Widgets.DrawMenuSection(rect);
            var inner = rect.ContractedBy(6f);

            // Compact vertical layout
            float labelHeight = 22f;
            float fieldHeight = 22f;
            float y = inner.y + 4f;

            // "Search:" label
            var searchLabelRect = new Rect(inner.x + 4f, y, 40f, labelHeight);
            Widgets.Label(searchLabelRect, "Search:");

            // Search text box
            var searchRect = new Rect(searchLabelRect.xMax + 6f, y, inner.width - searchLabelRect.width - 10f, fieldHeight);
            searchText = Widgets.TextField(searchRect, searchText);
        }
    }
}
