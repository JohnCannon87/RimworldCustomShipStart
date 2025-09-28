using HarmonyLib;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace GravshipExport
{
    [HarmonyPatch(typeof(Building_GravEngine))]
    [HarmonyPatch("GetGizmos")]
    public static class Patch_GravEngine_GetGizmos
    {
        private static readonly Texture2D SaveTex = TexButton.Save;

        public static void Postfix(Building_GravEngine __instance, ref IEnumerable<Gizmo> __result)
        {
            var list = new List<Gizmo>(__result);

            list.Add(new Command_Action
            {
                defaultLabel = "Export Gravship Layout",
                defaultDesc = "Save this ship as an XML layout.",
                icon = SaveTex,
                action = () =>
                {
                    // ✅ Grab the actual ship name from the engine
                    string currentShipName = __instance.RenamableLabel;

                    // ✅ Pass it into the name dialog as the initial value
                    Find.WindowStack.Add(new Dialog_NameShip(__instance, currentShipName));
                }
            });

            __result = list;
        }
    }
}
