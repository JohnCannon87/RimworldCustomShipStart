using HarmonyLib;
using Verse;

namespace RimworldCustomShipStart
{
    // Ensures PatchAll runs when the game loads the assembly
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("com.arcjc007.customshipstart");
            harmony.PatchAll();
        }
    }
}
