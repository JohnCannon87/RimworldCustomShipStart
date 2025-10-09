using System;
using UnityEngine;
using Verse;

namespace GravshipExport.Blueprints
{
    // add this utility (same namespace as your builder)
    static class TerrainGraphicFixer
    {
        public static void EnsureRenderable(TerrainDef def)
        {
            if (def == null) return;
            if (def.graphic != null) return;

            // Use Terrain shader (what floors use)
            var tex = string.IsNullOrEmpty(def.texturePath)
                ? "Terrain/Surfaces/Concrete" // safe fallback
                : def.texturePath;

            try
            {
                def.graphic = GraphicDatabase.Get<Graphic_Terrain>(
                    tex,
                    ShaderDatabase.TerrainEdge,
                    Vector2.one,
                    Color.white
                );
                GravshipLogger.Message($"[Fixer] Initialized terrain graphic for '{def.defName}' (tex={tex}).");
            }
            catch (Exception ex)
            {
                GravshipLogger.Warning($"[Fixer] Failed to init graphic for '{def?.defName ?? "null"}': {ex.Message}");
            }
        }
    }

}
