using Verse;

namespace RimworldCustomShipStart
{
    public class ShipStructureEntry
    {
        public int id; // e.g. 0,1,2,3
        public TerrainDef terrainDef; // usually Substructure
        public ThingDef thingDef;     // e.g. hull wall, door
        public ThingDef stuffDef;
    }
}
