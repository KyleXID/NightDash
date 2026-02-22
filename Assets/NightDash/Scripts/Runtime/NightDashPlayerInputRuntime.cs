using System;
using UnityEngine;

namespace NightDash.Runtime
{
    public sealed class NightDashPlayerInputRuntime : MonoBehaviour
    {
        public static Vector2 MoveAxis { get; private set; }

        private static Type _keyboardType;
        private static object _currentKeyboard;
        private static bool _reflectionReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            var existing = FindFirstObjectByType<NightDashPlayerInputRuntime>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return;
            }

            var go = new GameObject("NightDashPlayerInputRuntime");
            go.AddComponent<NightDashPlayerInputRuntime>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            MoveAxis = ReadMoveAxis();
        }

        private static Vector2 ReadMoveAxis()
        {
            // Legacy input path (works when legacy manager is enabled).
            try
            {
                float x = Input.GetAxisRaw("Horizontal");
                float y = Input.GetAxisRaw("Vertical");
                return Normalize(x, y);
            }
            catch (InvalidOperationException)
            {
                // New Input System only mode: fall back to reflection.
            }

            EnsureReflectionReady();
            if (!_reflectionReady || _currentKeyboard == null)
            {
                return Vector2.zero;
            }

            float rx = 0f;
            float ry = 0f;
            if (IsPressed("aKey") || IsPressed("leftArrowKey")) rx -= 1f;
            if (IsPressed("dKey") || IsPressed("rightArrowKey")) rx += 1f;
            if (IsPressed("sKey") || IsPressed("downArrowKey")) ry -= 1f;
            if (IsPressed("wKey") || IsPressed("upArrowKey")) ry += 1f;
            return Normalize(rx, ry);
        }

        private static Vector2 Normalize(float x, float y)
        {
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        private static void EnsureReflectionReady()
        {
            if (_keyboardType != null || _reflectionReady)
            {
                return;
            }

            _keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            if (_keyboardType == null)
            {
                _reflectionReady = true;
                return;
            }

            var currentProp = _keyboardType.GetProperty("current");
            _currentKeyboard = currentProp != null ? currentProp.GetValue(null) : null;
            _reflectionReady = true;
        }

        private static bool IsPressed(string propertyName)
        {
            if (_currentKeyboard == null)
            {
                return false;
            }

            var keyProp = _keyboardType.GetProperty(propertyName);
            if (keyProp == null)
            {
                return false;
            }

            object keyControl = keyProp.GetValue(_currentKeyboard);
            if (keyControl == null)
            {
                return false;
            }

            var pressedProp = keyControl.GetType().GetProperty("isPressed");
            if (pressedProp == null)
            {
                return false;
            }

            object value = pressedProp.GetValue(keyControl);
            return value is bool b && b;
        }
    }
}
