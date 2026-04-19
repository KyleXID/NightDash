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

        private void Start()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            // Zoom in: smaller ortho size = more zoomed in
            if (_camera != null && _camera.orthographic)
            {
                _camera.orthographicSize = targetOrthoSize;
            }
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

            // Force ortho size every frame in case anything else overrides it
            if (_camera.orthographic)
            {
                _camera.orthographicSize = targetOrthoSize;
            }

            using var players = _playerQuery.ToEntityArray(Allocator.Temp);
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
