// Save / Load slot picker (mock).
//
// Continue button on the title screen routes through here. Slot 0 mirrors
// the existing single-slot autosave (any session whose RunSelectionSession
// has a non-default stage or class is treated as "in progress"); slots 1
// and 2 always read as empty placeholders. Picking any slot just kicks off
// the normal Start flow — the multi-slot save system is not yet wired.

using System;
using NightDash.Runtime;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class NightDashSaveSlotModal : MonoBehaviour
    {
        // Above Collection (6500) since Continue → save slot is the deeper
        // modal stack — opened from the title menu, not nested inside another
        // modal, but the explicit gap keeps Z-order unambiguous.
        private const int SortOrder = 6600;

        private static NightDashSaveSlotModal s_Instance;
        private Action<int> _onSelected; // -1 = cancelled
        private Action _onCancel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_Instance = null;

        public static bool IsOpen => s_Instance != null && s_Instance.gameObject.activeInHierarchy;

        public static void Show(Action<int> onSelected, Action onCancel = null)
        {
            EnsureInstance();
            s_Instance._onSelected = onSelected;
            s_Instance._onCancel = onCancel;
            s_Instance._selectedSlot = 0;
            s_Instance.RefreshSlots();
            s_Instance.RefreshSlotHighlight();
            s_Instance.gameObject.SetActive(true);
        }

        private static void EnsureInstance()
        {
            if (s_Instance != null) return;
            var go = new GameObject("NightDashSaveSlotModal");
            s_Instance = go.AddComponent<NightDashSaveSlotModal>();
            s_Instance.BuildUI();
            s_Instance.gameObject.SetActive(false);
        }

        private RectTransform _panel;
        private readonly GameObject[] _slotGos = new GameObject[3];
        private readonly Text[] _slotNames = new Text[3];
        private readonly Text[] _slotMeta = new Text[3];
        private readonly Image[] _slotIcons = new Image[3];
        private readonly Image[] _slotBackgrounds = new Image[3];
        private int _selectedSlot;

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

            var bdGo = new GameObject("Backdrop",
                typeof(RectTransform), typeof(Image), typeof(Button));
            bdGo.transform.SetParent(transform, false);
            var br = (RectTransform)bdGo.transform;
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
            bdGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
            bdGo.GetComponent<Button>().onClick.AddListener(Cancel);

            // Panel
            var panelGo = new GameObject("Panel",
                typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(1100f, 640f);
            panelGo.GetComponent<Image>().color = new Color(0.30f, 0.12f, 0.14f, 0.95f);

            var fillGo = new GameObject("Fill",
                typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_panel, false);
            var fr = (RectTransform)fillGo.transform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(3f, 3f); fr.offsetMax = new Vector2(-3f, -3f);
            fillGo.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.07f, 1f);

            BuildHeader();
            BuildSlots();
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
            var t = go.GetComponent<Text>();
            t.text = "LOAD GAME";
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 56;
            t.color = new Color(0.92f, 0.78f, 0.50f, 1f);
            t.font = NightDashUIFonts.Arcade;
            t.raycastTarget = false;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.9f);
            o.effectDistance = new Vector2(2f, -2f);
        }

        private void BuildSlots()
        {
            // 3 horizontally laid out slots — fixed positions so a future
            // dynamic slot count would need its own layout pass.
            float slotW = 320f;
            float slotH = 360f;
            float gap = 24f;
            float totalW = slotW * 3 + gap * 2;
            float startX = -totalW * 0.5f + slotW * 0.5f;

            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                var go = new GameObject($"Slot_{i}",
                    typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_panel, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = new Vector2(0.5f, 0.5f);
                r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(slotW, slotH);
                r.anchoredPosition = new Vector2(startX + i * (slotW + gap), -10f);
                var bg = go.GetComponent<Image>();
                bg.color = new Color(0.16f, 0.08f, 0.10f, 0.95f);
                go.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _selectedSlot = captured;
                    RefreshSlotHighlight();
                    Select(captured);
                });
                _slotGos[i] = go;
                _slotBackgrounds[i] = bg;

                // Icon (load when occupied, save when empty)
                var iconRect = NightDashUIIcons.Attach(go.transform,
                    NightDashUIIcons.Save,
                    new Vector2(96f, 96f),
                    Vector2.zero,
                    "Icon");
                if (iconRect != null)
                {
                    iconRect.anchorMin = new Vector2(0.5f, 1f);
                    iconRect.anchorMax = new Vector2(0.5f, 1f);
                    iconRect.pivot = new Vector2(0.5f, 1f);
                    iconRect.anchoredPosition = new Vector2(0f, -30f);
                    _slotIcons[i] = iconRect.GetComponent<Image>();
                }

                // Slot title
                var nameGo = new GameObject("Name",
                    typeof(RectTransform), typeof(Text), typeof(Outline));
                nameGo.transform.SetParent(r, false);
                var nr = (RectTransform)nameGo.transform;
                nr.anchorMin = new Vector2(0f, 0f); nr.anchorMax = new Vector2(1f, 1f);
                nr.offsetMin = new Vector2(12f, 60f);
                nr.offsetMax = new Vector2(-12f, -140f);
                var nt = nameGo.GetComponent<Text>();
                nt.text = $"SLOT {i + 1}";
                nt.alignment = TextAnchor.UpperCenter;
                nt.fontSize = 36;
                nt.color = new Color(0.95f, 0.92f, 0.86f, 1f);
                nt.font = NightDashUIFonts.Arcade;
                nt.raycastTarget = false;
                var no = nameGo.GetComponent<Outline>();
                no.effectColor = new Color(0f, 0f, 0f, 0.9f);
                no.effectDistance = new Vector2(1.5f, -1.5f);
                _slotNames[i] = nt;

                // Metadata under the title (class / stage / time)
                var metaGo = new GameObject("Meta",
                    typeof(RectTransform), typeof(Text), typeof(Outline));
                metaGo.transform.SetParent(r, false);
                var mr = (RectTransform)metaGo.transform;
                mr.anchorMin = new Vector2(0f, 0f); mr.anchorMax = new Vector2(1f, 0f);
                mr.pivot = new Vector2(0.5f, 0f);
                mr.anchoredPosition = new Vector2(0f, 16f);
                mr.sizeDelta = new Vector2(-20f, 84f);
                var mt = metaGo.GetComponent<Text>();
                mt.alignment = TextAnchor.MiddleCenter;
                mt.fontSize = 22;
                mt.color = new Color(0.80f, 0.74f, 0.62f, 1f);
                mt.font = NightDashUIFonts.Arcade;
                mt.raycastTarget = false;
                mt.horizontalOverflow = HorizontalWrapMode.Overflow;
                mt.verticalOverflow = VerticalWrapMode.Overflow;
                var mo = metaGo.GetComponent<Outline>();
                mo.effectColor = new Color(0f, 0f, 0f, 0.9f);
                mo.effectDistance = new Vector2(1.5f, -1.5f);
                _slotMeta[i] = mt;
            }
        }

        private void BuildHint()
        {
            var go = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_panel, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.5f, 0f); r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0f, 16f);
            r.sizeDelta = new Vector2(800f, 32f);
            var t = go.GetComponent<Text>();
            t.text = "←/→  SELECT     ENTER  LOAD     [ESC]  BACK";
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 22;
            t.color = new Color(0.80f, 0.74f, 0.62f, 1f);
            t.font = NightDashUIFonts.Arcade;
            t.raycastTarget = false;
        }

        private void RefreshSlotHighlight()
        {
            // Highlight color shift only — the icon/text update happens
            // separately in RefreshSlots so we don't waste sprite swaps on
            // every left/right keypress.
            for (int i = 0; i < _slotBackgrounds.Length; i++)
            {
                if (_slotBackgrounds[i] == null) continue;
                _slotBackgrounds[i].color = i == _selectedSlot
                    ? new Color(0.34f, 0.10f, 0.12f, 0.96f)
                    : new Color(0.16f, 0.08f, 0.10f, 0.95f);
            }
        }

        private void RefreshSlots()
        {
            // Slot 0 mirrors the active autosave when one exists. Heuristic:
            // the session has a non-default stage/class. Other slots are
            // always shown as empty placeholders until multi-slot landing.
            RunSelectionSession.GetCurrent(out string stage, out string klass);
            bool slot0HasSave =
                !string.IsNullOrEmpty(stage) && stage != "stage_01"
                || !string.IsNullOrEmpty(klass) && klass != "class_warrior";

            for (int i = 0; i < 3; i++)
            {
                bool occupied = (i == 0) && slot0HasSave;
                if (_slotIcons[i] != null)
                {
                    _slotIcons[i].sprite = NightDashUIIcons.Get(
                        occupied ? NightDashUIIcons.Load : NightDashUIIcons.Save);
                    _slotIcons[i].color = occupied
                        ? Color.white
                        : new Color(0.55f, 0.55f, 0.55f, 0.85f);
                }
                if (_slotNames[i] != null)
                {
                    _slotNames[i].text = $"SLOT {i + 1}";
                    _slotNames[i].color = occupied
                        ? new Color(0.95f, 0.92f, 0.86f, 1f)
                        : new Color(0.55f, 0.55f, 0.60f, 1f);
                }
                if (_slotMeta[i] != null)
                {
                    if (occupied)
                    {
                        _slotMeta[i].text = $"Stage: {Pretty(stage)}\nClass: {Pretty(klass)}";
                    }
                    else
                    {
                        _slotMeta[i].text = "— EMPTY —";
                    }
                }
            }
        }

        private static string Pretty(string id)
        {
            if (string.IsNullOrEmpty(id)) return "—";
            // Strip "stage_" / "class_" prefix and capitalize so the slot
            // card reads like a human-friendly label.
            int u = id.IndexOf('_');
            string body = u < 0 ? id : id.Substring(u + 1);
            if (string.IsNullOrEmpty(body)) return id;
            return char.ToUpper(body[0]) + body.Substring(1);
        }

        private void Select(int slot)
        {
            Action<int> cb = _onSelected;
            _onSelected = null;
            _onCancel = null;
            gameObject.SetActive(false);
            cb?.Invoke(slot);
        }

        private void Cancel()
        {
            Action cb = _onCancel;
            _onSelected = null;
            _onCancel = null;
            gameObject.SetActive(false);
            cb?.Invoke();
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Step(-1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Step(+1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                || kb.spaceKey.wasPressedThisFrame)
            {
                Select(_selectedSlot);
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(+1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space))
            {
                Select(_selectedSlot);
            }
#endif
        }

        private void Step(int delta)
        {
            _selectedSlot = Mathf.Clamp(_selectedSlot + delta, 0, 2);
            RefreshSlotHighlight();
        }
    }
}
