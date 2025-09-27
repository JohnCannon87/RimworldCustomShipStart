using System.Text;
using Verse;

namespace GravshipExport
{
    internal static class ShipInfoFormatter
    {
        public static string GetInfo(ShipLayoutDefV2 ship)
        {
            if (ship == null)
            {
                return string.Empty;
            }

            int width = ship.width;
            int height = ship.height;

            int thingCount = 0;
            int terrainCells = 0;

            if (ship.rows != null)
            {
                foreach (var row in ship.rows)
                {
                    if (row == null)
                    {
                        continue;
                    }

                    foreach (var cell in row)
                    {
                        if (cell == null)
                        {
                            continue;
                        }

                        if (cell.things != null)
                        {
                            thingCount += cell.things.Count;
                        }

                        if (!string.IsNullOrEmpty(cell.foundationDef) || !string.IsNullOrEmpty(cell.terrainDef))
                        {
                            terrainCells++;
                        }
                    }
                }
            }

            var builder = new StringBuilder();
            builder.Append(width);
            builder.Append('×');
            builder.Append(height);
            builder.Append(" | ");
            builder.Append(thingCount);
            builder.Append(" things | ");
            builder.Append(terrainCells);
            builder.Append(" terrain cells");
            return builder.ToString();
        }
    }
}
