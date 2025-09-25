using System.Collections.Generic;
using Verse;

namespace RimworldCustomShipStart
{
    public class ShipLayoutDefV2 : Def
    {
        public List<List<ShipCell>> rows;  // Row by row (z from max->min)
        public int width;
        public int height;

        // Engine location (relative to minX/minZ)
        public int gravEngineX;
        public int gravEngineZ;
    }
}
