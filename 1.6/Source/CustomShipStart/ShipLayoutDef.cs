using System.Collections.Generic;
using Verse;

namespace RimworldCustomShipStart
{
    public class ShipLayoutDef : Def
    {
        // 2D int array encoded as rows in XML
        public List<string> structureRows;

        // Map of number → entry
        public List<ShipStructureEntry> structureLegend;

        // Optional things to spawn inside (beds, lamps, etc.)
        public List<ShipThingEntry> things;

        public int gravEngineXCoord;
        public int gravEngineZCoord;
        public bool placeConduitWithHull;
        public bool placeConduitWithDoor;

    }
}
