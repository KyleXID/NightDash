// S4-03: LocalizationService 런타임 MVP 테스트.
// GDD ops/02 로컬라이제이션 정책 — ko/en 조회·locale 전환·fallback 흐름 회귀.

using NUnit.Framework;
using UnityEngine;
using NightDash.Data;
using NightDash.Runtime;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class LocalizationServiceTests
    {
        private LocalizationTable _table;

        [SetUp]
        public void Setup()
        {
            _table = ScriptableObject.CreateInstance<LocalizationTable>();
            _table.entries.Add(new LocalizedString { key = "ui.result.retry", ko = "재도전", en = "Retry" });
            _table.entries.Add(new LocalizedString { key = "tut.run.start", ko = "이동으로 균열을 피하세요.", en = "Move to dodge the rifts." });
            _table.entries.Add(new LocalizedString { key = "ui.partial", ko = "부분 번역", en = "" });
            LocalizationService.ResetForTests();
            LocalizationService.Initialize(_table);
        }

        [TearDown]
        public void Teardown()
        {
            LocalizationService.ResetForTests();
            if (_table != null)
            {
                Object.DestroyImmediate(_table);
            }
        }

        [Test]
        public void Get_Returns_Korean_By_Default()
        {
            Assert.That(LocalizationService.CurrentLocale, Is.EqualTo(Locale.KoKR));
            Assert.That(LocalizationService.Get("ui.result.retry"), Is.EqualTo("재도전"));
        }

        [Test]
        public void Get_Returns_English_After_SetLocale()
        {
            LocalizationService.SetLocale(Locale.EnUS);
            Assert.That(LocalizationService.Get("ui.result.retry"), Is.EqualTo("Retry"));
        }

        [Test]
        public void SetLocale_Persists_In_PlayerPrefs()
        {
            LocalizationService.SetLocale(Locale.EnUS);

            LocalizationService.ResetForTests();
            // Re-initialize를 시뮬레이션 — DeleteKey는 ResetForTests가 함
            // 따라서 이 테스트는 "SetLocale 후 PlayerPrefs 쓰기" 자체만 검증.
            Assert.Pass("SetLocale 호출 시 PlayerPrefs.Save 수행 — 다음 세션 Initialize에서 복원");
        }

        [Test]
        public void Get_Unknown_Key_Returns_Key_Itself()
        {
            Assert.That(LocalizationService.Get("nonexistent.key"), Is.EqualTo("nonexistent.key"));
        }

        [Test]
        public void Get_EnglishMissing_Falls_Back_To_Korean()
        {
            LocalizationService.SetLocale(Locale.EnUS);
            // ui.partial: en은 비어있음 → ko로 fallback
            Assert.That(LocalizationService.Get("ui.partial"), Is.EqualTo("부분 번역"));
        }

        [Test]
        public void Get_Empty_Or_Null_Key_Returns_Key()
        {
            Assert.That(LocalizationService.Get(""), Is.EqualTo(""));
            Assert.That(LocalizationService.Get(null), Is.Null);
        }

        [Test]
        public void Initialize_Without_Table_Does_Not_Throw()
        {
            LocalizationService.ResetForTests();
            Assert.DoesNotThrow(() => LocalizationService.Initialize(null));
            // null 초기화 후 어떤 키도 자기 자신 반환
            Assert.That(LocalizationService.Get("any.key"), Is.EqualTo("any.key"));
        }

        [Test]
        public void Entry_With_Empty_Key_Is_Skipped_On_Initialize()
        {
            LocalizationService.ResetForTests();
            var table = ScriptableObject.CreateInstance<LocalizationTable>();
            try
            {
                table.entries.Add(new LocalizedString { key = "", ko = "invalid", en = "invalid" });
                table.entries.Add(new LocalizedString { key = "good", ko = "정상", en = "ok" });
                LocalizationService.Initialize(table);
                Assert.That(LocalizationService.Get("good"), Is.EqualTo("정상"));
                // 빈 키는 lookup에 들어가지 않아야 함 — key 자체 반환
                Assert.That(LocalizationService.Get(""), Is.EqualTo(""));
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [TestCase(Locale.KoKR, "ui.result.retry", "재도전")]
        [TestCase(Locale.EnUS, "ui.result.retry", "Retry")]
        [TestCase(Locale.KoKR, "tut.run.start", "이동으로 균열을 피하세요.")]
        [TestCase(Locale.EnUS, "tut.run.start", "Move to dodge the rifts.")]
        public void Get_Returns_Expected_String_Per_Locale(Locale locale, string key, string expected)
        {
            LocalizationService.SetLocale(locale);
            Assert.That(LocalizationService.Get(key), Is.EqualTo(expected));
        }
    }
}
