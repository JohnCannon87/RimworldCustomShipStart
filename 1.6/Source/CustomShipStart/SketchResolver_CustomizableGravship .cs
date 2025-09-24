using RimWorld;
using RimWorld.SketchGen;
using Verse;
using System.Collections.Generic;

namespace RimworldCustomShipStart
{
    public class SketchResolver_CustomizableGravship : SketchResolver
    {

        protected override bool CanResolveInt(SketchResolveParams parms)
        {
            return parms.sketch != null;
        }

        protected override void ResolveInt(SketchResolveParams parms)
        {
            if (ModLister.CheckOdyssey("Ancient launch pad"))
            {
                Sketch sketch = new Sketch();

                // Pick the layout (could be made configurable later via ModSettings)
                ShipLayoutDef layout = DefDatabase<ShipLayoutDef>.GetNamedSilentFail("CustomStarterShip");
                if (layout == null)
                {
                    Log.Error("[CustomShipStart] Could not find ShipLayoutDef named CustomStarterShip.");
                    return;
                }

                //jc//jcLog.Message($"[CustomShipStart] Found layout '{layout.defName}' with {layout.structureRows.Count} structureRows.");
                //jc//jcLog.Message($"[CustomShipStart] Found layout '{layout.defName}' with {layout.structureLegend.Count} structureLegends.");
                //jc//jcLog.Message($"[CustomShipStart] Found layout '{layout.defName}' with {layout.things.Count} things.");
                //jc//jcLog.Message($"[CustomShipStart] Found layout '{layout.defName}' with {layout.gravEngineXCoord} Grav Engine X Coord.");
                //jc//jcLog.Message($"[CustomShipStart] Found layout '{layout.defName}' with {layout.gravEngineZCoord} Grav Engine Z Coord.");

                sketch.AddThing(ThingDefOf.GravEngine, new IntVec3(layout.gravEngineXCoord, 0, layout.gravEngineZCoord), Rot4.North, null, 1, null, null, wipeIfCollides: true, 0.5f);

                // Build structure grid
                for (int row = 0; row < layout.structureRows.Count; row++)
                {
                    string rowStr = layout.structureRows[row];
                    for (int col = 0; col < rowStr.Length; col++)
                    {
                        int id = rowStr[col] - '0'; // quick char→int
                        ShipStructureEntry entry = layout.structureLegend.Find(e => e.id == id);
                        if (entry == null) continue;

                        IntVec3 pos = new IntVec3(col, 0, row);

                        if (entry.terrainDef != null)
                        {
                            sketch.AddTerrain(entry.terrainDef, pos);
                            //jc//jcLog.Message($"[CustomShipStart] Added terrain {entry.terrainDef.defName} at {pos} (id {id})");
                        }

                        if (entry.thingDef != null)
                        {
                            if(entry.stuffDef == null)
                            {
                                sketch.AddThing(entry.thingDef, pos, Rot4.North);
                            }
                            else
                            {
                                sketch.AddThing(entry.thingDef, pos, Rot4.North, entry.stuffDef);
                            }

                            if(entry.thingDef.defName.Contains("Hull") && layout.placeConduitWithHull)
                            {
                                sketch.AddThing(ThingDefOf.PowerConduit, pos, Rot4.North, null, 1, null, null, false, 0.5f);
                            }

                            if ((entry.thingDef.defName.Contains("Door") || (entry.thingDef.defName.Contains("Autodoor") || (entry.thingDef.defName.Contains("VacBarrier"))) && layout.placeConduitWithDoor))
                            {
                                sketch.AddThing(ThingDefOf.HiddenConduit, pos, Rot4.North, null, 1, null, null, false, 0.5f);
                            }

                            //jc//jcLog.Message($"[CustomShipStart] Added thing {entry.thingDef.defName} at {pos} (id {id})");
                        }
                    }
                }

                // Add furniture/things
                if (layout.things != null)
                {
                    foreach (var thing in layout.things)
                    {
                        IntVec3 pos = new IntVec3(thing.x, 0, thing.z);
                        if(thing.stuffDef == null)
                        {
                            sketch.AddThing(thing.thingDef, pos, thing.rot ?? Rot4.North);
                        }
                        else
                        {
                            sketch.AddThing(thing.thingDef, pos, thing.rot ?? Rot4.North, thing.stuffDef);
                        }
                        //jc//jcLog.Message($"[CustomShipStart] Added extra thing {thing.thingDef.defName} at {pos}");
                    }
                }

                //jc//jcLog.Message("[CustomShipStart] Finished generating sketch, merging into parms.sketch.");
                parms.sketch.Merge(sketch);
            }
        }
    }
}
