using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;
using Hjg.Pngcs;
using Hjg.Pngcs.Chunks;


namespace GravshipExport
{
    internal static class ShipPreviewUtility
    {
        // ✅ Toggle this to enable/disable all debug logging
        private const bool DebugLogs = false;

        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D GetPreviewFor(ShipListItem item)
        {
            if (item == null) return null;

            string key = (item.Ship != null && !string.IsNullOrEmpty(item.Ship.defName))
                ? item.Ship.defName
                : (item.ExportFilename ?? "unknown");

            if (cache.TryGetValue(key, out Texture2D cached))
                return cached;

            if (DebugLogs) Log.Message($"[GravshipExport] Attempting to load preview for: {key}");

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
                if (DebugLogs) Log.Message($"[GravshipExport] Checking mod preview at: {modPreviewPath}");

                if (File.Exists(modPreviewPath))
                {
                    string texPath = "Previews/" + item.Ship.defName.ToLowerInvariant().Replace(" ", "_");
                    if (DebugLogs) Log.Message("[GravshipExport] Trying fallback preview path: " + texPath);

                    Texture2D tex = ContentFinder<Texture2D>.Get(texPath, false);
                    if (tex != null)
                    {
                        if (DebugLogs) Log.Message("[GravshipExport] Loaded preview from mod Textures/Previews/");
                        return tex;
                    }
                    else if (DebugLogs)
                    {
                        Log.Warning("[GravshipExport] Failed to decode preview texture from mod folder.");
                    }
                }
                else if (DebugLogs)
                {
                    Log.Message("[GravshipExport] No mod preview found at that path.");
                }
            }

            // 2) Try our mod-bundled fallback: Textures/Previews/<defName>.png
            if (item.Ship != null && !string.IsNullOrEmpty(item.Ship.defName))
            {
                string texPath = "Previews/" + item.Ship.defName.ToLowerInvariant().Replace(" ", "_");
                if (DebugLogs) Log.Message("[GravshipExport] Trying fallback preview path: " + texPath);

                Texture2D t = ContentFinder<Texture2D>.Get(texPath, false);
                if (t != null) return t;

                if (DebugLogs) Log.Message("[GravshipExport] No preview found at fallback path.");
            }

            // 3) Try to load directly from the user export folder (Config/GravshipExport)
            string configPreviewPath = Path.Combine(
                GenFilePaths.ConfigFolderPath,
                "GravshipExport",
                $"{item.Ship?.defName.ToLowerInvariant().Replace(" ", "_")}.png"
            );

            if (File.Exists(configPreviewPath))
            {
                if (DebugLogs) Log.Message($"[GravshipExport] 🖼 Attempting to decode PNG from config: {configPreviewPath}");

                try
                {
                    PngReader png = FileHelper.CreatePngReader(configPreviewPath);
                    var info = png.ImgInfo;

                    if (DebugLogs)
                        Log.Message($"[GravshipExport] PNG info: {info.Cols}x{info.Rows}, channels={info.Channels}, bitDepth={info.BitDepth}");

                    Texture2D tex = new Texture2D(info.Cols, info.Rows, TextureFormat.RGBA32, false);
                    Color32[] pixels = new Color32[info.Cols * info.Rows];

                    for (int row = 0; row < info.Rows; row++)
                    {
                        ImageLine line = png.ReadRowByte(row);
                        byte[] scanline;

                        if (line != null)
                        {
                            scanline = line.ScanlineB;
                        }
                        else
                        {
                            // Fallback: read as int and convert manually
                            var intLine = png.ReadRowInt(row);
                            if (intLine == null)
                                throw new InvalidOperationException($"PNG decode failed: row {row} returned null scanline.");

                            scanline = new byte[intLine.Scanline.Length];
                            for (int i = 0; i < intLine.Scanline.Length; i++)
                                scanline[i] = (byte)Mathf.Clamp(intLine.Scanline[i], 0, 255);
                        }

                        for (int col = 0; col < info.Cols; col++)
                        {
                            int idx = (info.Rows - 1 - row) * info.Cols + col; // flip vertically

                            if (info.Channels >= 4)
                            {
                                pixels[idx] = new Color32(
                                    scanline[col * 4],
                                    scanline[col * 4 + 1],
                                    scanline[col * 4 + 2],
                                    scanline[col * 4 + 3]
                                );
                            }
                            else if (info.Channels == 3)
                            {
                                pixels[idx] = new Color32(
                                    scanline[col * 3],
                                    scanline[col * 3 + 1],
                                    scanline[col * 3 + 2],
                                    255
                                );
                            }
                            else if (info.Channels == 2)
                            {
                                byte v = scanline[col * 2];
                                byte a = scanline[col * 2 + 1];
                                pixels[idx] = new Color32(v, v, v, a);
                            }
                            else
                            {
                                byte v = scanline[col];
                                pixels[idx] = new Color32(v, v, v, 255);
                            }
                        }
                    }


                    png.End();

                    tex.SetPixels32(pixels);
                    tex.Apply();

                    if (DebugLogs) Log.Message("[GravshipExport] ✅ Successfully decoded PNG with Pngcs.");
                    return tex;
                }
                catch (Exception ex)
                {
                    Log.Error($"[GravshipExport] ❌ Failed to decode PNG with Pngcs: {ex}");
                }
            }

            if (DebugLogs) Log.Warning("[GravshipExport] ❌ No preview image found for any source.");
            return null;
        }
    }
}
