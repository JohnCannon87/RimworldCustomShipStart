using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using Hjg.Pngcs;

namespace GravshipExport
{
    /// <summary>
    /// Renders a top-down transparent PNG of the ship area.
    /// Uses the real RimWorld map camera and captures at end-of-frame.
    /// </summary>
    internal static class ShipPreviewRenderer
    {
        /// <summary>
        /// Convenience entry point: saves to Config/GravshipExport/&lt;shipName&gt;.png
        /// </summary>
        public static void Capture(Building_GravEngine engine, ShipLayoutDefV2 layout, string shipName)
        {
            if (string.IsNullOrWhiteSpace(shipName)) shipName = "ShipPreview";
            if (!shipName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                shipName += ".png";

            string cfgDir = Path.Combine(GenFilePaths.ConfigFolderPath, "GravshipExport");
            if (!Directory.Exists(cfgDir)) Directory.CreateDirectory(cfgDir);
            string outPath = Path.Combine(cfgDir, shipName);

            Capture(engine, layout, outPath, marginCells: 2, longestSidePixels: 1536, maxTextureSize: 4096);
        }

        /// <summary>
        /// Full control entry point.
        /// </summary>
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
                    GravshipLogger.Warning("ShipPreviewRenderer: invalid inputs.");
                    return;
                }

                // Compute ship bounds (in map cells) relative to placed engine.
                IntVec3 topLeft = new IntVec3(
                    engine.Position.x - layout.gravEngineX,
                    0,
                    engine.Position.z - layout.gravEngineZ);

                Map map = engine.Map;

                int minX = Mathf.Max(0, topLeft.x - marginCells);
                int minZ = Mathf.Max(0, topLeft.z - marginCells);
                int maxX = Mathf.Min(map.Size.x - 1, topLeft.x + layout.width - 1 + marginCells);
                int maxZ = Mathf.Min(map.Size.z - 1, topLeft.z + layout.height - 1 + marginCells);

                int cellsW = maxX - minX + 1;
                int cellsH = maxZ - minZ + 1;
                if (cellsW <= 0 || cellsH <= 0)
                {
                    GravshipLogger.Warning("ShipPreviewRenderer: computed empty bounds.");
                    return;
                }

                // Dynamic resolution: aim for ~longestSidePixels on the longer side.
                int longestCells = Mathf.Max(cellsW, cellsH);
                int ppc = Mathf.Clamp(Mathf.Max(1, Mathf.FloorToInt((float)longestSidePixels / Mathf.Max(1, longestCells))), 16, 128);
                int widthPx = Mathf.Clamp(cellsW * ppc, 128, maxTextureSize);
                int heightPx = Mathf.Clamp(cellsH * ppc, 128, maxTextureSize);

                float centerX = (minX + maxX + 1) * 0.5f;
                float centerZ = (minZ + maxZ + 1) * 0.5f;

                GravshipLogger.Message($"Preview bounds: ({minX},{minZ})–({maxX},{maxZ})  Cells {cellsW}x{cellsH}  | ppc {ppc}  => {widthPx}x{heightPx}px");

                // Ensure the map meshes are up to date
                map.mapDrawer.RegenerateEverythingNow();

                // Spin up a temporary runner to do the capture at end-of-frame.
                var go = new GameObject("GravshipExport_CaptureRunner");
                go.hideFlags = HideFlags.HideAndDontSave;
                var runner = go.AddComponent<ShipPreviewCaptureRunner>();

