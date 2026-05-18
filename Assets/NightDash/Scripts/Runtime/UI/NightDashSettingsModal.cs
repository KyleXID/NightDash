// Settings modal — Master / BGM / SFX volume sliders.
//
// Single shared modal instance lazily built into a top-level Canvas so both
// the title screen and the in-game pause menu can pop it open without each
// shipping its own copy. Master volume drives AudioListener.volume directly;
// BGM/SFX slots persist their values into PlayerPrefs so a future audio
// mixer rollout can pick them up without UI changes.

using System;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class NightDashSettingsModal : MonoBehaviour
    {
        // PlayerPrefs keys. Other systems (future AudioMixer wiring) can read
        // these to apply volumes without touching the modal.
        public const string PrefMaster = "nightdash.audio.master";
        public const string PrefBgm    = "nightdash.audio.bgm";
        public const string PrefSfx    = "nightdash.audio.sfx";

        // Default volume when no PlayerPrefs entry exists.
        private const float DefaultVolume = 0.8f;

        // Canvas sort order — has to sit above the pause overlay (5500) and
        // the title menu (5000) so the modal isn't clickable through.
        private const int SortOrder = 6500;

        private static NightDashSettingsModal s_Instance;
        private Action _onClosed;

        private Canvas _canvas;
        private RectTransform _panel;
        private Slider _masterSlider;
        private Slider _bgmSlider;
        private Slider _sfxSlider;
        private Text _masterValueText;
        private Text _bgmValueText;
        private Text _sfxValueText;
        // Slider drags fire onValueChanged every frame. We flag the modal as
        // dirty here and flush PlayerPrefs only on Close → no per-frame disk
        // I/O during a drag, but the final value still persists.
        private bool _prefsDirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain Reload Off + Play Mode reload would otherwise leave
            // s_Instance pointing at a destroyed GameObject.
            s_Instance = null;
        }

        // ------------------------------------------------------------------
        // Public entry — title/pause menus call Show; ESC or "Close" closes.
        // ------------------------------------------------------------------
        public static void Show(Action onClosed = null)
        {
            EnsureInstance();
            s_Instance._onClosed = onClosed;
            s_Instance.HydrateFromPrefs();
            s_Instance.gameObject.SetActive(true);
        }

        public static bool IsOpen => s_Instance != null && s_Instance.gameObject.activeInHierarchy;

        // ApplyPersistedVolume — call once at app start so Master volume
        // sticks across sessions even if the modal never opened this session.
        public static void ApplyPersistedVolume()
        {
            float master = PlayerPrefs.GetFloat(PrefMaster, DefaultVolume);
            AudioListener.volume = Mathf.Clamp01(master);
        }

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------
        private static void EnsureInstance()
        {
            if (s_Instance != null) return;

            var go = new GameObject("NightDashSettingsModal");
            s_Instance = go.AddComponent<NightDashSettingsModal>();
            s_Instance.BuildUI();
            s_Instance.gameObject.SetActive(false);
        }

        private void BuildUI()
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

            // Backdrop — dims the rest of the screen so the modal reads as
            // foreground. Click anywhere outside the panel closes the modal.
            var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdropGo.transform.SetParent(transform, false);
            var backdropRect = (RectTransform)backdropGo.transform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            var backdropImg = backdropGo.GetComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.82f);
            backdropImg.raycastTarget = true;
            backdropGo.GetComponent<Button>().onClick.AddListener(Close);

            // Modal panel — center anchored, 700×600. Burgundy ring matches
            // the lobby modifier panel for visual consistency.
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(740f, 760f);
            var panelBorder = panelGo.GetComponent<Image>();
            panelBorder.color = new Color(0.30f, 0.12f, 0.14f, 0.95f);
            panelBorder.raycastTarget = true; // swallow clicks so backdrop doesn't close

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_panel, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            var fillImg = fillGo.GetComponent<Image>();
            // Fully opaque so the pause menu beneath stops bleeding through
            // the panel. Backdrop already handles the cinematic dim.
            fillImg.color = new Color(0.08f, 0.06f, 0.07f, 1f);
            fillImg.raycastTarget = true;

            BuildHeader(_panel);
            // yOffset = 0 is panel center, +y is up. Panel is 600px tall:
            //   header sits at top (~+260)
            //   sliders cluster in the middle (+50, -30, -110)
            //   close button at bottom (-220), hint just above close
            _masterSlider = BuildSliderRow(_panel, "Master", 120f, OnMasterChanged, out _masterValueText);
            _bgmSlider    = BuildSliderRow(_panel, "BGM",     50f, OnBgmChanged,    out _bgmValueText);
            _sfxSlider    = BuildSliderRow(_panel, "SFX",    -20f, OnSfxChanged,    out _sfxValueText);
            BuildToggleRow(_panel, "Health Bars",
                () => NightDash.Runtime.NightDashHealthBarOverlay.EnemyBarsEnabled,
                v => NightDash.Runtime.NightDashHealthBarOverlay.EnemyBarsEnabled = v,
                -90f);
            BuildToggleRow(_panel, "Player Bar",
                () => NightDash.Runtime.NightDashHealthBarOverlay.PlayerBarEnabled,
                v => NightDash.Runtime.NightDashHealthBarOverlay.PlayerBarEnabled = v,
                -160f);
            BuildCloseButton(_panel);
            BuildHint(_panel);
        }

        // Generic ON/OFF toggle row. Used for the two health-bar options
        // (enemy bars + player bar) and is ready to host future settings
        // without another bespoke layout pass. The getter/setter delegates
        // are how the toggle reads/writes the actual setting — keeps the
        // modal free of any direct PlayerPrefs knowledge.
        private void BuildToggleRow(RectTransform parent, string label,
            System.Func<bool> getter, System.Action<bool> setter, float yOffset)
        {
            // Row container — same width / height as the slider rows so the
            // SETTINGS body reads as a clean vertical list.
            var rowGo = new GameObject($"Row_{label.Replace(' ', '_')}",
                typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var rr = (RectTransform)rowGo.transform;
            rr.anchorMin = new Vector2(0.5f, 0.5f);
            rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.pivot = new Vector2(0.5f, 0.5f);
            rr.anchoredPosition = new Vector2(0f, yOffset);
            rr.sizeDelta = new Vector2(640f, 60f);

            // Label
            var labelGo = new GameObject("Label",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            labelGo.transform.SetParent(rr, false);
            var lr = (RectTransform)labelGo.transform;
            lr.anchorMin = new Vector2(0f, 0.5f);
            lr.anchorMax = new Vector2(0f, 0.5f);
            lr.pivot = new Vector2(0f, 0.5f);
            lr.anchoredPosition = new Vector2(20f, 0f);
            lr.sizeDelta = new Vector2(360f, 60f);
            var lt = labelGo.GetComponent<Text>();
            lt.text = label;
            lt.alignment = TextAnchor.MiddleLeft;
            lt.fontSize = 36;
            lt.color = new Color(0.95f, 0.92f, 0.86f, 1f);
            lt.font = NightDashUIFonts.Arcade;
            lt.raycastTarget = false;
            var lo = labelGo.GetComponent<Outline>();
            lo.effectColor = new Color(0f, 0f, 0f, 0.9f);
            lo.effectDistance = new Vector2(1.5f, -1.5f);

            // Toggle box (custom-styled — Unity's default Toggle looks out
            // of place against the rest of the pixel-art chrome). The box
            // shows a filled / hollow square that flips on click.
            var btnGo = new GameObject("ToggleBox",
                typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(rr, false);
            var br = (RectTransform)btnGo.transform;
            br.anchorMin = new Vector2(1f, 0.5f);
            br.anchorMax = new Vector2(1f, 0.5f);
            br.pivot = new Vector2(1f, 0.5f);
            br.anchoredPosition = new Vector2(-20f, 0f);
            br.sizeDelta = new Vector2(56f, 36f);
            var boxImg = btnGo.GetComponent<Image>();
            boxImg.color = new Color(0.18f, 0.16f, 0.18f, 1f);

            var checkGo = new GameObject("Check",
                typeof(RectTransform), typeof(Image));
            checkGo.transform.SetParent(br, false);
            var cr = (RectTransform)checkGo.transform;
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
            cr.offsetMin = new Vector2(6f, 6f); cr.offsetMax = new Vector2(-6f, -6f);
            var checkImg = checkGo.GetComponent<Image>();
            checkImg.color = new Color(0.82f, 0.28f, 0.30f, 1f);

            // Numeric readout column — empty for the toggle row, kept just
            // for visual alignment with the slider rows above.
            var valGo = new GameObject("State",
                typeof(RectTransform), typeof(Text), typeof(Outline));
            valGo.transform.SetParent(rr, false);
            var vr = (RectTransform)valGo.transform;
            vr.anchorMin = new Vector2(1f, 0.5f);
            vr.anchorMax = new Vector2(1f, 0.5f);
            vr.pivot = new Vector2(1f, 0.5f);
            vr.anchoredPosition = new Vector2(-90f, 0f);
            vr.sizeDelta = new Vector2(80f, 60f);
            var vt = valGo.GetComponent<Text>();
            vt.alignment = TextAnchor.MiddleRight;
            vt.fontSize = 30;
            vt.color = new Color(0.95f, 0.92f, 0.86f, 1f);
            vt.font = NightDashUIFonts.Arcade;
            vt.raycastTarget = false;
            var vo = valGo.GetComponent<Outline>();
            vo.effectColor = new Color(0f, 0f, 0f, 0.9f);
            vo.effectDistance = new Vector2(1.5f, -1.5f);

            void Refresh()
            {
                bool on = getter();
                checkImg.enabled = on;
                vt.text = on ? "ON" : "OFF";
            }
            Refresh();

            btnGo.GetComponent<Button>().onClick.AddListener(() =>
            {
                setter(!getter());
                Refresh();
            });
        }

        private void BuildHeader(RectTransform parent)
        {
            var go = new GameObject("Header", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -20f);
            r.sizeDelta = new Vector2(-40f, 80f);

            var t = go.GetComponent<Text>();
            t.text = "SETTINGS";
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 56;
            t.color = new Color(0.92f, 0.78f, 0.50f, 1f);
            t.font = NightDashUIFonts.Arcade;
            t.raycastTarget = false;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Settings cog icon on the left of the title. Header rect's
            // geometric center is y = -60, but the Silver pixel font's glyph
            // cluster sits ~6px above the rect center (ascent > descent), so
            // we push the icon a corresponding amount *down* to keep the cog
            // and the SETTINGS glyphs visually on the same horizontal line.
            var iconRect = NightDashUIIcons.Attach(parent, NightDashUIIcons.Settings,
                new Vector2(56f, 56f),
                Vector2.zero);
            if (iconRect != null)
            {
                iconRect.anchorMin = new Vector2(0.5f, 1f);
                iconRect.anchorMax = new Vector2(0.5f, 1f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(-180f, -52f);
            }
        }

        private Slider BuildSliderRow(RectTransform parent, string label, float yOffset, Action<float> onChanged, out Text valueText)
        {
            // Row container
            var rowGo = new GameObject($"Row_{label}", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var rowRect = (RectTransform)rowGo.transform;
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(0f, yOffset);
            rowRect.sizeDelta = new Vector2(640f, 60f);

            // Label
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            labelGo.transform.SetParent(rowRect, false);
            var lr = (RectTransform)labelGo.transform;
            lr.anchorMin = new Vector2(0f, 0.5f);
            lr.anchorMax = new Vector2(0f, 0.5f);
            lr.pivot = new Vector2(0f, 0.5f);
            lr.anchoredPosition = new Vector2(20f, 0f);
            lr.sizeDelta = new Vector2(180f, 60f);
            var lt = labelGo.GetComponent<Text>();
            lt.text = label;
            lt.alignment = TextAnchor.MiddleLeft;
            lt.fontSize = 36;
            lt.color = new Color(0.95f, 0.92f, 0.86f, 1f);
            lt.font = NightDashUIFonts.Arcade;
            lt.raycastTarget = false;
            var lo = labelGo.GetComponent<Outline>();
            lo.effectColor = new Color(0f, 0f, 0f, 0.9f);
            lo.effectDistance = new Vector2(1.5f, -1.5f);

            // Slider
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(rowRect, false);
            var sr = (RectTransform)sliderGo.transform;
            sr.anchorMin = new Vector2(0f, 0.5f);
            sr.anchorMax = new Vector2(1f, 0.5f);
            sr.pivot = new Vector2(0.5f, 0.5f);
            sr.offsetMin = new Vector2(220f, -16f);
            sr.offsetMax = new Vector2(-100f, 16f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            // Background
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(sliderGo.transform, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0.18f, 0.16f, 0.18f, 1f);

            // Fill area
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            var fillAreaRect = (RectTransform)fillAreaGo.transform;
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5f, 0f);
            fillAreaRect.offsetMax = new Vector2(-5f, 0f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = new Color(0.82f, 0.28f, 0.30f, 1f);

            // Handle
            var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            var handleAreaRect = (RectTransform)handleAreaGo.transform;
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRect = (RectTransform)handleGo.transform;
            handleRect.sizeDelta = new Vector2(14f, 22f);
            var handleImg = handleGo.GetComponent<Image>();
            handleImg.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            slider.targetGraphic = handleImg;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.direction = Slider.Direction.LeftToRight;
            // Single listener — onChanged is responsible for updating both the
            // numeric readout (via the out Text below) and the dirty flag.
            slider.onValueChanged.AddListener(v => onChanged(v));

            // Numeric readout (right edge)
            var valGo = new GameObject("Value", typeof(RectTransform), typeof(Text), typeof(Outline));
            valGo.transform.SetParent(rowRect, false);
            var vr = (RectTransform)valGo.transform;
            vr.anchorMin = new Vector2(1f, 0.5f);
            vr.anchorMax = new Vector2(1f, 0.5f);
            vr.pivot = new Vector2(1f, 0.5f);
            vr.anchoredPosition = new Vector2(-10f, 0f);
            vr.sizeDelta = new Vector2(80f, 60f);
            valueText = valGo.GetComponent<Text>();
            valueText.alignment = TextAnchor.MiddleRight;
            valueText.fontSize = 30;
            valueText.color = new Color(0.95f, 0.92f, 0.86f, 1f);
            valueText.font = NightDashUIFonts.Arcade;
            valueText.raycastTarget = false;
            var vo = valGo.GetComponent<Outline>();
            vo.effectColor = new Color(0f, 0f, 0f, 0.9f);
            vo.effectDistance = new Vector2(1.5f, -1.5f);

            return slider;
        }

        private void BuildCloseButton(RectTransform parent)
        {
            var go = new GameObject("CloseButton",
                typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0f, 24f);
            r.sizeDelta = new Vector2(240f, 80f);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.34f, 0.08f, 0.10f, 0.96f);
            img.raycastTarget = true;
            go.GetComponent<Button>().onClick.AddListener(Close);

            // Cancel icon + text
            NightDashUIIcons.Attach(r, NightDashUIIcons.Cancel,
                new Vector2(40f, 40f), new Vector2(-70f, 0f));

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            labelGo.transform.SetParent(r, false);
            var lr = (RectTransform)labelGo.transform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.offsetMin = new Vector2(50f, 0f);
            lr.offsetMax = new Vector2(0f, 0f);
            var lt = labelGo.GetComponent<Text>();
            lt.text = "CLOSE";
            lt.alignment = TextAnchor.MiddleCenter;
            lt.fontSize = 36;
            lt.color = new Color(0.98f, 0.92f, 0.86f, 1f);
            lt.font = NightDashUIFonts.Arcade;
            lt.raycastTarget = false;
            var lo = labelGo.GetComponent<Outline>();
            lo.effectColor = new Color(0f, 0f, 0f, 0.9f);
            lo.effectDistance = new Vector2(2f, -2f);
        }

        private void BuildHint(RectTransform parent)
        {
            // Sits just under the header so it never collides with the
            // close button at the bottom — the SETTINGS title needs a
            // quick "ESC to exit" affordance and that's where the eye
            // naturally lands first.
            var go = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.5f, 1f);
            r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -100f);
            r.sizeDelta = new Vector2(600f, 32f);
            var t = go.GetComponent<Text>();
            t.text = "[ESC] CLOSE";
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 28;
            t.color = new Color(0.86f, 0.78f, 0.62f, 1f);
            t.font = NightDashUIFonts.Arcade;
            t.raycastTarget = false;
        }

        private void HydrateFromPrefs()
        {
            float m = PlayerPrefs.GetFloat(PrefMaster, DefaultVolume);
            float b = PlayerPrefs.GetFloat(PrefBgm, DefaultVolume);
            float s = PlayerPrefs.GetFloat(PrefSfx, DefaultVolume);
            _masterSlider.SetValueWithoutNotify(m);
            _bgmSlider.SetValueWithoutNotify(b);
            _sfxSlider.SetValueWithoutNotify(s);
            // Update numeric readouts directly — don't re-fire onValueChanged,
            // which would set _prefsDirty and rewrite identical PlayerPrefs
            // values on every open.
            if (_masterValueText != null) _masterValueText.text = $"{Mathf.RoundToInt(m * 100f)}%";
            if (_bgmValueText != null)    _bgmValueText.text    = $"{Mathf.RoundToInt(b * 100f)}%";
            if (_sfxValueText != null)    _sfxValueText.text    = $"{Mathf.RoundToInt(s * 100f)}%";
            // Master takes effect immediately.
            AudioListener.volume = Mathf.Clamp01(m);
        }

        private void OnMasterChanged(float v)
        {
            PlayerPrefs.SetFloat(PrefMaster, v);
            AudioListener.volume = Mathf.Clamp01(v);
            if (_masterValueText != null) _masterValueText.text = $"{Mathf.RoundToInt(v * 100f)}%";
            _prefsDirty = true;
        }

        private void OnBgmChanged(float v)
        {
            PlayerPrefs.SetFloat(PrefBgm, v);
            if (_bgmValueText != null) _bgmValueText.text = $"{Mathf.RoundToInt(v * 100f)}%";
            _prefsDirty = true;
            // BGM channel will be wired when the AudioMixer arrives.
        }

        private void OnSfxChanged(float v)
        {
            PlayerPrefs.SetFloat(PrefSfx, v);
            if (_sfxValueText != null) _sfxValueText.text = $"{Mathf.RoundToInt(v * 100f)}%";
            _prefsDirty = true;
            // SFX channel will be wired when the AudioMixer arrives.
        }

        private void FlushPrefs()
        {
            if (!_prefsDirty) return;
            PlayerPrefs.Save();
            _prefsDirty = false;
        }

        private void Close()
        {
            FlushPrefs();
            gameObject.SetActive(false);
            Action callback = _onClosed;
            _onClosed = null;
            callback?.Invoke();
        }

        private void Update()
        {
            // Single global "close on ESC" hook regardless of which screen
            // pushed the modal open. Keyboard polling — same pattern as the
            // rest of the menus — so we don't depend on EventSystem nav.
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) Close();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
#endif
        }

        private void OnDisable()
        {
            // Backstop flush in case something disables the GameObject
            // without going through Close() (e.g. scene unload).
            FlushPrefs();
        }
    }
}
