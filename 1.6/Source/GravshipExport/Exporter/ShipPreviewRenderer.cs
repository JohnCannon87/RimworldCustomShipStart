using System;
using System.IO;
using UnityEngine;
using Verse;
using RimWorld;
using Hjg.Pngcs;

namespace GravshipExport
{
    internal static class ShipPreviewRenderer
    {
        /// <summary>
        /// Convenience entry point: saves to Config/GravshipExport/&lt;shipName&gt;.png
        /// </summary>
        public static void Capture(Building_GravEngine engine, ShipLayoutDefV2 layout, string shipName)
        {
            if (string.IsNullOrWhiteSpace(shipName)) shipName = "ShipPreview";
            string cfgDir = Path.Combine(GenFilePaths.ConfigFolderPath, "GravshipExport");
            if (!Directory.Exists(cfgDir)) Directory.CreateDirectory(cfgDir);
            string outPath = Path.Combine(cfgDir, shipName + ".png");
            Capture(engine, layout, outPath, marginCells: 2, longestSidePixels: 1536, maxTextureSize: 4096);
        }

        /// <summary>
        /// Full control entry point.
        /// </summary>
        /// <param name="engine">Placed grav engine (ship is already spawned).</param>
        /// <param name="layout">Ship layout (width/height + gravEngineX/Z).</param>
        /// <param name="absoluteOutputPath">Absolute PNG path to write.</param>
        /// <param name="marginCells">Padding (cells) around ship bounds (default 2).</param>
        /// <param name="longestSidePixels">Target pixels for the longer ship side (default 1536).</param>
        /// <param name="maxTextureSize">Safety clamp for RT/Texture dimensions (default 4096).</param>
        public static void Capture(
            Building_GravEngine engine,
            ShipLayoutDefV2 layout,
            string absoluteOutputPath,
            int marginCells = 2,
            int longestSidePixels = 1536,
            int maxTextureSize = 4096)
        {
            try
            {
                if (engine?.Map == null || layout == null || layout.width <= 0 || layout.height <= 0)
                {
                    Log.Warning("[GravshipExport] ShipPreviewRenderer: invalid inputs.");
                    return;
                }

                Map map = engine.Map;

                // Compute ship bounds (in map cells) relative to placed engine.
                IntVec3 topLeft = new IntVec3(
                    engine.Position.x - layout.gravEngineX,
                    0,
                    engine.Position.z - layout.gravEngineZ);

                int minX = Mathf.Max(0, topLeft.x - marginCells);
                int minZ = Mathf.Max(0, topLeft.z - marginCells);
                int maxX = Mathf.Min(map.Size.x - 1, topLeft.x + layout.width - 1 + marginCells);
                int maxZ = Mathf.Min(map.Size.z - 1, topLeft.z + layout.height - 1 + marginCells);

                int cellsW = maxX - minX + 1;
                int cellsH = maxZ - minZ + 1;
                if (cellsW <= 0 || cellsH <= 0)
                {
                    Log.Warning("[GravshipExport] ShipPreviewRenderer: computed empty bounds.");
                    return;
                }

                // Dynamic resolution: aim for ~longestSidePixels on the longer side.
                int longestCells = Mathf.Max(cellsW, cellsH);
                int ppc = Mathf.Clamp(Mathf.Max(1, Mathf.FloorToInt((float)longestSidePixels / Mathf.Max(1, longestCells))), 16, 128);
                int widthPx = Mathf.Clamp(cellsW * ppc, 128, maxTextureSize);
                int heightPx = Mathf.Clamp(cellsH * ppc, 128, maxTextureSize);

                float centerX = (minX + maxX + 1) * 0.5f;
                float centerZ = (minZ + maxZ + 1) * 0.5f;

                Log.Message($"[GravshipExport] Bounds: ({minX},{minZ})–({maxX},{maxZ})  Cells {cellsW}x{cellsH}  | ppc {ppc}  => {widthPx}x{heightPx}px");

                // Ensure map sections are up-to-date
                map.mapDrawer.RegenerateEverythingNow();

                // Use active camera
                Camera camera = Find.Camera;
                if (camera == null)
                {
                    Log.Error("[GravshipExport] ShipPreviewRenderer: Find.Camera returned null.");
                    return;
                }

                var camDriver = Find.CameraDriver;
                var oldPos = map.rememberedCameraPos;
                bool oldEnabled = camDriver.enabled;

                // Backup camera state
                var clearFlagsOld = camera.clearFlags;
                var bgOld = camera.backgroundColor;
                var orthoOld = camera.orthographic;
                var orthoSizeOld = camera.orthographicSize;
                var farClipOld = camera.farClipPlane;
                var cullingMaskOld = camera.cullingMask;
                var rtOld = camera.targetTexture;
                var rotOld = camera.transform.rotation;
                var posOld = camera.transform.position;

                try
                {
                    camDriver.enabled = false;

                    camera.orthographic = true;
                    // Half of vertical world span; +1f tiny buffer
                    camera.orthographicSize = cellsH * 0.5f + 1f;
                    camera.transform.position = new Vector3(centerX, 100f, centerZ);
                    camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Color.clear; // transparent background
                    camera.farClipPlane = 2000f;
                    camera.cullingMask = ~0; // everything

                    using (var ctx = new CaptureContext(camera, widthPx, heightPx))
                    {
                        // Make sure projection is clean, then render
                        camera.ResetWorldToCameraMatrix();
                        camera.ResetProjectionMatrix();

                        // Tick the driver to ensure map draw lists are synced with new view
                        Find.CameraDriver.Update();

                        camera.Render();

                        // Grab pixels and write PNG
                        var tex = ctx.ReadBack();
                        SaveTextureWithPngcs(tex, absoluteOutputPath);
                        UnityEngine.Object.Destroy(tex);
                    }
                }
                finally
                {
                    // Restore everything
                    camera.clearFlags = clearFlagsOld;
                    camera.backgroundColor = bgOld;
                    camera.orthographic = orthoOld;
                    camera.orthographicSize = orthoSizeOld;
                    camera.farClipPlane = farClipOld;
                    camera.cullingMask = cullingMaskOld;
                    camera.targetTexture = rtOld;
                    camera.transform.rotation = rotOld;
                    camera.transform.position = posOld;

                    camDriver.enabled = oldEnabled;
                    camDriver.SetRootPosAndSize(oldPos.rootPos, oldPos.rootSize);
                }

                Log.Message($"[GravshipExport] ✅ Ship preview captured: {absoluteOutputPath}");
            }
            catch (Exception ex)
            {
                Log.Error("[GravshipExport] ShipPreviewRenderer.Capture failed: " + ex);
            }
        }

