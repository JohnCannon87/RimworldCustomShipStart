using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace GravshipExport
{
    internal static class ShipPreviewUtility
    {
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D GetPreviewFor(ShipListItem item)
        {
            if (item == null) return null;

            string key = (item.Ship != null && !string.IsNullOrEmpty(item.Ship.defName))
                ? item.Ship.defName
                : (item.ExportFilename ?? "unknown");

            if (cache.TryGetValue(key, out Texture2D cached))
                return cached;

            Log.Message($"[GravshipExport] Attempting to load preview for: {key}");

            Texture2D tex = TryLoadPreview(item);
            cache[key] = tex;
            return tex;
        }

        private static Texture2D TryLoadPreview(ShipListItem item)
        {
            // 1) Check mod Textures/Previews/
            if (item.Ship?.modContentPack != null)
            {
                string modPreviewPath = Path.Combine(item.Ship.modContentPack.RootDir, "Textures", "Previews", $"{item.Ship.defName}.png");
                Log.Message($"[GravshipExport] Checking mod preview at: {modPreviewPath}");
                if (File.Exists(modPreviewPath))
                {
                    string texPath = "Previews/" + item.Ship.defName.ToLowerInvariant().Replace(" ", "_");
                    Log.Message("[GravshipExport] Trying fallback preview path: " + texPath);
                    Texture2D tex = ContentFinder<Texture2D>.Get(texPath, false);
                    if (tex != null)
                    {
                        Log.Message("[GravshipExport] Loaded preview from mod Textures/Previews/");
                        return tex;
                    }
                    else
                    {
                        Log.Warning("[GravshipExport] Failed to decode preview texture from mod folder.");
                    }
                }
                else
                {
                    Log.Message("[GravshipExport] No mod preview found at that path.");
                }
            }

            // 2) Try our mod-bundled fallback: Textures/Previews/<defName>.png
            if (item.Ship != null && !string.IsNullOrEmpty(item.Ship.defName))
            {
                string texPath = "Previews/" + item.Ship.defName.ToLowerInvariant().Replace(" ", "_");
                Log.Message("[GravshipExport] Trying fallback preview path: " + texPath);
                Texture2D t = ContentFinder<Texture2D>.Get(texPath, false);
                if (t != null) return t;
                Log.Message("[GravshipExport] No preview found at fallback path.");
            }


            Log.Warning("[GravshipExport] ❌ No preview image found for any source.");
            return null;
        }

    }
}
