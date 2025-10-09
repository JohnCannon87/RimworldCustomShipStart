using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipExport.Blueprints
{

    /// Root button in Architect > Odyssey.
    public class Designator_PlaceShipBlueprintRoot : Designator
    {
        public Designator_PlaceShipBlueprintRoot()
        {
            defaultLabel = "Place ship blueprint";
            defaultDesc = "Choose a ship layout and place it as a blueprint.";
            icon = ContentFinder<Texture2D>.Get("UI/Commands/PlaceBlueprints");
            useMouseIcon = true;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c) => false; // root never places directly

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);

            // Prefer your settings layout browser if you can open it here.
            // If not, fallback to a FloatMenu of all layouts:
            var layouts = DefDatabase<ShipLayoutDefV2>.AllDefsListForReading
                .OrderBy(d => d.label ?? d.defName).ToList();

            if (layouts.Count == 0)
            {
                Messages.Message("No ship layouts found.", MessageTypeDefOf.RejectInput);
                return;
            }

            var options = new List<FloatMenuOption>();
            foreach (var layout in layouts)
            {
                var cap = (layout.label ?? layout.defName).CapitalizeFirst();
                options.Add(new FloatMenuOption(cap, () =>
                {
                    // Spawn a transient child designator bound to the chosen layout.
                    var child = new Designator_PlaceShipBlueprint(layout);
                    Find.DesignatorManager.Select(child);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
