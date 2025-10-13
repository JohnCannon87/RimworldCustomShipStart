using Verse;

namespace GravshipExport
{
    public class ShipThingEntry : IExposable
    {
        public string defName;
        public string stuffDef;
        public int rotInteger;
        public string quality;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref stuffDef, "stuffDef");
            Scribe_Values.Look(ref rotInteger, "rotInteger");
            Scribe_Values.Look(ref quality, "quality");
        }
        public override string ToString()
        {
            return $"{defName ?? "unknown"}" +
                   (stuffDef != null ? $":{stuffDef}" : "") +
                   $"(rot={rotInteger})";
        }

    }

}
