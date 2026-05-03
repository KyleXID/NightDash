// Auto-applies pixel-art import settings to UI PNGs under
// Assets/NightDash/Art/UI/. Stage01 sprites use a separate setup; UI assets
// follow Center pivot convention (FullRect mesh) so they composite cleanly
// onto Canvas. Campfire frames need BottomCenter so flames anchor to the logs.

using UnityEditor;
using UnityEngine;

namespace NightDash.Editor
{
    public class PixelArtUiImportSetup : AssetPostprocessor
    {
        const string UiRoot = "Assets/NightDash/Art/UI";
        const string ResourcesUiRoot = "Assets/Resources/NightDash/UI";
        const string CampfireRoot = "Assets/NightDash/Art/UI/Lobby/Campfire";
        const string ResourcesCampfireRoot = "Assets/Resources/NightDash/UI/Lobby/Campfire";

        void OnPreprocessTexture()
        {
            if (!assetPath.EndsWith(".png")) return;
            bool inUiRoot = assetPath.StartsWith(UiRoot) || assetPath.StartsWith(ResourcesUiRoot);
            if (!inUiRoot) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            // First import default: Center for static UI, BottomCenter for
            // campfire frames so the flame anchors at the log base.
            // Custom pivots (e.g. set by AutoDetectSpritePivot) survive.
            if (settings.spriteAlignment != (int)SpriteAlignment.Custom)
            {
                if (assetPath.StartsWith(CampfireRoot) || assetPath.StartsWith(ResourcesCampfireRoot))
                {
                    settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                }
                else
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Center;
                }
            }
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
        }
    }
}
