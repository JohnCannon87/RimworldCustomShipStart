using Verse;

namespace RimworldCustomShipStart
{
    public class CustomShipModSettings : ModSettings
    {
        public string selectedFileName = null;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref selectedFileName, "selectedFileName");
        }
    }
}
