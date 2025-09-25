using RimWorld;
using RimWorld.SketchGen;
using Verse;
using System.Collections.Generic;

namespace RimworldCustomShipStart
{
    public class SketchResolver_CustomizableGravship : SketchResolver
    {

        protected override bool CanResolveInt(SketchResolveParams parms)
        {
            return parms.sketch != null;
        }

        protected override void ResolveInt(SketchResolveParams parms)
        {
            if (ModLister.CheckOdyssey("Ancient launch pad"))
            {
                // Load your exported def (however you store/choose it)
                ShipLayoutDefV2 layout = DefDatabase<ShipLayoutDefV2>.GetNamedSilentFail("CustomShipDef");

                // Convert to Sketch and merge
                Sketch built = ShipSketchBuilder.BuildFromLayout(layout);
                parms.sketch.Merge(built);
            }
        }
    }
}
