using RimWorld;
using RimWorld.SketchGen;
using Verse;
using System.Collections.Generic;

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
                // If no ship was explicitly provided, try to load the last one from mod settings
                var settings = LoadedModManager.GetMod<GravshipExportMod>()?.GetSettings<GravshipExportModSettings>();
                if (settings?.lastUsedShip != null)
                {
                    //jcLog.Message("[GravshipExport] Auto-loading last used ship from settings...");
                    Sketch built = ShipSketchBuilder.BuildFromLayout(settings.lastUsedShip);
                    parms.sketch.Merge(built);
                }
            }
        }
    }
}
