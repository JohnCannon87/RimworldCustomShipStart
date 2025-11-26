using RimWorld;
using Verse;
using System.Collections.Generic;

namespace GravshipExport
{
    public class ShipThingEntry : IExposable
    {
        public string defName;
        public string stuffDef;
        public int rotInteger;
        public string quality;
        public string plantToGrowDef;

        // Generic-ish settings:

        // 1) Storage settings – store whole StorageSettings snapshot
        public StorageSettingsSnapshot exportedStorageSettings;

        // 2) Simple comp values stored reflectively (optional)
        public List<CompSettingsSnapshot> compSettings; // see below

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref stuffDef, "stuffDef");
            Scribe_Values.Look(ref rotInteger, "rotInteger");
            Scribe_Values.Look(ref quality, "quality");
            Scribe_Values.Look(ref plantToGrowDef, "plantToGrowDef");

            Scribe_Deep.Look(ref exportedStorageSettings, "exportedStorageSettings");

            Scribe_Collections.Look(ref compSettings, "compSettings", LookMode.Deep);
        }

        public override string ToString()
        {
            return $"{defName ?? "unknown"}" +
                   (stuffDef != null ? $":{stuffDef}" : "") +
                   $"(rot={rotInteger})";
        }

    }

}
