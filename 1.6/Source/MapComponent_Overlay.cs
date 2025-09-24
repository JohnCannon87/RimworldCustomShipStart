using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Rimball
{
    public class MapComponent_Overlay : MapComponent
    {
        private List<IntVec3> overlayCells = new List<IntVec3>();
        private Color overlayColor = Color.red;

        public MapComponent_Overlay(Map map) : base(map) { }

        public void AddOverlayCell(IntVec3 cell, Color color)
        {
            overlayCells.Add(cell);
            overlayColor = color;
        }

        public void ClearOverlays()
        {
            overlayCells.Clear();
        }

        // Method to draw the overlay
        public void DrawOverlays()
        {
            if (overlayCells != null && overlayCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(overlayCells, overlayColor);
            }
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            DrawOverlays(); // Draw overlays during map updates
        }
    }
}