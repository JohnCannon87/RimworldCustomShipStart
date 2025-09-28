using RimWorld;
using RimWorld.SketchGen;
using Verse;

namespace GravshipExport
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
                var settings = LoadedModManager.GetMod<GravshipExportMod>()?.GetSettings<GravshipExportModSettings>();
                var ship = settings?.lastUsedShip;

                // ✅ Defensive fallback (should normally be set by settings)
                if (ship == null)
                {
                    ship = DefDatabase<ShipLayoutDefV2>.GetNamedSilentFail("Odyssey_Original_Ship");
                    if (ship != null)
                    {
                        settings.lastUsedShip = ship;
                    }
                    else
                    {
                        Log.Warning("[GravshipExport] WARNING: Could not find fallback ship 'Odyssey_Original_Ship'. Generation may fail.");
                        return;
                    }
                }

                //jcLog.Message("[GravshipExport] Using ship layout: " + ship.defName);
                Sketch built = ShipSketchBuilder.BuildFromLayout(ship);
                parms.sketch.Merge(built);
            }
        }

    }
}
