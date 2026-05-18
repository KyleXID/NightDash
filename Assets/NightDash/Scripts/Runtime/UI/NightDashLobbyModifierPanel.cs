// Lobby difficulty-modifier panel.
//
// Renders a vertical row of stackable modifier chips next to the lobby's
// stage label. Each chip shows the modifier icon + display name + dot
// stack indicator + Lv.N text. Modifiers are gated by stage clear history
// (RunClearRecord) — if the current stage has never been cleared the entire
// panel is locked and a "한 번 클리어 후 잠금 해제" notice replaces the chip list.
//
// Stack interaction:
//   - Left click / number key (1..5)        = +1 level (clamped to MaxLevel)
//   - Right click / Shift+number key (1..5) = -1 level (clamped to 0 = off)
//
// Selection state (id + active level) is exposed via
// GetSelectedModifierStages so the lobby start flow can hand it to
// RunSelectionSession. GetSelectedModifierIds remains available for legacy
// callers that ignore stacking.

using System.Collections.Generic;
using NightDash.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class NightDashLobbyModifierPanel : MonoBehaviour
    {
        // ------------------------------------------------------------------ tuning
        private const float ChipHeight = 118f;
        private const float ChipIconSize = 48f;
        private const float ChipSpacing = 10f;
        private const float PanelWidth = 380f;
        private const int HeaderFontSize = 38;
        private const int LockedNoticeFontSize = 30;
        private const int ChipFontSize = 26;
        private const int ChipDescFontSize = 24;
        private const int ChipLevelFontSize = 22;
        private const float BorderThickness = 2f;
        private const float DotSize = 11f;
        private const float DotSpacing = 5f;

#if !ENABLE_INPUT_SYSTEM
        private static readonly KeyCode[] HotkeyAlpha = new[]
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
        };
        private static readonly KeyCode[] HotkeyKeypad = new[]
        {
            KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5,
        };
