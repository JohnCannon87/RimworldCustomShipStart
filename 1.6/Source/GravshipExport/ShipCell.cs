using System.Collections.Generic;
using Verse;

namespace GravshipExport
{
    public class ShipCell : IExposable
    {
        public string foundationDef;    // e.g. "Substructure"
        public string foundationStuff;  // optional

        public string terrainDef;       // e.g. "CarpetRed"
        public string terrainStuff;     // optional

        public List<ShipThingEntry> things = new List<ShipThingEntry>();

        public bool HasAnyData =>
            !string.IsNullOrEmpty(foundationDef) ||
            !string.IsNullOrEmpty(terrainDef) ||
            (things != null && things.Count > 0);

        public void ExposeData()
        {
            Scribe_Values.Look(ref foundationDef, "foundationDef");
            Scribe_Values.Look(ref foundationStuff, "foundationStuff");
            Scribe_Values.Look(ref terrainDef, "terrainDef");
            Scribe_Values.Look(ref terrainStuff, "terrainStuff");
            Scribe_Collections.Look(ref things, "things", LookMode.Deep);
        }
    }
}
