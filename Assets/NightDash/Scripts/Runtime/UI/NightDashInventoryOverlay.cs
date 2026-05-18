// In-game inventory overlay.
//
// Tab toggles a pause-style panel showing every weapon + passive the player
// owns. The overlay halts gameplay (Time.timeScale = 0) while it's open so
// the player can read at leisure. Keyboard navigation:
//   ←/→ : switch column (weapons ↔ passives)
//   ↑/↓ : move row inside the active column
//   Tab / ESC : close
//
// Each row shows icon + name + level. The currently-focused row is
// highlighted and its description renders in a detail strip at the bottom.

using System.Collections.Generic;
using NightDash.Data;
using NightDash.ECS.Components;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class NightDashInventoryOverlay : MonoBehaviour
    {
        private const int SortOrder = 5300;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            if (FindFirstObjectByType<NightDashInventoryOverlay>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("NightDashInventoryOverlay");
            go.AddComponent<NightDashInventoryOverlay>();
        }

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _panel;
        private RectTransform _weaponsContent;
        private RectTransform _passivesContent;
        private Text _detailDescriptionText;
        private Text _detailNameText;
        private Image _detailIcon;
        private RectTransform _detailHost;
        private ScrollRect _descScroll;
        private RectTransform _descScrollRect;
        // Auto-pan state for long descriptions. Reset on every focus change
        // and ticked in Update so a 3+ line blurb scrolls itself top→bottom
        // without the player needing the mouse wheel.
        private float _descPanT;
        private float _descPanOverflowPx;
        private const float DescPanPxPerSec  = 28f;
        private const float DescPanHoldTopS  = 1.4f;
        private const float DescPanHoldBotS  = 0.9f;
        private Text _columnTitleWeapons;
        private Text _columnTitlePassives;
        private bool _open;
        private float _restoreTimeScale = 1f;

        private static NightDashInventoryOverlay s_Instance;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_Instance = null;

        public static bool IsOpen => s_Instance != null && s_Instance._open;

        // Per-row binding so navigation can re-color the highlight + read the
        // description on focus changes without re-querying ECS.
        private readonly List<RowBinding> _weaponRows = new();
        private readonly List<RowBinding> _passiveRows = new();
        private World _queryWorld;
        private EntityQuery _ownedWeaponQuery;
        private EntityQuery _ownedPassiveQuery;
        // Focus state: which column (0=weapons, 1=passives), and which row.
        private int _focusColumn;
        private int _focusRow;

        private struct RowBinding
        {
            public GameObject Go;
            public Image Background;
            public Image Icon;
            public Text NameText;
            public Text LevelText;
            // Detail payload captured at populate time so the highlight
            // change can update the description with zero ECS work.
            public string Name;
            public string Description;
            public Sprite Sprite;
        }

        private void Awake()
        {
            s_Instance = this;
            BuildCanvas();
            BuildPanel();
            _group.alpha = 0f;
            _panel.gameObject.SetActive(false);
        }

        private void BuildCanvas()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();
        }

        private void BuildPanel()
        {
            var backdropGo = new GameObject("Backdrop",
                typeof(RectTransform), typeof(Image), typeof(Button));
            backdropGo.transform.SetParent(transform, false);
            var br = (RectTransform)backdropGo.transform;
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
            backdropGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
            backdropGo.GetComponent<Button>().onClick.AddListener(Close);

            var panelGo = new GameObject("Panel",
                typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(1500f, 920f);
            panelGo.GetComponent<Image>().color = new Color(0.30f, 0.12f, 0.14f, 0.95f);

            var fillGo = new GameObject("Fill",
                typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_panel, false);
            var fr = (RectTransform)fillGo.transform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(3f, 3f); fr.offsetMax = new Vector2(-3f, -3f);
            fillGo.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.06f, 0.96f);

            BuildHeader();
            _weaponsContent = BuildColumn(_panel, 0, "WEAPONS", new Vector2(0f, 0f), new Vector2(0.5f, 1f),
                out _columnTitleWeapons);
            _passivesContent = BuildColumn(_panel, 1, "PASSIVES", new Vector2(0.5f, 0f), new Vector2(1f, 1f),
                out _columnTitlePassives);
            BuildDetailStrip();
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
            r.anchoredPosition = new Vector2(0f, -28f);
            r.sizeDelta = new Vector2(-40f, 88f);
            var t = go.GetComponent<Text>();
            t.text = "INVENTORY";
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 64;
            t.color = new Color(0.92f, 0.78f, 0.50f, 1f);
            t.font = NightDashUIFonts.Arcade;
            t.raycastTarget = false;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.95f);
            o.effectDistance = new Vector2(2f, -2f);
        }

        private RectTransform BuildColumn(RectTransform host, int colIndex, string title,
            Vector2 anchorMin, Vector2 anchorMax, out Text titleText)
        {
            var colGo = new GameObject($"Col_{title}", typeof(RectTransform));
            colGo.transform.SetParent(host, false);
            var cr = (RectTransform)colGo.transform;
            cr.anchorMin = anchorMin;
            cr.anchorMax = anchorMax;
            // Leave room for the header at the top (140) and the detail
            // strip at the bottom (200) so this column only owns the middle.
            cr.offsetMin = new Vector2(40f, 200f);
            cr.offsetMax = new Vector2(-40f, -140f);

            var titleGo = new GameObject("Title",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            titleGo.transform.SetParent(cr, false);
            var tr = (RectTransform)titleGo.transform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = Vector2.zero;
            tr.sizeDelta = new Vector2(0f, 52f);
            titleText = titleGo.GetComponent<Text>();
            titleText.text = title;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontSize = 24;
            titleText.color = new Color(0.95f, 0.55f, 0.50f, 1f);
            titleText.font = NightDashUIFonts.Arcade;
            titleText.raycastTarget = false;
            var to = titleGo.GetComponent<Outline>();
            to.effectColor = new Color(0f, 0f, 0f, 0.9f);
            to.effectDistance = new Vector2(1.5f, -1.5f);

            var scrollGo = new GameObject("Scroll",
                typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(cr, false);
            var sr = (RectTransform)scrollGo.transform;
            sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 1f);
            sr.offsetMin = Vector2.zero;
            sr.offsetMax = new Vector2(0f, -60f);
            scrollGo.GetComponent<Image>().color = new Color(0.04f, 0.03f, 0.03f, 0.85f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewportGo = new GameObject("Viewport",
                typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(sr, false);
            var vr = (RectTransform)viewportGo.transform;
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = new Vector2(10f, 10f); vr.offsetMax = new Vector2(-10f, -10f);
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = true;

            var contentGo = new GameObject("Content",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(vr, false);
            var ctr = (RectTransform)contentGo.transform;
            ctr.anchorMin = new Vector2(0f, 1f); ctr.anchorMax = new Vector2(1f, 1f);
            ctr.pivot = new Vector2(0.5f, 1f);
            ctr.anchoredPosition = Vector2.zero;
            ctr.sizeDelta = new Vector2(0f, 0f);
            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vr;
            scroll.content = ctr;
            return ctr;
        }

        // Bottom-of-panel detail strip — icon + name + description. Updates
        // every time the highlighted row changes.
        //
        // Strip is sized so the description region fits ~3 lines at font 22;
        // if the text exceeds that, the ScrollRect lets the player wheel
        // through it. Without the ScrollRect a long description used to spill
        // out of the strip with VerticalWrapMode.Overflow.
        private void BuildDetailStrip()
        {
            var go = new GameObject("Detail",
                typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_panel, false);
            _detailHost = (RectTransform)go.transform;
            _detailHost.anchorMin = new Vector2(0f, 0f);
            _detailHost.anchorMax = new Vector2(1f, 0f);
            _detailHost.pivot = new Vector2(0.5f, 0f);
            _detailHost.anchoredPosition = new Vector2(0f, 70f);
            // Height = name row (~40) + description viewport (80 = 3 lines of
            // 24px + small padding) + outer padding.
            _detailHost.sizeDelta = new Vector2(-80f, 150f);
            go.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.10f, 0.95f);

            var iconGo = new GameObject("Icon",
                typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_detailHost, false);
            var ir = (RectTransform)iconGo.transform;
            ir.anchorMin = new Vector2(0f, 0.5f); ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0f, 0.5f);
            ir.anchoredPosition = new Vector2(16f, 0f);
            ir.sizeDelta = new Vector2(80f, 80f);
            _detailIcon = iconGo.GetComponent<Image>();
            _detailIcon.preserveAspect = true;
            _detailIcon.raycastTarget = false;

            // Name pinned to the TOP of the strip at a fixed height. The
            // description scroll below claims the rest with a fixed 80px
            // viewport — keeping these as explicit pixel boxes (instead of
            // percent splits) is what makes "exactly 3 lines visible" hold
            // regardless of font line-metrics quirks.
            var nameGo = new GameObject("Name",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            nameGo.transform.SetParent(_detailHost, false);
            var nr = (RectTransform)nameGo.transform;
            nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.offsetMin = new Vector2(112f, -50f);
            nr.offsetMax = new Vector2(-20f, -8f);
            _detailNameText = nameGo.GetComponent<Text>();
            _detailNameText.alignment = TextAnchor.MiddleLeft;
            _detailNameText.fontSize = 24;
            _detailNameText.color = new Color(0.95f, 0.55f, 0.50f, 1f);
            _detailNameText.font = NightDashUIFonts.Arcade;
            _detailNameText.raycastTarget = false;
            _detailNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var no = nameGo.GetComponent<Outline>();
            no.effectColor = new Color(0f, 0f, 0f, 0.95f);
            no.effectDistance = new Vector2(2f, -2f);

            // Description ScrollRect — anchored to the BOTTOM of the strip
            // with a FIXED 80px height so it always shows exactly 3 lines
            // at fontSize 24 (24×3 = 72, plus 8px breathing room). Anything
            // beyond line 3 overflows and the auto-pan tick scrolls it.
            var dscrollGo = new GameObject("DescScroll",
                typeof(RectTransform), typeof(ScrollRect));
            dscrollGo.transform.SetParent(_detailHost, false);
            _descScrollRect = (RectTransform)dscrollGo.transform;
            _descScrollRect.anchorMin = new Vector2(0f, 0f);
            _descScrollRect.anchorMax = new Vector2(1f, 0f);
            _descScrollRect.pivot = new Vector2(0.5f, 0f);
            _descScrollRect.offsetMin = new Vector2(112f, 8f);
            _descScrollRect.offsetMax = new Vector2(-20f, 88f);
            var dscroll = dscrollGo.GetComponent<ScrollRect>();
            dscroll.horizontal = false;
            dscroll.vertical = true;
            dscroll.movementType = ScrollRect.MovementType.Clamped;
            dscroll.scrollSensitivity = 24f;
            _descScroll = dscroll;

            var dviewGo = new GameObject("Viewport",
                typeof(RectTransform), typeof(RectMask2D));
            dviewGo.transform.SetParent(_descScrollRect, false);
            var dviewR = (RectTransform)dviewGo.transform;
            dviewR.anchorMin = Vector2.zero; dviewR.anchorMax = Vector2.one;
            dviewR.offsetMin = Vector2.zero; dviewR.offsetMax = Vector2.zero;

            var dcontGo = new GameObject("Content",
                typeof(RectTransform), typeof(ContentSizeFitter), typeof(VerticalLayoutGroup));
            dcontGo.transform.SetParent(dviewR, false);
            var dcontR = (RectTransform)dcontGo.transform;
            dcontR.anchorMin = new Vector2(0f, 1f); dcontR.anchorMax = new Vector2(1f, 1f);
            dcontR.pivot = new Vector2(0.5f, 1f);
            dcontR.anchoredPosition = Vector2.zero;
            dcontR.sizeDelta = Vector2.zero;
            var dfit = dcontGo.GetComponent<ContentSizeFitter>();
            dfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var dlayout = dcontGo.GetComponent<VerticalLayoutGroup>();
            dlayout.padding = new RectOffset(0, 0, 0, 0);
            dlayout.spacing = 0f;
            dlayout.childForceExpandWidth = true;
            dlayout.childForceExpandHeight = false;
            dlayout.childControlWidth = true;
            dlayout.childControlHeight = true;

            var descGo = new GameObject("Description",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            descGo.transform.SetParent(dcontR, false);
            _detailDescriptionText = descGo.GetComponent<Text>();
            _detailDescriptionText.alignment = TextAnchor.UpperLeft;
            _detailDescriptionText.fontSize = 24;
            // Pin lineSpacing so the viewport-height math (24 × 3 = 72) is
            // independent of the font's intrinsic line metrics.
            _detailDescriptionText.lineSpacing = 1f;
            _detailDescriptionText.supportRichText = false;
            _detailDescriptionText.color = new Color(0.90f, 0.86f, 0.76f, 1f);
            _detailDescriptionText.font = NightDashUIFonts.Arcade;
            _detailDescriptionText.raycastTarget = false;
            _detailDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
            var dro = descGo.GetComponent<Outline>();
            dro.effectColor = new Color(0f, 0f, 0f, 0.9f);
            dro.effectDistance = new Vector2(1.5f, -1.5f);

            dscroll.viewport = dviewR;
            dscroll.content = dcontR;
        }

        private void BuildHint()
        {
            var go = new GameObject("Hint",
                typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_panel, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.5f, 0f); r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0f, 20f);
            r.sizeDelta = new Vector2(1200f, 36f);
            var t = go.GetComponent<Text>();
            t.text = "←/→  COLUMN     ↑/↓  SELECT     [TAB] / [ESC]  CLOSE";
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 24;
            t.color = new Color(0.80f, 0.74f, 0.62f, 1f);
            t.font = NightDashUIFonts.Arcade;
            t.raycastTarget = false;
        }

        // ------------------------------------------------------------------ open / close
        private void Open()
        {
            _open = true;
            _panel.gameObject.SetActive(true);
            _group.alpha = 1f;
            // Pause gameplay while the inventory is up — same idea as the
            // pause menu but without the menu chrome. Time.timeScale is
            // restored on Close.
            _restoreTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            RebuildContent();
            // Default focus = first weapon row (or first passive if no weapons).
            _focusColumn = (_weaponRows.Count > 0) ? 0 : 1;
            _focusRow = 0;
            ApplyFocusVisuals();
        }

        private void Close()
        {
            _open = false;
            _group.alpha = 0f;
            _panel.gameObject.SetActive(false);
            // Only restore timeScale if we were the ones who paused it. If a
            // pause menu opened on top of us it owns the scale now.
            if (Time.timeScale == 0f) Time.timeScale = _restoreTimeScale;
        }

        private void Update()
        {
            NightDashInputContext ctx = NightDashInputContextStack.Top;
            bool inGame = ctx == NightDashInputContext.Playing;

            bool suspended = NightDashSettingsModal.IsOpen
                || NightDashCollectionScreen.IsOpen
                || NightDashSaveSlotModal.IsOpen;
            if (suspended)
            {
                if (_open) Close();
                return;
            }

            if (!inGame)
            {
                if (_open) Close();
                return;
            }

            if (TabPressedThisFrame()) { if (_open) Close(); else Open(); return; }
            if (!_open) return;

            if (EscPressedThisFrame()) { Close(); return; }

            HandleNavigation();
            TickDescAutoPan();
        }

        private void HandleNavigation()
        {
            int colDelta = 0, rowDelta = 0;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)  colDelta = -1;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) colDelta = +1;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)    rowDelta = -1;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)  rowDelta = +1;
