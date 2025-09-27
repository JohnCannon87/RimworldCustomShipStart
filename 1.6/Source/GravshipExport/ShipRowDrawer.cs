using System;
using UnityEngine;
using Verse;
using RimWorld;

namespace GravshipExport
{
    internal sealed class ShipRowDrawer
    {
        private const float RowPad = 4f;
        private const float DeleteButtonWidth = 32f;
        private const float ApplyButtonWidth = 50f;
        private const float ExportButtonWidth = 50f;
        private const float InfoWidth = 300f;
        private const float SourceWidth = 200f;

        private const int LabelMaxChars = 30;
        private const int SourceMaxChars = 30;
        private const int InfoMaxChars = 40;

        public const float RowHeight = 42f;

        public ShipRowResult DrawRow(Rect rowRect, ShipListItem item, bool isCurrent, bool isAlternateRow)
        {
            if (isCurrent)
            {
                Widgets.DrawBoxSolid(rowRect, new Color(0f, 1f, 0f, 0.15f));
            }
            else if (isAlternateRow)
            {
                Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.05f));
            }

            var result = new ShipRowResult();
            float curX = rowRect.x + RowPad;

            if (item.IsExported)
            {
                var deleteRect = new Rect(curX, rowRect.y + RowPad, DeleteButtonWidth, RowHeight - 2 * RowPad);
                if (Widgets.ButtonImage(deleteRect, TexButton.Delete))
                {
                    result.DeleteRequested = true;
                }
            }

            curX += DeleteButtonWidth + RowPad;

            float rightX = rowRect.xMax - RowPad;

            var applyRect = new Rect(rightX - ApplyButtonWidth, rowRect.y + RowPad, ApplyButtonWidth, RowHeight - 2 * RowPad);
            rightX -= ApplyButtonWidth + RowPad;

            var exportRect = new Rect(rightX - ExportButtonWidth, rowRect.y + RowPad, ExportButtonWidth, RowHeight - 2 * RowPad);
            rightX -= ExportButtonWidth + RowPad;

            var infoRect = new Rect(rightX - InfoWidth, rowRect.y + RowPad, InfoWidth, RowHeight - 2 * RowPad);
            rightX -= InfoWidth + RowPad;

            var sourceRect = new Rect(rightX - SourceWidth, rowRect.y + RowPad, SourceWidth, RowHeight - 2 * RowPad);
            rightX -= SourceWidth + RowPad;

            var labelRect = new Rect(curX, rowRect.y + RowPad, rightX - curX, RowHeight - 2 * RowPad);

            DrawLabel(item, labelRect);
            DrawSource(item, sourceRect);
            DrawInfo(item, infoRect);

            if (item.IsExported)
            {
                if (Widgets.ButtonText(exportRect, "Export"))
                {
                    result.ExportRequested = true;
                }
            }

            if (isCurrent)
            {
                GUI.color = Color.green;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(applyRect, "Active");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                if (Widgets.ButtonText(applyRect, "Apply"))
                {
                    result.ApplyRequested = true;
                }
            }

            return result;
        }

        private static void DrawLabel(ShipListItem item, Rect rect)
        {
            string fullLabel = item.Ship?.label ?? item.Ship?.defName ?? "Unnamed Ship";
            string drawLabel = StringUtilities.HardCut(fullLabel, LabelMaxChars);
            Widgets.Label(rect, drawLabel);
            if (!string.Equals(fullLabel, drawLabel, StringComparison.Ordinal))
            {
                TooltipHandler.TipRegion(rect, fullLabel);
            }
        }

        private static void DrawSource(ShipListItem item, Rect rect)
        {
            string fullSource = item.SourceLabel ?? string.Empty;
            string drawSource = StringUtilities.HardCut(fullSource, SourceMaxChars);
            GUI.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            Widgets.Label(rect, drawSource);
            GUI.color = Color.white;
            if (!string.Equals(fullSource, drawSource, StringComparison.Ordinal))
            {
                TooltipHandler.TipRegion(rect, fullSource);
            }
        }

        private static void DrawInfo(ShipListItem item, Rect rect)
        {
            string fullInfo = ShipInfoFormatter.GetInfo(item.Ship);
            string drawInfo = StringUtilities.HardCut(fullInfo, InfoMaxChars);
            Widgets.Label(rect, drawInfo);
            if (!string.Equals(fullInfo, drawInfo, StringComparison.Ordinal))
            {
                TooltipHandler.TipRegion(rect, fullInfo);
            }
        }
    }

    internal struct ShipRowResult
    {
        public bool DeleteRequested { get; set; }
        public bool ApplyRequested { get; set; }
        public bool ExportRequested { get; set; }
    }
}
