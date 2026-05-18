// Hold-to-show class info overlay.
//
// Tab is held → the overlay slides in at the center of the screen with the
// active class's name + full passive description. Releasing Tab fades it
// back out. The bridge owns its own Canvas so the overlay layers above the
// HUD but below pause / settings / collection modals.

using NightDash.Data;
using NightDash.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime
{
    [DisallowMultipleComponent]
    public sealed class NightDashClassInfoOverlay : MonoBehaviour
    {
        // Sort order — above HUD (4500) and pause hint canvases (~4700) but
        // strictly below the pause menu / settings / collection stacks (5500+).
        private const int SortOrder = 5200;

        // Fade in fast so the player gets the info immediately; fade out a
        // touch slower so a quick release doesn't feel jittery.
        private const float FadeInSec = 0.10f;
        private const float FadeOutSec = 0.18f;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            if (FindFirstObjectByType<NightDashClassInfoOverlay>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("NightDashClassInfoOverlay");
            go.AddComponent<NightDashClassInfoOverlay>();
        }

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _panel;
        private Image _iconImage;
        private Text _classNameText;
        private Text _passiveNameText;
        private Text _descriptionText;
        private Text _hintText;
        private string _renderedClassId;
        private float _alphaTarget;

        private void Awake()
        {
            BuildCanvas();
            BuildPanel();
            _group.alpha = 0f;
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

            // GraphicRaycaster intentionally omitted — the overlay never
            // accepts pointer events. It's a passive readout.

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void BuildPanel()
        {
            // Centered card. Burgundy ring + dark fill mirror the rest of the
            // foreground modals (Settings, Collection, SaveSlot).
            var panelGo = new GameObject("Panel",
                typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(1200f, 600f);
            var ring = panelGo.GetComponent<Image>();
            ring.color = new Color(0.30f, 0.12f, 0.14f, 0.95f);
            ring.raycastTarget = false;

            var fillGo = new GameObject("Fill",
                typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_panel, false);
            var fr = (RectTransform)fillGo.transform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(3f, 3f); fr.offsetMax = new Vector2(-3f, -3f);
            var fill = fillGo.GetComponent<Image>();
            fill.color = new Color(0.06f, 0.05f, 0.06f, 0.96f);
            fill.raycastTarget = false;

            // Class name header (top).
            var classNameGo = new GameObject("ClassName",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            classNameGo.transform.SetParent(_panel, false);
            var cr = (RectTransform)classNameGo.transform;
            cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = new Vector2(0f, -24f);
            cr.sizeDelta = new Vector2(-40f, 56f);
            _classNameText = classNameGo.GetComponent<Text>();
            _classNameText.alignment = TextAnchor.MiddleCenter;
            _classNameText.fontSize = 52;
            _classNameText.color = new Color(0.96f, 0.90f, 0.74f, 1f);
            _classNameText.font = NightDashUIFonts.Arcade;
            _classNameText.raycastTarget = false;
            _classNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var co = classNameGo.GetComponent<Outline>();
            co.effectColor = new Color(0f, 0f, 0f, 0.95f);
            co.effectDistance = new Vector2(2f, -2f);

            // Passive icon (left side, large).
            var iconGo = new GameObject("PassiveIcon",
                typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_panel, false);
            var ir = (RectTransform)iconGo.transform;
            ir.anchorMin = new Vector2(0f, 0.5f); ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0f, 0.5f);
            ir.anchoredPosition = new Vector2(48f, -10f);
            ir.sizeDelta = new Vector2(200f, 200f);
            _iconImage = iconGo.GetComponent<Image>();
            _iconImage.preserveAspect = true;
            _iconImage.raycastTarget = false;

            // Passive name (right of icon, top half).
            var passiveNameGo = new GameObject("PassiveName",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            passiveNameGo.transform.SetParent(_panel, false);
            var pnr = (RectTransform)passiveNameGo.transform;
            pnr.anchorMin = new Vector2(0f, 0.5f); pnr.anchorMax = new Vector2(1f, 1f);
            pnr.offsetMin = new Vector2(272f, 0f);
            pnr.offsetMax = new Vector2(-40f, -110f);
            _passiveNameText = passiveNameGo.GetComponent<Text>();
            _passiveNameText.alignment = TextAnchor.LowerLeft;
            _passiveNameText.fontSize = 40;
            _passiveNameText.color = new Color(0.95f, 0.55f, 0.50f, 1f);
            _passiveNameText.font = NightDashUIFonts.Arcade;
            _passiveNameText.raycastTarget = false;
            _passiveNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var pno = passiveNameGo.GetComponent<Outline>();
            pno.effectColor = new Color(0f, 0f, 0f, 0.95f);
            pno.effectDistance = new Vector2(2f, -2f);

            // Description body (right of icon, bottom half).
            var descGo = new GameObject("Description",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            descGo.transform.SetParent(_panel, false);
            var dr = (RectTransform)descGo.transform;
            dr.anchorMin = new Vector2(0f, 0f); dr.anchorMax = new Vector2(1f, 0.5f);
            dr.offsetMin = new Vector2(272f, 70f);
            dr.offsetMax = new Vector2(-40f, -10f);
            _descriptionText = descGo.GetComponent<Text>();
            _descriptionText.alignment = TextAnchor.UpperLeft;
            _descriptionText.fontSize = 32;
            _descriptionText.color = new Color(0.90f, 0.86f, 0.76f, 1f);
            _descriptionText.font = NightDashUIFonts.Arcade;
            _descriptionText.raycastTarget = false;
            _descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
            var dro = descGo.GetComponent<Outline>();
            dro.effectColor = new Color(0f, 0f, 0f, 0.9f);
            dro.effectDistance = new Vector2(1.5f, -1.5f);

            // Footer hint.
            var hintGo = new GameObject("Hint",
                typeof(RectTransform), typeof(Text));
            hintGo.transform.SetParent(_panel, false);
            var hr = (RectTransform)hintGo.transform;
            hr.anchorMin = new Vector2(0.5f, 0f); hr.anchorMax = new Vector2(0.5f, 0f);
            hr.pivot = new Vector2(0.5f, 0f);
            hr.anchoredPosition = new Vector2(0f, 18f);
            hr.sizeDelta = new Vector2(800f, 32f);
            _hintText = hintGo.GetComponent<Text>();
            _hintText.alignment = TextAnchor.MiddleCenter;
            _hintText.fontSize = 26;
            _hintText.color = new Color(0.74f, 0.70f, 0.60f, 1f);
            _hintText.font = NightDashUIFonts.Arcade;
            _hintText.raycastTarget = false;
            _hintText.text = "HOLD [TAB] FOR CLASS INFO";
        }

        private void Update()
        {
            // Only the lobby (and title, while we still render character
            // cards there one day) shows the class info overlay. In-game
            // Tab is owned by NightDashInventoryOverlay.
            NightDashInputContext ctx = NightDashInputContextStack.Top;
            bool contextAllows = ctx == NightDashInputContext.Lobby || ctx == NightDashInputContext.Title;

            // Suspend while any foreground modal is up — Tab inside the
            // pause/settings/collection screens should not flash this overlay.
            if (!contextAllows
                || NightDashSettingsModal.IsOpen
                || NightDashCollectionScreen.IsOpen
                || NightDashSaveSlotModal.IsOpen)
            {
                _alphaTarget = 0f;
                ApplyAlpha();
                return;
            }

            bool held = IsTabHeld();
            _alphaTarget = held ? 1f : 0f;

            if (held)
            {
                EnsureContent();
            }
            ApplyAlpha();
        }

        private void EnsureContent()
        {
            RunSelectionSession.GetCurrent(out _, out string classId);
            if (string.IsNullOrEmpty(classId)) { ClearContent(); return; }

            // Skip the lookup churn when the class didn't change since the
            // previous Tab press.
            if (classId == _renderedClassId && _iconImage.sprite != null) return;
            _renderedClassId = classId;

            DataRegistry registry = DataRegistry.Instance;
            if (registry == null || !registry.TryGetClass(classId, out ClassData klass) || klass == null)
            {
                ClearContent();
                return;
            }

            _classNameText.text = string.IsNullOrEmpty(klass.displayName)
                ? klass.id
                : klass.displayName;

            string passiveId = !string.IsNullOrEmpty(klass.uniquePassiveId)
                ? klass.uniquePassiveId
                : (klass.startingPassive != null ? klass.startingPassive.id : null);

            if (!string.IsNullOrEmpty(passiveId)
                && registry.TryGetPassive(passiveId, out PassiveData passive)
                && passive != null)
            {
                _passiveNameText.text = string.IsNullOrEmpty(passive.displayName)
                    ? passiveId
                    : passive.displayName;
                _descriptionText.text = passive.description ?? string.Empty;
                _iconImage.sprite = passive.icon != null
                    ? passive.icon
                    : NightDashUIIcons.GetPassive(passiveId);
                _iconImage.enabled = _iconImage.sprite != null;
            }
            else
            {
                _passiveNameText.text = string.Empty;
                _descriptionText.text = string.Empty;
                _iconImage.sprite = null;
                _iconImage.enabled = false;
            }
        }

        private void ClearContent()
        {
            _classNameText.text = string.Empty;
            _passiveNameText.text = string.Empty;
            _descriptionText.text = string.Empty;
            _iconImage.sprite = null;
            _iconImage.enabled = false;
        }

        private void ApplyAlpha()
        {
            float current = _group.alpha;
            float duration = _alphaTarget > current ? FadeInSec : FadeOutSec;
            float step = duration > 0f ? Time.unscaledDeltaTime / duration : 1f;
            _group.alpha = Mathf.MoveTowards(current, _alphaTarget, step);
        }

        private static bool IsTabHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.tabKey.isPressed;
#else
            return Input.GetKey(KeyCode.Tab);
#endif
        }
    }
}
