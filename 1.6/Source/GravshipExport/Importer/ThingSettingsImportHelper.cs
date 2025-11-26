using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using RimWorld;
using Verse;

namespace GravshipExport
{
    public static class ThingSettingsImportHelper
    {
        public static void ApplyLayoutThingSettings(Map map, ShipLayoutDefV2 layout, Rot4 rotation, IntVec3 offset)
        {
            if (map == null)
            {
                GravshipLogger.Warning("ThingSettingsImportHelper.ApplyLayoutThingSettings: map is null; aborting.");
                return;
            }

            if (layout == null || layout.rows == null)
            {
                GravshipLogger.Warning("ThingSettingsImportHelper.ApplyLayoutThingSettings: layout or rows null; aborting.");
                return;
            }

            GravshipLogger.Message(
                string.Format("ThingSettingsImportHelper: Applying settings for layout '{0}' with {1} rows.",
                    layout.defName ?? "null",
                    layout.rows.Count));

            int entriesWithSettings = 0;
            int entriesMatched = 0;
            int storageApplied = 0;
            int compSnapshotsApplied = 0;
            int plantsApplied = 0;

            for (int z = 0; z < layout.rows.Count; z++)
            {
                List<ShipCell> row = layout.rows[z];
                if (row == null)
                    continue;

                for (int x = 0; x < row.Count; x++)
                {
                    ShipCell cell = row[x];
                    if (cell == null || cell.things == null || cell.things.Count == 0)
                        continue;

                    IntVec3 local = new IntVec3(x, 0, z);
                    IntVec3 world = local.RotatedBy(rotation) + offset;

                    if (!world.InBounds(map))
                        continue;

                    List<Thing> thingsHere = world.GetThingList(map);
                    if (thingsHere == null || thingsHere.Count == 0)
                        continue;

                    for (int i = 0; i < cell.things.Count; i++)
                    {
                        ShipThingEntry entry = cell.things[i];
                        if (entry == null)
                            continue;

                        bool hasStorage = entry.exportedStorageSettings != null;
                        bool hasComps = entry.compSettings != null && entry.compSettings.Count > 0;
                        bool hasPlant = entry.plantToGrowDef != null;

                        if (!hasStorage && !hasComps && !hasPlant)
                            continue;

                        entriesWithSettings++;

                        Thing match = FindMatchingThing(thingsHere, entry, rotation);
                        if (match == null)
                        {
                            GravshipLogger.Message(
                                string.Format("ThingSettingsImportHelper: No matching Thing found at {0} for entry {1}.",
                                    world, entry));
                            continue;
                        }

                        bool appliedStorageThisThing;
                        int appliedCompSnapshotsThisThing;
                        bool appliedPlantThisThing;

                        ApplyEntrySettingsToThing(
                            entry,
                            match,
                            out appliedStorageThisThing,
                            out appliedCompSnapshotsThisThing,
                            out appliedPlantThisThing);

                        if (appliedStorageThisThing || appliedCompSnapshotsThisThing > 0 || appliedPlantThisThing)
                        {
                            entriesMatched++;
                            if (appliedStorageThisThing) storageApplied++;
                            compSnapshotsApplied += appliedCompSnapshotsThisThing;
                            if (appliedPlantThisThing) plantsApplied++;

                            GravshipLogger.Message(
                                string.Format(
                                    "ThingSettingsImportHelper: Applied settings to {0} at {1} (def={2}, rot={3}, stuff={4}).",
                                    DescribeThing(match),
                                    world,
                                    match.def != null ? match.def.defName : "null",
                                    match.Rotation.AsInt,
                                    match.Stuff != null ? match.Stuff.defName : "none"));
                        }
                    }
                }
            }

            GravshipLogger.Message(
                string.Format(
                    "ThingSettingsImportHelper: Finished applying layout thing settings. entriesWithSettings={0}, matchedEntries={1}, storageApplied={2}, compSnapshotsApplied={3}, plantsApplied={4}.",
                    entriesWithSettings, entriesMatched, storageApplied, compSnapshotsApplied, plantsApplied));
        }

        private static Thing FindMatchingThing(List<Thing> candidates, ShipThingEntry entry, Rot4 layoutToWorldRot)
        {
            if (candidates == null || entry == null || string.IsNullOrEmpty(entry.defName))
                return null;

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(entry.defName);
            if (def == null)
            {
                GravshipLogger.Warning(
                    string.Format("ThingSettingsImportHelper: Could not resolve ThingDef '{0}' for entry {1}.",
                        entry.defName, entry));
                return null;
            }

            Rot4 entryRot = new Rot4(entry.rotInteger);
            int combined = (entryRot.AsInt + layoutToWorldRot.AsInt) & 3;
            Rot4 expectedWorldRot = new Rot4(combined);

            for (int i = 0; i < candidates.Count; i++)
            {
                Thing t = candidates[i];
                if (t == null || t.def == null)
                    continue;

                if (t.def != def)
                    continue;

                if (!string.IsNullOrEmpty(entry.stuffDef))
                {
                    string stuffName = (t.def.MadeFromStuff && t.Stuff != null)
                        ? t.Stuff.defName
                        : null;

                    if (stuffName != entry.stuffDef)
                        continue;
                }

                if (t.def.rotatable && t.Rotation != expectedWorldRot)
                    continue;

                return t;
            }

            return null;
        }