#endif

        // Palette tuned for the dark-fantasy lobby — burgundy + dried blood
        // replaces the older bronze/gold trim so the panel reads "ominous"
        // instead of "ceremonial". Header text keeps a desaturated gold
        // because that matches the lobby stage label.
        private static readonly Color HeaderColor = new Color(0.88f, 0.74f, 0.48f, 1f);
        private static readonly Color ChipDefaultBg = new Color(0.09f, 0.07f, 0.09f, 0.92f);
        private static readonly Color ChipActiveBg = new Color(0.34f, 0.08f, 0.10f, 0.96f);
        private static readonly Color ChipLockedBg = new Color(0.05f, 0.05f, 0.07f, 0.92f);
        private static readonly Color ChipLabelColor = new Color(0.95f, 0.92f, 0.86f, 1f);
        private static readonly Color ChipLevelActive = new Color(0.98f, 0.55f, 0.50f, 1f);
        private static readonly Color ChipLockedLabel = new Color(0.50f, 0.50f, 0.55f, 1f);
        private static readonly Color BorderDefault = new Color(0.35f, 0.16f, 0.18f, 0.95f);
        private static readonly Color BorderActive = new Color(0.78f, 0.22f, 0.26f, 1f);
        private static readonly Color BorderLocked = new Color(0.18f, 0.18f, 0.22f, 0.92f);
        private static readonly Color PanelBorder = new Color(0.30f, 0.12f, 0.14f, 0.92f);
        private static readonly Color PanelFill = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color DotFilled = new Color(0.88f, 0.28f, 0.28f, 1f);
        private static readonly Color DotEmpty = new Color(0.28f, 0.22f, 0.24f, 0.90f);

        // ------------------------------------------------------------------ wiring
        [SerializeField] private DifficultyModifierData[] modifiers;
        [SerializeField, TextArea] private string lockedNotice =
            "이 스테이지를 한 번 이상 클리어하면\n난이도 조절을 활성화할 수 있어요.";

        // ------------------------------------------------------------------ state
        private RectTransform _rect;
        private RectTransform _chipContainer;
        private Text _headerText;
        private Text _lockedNoticeText;
        private string _currentStageId;
        private bool _isLocked;

        private readonly List<Chip> _chips = new();
        // Mirrors `_chips` 1:1. 0 = inactive, 1..MaxLevel = stacked level.
        private int[] _chipLevels = System.Array.Empty<int>();

        private static readonly string[] AlwaysUnlockedStages = { "stage_01" };

        // ------------------------------------------------------------------ public API
        public void Configure(string stageId, IList<string> preselectedModifierIds)
        {
            // Legacy entry point — every preselected id snaps to Lv.1.
            List<(string id, int level)> stages = null;
            if (preselectedModifierIds != null && preselectedModifierIds.Count > 0)
            {
                stages = new List<(string, int)>(preselectedModifierIds.Count);
                for (int i = 0; i < preselectedModifierIds.Count; i++)
                {
                    string id = preselectedModifierIds[i];
                    if (!string.IsNullOrEmpty(id)) stages.Add((id, 1));
                }
            }
            Configure(stageId, stages);
        }

        public void Configure(string stageId, IList<(string id, int level)> preselectedStages)
        {
            EnsureBuilt();

            _currentStageId = stageId;
            _isLocked = !IsStageUnlocked(stageId);

            for (int i = 0; i < _chipLevels.Length; i++) _chipLevels[i] = 0;

            if (!_isLocked && preselectedStages != null)
            {
                for (int i = 0; i < preselectedStages.Count; i++)
                {
                    (string id, int level) entry = preselectedStages[i];
                    if (string.IsNullOrEmpty(entry.id)) continue;
                    int chipIndex = FindChipIndex(entry.id);
                    if (chipIndex < 0) continue;
                    int max = MaxLevelOf(chipIndex);
                    _chipLevels[chipIndex] = Mathf.Clamp(entry.level, 0, max);
                }
            }

            RefreshVisuals();
        }

        public void GetSelectedModifierIds(List<string> destination)
        {
            if (destination == null) return;
            destination.Clear();
            if (_isLocked) return;
            for (int i = 0; i < _chips.Count; i++)
            {
                if (_chipLevels[i] < 1) continue;
                DifficultyModifierData data = _chips[i].Data;
                if (data == null || string.IsNullOrEmpty(data.id)) continue;
                destination.Add(data.id);
            }
        }

        public void GetSelectedModifierStages(List<(string id, int level)> destination)
        {
            if (destination == null) return;
            destination.Clear();
            if (_isLocked) return;
            for (int i = 0; i < _chips.Count; i++)
            {
                int level = _chipLevels[i];
                if (level < 1) continue;
                DifficultyModifierData data = _chips[i].Data;
                if (data == null || string.IsNullOrEmpty(data.id)) continue;
                destination.Add((data.id, level));
            }
        }

        public bool IsLocked => _isLocked;

        private static bool IsStageUnlocked(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return false;
            for (int i = 0; i < AlwaysUnlockedStages.Length; i++)
            {
                if (AlwaysUnlockedStages[i] == stageId) return true;
            }
            return RunClearRecord.IsCleared(stageId);
        }

        private int FindChipIndex(string id)
        {
            for (int i = 0; i < _chips.Count; i++)
            {
                DifficultyModifierData data = _chips[i].Data;
                if (data != null && data.id == id) return i;
            }
            return -1;
        }

        private int MaxLevelOf(int chipIndex)
        {
            if (chipIndex < 0 || chipIndex >= _chips.Count) return 1;
            DifficultyModifierData data = _chips[chipIndex].Data;
            return data != null ? Mathf.Max(1, data.MaxLevel) : 1;
        }

        // ------------------------------------------------------------------ unity hooks
        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            if (_currentStageId != null) Configure(_currentStageId, GetSelectedStagesSnapshot());
        }

        private void Update()
        {
            if (_isLocked) return;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            int delta = shift ? -1 : 1;
            for (int i = 0; i < _chips.Count && i < 5; i++)
            {
                if (PressedDigitThisFrame(kb, i)) AdjustLevel(i, delta);
            }
#else
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            int delta = shift ? -1 : 1;
            for (int i = 0; i < _chips.Count && i < HotkeyAlpha.Length; i++)
            {
                if (Input.GetKeyDown(HotkeyAlpha[i]) || Input.GetKeyDown(HotkeyKeypad[i]))
                {
                    AdjustLevel(i, delta);
                }
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool PressedDigitThisFrame(Keyboard kb, int zeroBasedIndex)
        {
            switch (zeroBasedIndex)
            {
                case 0: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
                case 1: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
                case 2: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
                case 3: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
                case 4: return kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame;
                default: return false;
            }
        }
#endif

        // ------------------------------------------------------------------ build
        private void EnsureBuilt()
        {
            if (_rect != null) return;

            if (modifiers == null || modifiers.Length == 0)
            {
                DifficultyModifierData[] loaded =
                    Resources.LoadAll<DifficultyModifierData>("NightDash/Data/Difficulty");
                if (loaded != null && loaded.Length > 0)
                {
                    System.Array.Sort(loaded, (a, b) =>
                        string.CompareOrdinal(a != null ? a.id : string.Empty,
                                              b != null ? b.id : string.Empty));
                    modifiers = loaded;
                }
                else
                {
                    modifiers = System.Array.Empty<DifficultyModifierData>();
                }
            }

            _rect = GetComponent<RectTransform>();
            if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();
            // Self-size to exactly fit (header band + N chips + spacings +
            // bottom padding). Any externally-stamped sizeDelta is overridden
            // so the panel always hugs its content with no trailing dead
            // space, regardless of how many modifiers ship in the asset list.
            int chipCount = modifiers != null ? modifiers.Length : 0;
            if (chipCount <= 0) chipCount = 1; // never collapse to zero
            const float HeaderBand = 84f;
            const float BottomPadding = 12f;
            float fittedHeight = HeaderBand
                + chipCount * ChipHeight
                + Mathf.Max(0, chipCount - 1) * ChipSpacing
                + BottomPadding;
            _rect.sizeDelta = new Vector2(PanelWidth, fittedHeight);

            var outerBg = gameObject.AddComponent<Image>();
            outerBg.color = PanelBorder;
            outerBg.raycastTarget = false;

            var fillGo = new GameObject("PanelFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(transform, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(BorderThickness, BorderThickness);
            fillRect.offsetMax = new Vector2(-BorderThickness, -BorderThickness);
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = PanelFill;
            fillImage.raycastTarget = false;
            fillGo.transform.SetAsFirstSibling();

            BuildHeader();
            BuildLockedNotice();
            BuildChipContainer();
            BuildChips();

            _chipLevels = new int[_chips.Count];
        }

        private void BuildHeader()
        {
            var go = new GameObject("Header", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -16f);
            // Make the rect taller than the 38pt font so descenders + outline
            // don't get truncated by the host RectTransform.
            r.sizeDelta = new Vector2(-20f, 56f);

            _headerText = go.GetComponent<Text>();
            _headerText.text = "난이도 조절";
            _headerText.alignment = TextAnchor.MiddleCenter;
            _headerText.fontSize = HeaderFontSize;
            _headerText.color = HeaderColor;
            _headerText.font = NightDashUIFonts.Arcade;
            _headerText.raycastTarget = false;
            _headerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _headerText.verticalOverflow = VerticalWrapMode.Overflow;
            AddTextOutline(_headerText);
        }

        private void BuildLockedNotice()
        {
            var go = new GameObject("LockedNotice", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(20f, 20f);
            // Match the chip container's top margin so the notice never
            // overlaps the header rect (header ends at -72).
            r.offsetMax = new Vector2(-20f, -84f);

            _lockedNoticeText = go.GetComponent<Text>();
            _lockedNoticeText.text = lockedNotice;
            _lockedNoticeText.alignment = TextAnchor.MiddleCenter;
            _lockedNoticeText.fontSize = LockedNoticeFontSize;
            _lockedNoticeText.color = ChipLockedLabel;
            _lockedNoticeText.font = NightDashUIFonts.Arcade;
            _lockedNoticeText.raycastTarget = false;
            _lockedNoticeText.gameObject.SetActive(false);
            AddTextOutline(_lockedNoticeText);
        }

        private void BuildChipContainer()
        {
            var go = new GameObject("ChipContainer", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _chipContainer = (RectTransform)go.transform;
            _chipContainer.anchorMin = new Vector2(0f, 0f);
            _chipContainer.anchorMax = new Vector2(1f, 1f);
            _chipContainer.offsetMin = new Vector2(12f, 12f);
            // Drop the top edge well below the header. Header sits at y=-16
            // with 56px height, ending at -72. We leave another 12px of
            // breathing room so the first chip never clips the header text.
            _chipContainer.offsetMax = new Vector2(-12f, -84f);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = ChipSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            // childControlHeight must be TRUE so VerticalLayoutGroup honors
            // each chip's LayoutElement.preferredHeight (=ChipHeight). With
            // it false the chips fall back to RectTransform's default 100px
            // and the container shows ~90px of dead space underneath.
            layout.childControlHeight = true;
        }

        private void BuildChips()
        {
            for (int i = 0; i < modifiers.Length; i++)
            {
                DifficultyModifierData data = modifiers[i];
                if (data == null) continue;
                _chips.Add(CreateChip(i, data));
            }
        }

        private Chip CreateChip(int index, DifficultyModifierData data)
        {
            var go = new GameObject($"Chip_{data.id}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(_chipContainer, false);
            // Belt-and-suspenders: stamp the rect height directly so the chip
            // never falls back to the RectTransform default of 100px, even if
            // the parent layout group's childControlHeight flag ever flips.
            var chipRect = (RectTransform)go.transform;
            chipRect.sizeDelta = new Vector2(0f, ChipHeight);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = ChipHeight;
            le.minHeight = ChipHeight;

            var border = go.GetComponent<Image>();
            border.color = BorderDefault;
            border.raycastTarget = true; // receives clicks via ChipClickHandler

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(BorderThickness, BorderThickness);
            fillRect.offsetMax = new Vector2(-BorderThickness, -BorderThickness);
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = ChipDefaultBg;
            fillImage.raycastTarget = false;

            // Icon
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(fillGo.transform, false);
            var ir = (RectTransform)iconGo.transform;
            ir.anchorMin = new Vector2(0f, 0.5f);
            ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0f, 0.5f);
            ir.anchoredPosition = new Vector2(10f, 0f);
            ir.sizeDelta = new Vector2(ChipIconSize, ChipIconSize);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = data.icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Chip is divided into three horizontal bands keyed to chip height:
            //   y = 0.66 .. 1.0   → name row
            //   y = 0.33 .. 0.66  → effect description row
            //   y = 0.0  .. 0.33  → dots row + Lv text
            // Icon spans the full vertical so it visually anchors all three.
            int maxLevel = Mathf.Max(1, data.MaxLevel);
            const float ContentLeftPadding = 14f;
            const float ContentRightPadding = 12f;
            float contentLeftX = ChipIconSize + 10f + ContentLeftPadding;
            const float TopRowMin = 0.62f;
            const float MidRowMax = 0.62f;
            const float MidRowMin = 0.30f;
            const float BotRowMax = 0.30f;

            // Top row: [hotkey] modifier name.
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(fillGo.transform, false);
            var lr = (RectTransform)labelGo.transform;
            lr.anchorMin = new Vector2(0f, TopRowMin);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.offsetMin = new Vector2(contentLeftX, 0f);
            lr.offsetMax = new Vector2(-ContentRightPadding, -4f);
            var label = labelGo.GetComponent<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = ChipFontSize;
            label.color = ChipLabelColor;
            label.font = NightDashUIFonts.Arcade;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = $"[{index + 1}] {data.displayName}";
            AddTextOutline(label);

            // Mid row: auto-generated effect summary. Text is updated by
            // RefreshVisuals() so the description tracks the active stage
            // (e.g. "적 HP +40%" at Lv.2 → "적 HP +60%" at Lv.3).
            var descGo = new GameObject("Description", typeof(RectTransform), typeof(Text));
            descGo.transform.SetParent(fillGo.transform, false);
            var dr = (RectTransform)descGo.transform;
            dr.anchorMin = new Vector2(0f, MidRowMin);
            dr.anchorMax = new Vector2(1f, MidRowMax);
            dr.pivot = new Vector2(0.5f, 0.5f);
            dr.offsetMin = new Vector2(contentLeftX, 0f);
            dr.offsetMax = new Vector2(-ContentRightPadding, 0f);
            var descText = descGo.GetComponent<Text>();
            descText.alignment = TextAnchor.MiddleLeft;
            descText.fontSize = ChipDescFontSize;
            descText.color = new Color(0.82f, 0.78f, 0.72f, 1f);
            descText.font = NightDashUIFonts.Arcade;
            descText.raycastTarget = false;
            descText.horizontalOverflow = HorizontalWrapMode.Overflow;
            descText.verticalOverflow = VerticalWrapMode.Overflow;
            descText.text = "";
            AddTextOutline(descText);

            // Bottom row: Lv text (right) + dot stack (left).
            float levelTextWidth = 96f;
            var lvGo = new GameObject("Level", typeof(RectTransform), typeof(Text));
            lvGo.transform.SetParent(fillGo.transform, false);
            var lvr = (RectTransform)lvGo.transform;
            lvr.anchorMin = new Vector2(1f, 0f);
            lvr.anchorMax = new Vector2(1f, BotRowMax);
            lvr.pivot = new Vector2(1f, 0.5f);
            lvr.anchoredPosition = new Vector2(-ContentRightPadding, 0f);
            lvr.sizeDelta = new Vector2(levelTextWidth, ChipHeight * BotRowMax - 8f);
            var lvText = lvGo.GetComponent<Text>();
            lvText.alignment = TextAnchor.MiddleRight;
            lvText.fontSize = ChipLevelFontSize;
            lvText.color = ChipLabelColor;
            lvText.font = NightDashUIFonts.Arcade;
            lvText.raycastTarget = false;
            lvText.horizontalOverflow = HorizontalWrapMode.Overflow;
            lvText.verticalOverflow = VerticalWrapMode.Overflow;
            lvText.text = $"Lv.0/{maxLevel}";
            AddTextOutline(lvText);

            var dotsRowGo = new GameObject("Dots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            dotsRowGo.transform.SetParent(fillGo.transform, false);
            var dotsRect = (RectTransform)dotsRowGo.transform;
            dotsRect.anchorMin = new Vector2(0f, 0f);
            dotsRect.anchorMax = new Vector2(1f, BotRowMax);
            dotsRect.pivot = new Vector2(0f, 0.5f);
            dotsRect.offsetMin = new Vector2(contentLeftX, 4f);
            dotsRect.offsetMax = new Vector2(-(levelTextWidth + ContentRightPadding + 8f), -4f);
            var dotsLayout = dotsRowGo.GetComponent<HorizontalLayoutGroup>();
            dotsLayout.spacing = DotSpacing;
            dotsLayout.childAlignment = TextAnchor.MiddleLeft;
            dotsLayout.childForceExpandWidth = false;
            dotsLayout.childForceExpandHeight = false;
            // childControlWidth = true so HorizontalLayoutGroup honors each
            // dot's LayoutElement.preferredWidth (otherwise the dot reverts
            // to the RectTransform's default 100x100 and bursts the chip).
            dotsLayout.childControlWidth = true;
            dotsLayout.childControlHeight = true;

            Image[] dots = new Image[maxLevel];
            for (int d = 0; d < maxLevel; d++)
            {
                var dotGo = new GameObject($"Dot_{d}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                dotGo.transform.SetParent(dotsRowGo.transform, false);
                // Belt-and-suspenders: stamp the rect size explicitly so even
                // if the parent layout group ever flips controlWidth back, the
                // dots stay 11x11 instead of ballooning to 100x100.
                var dotRect = (RectTransform)dotGo.transform;
                dotRect.sizeDelta = new Vector2(DotSize, DotSize);
                var dotLe = dotGo.GetComponent<LayoutElement>();
                dotLe.preferredWidth = DotSize;
                dotLe.preferredHeight = DotSize;
                dotLe.minWidth = DotSize;
                dotLe.minHeight = DotSize;
                var dotImg = dotGo.GetComponent<Image>();
                dotImg.color = DotEmpty;
                dotImg.raycastTarget = false;
                dots[d] = dotImg;
            }

            // Click receiver — left = +1, right = -1.
            var click = go.AddComponent<ChipClickHandler>();
            click.Bind(this, index);

            return new Chip
            {
                Data = data,
                Background = fillImage,
                Border = border,
                Label = label,
                DescText = descText,
                LevelText = lvText,
                Dots = dots,
                MaxLevel = maxLevel,
            };
        }

        // ------------------------------------------------------------------ behaviour
        internal void HandleChipClick(int index, PointerEventData.InputButton button)
        {
            if (_isLocked) return;
            switch (button)
            {
                case PointerEventData.InputButton.Left:
                    AdjustLevel(index, +1);
                    break;
                case PointerEventData.InputButton.Right:
                    AdjustLevel(index, -1);
                    break;
            }
        }

        private void AdjustLevel(int index, int delta)
        {
            if (_isLocked) return;
            if (index < 0 || index >= _chips.Count) return;
            int max = MaxLevelOf(index);
            int next = Mathf.Clamp(_chipLevels[index] + delta, 0, max);
            if (next == _chipLevels[index]) return;
            _chipLevels[index] = next;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (_lockedNoticeText != null)
            {
                _lockedNoticeText.gameObject.SetActive(_isLocked);
                _lockedNoticeText.text = lockedNotice;
            }
            if (_chipContainer != null)
            {
                _chipContainer.gameObject.SetActive(!_isLocked);
            }

            for (int i = 0; i < _chips.Count; i++)
            {
                Chip c = _chips[i];
                if (c.Background == null || c.Label == null) continue;
                int level = i < _chipLevels.Length ? _chipLevels[i] : 0;
                bool active = !_isLocked && level >= 1;

                if (_isLocked)
                {
                    c.Background.color = ChipLockedBg;
                    c.Label.color = ChipLockedLabel;
                    if (c.Border != null) c.Border.color = BorderLocked;
                    if (c.LevelText != null)
                    {
                        c.LevelText.color = ChipLockedLabel;
                        c.LevelText.text = "Lv.—";
                    }
                    if (c.DescText != null)
                    {
                        c.DescText.color = ChipLockedLabel;
                        c.DescText.text = "";
                    }
                    PaintDots(c, 0);
                }
                else
                {
                    c.Background.color = active ? ChipActiveBg : ChipDefaultBg;
                    c.Label.color = ChipLabelColor;
                    if (c.Border != null) c.Border.color = active ? BorderActive : BorderDefault;
                    if (c.LevelText != null)
                    {
                        c.LevelText.color = active ? ChipLevelActive : ChipLabelColor;
                        c.LevelText.text = $"Lv.{level}/{c.MaxLevel}";
                    }
                    if (c.DescText != null)
                    {
                        // Show the effect summary at the *next* level when
                        // inactive so players can preview what +1 will do;
                        // once stacked, show the actual active stage values.
                        int previewLevel = active ? level : 1;
                        c.DescText.color = active
                            ? new Color(1f, 0.85f, 0.78f, 1f)
                            : new Color(0.72f, 0.68f, 0.62f, 1f);
                        c.DescText.text = BuildEffectSummary(c.Data, previewLevel);
                    }
                    PaintDots(c, level);
                }
            }
        }

        private static void PaintDots(Chip chip, int filledCount)
        {
            if (chip.Dots == null) return;
            for (int i = 0; i < chip.Dots.Length; i++)
            {
                if (chip.Dots[i] == null) continue;
                chip.Dots[i].color = i < filledCount ? DotFilled : DotEmpty;
            }
        }

        private List<(string id, int level)> GetSelectedStagesSnapshot()
        {
            var snap = new List<(string, int)>(_chips.Count);
            for (int i = 0; i < _chips.Count; i++)
            {
                int level = i < _chipLevels.Length ? _chipLevels[i] : 0;
                if (level < 1) continue;
                DifficultyModifierData data = _chips[i].Data;
                if (data != null && !string.IsNullOrEmpty(data.id)) snap.Add((data.id, level));
            }
            return snap;
        }

        private static void AddTextOutline(Text text)
        {
            if (text == null) return;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        // Builds a short, human-readable effect summary for a given stage of
        // a modifier. Combines the strongest numeric effect with any boolean
        // flag so single-trick modifiers (like onKillExplosion-only) still
        // show what the stack is doing — joined with " · ".
        private static string BuildEffectSummary(DifficultyModifierData data, int level)
        {
            if (data == null) return string.Empty;
            if (!data.TryGetStage(level, out DifficultyStage stage)) return string.Empty;

            string headline = null;

            // Pick the most prominent numeric stat as the headline.
            if (Mathf.Abs(stage.enemyModifiers.hpPct) > 0.0001f)
                headline = FormatPct("적 HP", stage.enemyModifiers.hpPct);
            else if (Mathf.Abs(stage.enemyModifiers.moveSpeedPct) > 0.0001f)
                headline = FormatPct("적 이동속도", stage.enemyModifiers.moveSpeedPct);
            else if (Mathf.Abs(stage.enemyModifiers.spawnRatePct) > 0.0001f)
                headline = FormatPct("적 스폰", stage.enemyModifiers.spawnRatePct);
            else if (Mathf.Abs(stage.playerModifiers.healRatePct) > 0.0001f)
                headline = FormatPct("회복", stage.playerModifiers.healRatePct);
            else if (Mathf.Abs(stage.playerModifiers.cooldownPct) > 0.0001f)
                headline = FormatPct("쿨다운", stage.playerModifiers.cooldownPct);
            else if (Mathf.Abs(stage.runtimeEffects.hazardMultiplier) > 0.0001f)
                headline = FormatPct("위험", stage.runtimeEffects.hazardMultiplier);

            // Boolean flag rides along — at Lv.1 it stands alone, at higher
            // levels it joins the headline so the chip reads
            // "처치 시 폭발 · 위험 +50%".
            if (stage.runtimeEffects.onKillExplosion)
            {
                if (string.IsNullOrEmpty(headline)) return "처치 시 폭발";
                return $"처치 시 폭발 · {headline}";
            }

            return headline ?? string.Empty;
        }

        // "적 HP" + 0.4 → "적 HP +40%". Negative percentages keep the sign so
        // healing nerfs read naturally ("회복 -30%").
        private static string FormatPct(string label, float pct)
        {
            int whole = Mathf.RoundToInt(pct * 100f);
            string sign = whole >= 0 ? "+" : string.Empty;
            return $"{label} {sign}{whole}%";
        }

        private struct Chip
        {
            public DifficultyModifierData Data;
            public Image Background;
            public Image Border;
            public Text Label;
            public Text DescText;
            public Text LevelText;
            public Image[] Dots;
            public int MaxLevel;
        }
    }

    // Routes left vs. right pointer clicks back to the panel so the panel
    // can stay a single source of truth for level state.
    internal sealed class ChipClickHandler : MonoBehaviour, IPointerClickHandler
    {
        private NightDashLobbyModifierPanel _owner;
        private int _index;

        public void Bind(NightDashLobbyModifierPanel owner, int index)
        {
            _owner = owner;
            _index = index;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_owner == null) return;
            _owner.HandleChipClick(_index, eventData.button);
        }
    }
}
