// Lazy-loaded UI icon registry.
//
// Every menu surface (title, pause, HUD action buttons, lobby help, etc.)
// pulls its sprite handles through here so the Resources.Load path lives in
// exactly one spot and each sprite is fetched at most once per session.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NightDash.Runtime.UI
{
    public static class NightDashUIIcons
    {
        // Canonical key strings. Mirror the `nd_ui_icon_{key}_default.png`
        // files already imported under Resources/NightDash/UI/Icons.
        public const string Cancel          = "cancel";
        public const string Confirm         = "confirm";
        public const string Interact        = "interact";
        public const string InteractKey     = "interact_key";
        public const string Load            = "load";
        public const string Lock            = "lock";
        public const string ObjectiveMarker = "objective_marker";
        public const string Pause           = "pause";
        public const string Potion          = "potion";
        public const string Reroll          = "reroll";
        public const string Save            = "save";
        public const string Settings        = "settings";
        public const string UnlockClass     = "unlock_class";
        public const string UnlockRelic     = "unlock_relic";
        public const string UnlockStage     = "unlock_stage";
        public const string Warning         = "warning";
        public const string WaveSkull       = "wave_skull";

        private const string ResourcePrefix = "NightDash/UI/Icons/nd_ui_icon_";
        private const string ResourceSuffix = "_default";
        // Passive icons live under two parallel subfolders:
        //   Class/   — the 7 class-specific passives
        //   Passive/ — the shared/category passives (risk-return, etc.)
        // GetPassive tries Class first (legacy default), then falls back to
        // Passive so new categories don't have to migrate the existing set.
        private const string PassiveClassPrefix = "NightDash/UI/Icons/Class/nd_ui_icon_passive_";
        private const string PassiveSharedPrefix = "NightDash/UI/Icons/Passive/nd_ui_icon_passive_";

        private static readonly Dictionary<string, Sprite> s_Cache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            // Domain Reload Off would otherwise leave the dictionary holding
            // sprites whose underlying textures have been unloaded.
            s_Cache.Clear();
        }

        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (s_Cache.TryGetValue(key, out Sprite cached)) return cached;

            Sprite sprite = Resources.Load<Sprite>(ResourcePrefix + key + ResourceSuffix);
            // Cache misses too (null) so we don't hammer Resources.Load every
            // frame when an icon name is mistyped.
            s_Cache[key] = sprite;
            return sprite;
        }

        // Resolves the icon for a class-specific passive. Strips the leading
        // "passive_" prefix when present so callers can pass either the raw
        // passive id ("passive_warrior_guard_stack") or the short form
        // ("warrior_guard_stack"). Returns null when no matching sprite ships
        // under Resources/NightDash/UI/Icons/Class.
        public static Sprite GetPassive(string passiveId)
        {
            if (string.IsNullOrEmpty(passiveId)) return null;
            string body = passiveId.StartsWith("passive_")
                ? passiveId.Substring("passive_".Length)
                : passiveId;
            string cacheKey = "passive:" + body;
            if (s_Cache.TryGetValue(cacheKey, out Sprite cached)) return cached;
            // Try the class subfolder first (legacy default), then the
            // shared Passive subfolder (categories like risk-return).
            Sprite sprite = Resources.Load<Sprite>(PassiveClassPrefix + body + ResourceSuffix);
            if (sprite == null)
            {
                sprite = Resources.Load<Sprite>(PassiveSharedPrefix + body + ResourceSuffix);
            }
            // Mastery passives (burn_mastery, freeze_mastery, etc.) reuse
            // the matching status-effect glyph until dedicated art ships.
            if (sprite == null && body.EndsWith("_mastery"))
            {
                string statusKind = body.Substring(0, body.Length - "_mastery".Length);
                sprite = Resources.Load<Sprite>(
                    "NightDash/UI/Icons/Status/nd_ui_icon_status_" + statusKind + ResourceSuffix);
            }
            s_Cache[cacheKey] = sprite;
            return sprite;
        }

        // Builds a child GameObject under `parent` that hosts an Image with
        // the icon. Returns the child rect so callers can adjust further.
        // Falls back to a quiet no-op (null) when the sprite is missing.
        public static RectTransform Attach(
            Transform parent,
            string iconKey,
            Vector2 size,
            Vector2 anchoredPosition,
            string objectName = null)
        {
            if (parent == null) return null;
            Sprite sprite = Get(iconKey);
            if (sprite == null) return null;

            var go = new GameObject(
                objectName ?? $"Icon_{iconKey}",
                typeof(RectTransform),
                typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            return rect;
        }
    }
}
