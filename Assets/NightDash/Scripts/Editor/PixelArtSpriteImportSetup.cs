using UnityEditor;
using UnityEngine;

namespace NightDash.Editor
{
    // Auto-applies pixel art import settings to all PNGs under
    // Assets/NightDash/Art/Stage01/Characters/ and Enemies/
    public class PixelArtSpriteImportSetup : AssetPostprocessor
    {
        const string CharactersRoot = "Assets/NightDash/Art/Stage01/Characters";
        const string EnemiesRoot = "Assets/NightDash/Art/Stage01/Enemies";

        void OnPreprocessTexture()
        {
            if (!assetPath.EndsWith(".png")) return;
            if (!assetPath.StartsWith(CharactersRoot) && !assetPath.StartsWith(EnemiesRoot)) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;

            // Pivot at bottom-center for ground-based characters/enemies.
            // Base sprites use bottom-center pivot. Animation frames use the same.
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            // First import: default to BottomCenter. Custom pivots are preserved
            // so AutoDetectSpritePivot results survive subsequent reimports.
            if (settings.spriteAlignment != (int)SpriteAlignment.Custom)
            {
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            }
            // Tight mesh trims the rendered quad along alpha pixels — removes
            // the transparent "wall" feel around characters with padding.
            // Sprite bounds (rect/pivot) are unchanged, so Y-sort and physics
            // continue to use the full PNG rect.
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.spriteExtrude = 1;
            importer.SetTextureSettings(settings);
        }
    }
}
