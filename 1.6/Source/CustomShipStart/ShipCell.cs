using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimworldCustomShipStart
{
    public class ShipCell
    {
        public string foundationDef; // e.g. "Substructure"
        public string foundationStuff; // optional

        public string terrainDef; // e.g. "CarpetRed"
        public string terrainStuff; // optional

        public List<ShipThingEntry> things = new List<ShipThingEntry>();

        public bool HasAnyData =>
            !string.IsNullOrEmpty(foundationDef) ||
            !string.IsNullOrEmpty(terrainDef) ||
            (things != null && things.Count > 0);
    }
}
