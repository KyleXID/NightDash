using UnityEngine;
using NightDash.Data;
using NightDash.ECS.Systems;

namespace NightDash.Runtime
{
    /// <summary>
    /// ECS/게임 이벤트 ↔ AudioSource 어댑터. 씬에 1개 배치.
    /// 플레이스홀더 단계: boss kill(ECS 이벤트)만 자동 구독, 나머지 7 이벤트는
    /// UI/시스템 코드에서 <see cref="Play"/>를 직접 호출하도록 설계.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class NightDashAudioBridge : MonoBehaviour
    {
        [SerializeField] private AudioLibrary library;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
        }

        private void OnEnable()
        {
            NightDashCombatEvents.OnEnemyKilled += HandleEnemyKilled;
        }

        private void OnDisable()
        {
            NightDashCombatEvents.OnEnemyKilled -= HandleEnemyKilled;
        }

        /// <summary>One-shot 재생. library/클립이 비어있으면 조용히 무시.</summary>
        public void Play(AudioEventId eventId)
        {
            if (library == null || _audioSource == null)
            {
                return;
            }

            AudioClip clip = library.Resolve(eventId);
            if (clip == null)
            {
                return;
            }

            _audioSource.PlayOneShot(clip, sfxVolume);
        }

        private void HandleEnemyKilled(Unity.Mathematics.float3 position, bool isBoss)
        {
            if (!isBoss)
            {
                return;
            }

            Play(AudioEventId.BossKill);
        }
    }
}