        private static void SaveTextureWithPngcs(Texture2D tex, string path)
        {
            try
            {
                if (tex == null) return;

                var pixels = tex.GetPixels32();
                int w = tex.width;
                int h = tex.height;

                // Ensure dir exists
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var fs = File.Open(path, FileMode.Create))
                {
                    var png = new PngWriter(fs, new ImageInfo(w, h, 8, true));

                    for (int y = 0; y < h; y++)
                    {
                        var line = new ImageLine(png.ImgInfo);
                        int rowStart = (h - 1 - y) * w; // flip vertically for PNG
                        for (int x = 0; x < w; x++)
                        {
                            Color32 c = pixels[rowStart + x];
                            int i = x * 4;
                            line.Scanline[i + 0] = c.r;
                            line.Scanline[i + 1] = c.g;
                            line.Scanline[i + 2] = c.b;
                            line.Scanline[i + 3] = c.a;
                        }
                        png.WriteRow(line, y);
                    }

                    png.End();
                }

                Log.Message("[GravshipExport] ✅ PNG written via Pngcs: " + path);
            }
            catch (Exception ex)
            {
                Log.Error("[GravshipExport] Failed to write PNG: " + ex);
            }
        }

        /// <summary>
        /// Small helper to manage a temporary RT and readback texture safely.
        /// </summary>
        private sealed class CaptureContext : IDisposable
        {
            private readonly Camera cam;
            private readonly RenderTexture rt;
            private readonly int width;
            private readonly int height;
            private readonly RenderTexture prevActive;
            private readonly RenderTexture prevTarget;

            public CaptureContext(Camera cam, int width, int height)
            {
                this.cam = cam;
                this.width = width;
                this.height = height;

                prevTarget = cam.targetTexture;
                prevActive = RenderTexture.active;

                rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                RenderTexture.active = rt;
            }

            public Texture2D ReadBack()
            {
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                tex.Apply(false, false);
                return tex;
            }

            public void Dispose()
            {
                RenderTexture.active = prevActive;
                cam.targetTexture = prevTarget;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
