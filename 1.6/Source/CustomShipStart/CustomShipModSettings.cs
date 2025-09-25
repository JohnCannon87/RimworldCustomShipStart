using Verse;

namespace RimworldCustomShipStart
{
    public class CustomShipModSettings : ModSettings
    {
        public ShipLayoutDefV2 lastUsedShip;

        public override void ExposeData()
        {
            // ✅ Save the defName and label alongside the full layout
            if (Scribe.mode == LoadSaveMode.Saving && lastUsedShip != null)
            {
                if (string.IsNullOrEmpty(lastUsedShip.defName))
                    lastUsedShip.defName = "CustomShip_" + Find.TickManager.TicksGame;

                if (string.IsNullOrEmpty(lastUsedShip.label))
                    lastUsedShip.label = lastUsedShip.defName;
            }

            Scribe_Deep.Look(ref lastUsedShip, "lastUsedShip");
        }
    }
}
