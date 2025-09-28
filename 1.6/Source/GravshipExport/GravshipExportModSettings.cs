using Verse;

namespace GravshipExport
{
    public class GravshipExportModSettings : ModSettings
    {
        public ShipLayoutDefV2 lastUsedShip;

        public override void ExposeData()
        {
            // ✅ Save current selection
            if (Scribe.mode == LoadSaveMode.Saving && lastUsedShip != null)
            {
                if (string.IsNullOrEmpty(lastUsedShip.defName))
                    lastUsedShip.defName = "Gravship_" + Find.TickManager.TicksGame;

                if (string.IsNullOrEmpty(lastUsedShip.label))
                    lastUsedShip.label = lastUsedShip.defName;
            }

            Scribe_Deep.Look(ref lastUsedShip, "lastUsedShip");

            // ✅ Fallback to a default ship on first load if none was saved
            if (Scribe.mode == LoadSaveMode.PostLoadInit && lastUsedShip == null)
            {
                lastUsedShip = DefDatabase<ShipLayoutDefV2>.GetNamedSilentFail("Odyssey_Original_Ship");
            }
        }
    }
}
