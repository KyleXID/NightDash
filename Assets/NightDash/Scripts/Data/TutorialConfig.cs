using System.Collections.Generic;
using UnityEngine;

namespace NightDash.Data
{
    /// <summary>
    /// GDD(ops/03_tutorial_onboarding_script.md) 6 트리거 매핑 저장소.
    /// 각 엔트리는 트리거 ID·메시지·노출 제한(쿨다운)을 갖는다.
    /// </summary>
    [CreateAssetMenu(menuName = "NightDash/Data/Tutorial Config", fileName = "tutorial_config")]
    public sealed class TutorialConfig : ScriptableObject
    {
        [Header("Display")]
        [Tooltip("하나의 메시지가 화면에 머무는 시간(초)")]
        public float displaySeconds = 4f;

        [Tooltip("동일 카테고리 재노출 방지 쿨다운(초)")]
        public float rePromptCooldownSeconds = 10f;

        [Header("Entries (GDD T0~T5)")]
        public List<TutorialEntry> entries = new();
    }

    [System.Serializable]
    public struct TutorialEntry
    {
        public TutorialTrigger trigger;
        public string message;
    }

    public enum TutorialTrigger
    {
        RunStart,      // T0: 런 시작 5초
        FirstKill,     // T1: enemyKill >= 1
        FirstLevelUp,  // T2: levelUp == 1
        LowHp,         // T3: hpRatio < 0.4
        EliteSpawn,    // T4: eliteSpawn == true
        BossChestOpen, // T5: chestBossOpen == true
    }
}
