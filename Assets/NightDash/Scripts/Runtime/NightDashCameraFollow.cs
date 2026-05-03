using NightDash.ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Unity.Mathematics;

namespace NightDash.Runtime
{
    public sealed class NightDashCameraFollow : MonoBehaviour
    {
        [SerializeField] private float smoothTime = 0.08f;
        [SerializeField] private bool lockZ = true;
        [SerializeField] private float fixedZ = -10f;

        private Camera _camera;
        private Vector3 _velocity;
        private EntityQuery _playerQuery;
        private EntityQuery _stageQuery;
        private EntityManager _entityManager;
        private bool _initialized;

        private static float _shakeIntensity;
        private static float _shakeDuration;
        private static float _shakeTimer;
        private static int _shakeSeed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateIfMissing()
        {
            // Skip preview/editor scenes — they don't have ECS player entities
            // and don't need camera-follow behavior. Avoids forcing ortho size
            // and player-query NRE.
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "AutoSetupPreview" || sceneName.EndsWith("Preview"))
            {
                return;
            }

            var existing = FindFirstObjectByType<NightDashCameraFollow>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            cam.gameObject.AddComponent<NightDashCameraFollow>();
        }

        /// <summary>
        /// Trigger camera shake from any system.
        /// Default for enemy hits: intensity=0.1f, duration=0.2f
        /// Default for boss attacks: intensity=0.3f, duration=0.5f
        /// </summary>
        public static void Shake(float intensity = 0.1f, float duration = 0.2f)
        {
            if (intensity > _shakeIntensity || _shakeTimer <= 0f)
            {
                _shakeIntensity = intensity;
                _shakeDuration = duration;
                _shakeTimer = duration;
                _shakeSeed = (int)(Time.unscaledTime * 1000f);
            }
        }

        [SerializeField] private float targetOrthoSize = 3.8f;

        // True if a PixelPerfectCamera component owns the ortho size.
        // Detected once at Start; we then stop forcing orthographicSize so the
        // pixel-perfect math is not overridden each frame.
        private bool _pixelPerfectOwnsOrtho;

        private void Start()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            _pixelPerfectOwnsOrtho = HasPixelPerfectCamera(_camera);

            // Zoom in: smaller ortho size = more zoomed in
            if (_camera != null && _camera.orthographic && !_pixelPerfectOwnsOrtho)
            {
                _camera.orthographicSize = targetOrthoSize;
            }
        }

        // Avoids a hard reference to the optional 2D Pixel Perfect package by
        // resolving the type via reflection. Returns true if the component
        // exists on the camera GameObject.
        private static bool HasPixelPerfectCamera(Camera cam)
        {
            if (cam == null) return false;
            var t = System.Type.GetType("UnityEngine.U2D.PixelPerfectCamera, Unity.2D.PixelPerfect.Runtime")
                 ?? System.Type.GetType("UnityEngine.Experimental.Rendering.Universal.PixelPerfectCamera, Unity.RenderPipelines.Universal.Runtime");
            if (t == null) return false;
            return cam.GetComponent(t) != null;
        }

        private void LateUpdate()
        {
            if (!EnsureInitialized())
            {
                return;
            }
            if (_camera == null)
            {
                return;
            }

            // Force ortho size every frame unless PixelPerfectCamera owns it.
            if (_camera.orthographic && !_pixelPerfectOwnsOrtho)
            {
                _camera.orthographicSize = targetOrthoSize;
            }

            // Guard against scenes without ECS world / player entity
            // (e.g. AutoSetupPreview, editor preview scenes).
            NativeArray<Entity> players;
            try
            {
                players = _playerQuery.ToEntityArray(Allocator.Temp);
            }
            catch (System.Exception)
            {
                _initialized = false;
                return;
            }

            using (players)
            {
            if (players.Length == 0)
            {
                return;
            }

            LocalTransform player = _entityManager.GetComponentData<LocalTransform>(players[0]);
            Vector3 current = _camera.transform.position;
            float targetZ = lockZ ? fixedZ : current.z;
            float targetX = player.Position.x;
            float targetY = player.Position.y;

            if (!_stageQuery.IsEmptyIgnoreFilter)
            {
                StageRuntimeConfig stage = _stageQuery.GetSingleton<StageRuntimeConfig>();
                if (stage.UseBounds == 1)
                {
                    float halfHeight = _camera.orthographic ? _camera.orthographicSize : 5f;
                    float halfWidth = halfHeight * _camera.aspect;
                    float minX = stage.BoundsMin.x + halfWidth;
                    float maxX = stage.BoundsMax.x - halfWidth;
                    float minY = stage.BoundsMin.y + halfHeight;
                    float maxY = stage.BoundsMax.y - halfHeight;

                    if (minX > maxX)
                    {
                        targetX = (stage.BoundsMin.x + stage.BoundsMax.x) * 0.5f;
                    }
                    else
                    {
                        targetX = math.clamp(targetX, minX, maxX);
                    }

                    if (minY > maxY)
                    {
                        targetY = (stage.BoundsMin.y + stage.BoundsMax.y) * 0.5f;
                    }
                    else
                    {
                        targetY = math.clamp(targetY, minY, maxY);
                    }
                }
            }

            Vector3 target = new Vector3(targetX, targetY, targetZ);
            Vector3 smoothed = Vector3.SmoothDamp(current, target, ref _velocity, smoothTime);

            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                float decay = Mathf.Clamp01(_shakeTimer / _shakeDuration);
                float currentIntensity = _shakeIntensity * decay;

                float time = Time.unscaledTime * 25f;
                float offsetX = (Mathf.PerlinNoise(_shakeSeed + time, 0f) - 0.5f) * 2f * currentIntensity;
                float offsetY = (Mathf.PerlinNoise(0f, _shakeSeed + time) - 0.5f) * 2f * currentIntensity;

                smoothed.x += offsetX;
                smoothed.y += offsetY;

                if (_shakeTimer <= 0f)
                {
                    _shakeIntensity = 0f;
                    _shakeDuration = 0f;
                }
            }

            _camera.transform.position = smoothed;
            } // end using (players)
        }

        // Public hook for RunTeardownBridge: snap the camera to the player's
        // current position and zero its smoothing velocity. Without this,
        // SmoothDamp would lerp from the previous run's last camera position
        // to the reset Player at origin — visible to the user as the camera
        // sliding back to (0,0,0) over ~0.08s when starting a new run.
        public void SnapToPlayer()
        {
            if (!EnsureInitialized() || _camera == null) return;

            using NativeArray<Entity> players = _playerQuery.ToEntityArray(Allocator.Temp);
            if (players.Length == 0) return;

            LocalTransform pt = _entityManager.GetComponentData<LocalTransform>(players[0]);
            float z = lockZ ? fixedZ : _camera.transform.position.z;
            _camera.transform.position = new Vector3(pt.Position.x, pt.Position.y, z);
            _velocity = Vector3.zero;
        }

        private bool EnsureInitialized()
        {
            if (_initialized)
            {
                return true;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            _entityManager = world.EntityManager;
            _playerQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            _stageQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<StageRuntimeConfig>());
            _initialized = true;
            return true;
        }
    }
}
