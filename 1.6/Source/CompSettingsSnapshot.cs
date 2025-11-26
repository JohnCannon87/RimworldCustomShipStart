using System;
using System.Collections.Generic;
using Verse;

namespace GravshipExport
{
    public class CompSettingsSnapshot : IExposable
    {
        public string compClassName;        // e.g. "RimWorld.CompTempControl"
        public List<string> keys;          // field or property name
        public List<string> values;        // serialized value as string
        public List<string> valueTypeNames; // type full name for parsing

        public void ExposeData()
        {
            Scribe_Values.Look(ref compClassName, "compClassName");
            Scribe_Collections.Look(ref keys, "keys", LookMode.Value);
            Scribe_Collections.Look(ref values, "values", LookMode.Value);
            Scribe_Collections.Look(ref valueTypeNames, "valueTypeNames", LookMode.Value);
        }
    }
}