                runner.Begin(new ShipPreviewCaptureRunner.Args
                {
                    Map = map,
                    CenterX = centerX,
                    CenterZ = centerZ,
                    CellsH = cellsH,
                    WidthPx = widthPx,
                    HeightPx = heightPx,
                    OutputPath = absoluteOutputPath
                });
            }
            catch (Exception ex)
            {
                GravshipLogger.Error("ShipPreviewRenderer.Capture failed: " + ex);
            }
        }

        /// <summary>
        /// MonoBehaviour that performs the capture at end-of-frame
        /// when RimWorld has actually drawn the map.
        /// </summary>
        private sealed class ShipPreviewCaptureRunner : MonoBehaviour
        {
            public struct Args
            {
                public Map Map;
                public float CenterX;
                public float CenterZ;
                public int CellsH;
                public int WidthPx;
                public int HeightPx;
                public string OutputPath;
            }

            private Args a;
            private Camera camera;
            private CameraDriver camDriver;

            // Backup camera state
            private CameraClearFlags clearFlagsOld;
            private Color bgOld;
            private bool orthoOld;
            private float orthoSizeOld;
            private float farClipOld, nearClipOld;
            private int cullingMaskOld;
            private RenderTexture rtOld;
            private Quaternion rotOld;
            private Vector3 posOld;
            private bool driverEnabledOld;
            private RememberedCameraPos oldRemembered;
            private List<object> previousSelection;

            public void Begin(Args args)
            {
                a = args;

                // Save and clear selection to prevent overlays
                previousSelection = new List<object>(Find.Selector.SelectedObjectsListForReading);
                Find.Selector.ClearSelection();

                camera = Find.Camera ?? Camera.main;
                camDriver = Find.CameraDriver;

                if (camera == null || camDriver == null)
                {
                    GravshipLogger.Error("No valid map camera / driver found.");
                    Destroy(gameObject);
                    return;
                }

                // Backup state
                clearFlagsOld = camera.clearFlags;
                bgOld = camera.backgroundColor;
                orthoOld = camera.orthographic;
                orthoSizeOld = camera.orthographicSize;
                farClipOld = camera.farClipPlane;
                nearClipOld = camera.nearClipPlane;
                cullingMaskOld = camera.cullingMask;
                rtOld = camera.targetTexture;
                rotOld = camera.transform.rotation;
                posOld = camera.transform.position;
                driverEnabledOld = camDriver.enabled;
                oldRemembered = a.Map.rememberedCameraPos;

                // Configure camera
                try
                {
                    camDriver.enabled = false;
                    camera.orthographic = true;
                    camera.orthographicSize = a.CellsH * 0.5f + 1f;
                    camera.transform.position = new Vector3(a.CenterX, 50f, a.CenterZ);
                    camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Color.clear;
                    camera.nearClipPlane = 0.1f;
                    camera.farClipPlane = 500f;
                    camera.cullingMask = -1;

                    GravshipLogger.Message($"Camera setup: pos=({camera.transform.position.x},{camera.transform.position.y},{camera.transform.position.z}) orthoSize={camera.orthographicSize}");
                }
                catch (Exception ex)
                {
                    GravshipLogger.Error("Failed to set up camera: " + ex);
                    Restore();
                    Destroy(gameObject);
                    return;
                }

                StartCoroutine(CaptureCoroutine());
            }

            private void Restore()
            {
                try
                {
                    // Restore previous selection
                    if (previousSelection != null && previousSelection.Count > 0)
                    {
                        Find.Selector.ClearSelection();
                        foreach (var obj in previousSelection)
                        {
                            try { Find.Selector.Select(obj); }
                            catch (Exception selEx) { GravshipLogger.Warning($"Failed to restore selection for {obj}: {selEx.Message}"); }
                        }
                    }

                    if (camera != null)
                    {
                        camera.clearFlags = clearFlagsOld;
                        camera.backgroundColor = bgOld;
                        camera.orthographic = orthoOld;
                        camera.orthographicSize = orthoSizeOld;
                        camera.farClipPlane = farClipOld;
                        camera.nearClipPlane = nearClipOld;
                        camera.cullingMask = cullingMaskOld;
                        camera.targetTexture = rtOld;
                        camera.transform.rotation = rotOld;
                        camera.transform.position = posOld;
                    }

                    if (camDriver != null)
                    {
                        camDriver.enabled = driverEnabledOld;
                        camDriver.SetRootPosAndSize(oldRemembered.rootPos, oldRemembered.rootSize);
                    }
                }
                catch (Exception ex)
                {
                    GravshipLogger.Error("Failed to restore camera/driver: " + ex);
                }
            }

            private IEnumerator CaptureCoroutine()
            {
                yield return new WaitForEndOfFrame();

                RenderTexture rt = null;
                Texture2D tex = null;

                try
                {
                    rt = RenderTexture.GetTemporary(a.WidthPx, a.HeightPx, 24, RenderTextureFormat.ARGB32);

                    var prevTarget = camera.targetTexture;
                    var prevActive = RenderTexture.active;

                    camera.targetTexture = rt;
                    RenderTexture.active = rt;
                    camera.Render();

                    tex = new Texture2D(a.WidthPx, a.HeightPx, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, a.WidthPx, a.HeightPx), 0, 0, false);
                    tex.Apply(false, false);

                    RenderTexture.active = prevActive;
                    camera.targetTexture = prevTarget;

                    tex = DownscaleTexture(tex, 0.5f);

                    SaveTextureWithPngcs(tex, a.OutputPath);
                    GravshipLogger.Message($"✅ Ship preview captured: {a.OutputPath}");
                }
                catch (Exception ex)
                {
                    GravshipLogger.Error("Capture failed: " + ex);
                }
                finally
                {
                    if (tex != null) Destroy(tex);
                    if (rt != null) RenderTexture.ReleaseTemporary(rt);
                    Restore();
                    Destroy(gameObject);
                }
            }
        }

        private static Texture2D DownscaleTexture(Texture2D source, float scale)
        {
            int newW = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int newH = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            RenderTexture rt = RenderTexture.GetTemporary(newW, newH, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D result = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, newW, newH), 0, 0);
            result.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.Destroy(source);

            return result;
        }

        private static void SaveTextureWithPngcs(Texture2D tex, string path)
        {
            try
            {
                if (tex == null) return;

                var pixels = tex.GetPixels32();
                int w = tex.width;
                int h = tex.height;

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var fs = File.Open(path, FileMode.Create))
                {
                    var png = new PngWriter(fs, new ImageInfo(w, h, 8, true));

                    for (int y = 0; y < h; y++)
                    {
                        var line = new ImageLine(png.ImgInfo);
                        int rowStart = (h - 1 - y) * w;
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

                GravshipLogger.Message("✅ PNG written via Pngcs: " + path);
            }
            catch (Exception ex)
            {
                GravshipLogger.Error("Failed to write PNG: " + ex);
            }
        }
    }
}
