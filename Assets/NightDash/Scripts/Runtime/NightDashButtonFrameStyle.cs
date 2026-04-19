using UnityEngine;

namespace NightDash.Runtime
{
    internal static class NightDashButtonFrameStyle
    {
        private const string DefaultTexturePath = "NightDash/UI/Frames/nd_ui_frame_button_default";
        private const string HoverTexturePath = "NightDash/UI/Frames/nd_ui_frame_button_hover";
        private const string PressedTexturePath = "NightDash/UI/Frames/nd_ui_frame_button_pressed";
        private const string DisabledTexturePath = "NightDash/UI/Frames/nd_ui_frame_button_disabled";

        public static void LoadAndCropFrameTextures(
            ref Texture2D defaultTex,
            ref Texture2D hoverTex,
            ref Texture2D pressedTex,
            ref Texture2D disabledTex)
        {
            defaultTex = defaultTex != null ? defaultTex : Resources.Load<Texture2D>(DefaultTexturePath);
            hoverTex = hoverTex != null ? hoverTex : Resources.Load<Texture2D>(HoverTexturePath);
            pressedTex = pressedTex != null ? pressedTex : Resources.Load<Texture2D>(PressedTexturePath);
            disabledTex = disabledTex != null ? disabledTex : Resources.Load<Texture2D>(DisabledTexturePath);

            defaultTex = CropFrameTexture(defaultTex);
            hoverTex = CropFrameTexture(hoverTex);
            pressedTex = CropFrameTexture(pressedTex);
            disabledTex = CropFrameTexture(disabledTex);
        }

        public static Texture2D CropFrameTexture(Texture2D source)
        {
            if (source == null)
            {
                return null;
            }

            int x = Mathf.RoundToInt(source.width * 0.07f);
            int y = Mathf.RoundToInt(source.height * 0.29f);
            int w = Mathf.RoundToInt(source.width * 0.86f);
            int h = Mathf.RoundToInt(source.height * 0.42f);

            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
            cropped.ReadPixels(new Rect(x, y, w, h), 0, 0);
            cropped.Apply(false, true);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return cropped;
        }

        public static GUIStyle BuildActionButtonStyle(
            Texture2D defaultTex,
            Texture2D hoverTex,
            Texture2D pressedTex,
            Texture2D disabledTex)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                border = new RectOffset(24, 24, 20, 20),
                margin = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(30, 30, 12, 14)
            };

            style.normal.background = defaultTex;
            style.hover.background = hoverTex != null ? hoverTex : defaultTex;
            style.active.background = pressedTex != null ? pressedTex : defaultTex;
            style.focused.background = style.hover.background;

            if (disabledTex != null)
            {
                style.onNormal.background = disabledTex;
                style.onActive.background = disabledTex;
            }

            style.normal.textColor = new Color(0.94f, 0.89f, 0.97f, 1f);
            style.hover.textColor = Color.white;
            style.active.textColor = new Color(0.92f, 0.86f, 0.96f, 1f);
            style.focused.textColor = Color.white;
            return style;
        }
    }
}
