using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Globalization;
using RimWorld;
using Verse;

namespace GravshipExport
{
    static class ThingSettingsExportHelper
    {
        /// <summary>
        /// Snapshot all whitelisted comp settings on this thing.
        /// </summary>
        public static List<CompSettingsSnapshot> SnapshotThingComps(Thing thing)
        {
            if (thing == null)
            {
                GravshipLogger.Warning("SnapshotThingComps called with null Thing.");
                return null;
            }

            ThingWithComps twc = thing as ThingWithComps;
            if (twc == null)
            {
                GravshipLogger.Message(
                    string.Format("SnapshotThingComps: Thing {0} is not ThingWithComps; skipping.",
                        DescribeThing(thing)));
                return null;
            }

            if (twc.AllComps == null || twc.AllComps.Count == 0)
            {
                GravshipLogger.Message(
                    string.Format("SnapshotThingComps: Thing {0} has no comps; nothing to snapshot.",
                        DescribeThing(thing)));
                return null;
            }

            GravshipLogger.Message(
                string.Format("SnapshotThingComps: capturing settings for {0} comps on {1}.",
                    twc.AllComps.Count, DescribeThing(thing)));

            List<CompSettingsSnapshot> result = new List<CompSettingsSnapshot>();

            foreach (ThingComp comp in twc.AllComps)
            {
                if (comp == null) continue;

                CompSettingsSnapshot snap = SnapshotComp(comp);
                if (snap != null)
                {
                    result.Add(snap);
                    GravshipLogger.Message(
                        string.Format("SnapshotThingComps: captured {0} values from comp {1} on {2}.",
                            snap.keys != null ? snap.keys.Count : 0,
                            snap.compClassName ?? comp.GetType().FullName,
                            DescribeThing(thing)));
                }
            }

            if (result.Count == 0)
            {
                GravshipLogger.Message(
                    string.Format("SnapshotThingComps: no whitelisted comp settings captured for {0}.",
                        DescribeThing(thing)));
                return null;
            }

            return result;
        }

        /// <summary>
        /// Snapshot comp settings for selected comp types (generic across defs).
        /// </summary>
        private static CompSettingsSnapshot SnapshotComp(ThingComp comp)
        {
            Type compType = comp.GetType();
            string fullName = compType.FullName;
            if (fullName == null) return null;

            // Whitelist: temperature, power, refuelable, flickable
            if (fullName != typeof(RimWorld.CompTempControl).FullName &&
                fullName != typeof(RimWorld.CompPowerTrader).FullName &&
                fullName != typeof(RimWorld.CompRefuelable).FullName &&
                fullName != typeof(RimWorld.CompFlickable).FullName)
            {
                return null;
            }

            CompSettingsSnapshot snapshot = new CompSettingsSnapshot();
            snapshot.compClassName = fullName;
            snapshot.keys = new List<string>();
            snapshot.values = new List<string>();
            snapshot.valueTypeNames = new List<string>();

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            // Fields
            FieldInfo[] fields = compType.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (!IsSupportedPrimitive(f.FieldType)) continue;

                object v = f.GetValue(comp);
                if (v == null) continue;

                snapshot.keys.Add(f.Name);
                snapshot.values.Add(Convert.ToString(v, CultureInfo.InvariantCulture));
                snapshot.valueTypeNames.Add(f.FieldType.AssemblyQualifiedName);
            }

            // Properties with public getter/setter
            PropertyInfo[] props = compType.GetProperties(flags);
            for (int i = 0; i < props.Length; i++)
            {
                PropertyInfo p = props[i];
                if (!p.CanRead || !p.CanWrite) continue;
                if (!IsSupportedPrimitive(p.PropertyType)) continue;

                object v = null;
                try
                {
                    v = p.GetValue(comp, null);
                }
                catch (Exception ex)
                {
                    GravshipLogger.Warning(
                        string.Format("SnapshotComp: failed to read property {0} on {1}: {2}",
                            p.Name, fullName, ex.Message));
                    continue;
                }

                if (v == null) continue;

                snapshot.keys.Add(p.Name);
                snapshot.values.Add(Convert.ToString(v, CultureInfo.InvariantCulture));
                snapshot.valueTypeNames.Add(p.PropertyType.AssemblyQualifiedName);
            }

            if (snapshot.keys.Count == 0)
            {
                GravshipLogger.Message(
                    string.Format("SnapshotComp: no supported primitive fields/properties found on comp {0}.",
                        fullName));
                return null;
            }

            GravshipLogger.Message(
                string.Format("SnapshotComp: captured {0} values from comp {1} on {2}.",
                    snapshot.keys.Count, fullName, DescribeThing(comp.parent)));

            return snapshot;
        }

