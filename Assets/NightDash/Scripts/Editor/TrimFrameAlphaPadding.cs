// Editor utility: trims transparent alpha padding around each Stage01
// character/enemy frame PNG so the BottomCenter pivot lands on the
// character's feet. Modifies PNG bytes in place — rely on git for backup.
//
// Menu: NightDash > Trim Frame Alpha Padding (Stage01)
//
// Implementation note: reads/decodes PNGs directly via ImageConversion.LoadImage
// instead of going through TextureImporter. This avoids the
// "texture data is either not readable" error caused by SaveAndReimport()
// being deferred inside StartAssetEditing/StopAssetEditing blocks.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NightDash.Editor
{
    public static class TrimFrameAlphaPadding
    {
        private const string CharactersRoot = "Assets/NightDash/Art/Stage01/Characters/Animations";
        private const string EnemiesRoot    = "Assets/NightDash/Art/Stage01/Enemies/Animations";
        private const byte   AlphaCutoff    = 12; // ~5% alpha threshold

        [MenuItem("NightDash/Trim Frame Alpha Padding (Stage01)", priority = 213)]
        public static void Run()
        {
            if (!EditorUtility.DisplayDialog(
                "Trim Frame Alpha Padding",
                "This will modify PNG files under:\n" +
                $"  {CharactersRoot}\n  {EnemiesRoot}\n\n" +
                "Each frame is cropped to the bounding box of opaque pixels. " +
                "PNGs are read/written directly on disk; no importer toggling.\n\n" +
                "Git tracks changes for rollback. Continue?",
                "Trim", "Cancel"))
            {
                return;
            }

            var paths = new List<string>();
            CollectPngsRecursive(CharactersRoot, paths);
            CollectPngsRecursive(EnemiesRoot, paths);

            int trimmed = 0, unchanged = 0, transparent = 0, errors = 0;

            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    string p = paths[i];
                    EditorUtility.DisplayProgressBar("Trim Frame Padding", p, (float)i / paths.Count);

                    var result = TrimSingle(p);
                    if (result == TrimResult.Trimmed) trimmed++;
                    else if (result == TrimResult.Unchanged) unchanged++;
                    else if (result == TrimResult.Transparent) transparent++;
                    else errors++;
                }

                // One refresh at the end so Unity reimports all modified PNGs.
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.DisplayDialog(
                "Trim Complete",
                $"Trimmed:        {trimmed}\nNo padding:     {unchanged}\nFully transparent: {transparent}\nErrors:         {errors}\nTotal scanned:  {paths.Count}",
                "OK");
        }

        private enum TrimResult { Trimmed, Unchanged, Transparent, Error }

        private static void CollectPngsRecursive(string root, List<string> paths)
        {
            if (!AssetDatabase.IsValidFolder(root)) return;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) paths.Add(p);
            }
        }

        private static TrimResult TrimSingle(string assetPath)
        {
            string fullPath = Path.GetFullPath(assetPath);

            byte[] sourceBytes;
            try
            {
                sourceBytes = File.ReadAllBytes(fullPath);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TrimFrameAlphaPadding] read failed {assetPath}: {e.Message}");
                return TrimResult.Error;
            }

            // Decode PNG directly into a non-readable scratch texture (mark as
            // mipChain false; ImageConversion.LoadImage gives us readable pixels).
            var srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(srcTex, sourceBytes, markNonReadable: false))
                {
                    Debug.LogError($"[TrimFrameAlphaPadding] decode failed {assetPath}");
                    return TrimResult.Error;
                }

                int w = srcTex.width;
                int h = srcTex.height;
                Color32[] px = srcTex.GetPixels32();

                int minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        if (px[row + x].a > AlphaCutoff)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }

                if (maxX < 0) return TrimResult.Transparent;

                int newW = maxX - minX + 1;
                int newH = maxY - minY + 1;
                if (newW == w && newH == h) return TrimResult.Unchanged;

                var cropped = new Color32[newW * newH];
                for (int y = 0; y < newH; y++)
                {
                    int srcRow = (y + minY) * w + minX;
                    int dstRow = y * newW;
                    for (int x = 0; x < newW; x++)
                    {
                        cropped[dstRow + x] = px[srcRow + x];
                    }
                }

                var outTex = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
                try
                {
                    outTex.SetPixels32(cropped);
                    outTex.Apply();
                    byte[] outBytes = ImageConversion.EncodeToPNG(outTex);
                    File.WriteAllBytes(fullPath, outBytes);
                }
                finally
                {
                    Object.DestroyImmediate(outTex);
                }

                return TrimResult.Trimmed;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TrimFrameAlphaPadding] {assetPath}: {e.Message}");
                return TrimResult.Error;
            }
            finally
            {
                Object.DestroyImmediate(srcTex);
            }
        }
    }
}
