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

            // 🎲 Check if random mode is enabled and pool is valid
            if (settings?.randomSelectionEnabled == true && settings.randomShipPool != null && settings.randomShipPool.Count > 0)
            {
                var pool = settings.randomShipPool
                    .Select(defName => DefDatabase<ShipLayoutDefV2>.GetNamedSilentFail(defName))
                    .Where(s => s != null)
                    .ToList();

                if (pool.Count > 0)
                {
                    ship = pool.RandomElement();
                    Log.Message($"[GravshipExport] 🎲 Random mode active — selected random ship: {ship.defName}");
                }
                else
                {
                    Log.Warning("[GravshipExport] Random mode was enabled but no valid ships were found in the pool.");
                }
            }

            // 🛠️ If random mode not active or no valid ship found, fall back to last used ship
            if (ship == null)
            {
                ship = settings?.lastUsedShip;
            }

            // ✅ Final fallback if nothing is selected
            if (ship == null)
            {
                ship = DefDatabase<ShipLayoutDefV2>.GetNamedSilentFail("Odyssey_Original_Ship");
                if (ship != null && settings != null)
                {
                    settings.lastUsedShip = ship;
                    settings.Write();
                }

                if (ship == null)
                {
                    Log.Warning("[GravshipExport] ❌ Could not find any ship to spawn. Generation will fail.");
                    return;
                }
            }

            // 🚀 Spawn the ship
            Log.Message($"[GravshipExport] Using ship layout: {ship.defName}");
            Sketch built = ShipSketchBuilder.BuildFromLayout(ship);
            parms.sketch.Merge(built);
        }
    }
}
