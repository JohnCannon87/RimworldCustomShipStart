using Verse;

namespace GravshipExport
{
    public class ShipStructureEntry
    {
        public string id; // e.g. 0,1,2,3
        public TerrainDef terrainDef; // usually Substructure
        public ThingDef thingDef;     // e.g. hull wall, door
        public ThingDef stuffDef;
    }
}
