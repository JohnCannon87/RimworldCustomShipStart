using RimWorld;
using System.Collections.Generic;
using Verse;

namespace GravshipExport
{
    public class StorageSettingsSnapshot : IExposable
    {
        public string priority;

        public List<string> allowedThingDefs;
        public List<string> allowedCategories;
        public List<string> allowedStuffCategories;

        public FloatRange? hitpointsRange;
        public FloatRange? mentalBreakRange;
        public QualityRange? qualityRange;
        public Dictionary<string, bool> specialFilterStates;

        public void ExposeData()
        {
            Scribe_Values.Look(ref priority, "priority");

            Scribe_Collections.Look(ref allowedThingDefs, "allowedThingDefs", LookMode.Value);
            Scribe_Collections.Look(ref allowedCategories, "allowedCategories", LookMode.Value);
            Scribe_Collections.Look(ref allowedStuffCategories, "allowedStuffCategories", LookMode.Value);

            Scribe_Values.Look(ref hitpointsRange, "hitpointsRange");
            Scribe_Values.Look(ref mentalBreakRange, "mentalBreakRange");
            Scribe_Values.Look(ref qualityRange, "qualityRange");
            Scribe_Collections.Look(ref specialFilterStates, "specialFilterStates", LookMode.Value, LookMode.Value);

        }
    }
}