        private static void ApplyEntrySettingsToThing(
            ShipThingEntry entry,
            Thing thing,
            out bool appliedStorage,
            out int appliedCompSnapshots,
            out bool appliedPlant)
        {
            appliedStorage = false;
            appliedCompSnapshots = 0;
            appliedPlant = false;

            if (entry == null || thing == null)
                return;

            // 1) Storage settings (snapshot version)
            if (entry.exportedStorageSettings != null)
            {
                IStoreSettingsParent storageParent = thing as IStoreSettingsParent;
                if (storageParent != null)
                {
                    ApplyStorageSettings(entry.exportedStorageSettings, storageParent, thing, out appliedStorage);
                }
            }


            // 2) Component settings
            if (entry.compSettings != null && entry.compSettings.Count > 0)
            {
                ThingWithComps twc = thing as ThingWithComps;
                if (twc != null && twc.AllComps != null && twc.AllComps.Count > 0)
                {
                    for (int i = 0; i < entry.compSettings.Count; i++)
                    {
                        CompSettingsSnapshot snap = entry.compSettings[i];
                        if (snap == null || string.IsNullOrEmpty(snap.compClassName))
                            continue;

                        ThingComp comp = FindCompByClassName(twc, snap.compClassName);
                        if (comp == null)
                        {
                            GravshipLogger.Message(
                                string.Format("ThingSettingsImportHelper: No comp {0} found on {1}.",
                                    snap.compClassName, DescribeThing(thing)));
                            continue;
                        }

                        bool appliedThisComp = ApplySnapshotToComp(comp, snap, thing);
                        if (appliedThisComp)
                            appliedCompSnapshots++;
                    }
                }
            }

            // 3) Plant settings (hydroponics, grow lamps, modded growers)
            if (!string.IsNullOrEmpty(entry.plantToGrowDef))
            {
                IPlantToGrowSettable settable = thing as IPlantToGrowSettable;
                if (settable != null)
                {
                    ThingDef plantDef = DefDatabase<ThingDef>.GetNamedSilentFail(entry.plantToGrowDef);

                    if (plantDef == null)
                    {
                        GravshipLogger.Warning(
                            $"ThingSettingsImportHelper: plantToGrowDef '{entry.plantToGrowDef}' not found in ThingDef database for {DescribeThing(thing)}.");
                    }
                    else
                    {
                        try
                        {
                            settable.SetPlantDefToGrow(plantDef);
                            appliedPlant = true;

                            GravshipLogger.Message(
                                $"ThingSettingsImportHelper: Set plant '{plantDef.defName}' on {DescribeThing(thing)}.");
                        }
                        catch (Exception ex)
                        {
                            GravshipLogger.Warning(
                                $"ThingSettingsImportHelper: Failed to set plant '{plantDef.defName}' on {DescribeThing(thing)}: {ex.Message}");
                        }
                    }
                }
            }

        }

        private static ThingComp FindCompByClassName(ThingWithComps twc, string className)
        {
            for (int i = 0; i < twc.AllComps.Count; i++)
            {
                ThingComp comp = twc.AllComps[i];
                if (comp == null)
                    continue;

                Type type = comp.GetType();
                if (type != null && type.FullName == className)
                    return comp;
            }

            return null;
        }

        private static bool ApplySnapshotToComp(ThingComp comp, CompSettingsSnapshot snap, Thing parent)
        {
            if (snap.keys == null || snap.values == null || snap.valueTypeNames == null)
                return false;

            int count = snap.keys.Count;
            if (count != snap.values.Count || count != snap.valueTypeNames.Count)
            {
                GravshipLogger.Warning(
                    string.Format("ThingSettingsImportHelper: Snapshot size mismatch for comp {0} on {1}.",
                        snap.compClassName ?? "null", DescribeThing(parent)));


                return false;
            }

            Type compType = comp.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            bool anyApplied = false;

            for (int i = 0; i < count; i++)
            {
                string key = snap.keys[i];
                string valueString = snap.values[i];
                string typeName = snap.valueTypeNames[i];

                if (key == null || valueString == null || typeName == null)
                    continue;

                Type valueType = GetTypeByName(typeName);
                if (valueType == null || !IsSupportedPrimitive(valueType))
                    continue;

                object parsed = ParseValue(valueString, valueType);
                if (parsed == null)
                    continue;

                try
                {
                    FieldInfo f = compType.GetField(key, flags);
                    if (f != null && IsSupportedPrimitive(f.FieldType))
                    {
                        f.SetValue(comp, parsed);
                        anyApplied = true;
                        continue;
                    }

                    PropertyInfo p = compType.GetProperty(key, flags);
                    if (p != null && p.CanWrite && IsSupportedPrimitive(p.PropertyType))
                    {
                        p.SetValue(comp, parsed, null);
                        anyApplied = true;
                    }
                }
                catch (Exception ex)
                {
                    GravshipLogger.Warning(
                        string.Format("ThingSettingsImportHelper: Failed to apply {0} on comp {1} ({2}) of {3}: {4}",
                            key, snap.compClassName ?? "null", compType.FullName,
                            DescribeThing(parent), ex.Message));
                }
            }

            if (anyApplied)
            {
                GravshipLogger.Message(
                    string.Format("ThingSettingsImportHelper: Applied snapshot to comp {0} on {1}.",
                        snap.compClassName ?? compType.FullName, DescribeThing(parent)));
            }

            return anyApplied;
        }