#else
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))  colDelta = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) colDelta = +1;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))    rowDelta = -1;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))  rowDelta = +1;
#endif
            if (colDelta == 0 && rowDelta == 0) return;

            if (colDelta != 0)
            {
                int target = Mathf.Clamp(_focusColumn + colDelta, 0, 1);
                if (target != _focusColumn)
                {
                    _focusColumn = target;
                    // Clamp row to the new column's active count.
                    int n = ActiveCount(_focusColumn);
                    if (n == 0) { /* nothing on this side, keep focus row */ }
                    else if (_focusRow >= n) _focusRow = n - 1;
                }
            }
            if (rowDelta != 0)
            {
                int n = ActiveCount(_focusColumn);
                if (n > 0)
                {
                    _focusRow = Mathf.Clamp(_focusRow + rowDelta, 0, n - 1);
                }
            }
            ApplyFocusVisuals();
        }

        private int ActiveCount(int column)
        {
            var pool = column == 0 ? _weaponRows : _passiveRows;
            int n = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Go != null && pool[i].Go.activeSelf) n++;
            }
            return n;
        }

        // Refreshes background tint for every row + populates the detail
        // strip with the focused row's payload.
        private void ApplyFocusVisuals()
        {
            TintColumn(_weaponRows, _focusColumn == 0);
            TintColumn(_passiveRows, _focusColumn == 1);
            _columnTitleWeapons.color = _focusColumn == 0
                ? new Color(0.98f, 0.55f, 0.50f, 1f)
                : new Color(0.65f, 0.45f, 0.45f, 1f);
            _columnTitlePassives.color = _focusColumn == 1
                ? new Color(0.98f, 0.55f, 0.50f, 1f)
                : new Color(0.65f, 0.45f, 0.45f, 1f);

            var pool = _focusColumn == 0 ? _weaponRows : _passiveRows;
            if (_focusRow >= 0 && _focusRow < pool.Count && pool[_focusRow].Go != null && pool[_focusRow].Go.activeSelf)
            {
                RowBinding focus = pool[_focusRow];
                _detailNameText.text = focus.Name ?? string.Empty;
                _detailDescriptionText.text = focus.Description ?? string.Empty;
                _detailIcon.sprite = focus.Sprite;
                _detailIcon.enabled = focus.Sprite != null;
            }
            else
            {
                _detailNameText.text = string.Empty;
                _detailDescriptionText.text = string.Empty;
                _detailIcon.sprite = null;
                _detailIcon.enabled = false;
            }

            // Snap row list so the focused row is fully visible, and reset
            // the description scroll so a newly-focused entry starts at the
            // top of its blurb instead of mid-paragraph.
            EnsureFocusedRowVisible();
            ResetDescAutoPan();
        }

        // Recomputes how much description text actually overflows the
        // viewport, and rewinds the auto-pan cycle to the top. Called on
        // every focus change so each entry starts reading from line 1.
        private void ResetDescAutoPan()
        {
            _descPanT = 0f;
            _descPanOverflowPx = 0f;
            if (_descScroll == null || _descScroll.content == null || _descScroll.viewport == null) return;

            // ContentSizeFitter recomputes on the next layout pass; force it
            // now so we can read the true overflow this frame.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_descScroll.content);

            float contentH = _descScroll.content.rect.height;
            float viewportH = _descScroll.viewport.rect.height;
            _descPanOverflowPx = Mathf.Max(0f, contentH - viewportH);

            // Park at the top regardless — if there's no overflow this is a
            // no-op; if there is, the pan tick will progress from here.
            var ap = _descScroll.content.anchoredPosition;
            ap.y = 0f;
            _descScroll.content.anchoredPosition = ap;
            _descScroll.velocity = Vector2.zero;
        }

        // Ticked from Update while the overlay is open. Cycle:
        //   hold-top → pan-down (linear) → hold-bottom → snap back to top.
        // Uses unscaledDeltaTime because gameplay is paused (Time.timeScale=0)
        // while the inventory is up.
        private void TickDescAutoPan()
        {
            if (_descScroll == null || _descScroll.content == null) return;
            if (_descPanOverflowPx <= 0.5f) return;

            _descPanT += Time.unscaledDeltaTime;

            float panSec = _descPanOverflowPx / DescPanPxPerSec;
            float cycleSec = DescPanHoldTopS + panSec + DescPanHoldBotS;
            if (cycleSec <= 0f) return;
            float t = _descPanT % cycleSec;

            float scrollPx;
            if (t < DescPanHoldTopS)
            {
                scrollPx = 0f;
            }
            else if (t < DescPanHoldTopS + panSec)
            {
                scrollPx = (t - DescPanHoldTopS) * DescPanPxPerSec;
            }
            else
            {
                scrollPx = _descPanOverflowPx;
            }

            var ap = _descScroll.content.anchoredPosition;
            ap.y = scrollPx;
            _descScroll.content.anchoredPosition = ap;
            // Suppress any drag-velocity carry-over so the next frame doesn't
            // fight our anchored write.
            _descScroll.velocity = Vector2.zero;
        }

        // Adjusts the active column's ScrollRect so the focused row is fully
        // visible. Without this, the player can press ↓ past the bottom of
        // the viewport and only see the partial sliver of the last row.
        private void EnsureFocusedRowVisible()
        {
            var pool = _focusColumn == 0 ? _weaponRows : _passiveRows;
            if (_focusRow < 0 || _focusRow >= pool.Count) return;
            RowBinding binding = pool[_focusRow];
            if (binding.Go == null || !binding.Go.activeSelf) return;

            var rowRect = (RectTransform)binding.Go.transform;
            ScrollRect scroll = rowRect.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.viewport == null || scroll.content == null) return;

            // VerticalLayoutGroup defers placement; force a layout pass so
            // we sample current world positions rather than last frame's.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);

            RectTransform content = scroll.content;
            RectTransform viewport = scroll.viewport;
            float contentH = content.rect.height;
            float viewportH = viewport.rect.height;
            float scrollable = contentH - viewportH;
            if (scrollable <= 0.5f) return; // nothing to scroll

            // Convert row center into content-local space; content's pivot is
            // (0.5, 1) so its local +Y is "up from top edge". Depth below the
            // top edge = -localY.
            Vector3 rowCenterWorld = rowRect.TransformPoint(rowRect.rect.center);
            Vector3 rowCenterLocal = content.InverseTransformPoint(rowCenterWorld);
            float rowH = rowRect.rect.height;
            float rowTopDepth = -(rowCenterLocal.y + rowH * 0.5f);
            float rowBotDepth = -(rowCenterLocal.y - rowH * 0.5f);

            float ap = content.anchoredPosition.y;
            // Visible window in "depth from content top" coordinates.
            float visTop = ap;
            float visBot = ap + viewportH;
            const float margin = 4f;

            float newAp = ap;
            if (rowTopDepth < visTop + margin)
            {
                newAp = rowTopDepth - margin;
            }
            else if (rowBotDepth > visBot - margin)
            {
                newAp = rowBotDepth - viewportH + margin;
            }
            newAp = Mathf.Clamp(newAp, 0f, scrollable);

            if (Mathf.Abs(newAp - ap) > 0.1f)
            {
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, newAp);
            }
        }

        private void TintColumn(List<RowBinding> pool, bool isFocusedColumn)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Background == null) continue;
                bool focusedRow = isFocusedColumn && i == _focusRow && pool[i].Go.activeSelf;
                pool[i].Background.color = focusedRow
                    ? new Color(0.36f, 0.12f, 0.14f, 0.96f)
                    : new Color(0.14f, 0.10f, 0.12f, 0.88f);
            }
        }

        private static bool TabPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.tabKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Tab);
