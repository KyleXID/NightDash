using UnityEngine;

namespace NightDash.Data
{
    /// <summary>
    /// GDD(ops/01_art_audio_pipeline.md §5)의 8 필수 이벤트를 AudioClip에 매핑한다.
    /// 플레이스홀더 단계에서는 슬롯만 존재하고 클립은 null — 실제 사운드 의뢰 후 할당.
    /// </summary>
    [CreateAssetMenu(menuName = "NightDash/Data/Audio Library", fileName = "audio_library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Header("Run Lifecycle")]
        public AudioClip runStart;
        public AudioClip runVictory;
        public AudioClip runDefeat;

        [Header("Combat Milestones")]
        public AudioClip levelUp;
        public AudioClip chestOpen;
        public AudioClip evolutionTrigger;

        [Header("Boss")]
        public AudioClip bossSpawn;
        public AudioClip bossKill;

        public AudioClip Resolve(AudioEventId eventId)
        {
            switch (eventId)
            {
                case AudioEventId.RunStart: return runStart;
                case AudioEventId.RunVictory: return runVictory;
                case AudioEventId.RunDefeat: return runDefeat;
                case AudioEventId.LevelUp: return levelUp;
                case AudioEventId.ChestOpen: return chestOpen;
                case AudioEventId.EvolutionTrigger: return evolutionTrigger;
                case AudioEventId.BossSpawn: return bossSpawn;
                case AudioEventId.BossKill: return bossKill;
                default: return null;
            }
        }
    }

    public enum AudioEventId
    {
        RunStart,
        RunVictory,
        RunDefeat,
        LevelUp,
        ChestOpen,
        EvolutionTrigger,
        BossSpawn,
        BossKill,
    }
}