        private static Type GetTypeByName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            Type t = Type.GetType(typeName);
            if (t != null)
                return t;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    t = assemblies[i].GetType(typeName);
                    if (t != null)
                        return t;
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }

        private static object ParseValue(string value, Type t)
        {
            try
            {
                if (t == typeof(string)) return value;
                if (t == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
                if (t == typeof(float)) return float.Parse(value, CultureInfo.InvariantCulture);
                if (t == typeof(double)) return double.Parse(value, CultureInfo.InvariantCulture);
                if (t == typeof(bool)) return bool.Parse(value);
                if (t.IsEnum) return Enum.Parse(t, value);

                return null;
            }
            catch
            {
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

        private static void ApplyStorageSettings(StorageSettingsSnapshot snap, IStoreSettingsParent parent, Thing thing, out bool appliedStorage)
        {
            appliedStorage = false;

            StorageSettings current = parent.GetStoreSettings() ?? parent.GetParentStoreSettings();
            if (current == null)
                return;

            try
            {
                ThingFilter filter = current.filter;

                //
                // 1) Priority
                //
                if (!string.IsNullOrEmpty(snap.priority))
                {
                    if (Enum.TryParse<StoragePriority>(snap.priority, out var pr))
                        current.Priority = pr;
                }

                //
                // 2) Reset filter first (so we don’t merge old settings accidentally)
                //
                filter.SetDisallowAll();

                //
                // 3) Allowed ThingDefs
                //
                if (snap.allowedThingDefs != null)
                {
                    foreach (string defName in snap.allowedThingDefs)
                    {
                        ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                        if (def != null)
                            filter.SetAllow(def, true);
                    }
                }

                //
                // 4) Allowed Categories (indirect)
                //
                if (snap.allowedCategories != null)
                {
                    foreach (string catName in snap.allowedCategories)
                    {
                        ThingCategoryDef cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(catName);
                        if (cat == null)
                            continue;

                        if (cat.childThingDefs != null)
                        {
                            foreach (ThingDef def in cat.childThingDefs)
                                filter.SetAllow(def, true);
                        }
                    }
                }

                //
                // 5) Allowed Stuff Categories (indirect)
                //
                if (snap.allowedStuffCategories != null)
                {
                    foreach (string scName in snap.allowedStuffCategories)
                    {
                        StuffCategoryDef sc = DefDatabase<StuffCategoryDef>.GetNamedSilentFail(scName);
                        if (sc == null)
                            continue;

                        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
                        {
                            if (def.stuffCategories != null && def.stuffCategories.Contains(sc))
                                filter.SetAllow(def, true);
                        }
                    }
                }

                //
                // 6) Allowed Special Filters
                //
                if (snap.specialFilterStates != null)
                {
                    foreach (var kv in snap.specialFilterStates)
                    {
                        string defName = kv.Key;
                        bool value = kv.Value;

                        var sf = DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail(defName);
                        if (sf != null)
                        {
                            filter.SetAllow(sf, value);
                        }
                    }
                }


                //
                // 7) Hitpoints %
                //
                if (snap.hitpointsRange.HasValue)
                    filter.AllowedHitPointsPercents = snap.hitpointsRange.Value;

                //
                // 8) Mental Break %
                //
                if (snap.mentalBreakRange.HasValue)
                    filter.AllowedMentalBreakChance = snap.mentalBreakRange.Value;

                //
                // 9) Quality range
                //
                if (snap.qualityRange.HasValue)
                    filter.AllowedQualityLevels = snap.qualityRange.Value;

                appliedStorage = true;

                GravshipLogger.Message(
                    $"ThingSettingsImportHelper: Applied StorageSettings to {DescribeThing(thing)}.");
            }
            catch (Exception ex)
            {
                GravshipLogger.Error(
                    $"ThingSettingsImportHelper: Failed to apply StorageSettings to {DescribeThing(thing)}: {ex}");
            }
        }

    }
}
