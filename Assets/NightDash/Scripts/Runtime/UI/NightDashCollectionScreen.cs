// Collection / codex screen (mock).
//
// Three tabs — Classes / Stages / Relics — each rendering a fixed grid pulled
// from MockCollectionData. Locked entries are tinted dark with a `lock` icon
// overlay; unlocked entries get the category's `unlock_*` icon as a badge.
// Esc closes. ←/→ swap tabs.
//
// Single shared instance, same pattern as NightDashSettingsModal.

using System;
using System.Collections.Generic;
using NightDash.Data.Mock;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class NightDashCollectionScreen : MonoBehaviour
    {
        private const int SortOrder = 6500;

        private enum Tab { Classes, Stages, Relics }

        private static NightDashCollectionScreen s_Instance;
        private Action _onClosed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_Instance = null;

        public static bool IsOpen => s_Instance != null && s_Instance.gameObject.activeInHierarchy;

        public static void Show(Action onClosed = null)
        {
            EnsureInstance();
            s_Instance._onClosed = onClosed;
            s_Instance.SetTab(Tab.Classes);
            s_Instance.gameObject.SetActive(true);
        }

        private static void EnsureInstance()
        {
            if (s_Instance != null) return;
            var go = new GameObject("NightDashCollectionScreen");
            s_Instance = go.AddComponent<NightDashCollectionScreen>();
            s_Instance.BuildUI();
            s_Instance.gameObject.SetActive(false);
        }

        private RectTransform _panel;
        private RectTransform _grid;
        private Text _headerText;
        private readonly Text[] _tabLabels = new Text[3];
        private readonly Image[] _tabIndicators = new Image[3];
        private Tab _currentTab = Tab.Classes;
        // Recycle child cells across tab switches to avoid Destroy thrash.
        private readonly List<GameObject> _cells = new();

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Backdrop
            var backdropGo = new GameObject("Backdrop",
                typeof(RectTransform), typeof(Image), typeof(Button));
            backdropGo.transform.SetParent(transform, false);
            var br = (RectTransform)backdropGo.transform;
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
            backdropGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
            backdropGo.GetComponent<Button>().onClick.AddListener(Close);

            // Panel
            var panelGo = new GameObject("Panel",
                typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(1400f, 820f);
            panelGo.GetComponent<Image>().color = new Color(0.30f, 0.12f, 0.14f, 0.95f);
            panelGo.GetComponent<Image>().raycastTarget = true;

            var fillGo = new GameObject("Fill",
                typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_panel, false);
            var fr = (RectTransform)fillGo.transform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(3f, 3f); fr.offsetMax = new Vector2(-3f, -3f);
            fillGo.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.07f, 1f);

            BuildHeader();
            BuildTabs();
            BuildGrid();
            BuildHint();
        }

        private void BuildHeader()
        {
            var go = new GameObject("Header",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(_panel, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -24f);
            r.sizeDelta = new Vector2(-40f, 80f);
            _headerText = go.GetComponent<Text>();
            _headerText.text = "COLLECTION";
            _headerText.alignment = TextAnchor.MiddleCenter;
            _headerText.fontSize = 56;
            _headerText.color = new Color(0.92f, 0.78f, 0.50f, 1f);
            _headerText.font = NightDashUIFonts.Arcade;
            _headerText.raycastTarget = false;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.9f);
            o.effectDistance = new Vector2(2f, -2f);
        }

        private void BuildTabs()
        {
            // 3 evenly spaced tab buttons under the header.
            string[] names = { "CLASSES", "STAGES", "RELICS" };
            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                var go = new GameObject($"Tab_{names[i]}",
                    typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_panel, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = new Vector2(0.5f, 1f);
                r.anchorMax = new Vector2(0.5f, 1f);
                r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(240f, 56f);
                r.anchoredPosition = new Vector2((i - 1) * 280f, -120f);
                go.GetComponent<Image>().color = new Color(0.18f, 0.10f, 0.12f, 0.92f);
                go.GetComponent<Button>().onClick.AddListener(() => SetTab((Tab)captured));

                var labelGo = new GameObject("Label",
                    typeof(RectTransform), typeof(Text), typeof(Outline));
                labelGo.transform.SetParent(r, false);
                var lr = (RectTransform)labelGo.transform;
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
                var lt = labelGo.GetComponent<Text>();
                lt.text = names[i];
                lt.alignment = TextAnchor.MiddleCenter;
                lt.fontSize = 28;
                lt.color = new Color(0.95f, 0.92f, 0.86f, 1f);
                lt.font = NightDashUIFonts.Arcade;
                lt.raycastTarget = false;
                var o = labelGo.GetComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.9f);
                o.effectDistance = new Vector2(1.5f, -1.5f);
                _tabLabels[i] = lt;

                // Bottom highlight bar — shown for the active tab.
                var hlGo = new GameObject("Indicator",
                    typeof(RectTransform), typeof(Image));
                hlGo.transform.SetParent(r, false);
                var hr = (RectTransform)hlGo.transform;
                hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0f);
                hr.pivot = new Vector2(0.5f, 0f);
                hr.anchoredPosition = Vector2.zero;
                hr.sizeDelta = new Vector2(0f, 4f);
                var hi = hlGo.GetComponent<Image>();
                hi.color = new Color(0.92f, 0.30f, 0.34f, 1f);
                hi.raycastTarget = false;
                _tabIndicators[i] = hi;
            }
        }

        private void BuildGrid()
        {
            var go = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            go.transform.SetParent(_panel, false);
            _grid = (RectTransform)go.transform;
            _grid.anchorMin = new Vector2(0f, 0f); _grid.anchorMax = new Vector2(1f, 1f);
            _grid.pivot = new Vector2(0.5f, 0.5f);
            _grid.offsetMin = new Vector2(40f, 100f);
            _grid.offsetMax = new Vector2(-40f, -200f);
            var layout = go.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(210f, 250f);
            layout.spacing = new Vector2(18f, 18f);
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 6;
        }

        private void BuildHint()
        {
            var go = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_panel, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.5f, 0f); r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0f, 24f);
            r.sizeDelta = new Vector2(800f, 32f);
            var t = go.GetComponent<Text>();
            t.text = "←/→  TABS     [ESC]  CLOSE";
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 26;
            t.color = new Color(0.80f, 0.74f, 0.62f, 1f);
            t.font = NightDashUIFonts.Arcade;
            t.raycastTarget = false;
        }

        private void SetTab(Tab tab)
        {
            _currentTab = tab;
            for (int i = 0; i < _tabIndicators.Length; i++)
            {
                bool active = i == (int)tab;
                if (_tabIndicators[i] != null)
                    _tabIndicators[i].enabled = active;
                if (_tabLabels[i] != null)
                    _tabLabels[i].color = active
                        ? new Color(0.98f, 0.55f, 0.50f, 1f)
                        : new Color(0.95f, 0.92f, 0.86f, 1f);
            }
            PopulateGrid(tab);
        }

        private void PopulateGrid(Tab tab)
        {
            // Clear old cells.
            for (int i = 0; i < _cells.Count; i++) Destroy(_cells[i]);
            _cells.Clear();

            IReadOnlyList<MockCollectionData.Entry> source;
            string unlockKey;
            switch (tab)
            {
                case Tab.Classes:
                    source = MockCollectionData.Classes;
                    unlockKey = NightDashUIIcons.UnlockClass;
                    break;
                case Tab.Stages:
                    source = MockCollectionData.Stages;
                    unlockKey = NightDashUIIcons.UnlockStage;
                    break;
                case Tab.Relics:
                default:
                    source = MockCollectionData.Relics;
                    unlockKey = NightDashUIIcons.UnlockRelic;
                    break;
            }

            for (int i = 0; i < source.Count; i++)
            {
                _cells.Add(BuildCell(source[i], unlockKey));
            }
        }

        private GameObject BuildCell(MockCollectionData.Entry entry, string unlockKey)
        {
            var go = new GameObject($"Cell_{entry.Id}",
                typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_grid, false);
            var bg = go.GetComponent<Image>();
            bg.color = entry.Unlocked
                ? new Color(0.14f, 0.10f, 0.12f, 0.95f)
                : new Color(0.06f, 0.05f, 0.06f, 0.92f);
            bg.raycastTarget = false;

            // Body icon. Locked → generic lock glyph. Unlocked classes get
            // their unique passive icon (when shipped), otherwise the generic
            // unlock_class badge. Stages and relics keep their category badge.
            Sprite resolvedSprite = null;
            if (entry.Unlocked)
            {
                if (unlockKey == NightDashUIIcons.UnlockClass)
                {
                    var registry = DataRegistry.Instance;
                    if (registry != null && registry.TryGetClass(entry.Id, out NightDash.Data.ClassData klass) && klass != null)
                    {
                        string passiveId = !string.IsNullOrEmpty(klass.uniquePassiveId)
                            ? klass.uniquePassiveId
                            : (klass.startingPassive != null ? klass.startingPassive.id : null);
                        if (!string.IsNullOrEmpty(passiveId))
                        {
                            if (registry.TryGetPassive(passiveId, out NightDash.Data.PassiveData passive) && passive != null && passive.icon != null)
                            {
                                resolvedSprite = passive.icon;
                            }
                            if (resolvedSprite == null)
                            {
                                resolvedSprite = NightDashUIIcons.GetPassive(passiveId);
                            }
                        }
                    }
                }
                if (resolvedSprite == null) resolvedSprite = NightDashUIIcons.Get(unlockKey);
            }
            else
            {
                resolvedSprite = NightDashUIIcons.Get(NightDashUIIcons.Lock);
            }

            RectTransform iconRect = null;
            if (resolvedSprite != null)
            {
                var iconGo = new GameObject("BadgeIcon",
                    typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                iconRect = (RectTransform)iconGo.transform;
                iconRect.sizeDelta = new Vector2(112f, 112f);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = resolvedSprite;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }
            if (iconRect != null)
            {
                iconRect.anchorMin = new Vector2(0.5f, 1f);
                iconRect.anchorMax = new Vector2(0.5f, 1f);
                iconRect.pivot = new Vector2(0.5f, 1f);
                iconRect.anchoredPosition = new Vector2(0f, -16f);
                // Dim locked icons so the row reads "not earned yet".
                var img = iconRect.GetComponent<Image>();
                if (img != null && !entry.Unlocked)
                    img.color = new Color(0.55f, 0.55f, 0.55f, 0.95f);
            }

            // Name label
            var labelGo = new GameObject("Name",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            labelGo.transform.SetParent(go.transform, false);
            var lr = (RectTransform)labelGo.transform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0f);
            lr.pivot = new Vector2(0.5f, 0f);
            lr.anchoredPosition = new Vector2(0f, 16f);
            lr.sizeDelta = new Vector2(0f, 70f);
            var lt = labelGo.GetComponent<Text>();
            lt.text = entry.Unlocked ? entry.DisplayName : "???";
            lt.alignment = TextAnchor.MiddleCenter;
            lt.fontSize = 28;
            lt.color = entry.Unlocked
                ? new Color(0.95f, 0.92f, 0.86f, 1f)
                : new Color(0.55f, 0.55f, 0.60f, 1f);
            lt.font = NightDashUIFonts.Arcade;
            lt.raycastTarget = false;
            lt.horizontalOverflow = HorizontalWrapMode.Overflow;
            lt.verticalOverflow = VerticalWrapMode.Overflow;
            var lo = labelGo.GetComponent<Outline>();
            lo.effectColor = new Color(0f, 0f, 0f, 0.9f);
            lo.effectDistance = new Vector2(1.5f, -1.5f);

            return go;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) StepTab(-1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) StepTab(+1);
#else
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) StepTab(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) StepTab(+1);
#endif
        }

        private void StepTab(int delta)
        {
            int n = 3;
            int next = ((int)_currentTab + delta + n) % n;
            SetTab((Tab)next);
        }

        private void Close()
        {
            gameObject.SetActive(false);
            Action cb = _onClosed;
            _onClosed = null;
            cb?.Invoke();
        }
    }
}
