using System.Collections.Generic;
using NightDash.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NightDash.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class NightDashHudModifierStrip : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float iconSize = 48f;
        [SerializeField] private float spacing = 6f;
        [SerializeField] private TextAnchor childAlignment = TextAnchor.MiddleLeft;
        [SerializeField] private bool buildOnAwake = true;

        [Header("Active Modifiers")]
        [Tooltip("HUD에 표시할 활성 modifier SO 목록. ECS data flow 연결 전에는 Inspector wiring으로 가시화.")]
        [SerializeField] private DifficultyModifierData[] activeModifiers;

        // Mirrors `activeModifiers` index-for-index. Levels <= 1 hide the
        // Lv.N badge (Lv.1 is the implicit baseline).
        private int[] _activeLevels;

        private readonly List<Image> _spawned = new();
        private HorizontalLayoutGroup _layout;
        private RectTransform _rect;
        private ContentSizeFitter _fitter;

        private void Awake()
        {
            EnsureLayout();
            if (buildOnAwake) Rebuild();
        }

        public void ConfigureLayout(float iconSizeOverride, float spacingOverride, TextAnchor alignment)
        {
            iconSize = iconSizeOverride;
            spacing = spacingOverride;
            childAlignment = alignment;
            EnsureLayout();
        }

        public void SetActiveModifiers(IList<DifficultyModifierData> modifiers)
        {
            SetActiveModifiers(modifiers, null);
        }

        public void SetActiveModifiers(IList<DifficultyModifierData> modifiers, IList<int> levels)
        {
            if (modifiers == null)
            {
                activeModifiers = System.Array.Empty<DifficultyModifierData>();
                _activeLevels = System.Array.Empty<int>();
            }
            else
            {
                activeModifiers = new DifficultyModifierData[modifiers.Count];
                _activeLevels = new int[modifiers.Count];
                for (int i = 0; i < modifiers.Count; i++)
                {
                    activeModifiers[i] = modifiers[i];
                    _activeLevels[i] = (levels != null && i < levels.Count) ? levels[i] : 1;
                }
            }
            Rebuild();
        }

        public void Rebuild()
        {
            EnsureLayout();
            ClearSpawned();
            if (activeModifiers == null || activeModifiers.Length == 0) return;

            for (int i = 0; i < activeModifiers.Length; i++)
            {
                DifficultyModifierData data = activeModifiers[i];
                if (data == null || data.icon == null) continue;

                string objName = string.IsNullOrEmpty(data.id)
                    ? $"Modifier_{i}"
                    : $"Modifier_{data.id}";

                GameObject go = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                go.transform.SetParent(transform, false);

                Image img = go.GetComponent<Image>();
                img.sprite = data.icon;
                img.preserveAspect = true;
                img.raycastTarget = false;

                LayoutElement le = go.GetComponent<LayoutElement>();
                le.preferredWidth = iconSize;
                le.preferredHeight = iconSize;
                le.minWidth = iconSize;
                le.minHeight = iconSize;

                int level = (_activeLevels != null && i < _activeLevels.Length) ? _activeLevels[i] : 1;
                if (level >= 2) BuildLevelBadge(go.transform, level);

                _spawned.Add(img);
            }
        }

        // Bottom-right "Lv.N" badge — rendered only when level >= 2 so the
        // baseline Lv.1 case stays visually clean.
        private void BuildLevelBadge(Transform parent, int level)
        {
            GameObject badgeGo = new GameObject("LvBadge",
                typeof(RectTransform), typeof(Text), typeof(Outline), typeof(LayoutElement));
            badgeGo.transform.SetParent(parent, false);

            // Pinned overlay — exclude from the icon's LayoutGroup math so the
            // ContentSizeFitter on the strip doesn't grow to include the
            // badge's offset rect.
            LayoutElement le = badgeGo.GetComponent<LayoutElement>();
            le.ignoreLayout = true;

            RectTransform r = (RectTransform)badgeGo.transform;
            r.anchorMin = new Vector2(1f, 0f);
            r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(1f, 0f);
            r.anchoredPosition = new Vector2(2f, -2f);
            r.sizeDelta = new Vector2(iconSize * 0.85f, iconSize * 0.4f);

            Text t = badgeGo.GetComponent<Text>();
            t.text = $"Lv.{level}";
            t.alignment = TextAnchor.LowerRight;
            t.fontSize = Mathf.Max(12, Mathf.RoundToInt(iconSize * 0.4f));
            t.color = new Color(1f, 0.85f, 0.45f, 1f);
            t.font = NightDashUIFonts.Arcade;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            Outline o = badgeGo.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.95f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void EnsureLayout()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();

            _layout = GetComponent<HorizontalLayoutGroup>();
            if (_layout == null) _layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            _layout.spacing = spacing;
            _layout.childAlignment = childAlignment;
            _layout.childForceExpandWidth = false;
            _layout.childForceExpandHeight = false;
            _layout.childControlWidth = true;
            _layout.childControlHeight = true;

            _fitter = GetComponent<ContentSizeFitter>();
            if (_fitter == null) _fitter = gameObject.AddComponent<ContentSizeFitter>();
            _fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            _fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
            }
            _spawned.Clear();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled) Rebuild();
        }
#endif
    }
}
