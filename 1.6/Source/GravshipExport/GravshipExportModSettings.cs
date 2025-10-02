using System.Collections.Generic;
using Verse;

namespace GravshipExport
{
    public class GravshipExportModSettings : ModSettings
    {
        public ShipLayoutDefV2 lastUsedShip;

        // 🎲 New fields for random ship selection
        public bool randomSelectionEnabled = false;
        public List<string> randomShipPool = new List<string>();

        public override void ExposeData()
        {
            // ✅ Save current selection safely
            if (Scribe.mode == LoadSaveMode.Saving && lastUsedShip != null)
            {
                if (string.IsNullOrEmpty(lastUsedShip.defName))
                    lastUsedShip.defName = "Gravship_" + Find.TickManager.TicksGame;

                if (string.IsNullOrEmpty(lastUsedShip.label))
                    lastUsedShip.label = lastUsedShip.defName;
            }

            // 🔁 Save/load the active ship
            Scribe_Deep.Look(ref lastUsedShip, "lastUsedShip");

            // ✅ Fallback to a default ship if none was saved
            if (Scribe.mode == LoadSaveMode.PostLoadInit && lastUsedShip == null)
            {
                lastUsedShip = DefDatabase<ShipLayoutDefV2>.GetNamedSilentFail("Odyssey_Original_Ship");
            }

            // 🎲 Save/load random mode state
            Scribe_Values.Look(ref randomSelectionEnabled, "randomSelectionEnabled", false);

            // 🎲 Save/load random pool
            Scribe_Collections.Look(ref randomShipPool, "randomShipPool", LookMode.Value);
            if (randomShipPool == null)
                randomShipPool = new List<string>();
        }
    }
}
