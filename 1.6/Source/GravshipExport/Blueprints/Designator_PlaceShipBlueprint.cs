using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipExport.Blueprints
{
    /// <summary>
    /// Active designator that places the chosen ship layout as a Sketch blueprint.
    /// This version places everything from the sketch exactly as defined—no filtering or validation.
    /// </summary>
    public class Designator_PlaceShipBlueprint : Designator
    {
        private readonly ShipLayoutDefV2 layout;
        private Sketch baseSketch;
        private Sketch rotatedSketch;
        private Rot4 placingRot = Rot4.North;

        public Designator_PlaceShipBlueprint(ShipLayoutDefV2 layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));

            defaultLabel = (layout.label ?? layout.defName).CapitalizeFirst();
            defaultDesc = "Place this ship layout as a blueprint.";
            icon = ContentFinder<Texture2D>.Get("UI/Commands/PlaceBlueprints");
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_PlaceBuilding;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;

            // Build the base sketch from layout
            baseSketch = ShipSketchBuilder.BuildBlueprintFromLayout(layout) ?? new Sketch();
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

            // Always work from a fresh rotated copy for preview
            rotatedSketch = baseSketch.DeepCopy();
            rotatedSketch.Rotate(placingRot);

            // Build a separate copy for ghost rendering
            var ghostSketch = rotatedSketch.DeepCopy();

            // Prune only for ghost drawing safety (graphics-only)
            for (int i = ghostSketch.Terrain.Count - 1; i >= 0; i--)
            {
                var t = ghostSketch.Terrain[i];
                if (t?.def == null || t.def.graphic == null || string.IsNullOrEmpty(t.def.texturePath))
                {
                    ghostSketch.Remove(t);
                }
            }

            // Try drawing the ghost preview
            try
            {
                ghostSketch.DrawGhost(mouse, Sketch.SpawnPosType.Unchanged, placingMode: true);
            }
            catch (Exception ex)
            {
                GravshipLogger.Error($"[Ghost] DrawGhost failed: {ex}");
            }

            // Draw the outline of the occupied area
            var cells = new List<IntVec3>(ghostSketch.OccupiedRect.MovedBy(mouse).Cells);
            GenDraw.DrawFieldEdges(cells);
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var sketchToSpawn = baseSketch.DeepCopy();
            sketchToSpawn.Rotate(placingRot);

            try
            {
                GravshipLogger.Message($"[Blueprint] Starting placement for layout '{layout.defName}'...");
                GravshipLogger.Message($"[Blueprint] Sketch contains {sketchToSpawn.Things.Count} things, {sketchToSpawn.Terrain.Count} terrain entries.");

                // Log each thing for debugging
                foreach (var thing in sketchToSpawn.Things)
                {
                    GravshipLogger.Message($"[Blueprint Thing] {thing.def?.defName ?? "null"} at {thing.pos}");
                }

                foreach (var terr in sketchToSpawn.Terrain)
                {
                    GravshipLogger.Message($"[Blueprint Terrain] {terr.def?.defName ?? "null"} at {terr.pos}");
                }

                // Spawn everything in the sketch exactly as defined
                sketchToSpawn.Spawn(
                    Map,
                    c,
                    Faction.OfPlayer,
                    Sketch.SpawnPosType.Unchanged,
                    Sketch.SpawnMode.Blueprint
                );

                GravshipLogger.Message($"[Blueprint] Placement complete for '{layout.defName}'.");
            }
            catch (Exception ex)
            {
                GravshipLogger.Error($"[Blueprint] Placement failed: {ex}");
            }
            finally
            {
                // Restore GravEngine range if temporarily modified earlier
                ShipSketchBuilder.RestoreGravEngineRange();
            }
        }

        /// <summary>
        /// Manual rotation control (1.4+ no longer overrides default keybinds automatically).
        /// </summary>
        public void Rotate(RotationDirection rotDir)
        {
            placingRot = placingRot.Rotated(rotDir);
            GravshipLogger.Message($"[Blueprint] Rotated placement to {placingRot.AsInt}.");
        }

        public override bool DragDrawMeasurements => true;
    }
}