        /// <summary>
        /// Snapshot StorageSettings for anything implementing IStoreSettingsParent (shelves, stockpiles, etc).
        /// Returns a deep copy that can be serialized into ShipThingEntry.exportedStorageSettings.
        /// </summary>
        public static StorageSettingsSnapshot SnapshotStorageSettings(Thing thing)
        {
            IStoreSettingsParent parent = thing as IStoreSettingsParent;
            if (parent == null)
                return null;

            StorageSettings settings = parent.GetStoreSettings() ?? parent.GetParentStoreSettings();
            if (settings == null)
                return null;

            ThingFilter filter = settings.filter;

            var snap = new StorageSettingsSnapshot
            {
                priority = settings.Priority.ToString(),
                allowedThingDefs = new List<string>(),
                allowedSpecialFilters = new List<string>(),
                allowedCategories = new List<string>(),
                allowedStuffCategories = new List<string>(),
                hitpointsRange = filter.AllowedHitPointsPercents,
                mentalBreakRange = filter.AllowedMentalBreakChance,
                qualityRange = filter.AllowedQualityLevels
            };

            //
            // 1) Allowed ThingDefs (direct)
            //
            foreach (ThingDef td in DefDatabase<ThingDef>.AllDefs)
            {
                if (filter.Allows(td))
                    snap.allowedThingDefs.Add(td.defName);
            }

            //
            // 2) Allowed Special Filters (direct)
            //
            snap.specialFilterStates = new Dictionary<string, bool>();

            foreach (SpecialThingFilterDef sf in DefDatabase<SpecialThingFilterDef>.AllDefs)
            {
                bool allowed = filter.Allows(sf);
                snap.specialFilterStates[sf.defName] = allowed;
            }

            //
            // 3) Allowed Thing Categories (indirect, RW API limitation)
            //
            foreach (ThingCategoryDef cat in DefDatabase<ThingCategoryDef>.AllDefs)
            {
                bool anyAllowed = false;

                // A category is allowed if ANY of its defs are allowed
                foreach (ThingDef def in cat.childThingDefs)
                {
                    if (filter.Allows(def))
                    {
                        anyAllowed = true;
                        break;
                    }
                }

                if (anyAllowed)
                    snap.allowedCategories.Add(cat.defName);
            }

            //
            // 4) Allowed Stuff Categories (indirect, via defs using the category)
            //
            foreach (StuffCategoryDef sc in DefDatabase<StuffCategoryDef>.AllDefs)
            {
                bool anyAllowed = false;

                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
                {
                    if (def.stuffCategories != null && def.stuffCategories.Contains(sc))
                    {
                        if (filter.Allows(def))
                        {
                            anyAllowed = true;
                            break;
                        }
                    }
                }

                if (anyAllowed)
                    snap.allowedStuffCategories.Add(sc.defName);
            }

            return snap;
        }




        /// <summary>
        /// Read what plant is configured to grow (for hydroponics, grow lamps, modded grow buildings, etc).
        /// Uses IPlantToGrowSettable so it should work for modded buildings as long as they implement the interface.
        /// </summary>
        public static ThingDef GetPlantToGrowDef(Thing thing)
        {
            if (thing == null)
                return null;

            IPlantToGrowSettable settable = thing as IPlantToGrowSettable;
            if (settable == null)
                return null;

            try
            {
                ThingDef def = settable.GetPlantDefToGrow();
                if (def != null)
                {
                    GravshipLogger.Message(
                        string.Format("ThingSettingsExportHelper: Plant-to-grow on {0} is {1}.",
                            DescribeThing(thing), def.defName));
                }
                return def;
            }
            catch (Exception ex)
            {
                GravshipLogger.Warning(
                    string.Format("ThingSettingsExportHelper: Failed to read plant to grow on {0}: {1}",
                        DescribeThing(thing), ex.Message));
                return null;
            }
        }

        private static bool IsSupportedPrimitive(Type t)
        {
            if (t == typeof(int) || t == typeof(float) || t == typeof(double) ||
                t == typeof(bool) || t == typeof(string))
                return true;

            if (t.IsEnum) return true;

            return false;
        }

        private static string DescribeThing(Thing thing)
        {
            if (thing == null) return "null";

            try
            {
                string defName = (thing.def != null) ? thing.def.defName : "no-def";
                string label = null;

                try
                {
                    label = thing.LabelCap != null ? thing.LabelCap.ToString() : null;
                }
                catch
                {
                    // ignore
                }

                if (!string.IsNullOrEmpty(label))
                {
                    return string.Format("{0}({1})", label, defName);
                }

                return defName;
            }
            catch
            {
                return thing.ToString();
            }
        }
    }
}
