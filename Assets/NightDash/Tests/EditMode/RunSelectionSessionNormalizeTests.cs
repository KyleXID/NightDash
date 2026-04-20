// S4-07: RunSelectionSession.Normalize 입력 길이 guard 회귀.
// FixedString64Bytes 캡 초과 시 fallback으로 대체되는지 검증해 크래시 방지 확인.

using System.Reflection;
using System.Text;
using NUnit.Framework;
using NightDash.Runtime;

namespace NightDash.Tests.EditMode
{
    [TestFixture]
    public class RunSelectionSessionNormalizeTests
    {
        private MethodInfo _normalizeMethod;

        [OneTimeSetUp]
        public void SetUp()
        {
            _normalizeMethod = typeof(RunSelectionSession).GetMethod(
                "Normalize",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(_normalizeMethod, Is.Not.Null,
                "Normalize 메서드는 RunSelectionSession 내부 static helper");
        }

        private string Invoke(string value, string fallback)
        {
            return (string)_normalizeMethod.Invoke(null, new object[] { value, fallback });
        }

        [Test]
        public void Normalize_EmptyString_Returns_Fallback()
        {
            Assert.That(Invoke("", "fallback"), Is.EqualTo("fallback"));
        }

        [Test]
        public void Normalize_Whitespace_Returns_Fallback()
        {
            Assert.That(Invoke("   ", "fallback"), Is.EqualTo("fallback"));
        }

        [Test]
        public void Normalize_Null_Returns_Fallback()
        {
            Assert.That(Invoke(null, "fallback"), Is.EqualTo("fallback"));
        }

        [Test]
        public void Normalize_Short_Value_Trims_And_Returns()
        {
            Assert.That(Invoke("  stage_01  ", "fallback"), Is.EqualTo("stage_01"));
        }

        [Test]
        public void Normalize_Exactly_Sixty_Bytes_Returns_Value()
        {
            string sixty = new string('a', 60);
            Assert.That(Encoding.UTF8.GetByteCount(sixty), Is.EqualTo(60));
            Assert.That(Invoke(sixty, "fallback"), Is.EqualTo(sixty));
        }

        [Test]
        public void Normalize_Over_Sixty_Bytes_Returns_Fallback()
        {
            string oversized = new string('a', 61);
            Assert.That(Invoke(oversized, "fallback"), Is.EqualTo("fallback"));
        }

        [Test]
        public void Normalize_Multibyte_Over_Limit_Returns_Fallback()
        {
            // Korean char = 3 bytes in UTF-8. 21자 × 3 = 63 bytes > 60 → fallback
            string koreanLong = new string('가', 21);
            Assert.That(Encoding.UTF8.GetByteCount(koreanLong), Is.GreaterThan(60));
            Assert.That(Invoke(koreanLong, "fallback"), Is.EqualTo("fallback"));
        }
    }
}
