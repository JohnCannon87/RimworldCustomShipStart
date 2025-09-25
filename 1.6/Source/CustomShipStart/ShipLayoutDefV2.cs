using Verse;
using System.Collections.Generic;

namespace RimworldCustomShipStart
{
    public class ShipLayoutDefV2 : Def, IExposable
    {
        public List<List<ShipCell>> rows;
        public int width;
        public int height;
        public int gravEngineX;
        public int gravEngineZ;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref label, "label");

            Scribe_Collections.Look(ref rows, "rows", LookMode.Deep);
            Scribe_Values.Look(ref width, "width");
            Scribe_Values.Look(ref height, "height");
            Scribe_Values.Look(ref gravEngineX, "gravEngineX");
            Scribe_Values.Look(ref gravEngineZ, "gravEngineZ");
        }
    }
}
