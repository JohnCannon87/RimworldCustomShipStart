using GravshipExport;
using HarmonyLib;
using RimWorld;
using Verse;

[HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.PlaceBlueprintForBuild))]
public static class DebugPatch_PlaceBlueprintForBuild
{
    public static void Prefix(BuildableDef sourceDef)
    {
        GravshipLogger.Message($"[Blueprint Trace] PlaceBlueprintForBuild called for {sourceDef?.defName ?? "null"} ({sourceDef?.GetType().Name})");
    }
}
