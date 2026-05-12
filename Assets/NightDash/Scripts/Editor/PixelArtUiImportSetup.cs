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

            // 9-slice border for button frames so Image type=Sliced can stretch
            // the center across arbitrary RectTransform sizes while leaving the
            // bronze trim corners + rivets pixel-perfect. Border format is
            // Vector4(left, bottom, right, top) in source-pixel units.
            // Tuned for the alpha-trimmed 101×37 button sprites — corner
            // covers the rivet cluster at each end.
            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (fileName.StartsWith("nd_ui_frame_button_"))
            {
                settings.spriteBorder = new Vector4(10f, 8f, 10f, 6f);
            }
            // Large dialog panel — ornate corner ornament (skull + gold
            // filigree) occupies the outermost ~50px of the trimmed 256×188
            // source. Border preserves the corner while letting Image.type=
            // Sliced stretch the middle edges.
            else if (fileName == "nd_ui_frame_panel_default")
            {
                settings.spriteBorder = new Vector4(50f, 48f, 50f, 48f);
            }
            // Horizontal bars — corners (rivets + trim) take the leftmost
            // and rightmost ~8px of the trimmed source. 9-slice lets the
            // bar stretch horizontally while keeping pixel-perfect ends.
            // Vertical border 0 (no top/bottom stretch needed for a strip).
            else if (fileName == "nd_ui_bar_empty" || fileName == "nd_ui_bar_fill")
            {
                settings.spriteBorder = new Vector4(8f, 0f, 8f, 0f);
            }

            importer.SetTextureSettings(settings);
        }
    }
}
