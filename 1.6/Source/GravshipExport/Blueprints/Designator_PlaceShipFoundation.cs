using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipExport.Blueprints
{
    /// <summary>
    /// Places only foundation terrain from the given layout (Substructure, etc.).
    /// </summary>
    public class Designator_PlaceShipFoundation : Designator
    {
        private readonly ShipLayoutDefV2 layout;
        private Sketch baseSketch;
        private Sketch rotatedSketch;
        private Rot4 placingRot = Rot4.North;

        public Designator_PlaceShipFoundation(ShipLayoutDefV2 layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));

            defaultLabel = $"{layout.label ?? layout.defName} (Foundations)";
            defaultDesc = "Place only the ship’s foundation layer.";
            icon = ContentFinder<Texture2D>.Get("UI/Commands/PlaceBlueprints");
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_PlaceBuilding;
            soundDragSustain = SoundDefOf.Designate_DragStandard;

            baseSketch = ShipSketchBuilder.BuildFoundationsFromLayout(layout) ?? new Sketch();
            rotatedSketch = baseSketch.DeepCopy();
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            return c.InBounds(Map);
        }

        public override void SelectedUpdate()
        {
            base.SelectedUpdate();

            IntVec3 mouse = UI.MouseCell();
            rotatedSketch = baseSketch.DeepCopy();
            rotatedSketch.Rotate(placingRot);

            var ghostSketch = rotatedSketch.DeepCopy();

            try
            {
                ghostSketch.DrawGhost(mouse, Sketch.SpawnPosType.Unchanged, placingMode: true);
            }
            catch (Exception ex)
            {
                GravshipLogger.Error($"[Foundation Ghost] {ex}");
            }

            GenDraw.DrawFieldEdges(new List<IntVec3>(ghostSketch.OccupiedRect.MovedBy(mouse).Cells));
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var sketchToSpawn = baseSketch.DeepCopy();
            sketchToSpawn.Rotate(placingRot);

            try
            {
                GravshipLogger.Message($"[Foundation] Spawning foundation layer for '{layout.defName}'...");

                sketchToSpawn.Spawn(
                    Map,
                    c,
                    Faction.OfPlayer,
                    Sketch.SpawnPosType.Unchanged,
                    Sketch.SpawnMode.Blueprint
                );

                GravshipLogger.Message("[Foundation] Placement complete.");
            }
            catch (Exception ex)
            {
                GravshipLogger.Error($"[Foundation] Placement failed: {ex}");
            }
        }

        public void Rotate(RotationDirection rotDir)
        {
            placingRot = placingRot.Rotated(rotDir);
            GravshipLogger.Message($"[Foundation] Rotated placement to {placingRot.AsInt}.");
        }

        public override bool DragDrawMeasurements => true;
    }
}
