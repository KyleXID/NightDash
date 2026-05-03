// Sprint B / M3 — Pause Menu screen.
// Auto-created at startup as inactive. Activated by NightDashGameplayPauseController
// when ESC is pressed during the Playing context. Owns the pause invariant
// (Time.timeScale=0 + GameplayPauseTag in the ECS world) and four menu actions:
// Resume, Settings (placeholder), Return to Lobby (RunTeardownBridge — Phase 4),
// Quit. Mirrors NightDashTitleScreenUI / NightDashLobbyScreenUI patterns:
//   - Direct Keyboard.current polling (avoids InputSystemUIInputModule defaults)
//   - unscaledDeltaTime animation (works while timeScale=0)
//   - OnDisable does NOT restore Time.timeScale unconditionally — Resume vs
//     Return-to-Lobby differ on whether the next screen owns the freeze policy.

using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NightDash.ECS.Components;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightDash.Runtime.UI
{
    public sealed class NightDashPauseMenuUI : MonoBehaviour
    {
        // Higher than Lobby (4500) and Title (5000) so the pause overlay
        // always covers any game UI underneath.
        private const int PauseCanvasSortingOrder = 6000;
        private const float BackdropAlpha = 0.62f;

        private const string ButtonResumeLabel = "Resume";
        private const string ButtonSettingsLabel = "Settings";
        private const string ButtonReturnLobbyLabel = "Return to Lobby";
        private const string ButtonQuitLabel = "Quit";

        // Settings is intentionally disabled in M3 — placeholder until the
        // settings panel ships in a later sprint. Navigation skips it.
        private static readonly bool[] ButtonEnabled = { true, false, true, true };

        private Canvas _canvas;
        private GameObject _backdrop;
        private readonly GameObject[] _buttonObjects = new GameObject[4];
        private readonly Text[] _buttonLabels = new Text[4];
        private int _selectedIndex;

        // Set true when a menu action explicitly hands off ownership of
        // Time.timeScale to the next screen (Return-to-Lobby case). When
        // true, OnDisable will NOT restore timeScale — letting the next
        // screen apply its own policy.
        private bool _handsOffTimeScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            var existing = FindFirstObjectByType<NightDashPauseMenuUI>(FindObjectsInactive.Include);
            if (existing != null) return;

            var go = new GameObject("NightDashPauseMenuUI");
            go.AddComponent<NightDashPauseMenuUI>();
            go.SetActive(false); // Hidden until ESC during Playing.
        }

        private void Awake()
        {
            EnsureEventSystem();
            BuildCanvas();
        }

        private void OnEnable()
        {
            // Take ownership of input + screen + simulation pause.
            NightDashInputContextStack.Push(NightDashInputContext.Pause);
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Paused);
            Time.timeScale = 0f;
            EnsurePauseTag();

            _handsOffTimeScale = false;
            SelectIndex(FirstEnabledIndex());
        }

        private void OnDisable()
        {
            // Always release the pause invariants we set on enable. Even on
            // hand-off paths, the next screen will (re)apply its own policy.
            NightDashInputContextStack.Pop(NightDashInputContext.Pause);
            RemovePauseTag();

            // Resume path restores 1x; hand-off path leaves timeScale=0 and
            // lets the next screen (Lobby) decide. Title/Lobby OnEnable both
            // set their own policy explicitly.
            if (!_handsOffTimeScale)
            {
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            if (NightDashInputContextStack.Top != NightDashInputContext.Pause) return;

            ReadKeyboard(out bool up, out bool down, out bool confirm, out bool cancel);

            if (up) SelectIndex(StepIndex(_selectedIndex, -1));
            else if (down) SelectIndex(StepIndex(_selectedIndex, +1));
            else if (confirm) ClickSelected();
            else if (cancel) Resume();
        }

        // ====================================================================
        // Menu actions
        // ====================================================================

        private void ClickSelected()
        {
            switch (_selectedIndex)
            {
                case 0: Resume(); break;
                case 1: /* Settings disabled */ break;
                case 2: ReturnToLobby(); break;
                case 3: Quit(); break;
            }
        }

        private void Resume()
        {
            // SetActive(false) cascades into OnDisable which restores everything.
            gameObject.SetActive(false);
        }

        private void ReturnToLobby()
        {
            // Phase 4 will route through RunTeardownBridge.DestroyCurrentRun()
            // before activating Lobby. For now this is a soft transition that
            // hides the pause menu and surfaces Lobby — entities from the run
            // are NOT yet cleaned up. TODO(M3 Phase 4): wire teardown bridge.
            _handsOffTimeScale = true;

            var lobby = FindFirstObjectByType<NightDashLobbyScreenUI>(FindObjectsInactive.Include);
            if (lobby != null) lobby.gameObject.SetActive(true);
            NightDashUIScreenRouter.GoTo(NightDashUIScreen.Lobby);
            gameObject.SetActive(false);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ====================================================================
        // ECS pause-tag helpers
        // ====================================================================

        private static void EnsurePauseTag()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameplayPauseTag>());
            if (!query.IsEmpty) return; // Idempotent.
            var e = em.CreateEntity();
            em.AddComponent<GameplayPauseTag>(e);
        }

        private static void RemovePauseTag()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameplayPauseTag>());
            if (query.IsEmpty) return;
            em.DestroyEntity(query);
        }

        // ====================================================================
        // Input
        // ====================================================================

        private static void ReadKeyboard(out bool up, out bool down, out bool confirm, out bool cancel)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
            {
                up = down = confirm = cancel = false;
                return;
            }
            up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            confirm = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame
                      || kb.numpadEnterKey.wasPressedThisFrame;
            cancel = kb.escapeKey.wasPressedThisFrame;
