// Editor utility: scans Stage01 character/enemy/animation PNGs, finds the
// bottom-most opaque pixel column-center per frame, and sets that as a
// per-asset custom pivot. Solves the "character floats above ground" issue
// caused by transparent padding under the feet.
//
// Menu: NightDash > Auto-detect Sprite Pivots (Stage01)
//
// Safe: only modifies TextureImporter settings (alignment/pivot). PNG bytes
// are untouched. Reimport is triggered per asset.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NightDash.Editor
{
    public static class AutoDetectSpritePivot
    {
        private const string CharactersRoot = "Assets/NightDash/Art/Stage01/Characters";
        private const string EnemiesRoot    = "Assets/NightDash/Art/Stage01/Enemies";
        private const float  AlphaThreshold = 0.05f;

        [MenuItem("NightDash/Auto-detect Sprite Pivots (Stage01)", priority = 212)]
        public static void Run()
        {
            try
            {
                var paths = new List<string>();
                CollectPngsRecursive(CharactersRoot, paths);
                CollectPngsRecursive(EnemiesRoot, paths);

                int adjusted = 0;
                int skipped = 0;

                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < paths.Count; i++)
                {
                    string p = paths[i];
                    EditorUtility.DisplayProgressBar("Auto-detect Pivots", p, (float)i / paths.Count);

                    if (TrySetPivotFromAlpha(p)) adjusted++;
                    else skipped++;
                }

                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();

                EditorUtility.DisplayDialog(
                    "Auto-detect Pivots Complete",
                    $"Adjusted: {adjusted}\nSkipped (fully transparent or unreadable): {skipped}\nTotal scanned: {paths.Count}",
                    "OK");
            }
            catch (System.Exception e)
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[AutoDetectSpritePivot] {e}");
                EditorUtility.DisplayDialog("Failed", e.Message, "OK");
            }
        }

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

        private static bool TrySetPivotFromAlpha(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;

            // Force readable so we can sample pixels. Restore at the end.
            bool prevReadable = importer.isReadable;
            bool prevCrunched = importer.crunchedCompression;
            TextureImporterCompression prevCompression = importer.textureCompression;

            importer.isReadable = true;
            importer.crunchedCompression = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                RestoreImporter(importer, prevReadable, prevCrunched, prevCompression);
                return false;
            }

            int width = tex.width;
            int height = tex.height;
            Color32[] pixels;
            try
            {
                pixels = tex.GetPixels32();
            }
            catch
            {
                RestoreImporter(importer, prevReadable, prevCrunched, prevCompression);
                return false;
            }

            // Find bottom-most row with any opaque pixel.
            int bottomRow = -1;
            byte alphaCutoff = (byte)(AlphaThreshold * 255f);
            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[rowStart + x].a > alphaCutoff)
                    {
                        bottomRow = y;
                        break;
                    }
                }
                if (bottomRow >= 0) break;
            }

            if (bottomRow < 0)
            {
                // fully transparent
                RestoreImporter(importer, prevReadable, prevCrunched, prevCompression);
                return false;
            }

            // Horizontal center of opaque pixels at the bottom row.
            int rowOff = bottomRow * width;
            int firstX = -1, lastX = -1;
            for (int x = 0; x < width; x++)
            {
                if (pixels[rowOff + x].a > alphaCutoff)
                {
                    if (firstX < 0) firstX = x;
                    lastX = x;
                }
            }
            int centerX = (firstX + lastX) / 2;

            // Convert pixel coords to normalized pivot (Unity sprite pivot uses 0..1, origin bottom-left).
            float pivotX = (centerX + 0.5f) / width;
            float pivotY = (bottomRow + 0.5f) / height;

            // Apply custom pivot via TextureImporterSettings.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(pivotX, pivotY);
            importer.SetTextureSettings(settings);

            RestoreImporter(importer, prevReadable, prevCrunched, prevCompression);
            return true;
        }

        private static void RestoreImporter(TextureImporter importer, bool prevReadable, bool prevCrunched, TextureImporterCompression prevCompression)
        {
            importer.isReadable = prevReadable;
            importer.crunchedCompression = prevCrunched;
            importer.textureCompression = prevCompression;
            importer.SaveAndReimport();
        }
    }
}
