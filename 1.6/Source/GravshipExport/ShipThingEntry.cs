using Verse;

namespace GravshipExport
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
        public override string ToString()
        {
            return $"{defName ?? "unknown"}" +
                   (stuffDef != null ? $":{stuffDef}" : "") +
                   $"(rot={rotInteger})";
        }

    }

}
