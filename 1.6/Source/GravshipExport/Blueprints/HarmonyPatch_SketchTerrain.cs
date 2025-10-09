using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipExport.GravshipExport.Blueprints
{
    [HarmonyPatch(typeof(SketchTerrain), nameof(SketchTerrain.DrawGhost))]
    static class HarmonyPatch_SketchTerrain
    {
        static bool Prefix(SketchTerrain __instance, IntVec3 at, Color color)
        {
            var def = __instance.def;
            var bp = def?.blueprintDef;

            if (def == null || bp == null || bp.graphic == null)
            {
                var mat = def?.graphic?.MatSingle ?? BaseContent.ClearMat;
                // Use the alpha channel of the color for transparency
                float alpha = color.a <= 0f ? 0.35f : color.a;
                var faded = FadedMaterialPool.FadedVersionOf(mat, alpha);
                Graphics.DrawMesh(
                    MeshPool.plane10,
                    Matrix4x4.TRS(at.ToVector3ShiftedWithAltitude(AltitudeLayer.Blueprint), Quaternion.identity, Vector3.one),
                    faded,
                    0
                );
                return false; // skip original (prevents NRE)
            }

            // let vanilla run if everything is valid
            return true;
        }
    }

}
