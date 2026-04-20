// S3-06: TutorialConfig 회귀 테스트.
// GDD 6 트리거(T0~T5) 전부 enum에 정의돼있고 메시지 해결이 동작하는지 잠금.

using NUnit.Framework;
using UnityEngine;
using NightDash.Data;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class TutorialConfigTests
    {
        [Test]
        public void TutorialTrigger_Enum_Covers_GDD_T0_Through_T5()
        {
            // GDD(03_tutorial_onboarding_script.md)는 T0~T5까지 6종 트리거를 정의.
            // 추가/삭제 시 본 테스트와 TutorialBridge 양쪽을 함께 수정.
            Assert.That(System.Enum.GetValues(typeof(TutorialTrigger)).Length, Is.EqualTo(6));

            Assert.That(System.Enum.IsDefined(typeof(TutorialTrigger), "RunStart"), Is.True);
            Assert.That(System.Enum.IsDefined(typeof(TutorialTrigger), "FirstKill"), Is.True);
            Assert.That(System.Enum.IsDefined(typeof(TutorialTrigger), "FirstLevelUp"), Is.True);
            Assert.That(System.Enum.IsDefined(typeof(TutorialTrigger), "LowHp"), Is.True);
            Assert.That(System.Enum.IsDefined(typeof(TutorialTrigger), "EliteSpawn"), Is.True);
            Assert.That(System.Enum.IsDefined(typeof(TutorialTrigger), "BossChestOpen"), Is.True);
        }

        [Test]
        public void TutorialConfig_Default_Display_And_Cooldown_Are_Reasonable()
        {
            TutorialConfig config = ScriptableObject.CreateInstance<TutorialConfig>();
            try
            {
                Assert.That(config.displaySeconds, Is.GreaterThan(0f),
                    "기본 노출 시간은 0보다 커야 함 (UX: 메시지 읽을 시간 확보)");
                Assert.That(config.rePromptCooldownSeconds, Is.GreaterThanOrEqualTo(5f),
                    "GDD §3 동일 카테고리 10초 쿨다운 기준 — 5초 미만은 과다 노출");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void TutorialEntry_Is_Addressable_By_Trigger()
        {
            TutorialConfig config = ScriptableObject.CreateInstance<TutorialConfig>();
            try
            {
                config.entries.Add(new TutorialEntry
                {
                    trigger = TutorialTrigger.RunStart,
                    message = "이동으로 균열을 피하세요."
                });
                config.entries.Add(new TutorialEntry
                {
                    trigger = TutorialTrigger.FirstKill,
                    message = "적 처치로 경험치를 획득합니다."
                });

                string msg = ResolveByTrigger(config, TutorialTrigger.FirstKill);
                Assert.That(msg, Is.EqualTo("적 처치로 경험치를 획득합니다."));

                string missing = ResolveByTrigger(config, TutorialTrigger.BossChestOpen);
                Assert.That(missing, Is.Null, "정의되지 않은 트리거는 null을 반환");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static string ResolveByTrigger(TutorialConfig config, TutorialTrigger trigger)
        {
            foreach (TutorialEntry entry in config.entries)
            {
                if (entry.trigger == trigger)
                {
                    return entry.message;
                }
            }
            return null;
        }
    }
}