#else
            up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)
                      || Input.GetKeyDown(KeyCode.KeypadEnter);
            cancel = Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private int StepIndex(int from, int direction)
        {
            int n = _buttonObjects.Length;
            int idx = from;
            for (int i = 0; i < n; i++)
            {
                idx = (idx + direction + n) % n;
                if (ButtonEnabled[idx]) return idx;
            }
            return from; // No enabled button found (shouldn't happen).
        }

        private static int FirstEnabledIndex()
        {
            for (int i = 0; i < ButtonEnabled.Length; i++)
            {
                if (ButtonEnabled[i]) return i;
            }
            return 0;
        }

        // ====================================================================
        // Canvas construction
        // ====================================================================

        private void BuildCanvas()
        {
            // Root canvas — overlay so it floats above all gameplay rendering.
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = PauseCanvasSortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            BuildBackdrop();
            BuildTitleLabel();
            BuildButtons();
        }

        private void BuildBackdrop()
        {
            _backdrop = new GameObject("Backdrop");
            var rect = _backdrop.AddComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = _backdrop.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, BackdropAlpha);
            img.raycastTarget = true; // Block clicks to gameplay underneath.
        }

        private void BuildTitleLabel()
        {
            var go = new GameObject("PausedTitle");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.78f);
            rect.anchorMax = new Vector2(0.5f, 0.78f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600f, 120f);
            rect.anchoredPosition = Vector2.zero;

            var text = go.AddComponent<Text>();
            text.text = "PAUSED";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 88;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.92f, 0.78f, 1f); // Warm parchment tone.
            text.raycastTarget = false;
        }

        private void BuildButtons()
        {
            string[] labels = { ButtonResumeLabel, ButtonSettingsLabel, ButtonReturnLobbyLabel, ButtonQuitLabel };

            const float buttonWidth = 360f;
            const float buttonHeight = 60f;
            const float buttonSpacing = 18f;
            const float stackY = -20f; // Slightly below screen center.

            int n = labels.Length;
            float totalHeight = n * buttonHeight + (n - 1) * buttonSpacing;
            float startY = stackY + totalHeight * 0.5f - buttonHeight * 0.5f;

            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"Button_{labels[i].Replace(" ", "")}");
                var rect = go.AddComponent<RectTransform>();
                rect.SetParent(transform, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
                rect.anchoredPosition = new Vector2(0f, startY - i * (buttonHeight + buttonSpacing));

                var bg = go.AddComponent<Image>();
                bg.color = new Color(0.10f, 0.08f, 0.12f, 0.85f);
                bg.raycastTarget = false; // Keyboard-only menu.

                var labelGo = new GameObject("Label");
                var labelRect = labelGo.AddComponent<RectTransform>();
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                var text = labelGo.AddComponent<Text>();
                text.text = labels[i];
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 32;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;

                _buttonObjects[i] = go;
                _buttonLabels[i] = text;
            }
        }

        private void SelectIndex(int idx)
        {
            _selectedIndex = idx;
            for (int i = 0; i < _buttonObjects.Length; i++)
            {
                if (_buttonObjects[i] == null || _buttonLabels[i] == null) continue;
                bool enabled = ButtonEnabled[i];
                bool selected = (i == _selectedIndex);

                var bg = _buttonObjects[i].GetComponent<Image>();
                if (bg != null)
                {
                    if (!enabled)
                    {
                        bg.color = new Color(0.05f, 0.05f, 0.06f, 0.55f);
                    }
                    else if (selected)
                    {
                        bg.color = new Color(0.32f, 0.16f, 0.06f, 0.95f); // warm bronze accent
                    }
                    else
                    {
                        bg.color = new Color(0.10f, 0.08f, 0.12f, 0.85f);
                    }
                }

                if (!enabled)
                {
                    _buttonLabels[i].color = new Color(0.45f, 0.45f, 0.45f, 1f);
                }
                else if (selected)
                {
                    _buttonLabels[i].color = new Color(1f, 0.95f, 0.78f, 1f);
                }
                else
                {
                    _buttonLabels[i].color = new Color(0.78f, 0.74f, 0.68f, 1f);
                }
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
