using Verse;

namespace RimworldCustomShipStart
{
    public class ShipThingEntry : IExposable
    {
        public string defName;
        public string stuffDef;
        public int rotInteger;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref stuffDef, "stuffDef");
            Scribe_Values.Look(ref rotInteger, "rotInteger");
        }
    }

}
