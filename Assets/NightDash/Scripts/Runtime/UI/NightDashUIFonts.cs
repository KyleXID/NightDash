// Sprint B / M3 — Shared font cache for NightDash UI screens.
// Loads the arcade-style pixel font once and falls back to Unity's built-in
// LegacyRuntime if the asset is missing (e.g. fresh checkout before LFS pull).
// All UI panels go through NightDashUIFonts.Arcade so a future font swap
// touches a single property.

using UnityEngine;

namespace NightDash.Runtime.UI
{
    public static class NightDashUIFonts
    {
        // Silver (Poppy Works) — pixel-art display font with a fuller glyph
        // coverage than Press Start 2P. Lives under Assets/Resources/NightDash/Fonts/.
        private const string ArcadeFontResourcePath = "NightDash/Fonts/Silver";
        private static Font _arcade;

        public static Font Arcade
        {
            get
            {
                if (_arcade == null)
                {
                    _arcade = Resources.Load<Font>(ArcadeFontResourcePath);
                }
                if (_arcade == null)
                {
                    _arcade = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _arcade;
            }
        }
    }
}
