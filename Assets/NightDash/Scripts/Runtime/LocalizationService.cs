using System.Collections.Generic;
using NightDash.Data;
using UnityEngine;

namespace NightDash.Runtime
{
    /// <summary>
    /// 로컬라이제이션 런타임 조회 서비스. 일회성 초기화(Initialize) 후 키로 문자열 조회.
    /// 키 미존재·언어 미번역 시 fallback 흐름: 현재언어 → ko → key 문자열 그대로.
    /// 프레임 당 호출을 고려해 Dictionary 캐싱 사용.
    /// </summary>
    public static class LocalizationService
    {
        private const string LocalePrefKey = "nightdash.loc.locale";

        private static Locale _currentLocale = Locale.KoKR;
        private static Dictionary<string, LocalizedString> _lookup;

        public static Locale CurrentLocale => _currentLocale;

        public static void Initialize(LocalizationTable table)
        {
            _lookup = new Dictionary<string, LocalizedString>();
            if (table == null)
            {
                return;
            }

            foreach (LocalizedString entry in table.entries)
            {
                if (string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }
                _lookup[entry.key] = entry;
            }

            string saved = PlayerPrefs.GetString(LocalePrefKey, "");
            _currentLocale = ParseLocale(saved, Locale.KoKR);
        }

        public static void SetLocale(Locale locale)
        {
            _currentLocale = locale;
            PlayerPrefs.SetString(LocalePrefKey, locale.ToString());
            PlayerPrefs.Save();
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return key;
            }

            if (_lookup == null || !_lookup.TryGetValue(key, out LocalizedString entry))
            {
                return key;
            }

            string primary = SelectLanguage(entry, _currentLocale);
            if (!string.IsNullOrWhiteSpace(primary))
            {
                return primary;
            }

            string koFallback = entry.ko;
            if (!string.IsNullOrWhiteSpace(koFallback))
            {
                return koFallback;
            }

            return key;
        }

        internal static string SelectLanguage(LocalizedString entry, Locale locale)
        {
            switch (locale)
            {
                case Locale.EnUS: return entry.en;
                case Locale.KoKR: return entry.ko;
                default: return entry.ko;
            }
        }

        private static Locale ParseLocale(string raw, Locale fallback)
        {
            if (System.Enum.TryParse(raw, out Locale parsed))
            {
                return parsed;
            }
            return fallback;
        }

        // 테스트 전용: 싱글톤 상태 초기화.
        internal static void ResetForTests()
        {
            _lookup = null;
            _currentLocale = Locale.KoKR;
            PlayerPrefs.DeleteKey(LocalePrefKey);
        }
    }
}