#endif
        }

        private static bool EscPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        // ------------------------------------------------------------------ content
        private void RebuildContent()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) { HideAllRows(); return; }
            EntityManager em = world.EntityManager;
            EnsureQueriesFor(world, em);

            DataRegistry registry = DataRegistry.Instance;

            // Weapons
            DynamicBuffer<OwnedWeaponElement> weapons = default;
            bool hasWeapons = false;
            if (!_ownedWeaponQuery.IsEmptyIgnoreFilter)
            {
                var ent = _ownedWeaponQuery.GetSingletonEntity();
                if (em.HasBuffer<OwnedWeaponElement>(ent))
                {
                    weapons = em.GetBuffer<OwnedWeaponElement>(ent);
                    hasWeapons = true;
                }
            }
            int weaponCount = hasWeapons ? weapons.Length : 0;
            EnsureRowPool(_weaponRows, _weaponsContent, weaponCount);
            for (int i = 0; i < weaponCount; i++)
            {
                OwnedWeaponElement w = weapons[i];
                string id = w.Id.ToString();
                string display = id;
                string desc = string.Empty;
                Sprite sprite = null;
                if (registry != null && registry.TryGetWeapon(id, out WeaponData wd) && wd != null)
                {
                    if (!string.IsNullOrEmpty(wd.displayName)) display = wd.displayName;
                    if (wd.icon != null) sprite = wd.icon;
                    // WeaponData has no description field today — fall back
                    // to the id so the detail strip isn't empty.
                    desc = $"기본 쿨다운 {wd.baseCooldown:0.0}s · 사거리 {wd.baseRange:0.0}";
                }
                PopulateRow(_weaponRows, i, display, $"Lv.{w.Level}/{w.MaxLevel}", sprite, desc);
            }

            // Passives
            DynamicBuffer<OwnedPassiveElement> passives = default;
            bool hasPassives = false;
            if (!_ownedPassiveQuery.IsEmptyIgnoreFilter)
            {
                var ent = _ownedPassiveQuery.GetSingletonEntity();
                if (em.HasBuffer<OwnedPassiveElement>(ent))
                {
                    passives = em.GetBuffer<OwnedPassiveElement>(ent);
                    hasPassives = true;
                }
            }
            int passiveCount = hasPassives ? passives.Length : 0;
            EnsureRowPool(_passiveRows, _passivesContent, passiveCount);
            for (int i = 0; i < passiveCount; i++)
            {
                OwnedPassiveElement p = passives[i];
                string id = p.Id.ToString();
                string display = id;
                string desc = string.Empty;
                Sprite sprite = null;
                if (registry != null && registry.TryGetPassive(id, out PassiveData pd) && pd != null)
                {
                    if (!string.IsNullOrEmpty(pd.displayName)) display = pd.displayName;
                    if (pd.icon != null) sprite = pd.icon;
                    desc = pd.description ?? string.Empty;
                }
                if (sprite == null) sprite = NightDashUIIcons.GetPassive(id);
                PopulateRow(_passiveRows, i, display, $"Lv.{p.Level}/{p.MaxLevel}", sprite, desc);
            }
        }

        private void EnsureQueriesFor(World world, EntityManager em)
        {
            if (_queryWorld == world) return;
            _queryWorld = world;
            _ownedWeaponQuery = em.CreateEntityQuery(ComponentType.ReadOnly<OwnedWeaponElement>());
            _ownedPassiveQuery = em.CreateEntityQuery(ComponentType.ReadOnly<OwnedPassiveElement>());
        }

        private void OnDestroy()
        {
            if (_queryWorld != null && _queryWorld.IsCreated)
            {
                _ownedWeaponQuery.Dispose();
                _ownedPassiveQuery.Dispose();
            }
        }

        private void EnsureRowPool(List<RowBinding> pool, RectTransform parent, int desired)
        {
            while (pool.Count < desired)
            {
                pool.Add(CreateRow(parent));
            }
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].Go.SetActive(i < desired);
            }
        }

        private RowBinding CreateRow(RectTransform parent)
        {
            var rowGo = new GameObject("Row",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowGo.transform.SetParent(parent, false);
            var bg = rowGo.GetComponent<Image>();
            bg.color = new Color(0.14f, 0.10f, 0.12f, 0.88f);
            bg.raycastTarget = false;
            var le = rowGo.GetComponent<LayoutElement>();
            le.preferredHeight = 86f;
            le.minHeight = 86f;

            var iconGo = new GameObject("Icon",
                typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(rowGo.transform, false);
            var ir = (RectTransform)iconGo.transform;
            ir.anchorMin = new Vector2(0f, 0.5f);
            ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0f, 0.5f);
            ir.anchoredPosition = new Vector2(14f, 0f);
            ir.sizeDelta = new Vector2(56f, 56f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Row body split into two lines so long Korean names don't get
            // truncated by the level chip on the same baseline:
            //   top half  →  display name (full width minus icon)
            //   bot half  →  "Lv.X/Y"
            var nameGo = new GameObject("Name",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            nameGo.transform.SetParent(rowGo.transform, false);
            var nr = (RectTransform)nameGo.transform;
            nr.anchorMin = new Vector2(0f, 0.5f); nr.anchorMax = new Vector2(1f, 1f);
            nr.offsetMin = new Vector2(80f, 0f);
            nr.offsetMax = new Vector2(-14f, -2f);
            var nameText = nameGo.GetComponent<Text>();
            nameText.alignment = TextAnchor.LowerLeft;
            nameText.fontSize = 24;
            nameText.color = new Color(0.95f, 0.92f, 0.86f, 1f);
            nameText.font = NightDashUIFonts.Arcade;
            nameText.raycastTarget = false;
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var no = nameGo.GetComponent<Outline>();
            no.effectColor = new Color(0f, 0f, 0f, 0.9f);
            no.effectDistance = new Vector2(1.5f, -1.5f);

            var lvGo = new GameObject("Level",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            lvGo.transform.SetParent(rowGo.transform, false);
            var lr = (RectTransform)lvGo.transform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0.5f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.offsetMin = new Vector2(80f, 2f);
            lr.offsetMax = new Vector2(-14f, 0f);
            var lvText = lvGo.GetComponent<Text>();
            lvText.alignment = TextAnchor.UpperLeft;
            lvText.fontSize = 24;
            lvText.color = new Color(0.95f, 0.55f, 0.50f, 1f);
            lvText.font = NightDashUIFonts.Arcade;
            lvText.raycastTarget = false;
            var lo = lvGo.GetComponent<Outline>();
            lo.effectColor = new Color(0f, 0f, 0f, 0.9f);
            lo.effectDistance = new Vector2(1.5f, -1.5f);

            return new RowBinding
            {
                Go = rowGo,
                Background = bg,
                Icon = iconImg,
                NameText = nameText,
                LevelText = lvText,
            };
        }

        private void PopulateRow(List<RowBinding> pool, int index, string name, string level, Sprite icon, string description)
        {
            RowBinding row = pool[index];
            if (row.NameText != null) row.NameText.text = name;
            if (row.LevelText != null) row.LevelText.text = level;
            if (row.Icon != null)
            {
                row.Icon.sprite = icon;
                row.Icon.enabled = icon != null;
            }
            // Cache detail payload on the row binding so navigation can pull
            // it without re-querying ECS each time.
            row.Name = name;
            row.Description = description;
            row.Sprite = icon;
            pool[index] = row;
        }

        private void HideAllRows()
        {
            for (int i = 0; i < _weaponRows.Count; i++) _weaponRows[i].Go.SetActive(false);
            for (int i = 0; i < _passiveRows.Count; i++) _passiveRows[i].Go.SetActive(false);
        }
    }
}
