using System.Linq;
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
            if (!ModLister.CheckOdyssey("Ancient launch pad"))
                return;

            var settings = LoadedModManager.GetMod<GravshipExportMod>()?.GetSettings<GravshipExportModSettings>();
            ShipLayoutDefV2 ship = null;

            // 🎲 Random selection mode
            if (settings?.randomSelectionEnabled == true && settings.randomShipPool != null && settings.randomShipPool.Count > 0)
            {
                var pool = settings.randomShipPool
                    .Select(defName => DefDatabase<ShipLayoutDefV2>.GetNamedSilentFail(defName))
                    .Where(s => s != null)
                    .ToList();

                if (pool.Count > 0)
                {
                    ship = pool.RandomElement();
                    GravshipLogger.Message($"🎲 Random ship selection active — randomly selected: {ship.defName}");
                }
                else
                {
                    GravshipLogger.Warning("Random mode was enabled, but no valid ships were found in the pool.");
                }
            }

            // 🛠️ Fallback: last used ship
            if (ship == null)
            {
                ship = settings?.lastUsedShip;
                if (ship != null)
                {
                    GravshipLogger.Message($"Using last used ship layout: {ship.defName}");
                }
            }

            // ✅ Final fallback: built-in default
            if (ship == null)
            {
                ship = DefDatabase<ShipLayoutDefV2>.GetNamedSilentFail("Odyssey_Original_Ship");
                if (ship != null && settings != null)
                {
                    settings.lastUsedShip = ship;
                    settings.Write();
                    GravshipLogger.Message($"Falling back to default ship: {ship.defName}");
                }

                if (ship == null)
                {
                    GravshipLogger.Warning("❌ Could not find any ship layout to spawn. Gravship generation will fail.");
                    return;
                }
            }

            // 🚀 Build and merge the final sketch
            GravshipLogger.Message($"🚀 Finalizing: building sketch from ship layout '{ship.defName}'...");
            Sketch built = ShipSketchBuilder.BuildFromLayout(ship);
            parms.sketch.Merge(built);
            GravshipLogger.Message($"✅ Ship '{ship.defName}' merged successfully into sketch.");
        }
    }
}
