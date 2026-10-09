using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 툴팁 키워드 포매터 (정적 유틸리티 클래스)
    ///
    /// 역할:
    ///   - ScriptableObject(TooltipKeywordDatabase)에서 키워드 데이터를 로드합니다.
    ///   - 키워드 조회를 Dictionary로 캐싱해 O(1) 탐색을 제공합니다.
    ///   - 상태이상(StatusEffect)의 표시용 데이터를 빌드합니다.
    ///   - 본문 텍스트에서 등록된 키워드를 추출합니다.
    ///
    /// 이 클래스는 UI 컴포넌트(HoverTooltipUI)로부터 완전히 분리된 순수 데이터 처리 레이어입니다.
    /// MonoBehaviour가 아니므로 씬에 배치할 필요가 없습니다.
    /// </summary>
    public static class TooltipKeywordFormatter
    {
        // ═══════════════════════════════════════════════════════
        // 공개 데이터 타입
        // KeywordGlossaryPanelUI, StatusGlossaryPanelUI 등 외부 패널에 넘기는 표시용 구조체입니다.
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 키워드 목록 패널(KeywordGlossaryPanelUI)에 전달하는 표시용 데이터입니다.
        /// </summary>
        public readonly struct KeywordDisplayData
        {
            public readonly string Key;         // 키워드 이름 (예: "집중", "중독")
            public readonly string Description; // 키워드 설명 텍스트
            public readonly Color  Color;       // 키워드 강조 색상
            public readonly Sprite Icon;        // 키워드 아이콘 (없으면 null)

            public KeywordDisplayData(string key, string description, Color color, Sprite icon)
            {
                Key         = key;
                Description = description;
                Color       = color;
                Icon        = icon;
            }
        }

        /// <summary>
        /// 상태이상 목록 패널(StatusGlossaryPanelUI)에 전달하는 표시용 데이터입니다.
        /// </summary>
        public readonly struct StatusDisplayData
        {
            public readonly string Name;         // 표시 이름 (예: "집중") — enum 이름이 아닌 한국어
            public readonly string StackText;    // 스택 수 (예: "3"), 스택이 없으면 빈 문자열
            public readonly string DurationText; // 지속 텍스트 (예: "2T"), 무한 지속은 빈 문자열
            public readonly string Description;  // 상태이상 설명
            public readonly Color  Color;        // 텍스트 강조 색상

            public StatusDisplayData(string name, string stackText, string durationText, string description, Color color)
            {
                Name         = name;
                StackText    = stackText;
                DurationText = durationText;
                Description  = description;
                Color        = color;
            }
        }

        // ═══════════════════════════════════════════════════════
        // 내부 데이터 타입
        // DB에서 읽어온 항목을 런타임 캐시에 보관할 때 사용합니다.
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// TooltipKeywordDatabase의 항목을 런타임 Dictionary에 캐싱하는 내부 구조체입니다.
        /// </summary>
        private readonly struct KeywordEntry
        {
            public readonly string Key;
            public readonly string Description;
            public readonly Color  Color;
            public readonly Sprite Icon;

            public KeywordEntry(string key, string description, Color color, Sprite icon = null)
            {
                Key         = key;
                Description = description;
                Color       = color;
                Icon        = icon;
            }
        }

        // ═══════════════════════════════════════════════════════
        // 상수 & 정적 캐시
        // ═══════════════════════════════════════════════════════

        // Resources 폴더 기준 DB 에셋 경로
        private const string DatabaseResourcePath = "UI/TooltipKeywordDatabase";

        // DB에 색상이 지정되지 않은 키워드에 사용하는 기본 색상 (노란빛)
        private static readonly Color DefaultKeywordColor = new Color(1f, 0.83f, 0.42f, 1f);

        // DB에 색상이 지정되지 않은 상태이상에 사용하는 기본 색상 (연한 파란빛)
        private static readonly Color DefaultStatusColor = new Color(0.85f, 0.93f, 1f, 1f);

        /// <summary>
        /// C# 열거형 이름(영문) → 게임 내 표시 이름(한국어) 매핑 테이블입니다.
        /// StatusEffect의 Type.ToString()은 영문 열거형 이름이므로 이 테이블로 변환합니다.
        /// 대소문자를 구분하지 않습니다(OrdinalIgnoreCase).
        /// </summary>
        private static readonly Dictionary<string, string> StatusNameAliases =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "Honey",         "꿀"      },
                { "SlushSnow",     "진창눈"   },
                { "Focus",         "집중"    },
                { "Poison",        "중독"    },
                { "Weak",          "쇠약"    },
                { "Shock",         "감전"    },
                { "Catalyst",      "촉매"    },
                { "Inspire",       "고양"    },
                { "Weaken",        "약화"    },
                { "Charge",        "충전"    },
                { "Power",         "파워"    },
                { "Vulnerable",    "취약"    },
                { "Stun",          "기절"    },
                { "Silence",       "침묵"    },
                { "Bound",         "속박"    },
                { "CrystalStack",  "수정 중첩" },
                { "SnowDamageTaken", "받은 피해" },
                { "FrostStack",    "빙결"    },
                { "Slowed",        "둔화"    },
                { "BloodSugarSpike", "혈당 스파이크" },
                { "CrystalDamageTaken", "받은 피해" },
                { "BiteDamageTaken", "받은 피해" },
                { "VitalityStack", "활력" },
                { "BuffAttack",    "공격력 증가" },
                { "BuffDefense",   "방어력 증가" },
                { "DebuffAttack",  "공격력 감소" },
                { "DebuffDefense", "방어력 감소" },
                { "Dot",           "지속 피해" },
                { "Shield",        "보호막"   },
                { "Dodge",         "회피"    },
            };

        // ScriptableObject DB 인스턴스 캐시 (Resources.Load 반복 호출 방지)
        private static TooltipKeywordDatabase _cachedDatabase;

        // _lookup이 채워진 DB의 InstanceID — DB 에셋이 교체되면 자동으로 재구성합니다
        private static int _cachedDatabaseInstanceId;

        // 키워드 key → KeywordEntry 빠른 조회용 Dictionary (O(1) 탐색)
        // 기존 O(n) 배열 순회에서 개선되었습니다.
        private static Dictionary<string, KeywordEntry> _lookup;

        // 경고 로그를 최초 1회만 출력하기 위한 플래그
        private static bool _hasLoggedMissingDatabase;
        private static bool _hasLoggedEmptyDatabase;

        // ═══════════════════════════════════════════════════════
        // DB 로드 & Dictionary 캐시 구성
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 키워드 조회용 Dictionary를 반환합니다.
        /// DB 에셋이 처음 호출되거나 교체될 때만 재구성하고, 이후에는 캐시를 반환합니다.
        /// </summary>
        private static Dictionary<string, KeywordEntry> GetLookup()
        {
            var db = LoadDatabase();

            // DB 에셋 자체가 없으면 경고 후 빈 Dictionary를 반환합니다
            if (db == null)
            {
                if (!_hasLoggedMissingDatabase)
                {
                    Debug.LogWarning(
                        $"[TooltipKeywordFormatter] Resources/{DatabaseResourcePath}에서 DB를 찾을 수 없습니다." +
                        " 키워드 패널이 표시되지 않습니다.");
                    _hasLoggedMissingDatabase = true;
                }
                return _lookup ?? (_lookup = new Dictionary<string, KeywordEntry>());
            }

            // DB에 항목이 전혀 없을 때 경고 후 빈 Dictionary를 반환합니다
            if (db.entries == null || db.entries.Count == 0)
            {
                if (!_hasLoggedEmptyDatabase)
                {
                    Debug.LogWarning(
                        "[TooltipKeywordFormatter] DB에 키워드 항목이 없습니다." +
                        " TooltipKeywordDatabase 에셋에 키워드를 추가해주세요.");
                    _hasLoggedEmptyDatabase = true;
                }
                return _lookup ?? (_lookup = new Dictionary<string, KeywordEntry>());
            }

            int id = db.GetInstanceID();

            // 같은 DB가 이미 캐시되어 있으면 재구성 없이 반환합니다
            if (_lookup != null && _cachedDatabaseInstanceId == id)
                return _lookup;

            // DB가 처음 로드되거나 교체된 경우 → Dictionary를 새로 구성합니다
            _lookup = new Dictionary<string, KeywordEntry>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var entry in db.entries)
            {
                // key 또는 description이 비어있는 항목은 건너뜁니다
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
                if (string.IsNullOrWhiteSpace(entry.description))          continue;

                string trimmedKey = entry.key.Trim();

                // 동일 key가 중복 등록된 경우 먼저 등록된 항목을 우선합니다
                if (!_lookup.ContainsKey(trimmedKey))
                    _lookup[trimmedKey] = new KeywordEntry(
                        trimmedKey, entry.description.Trim(), entry.color, entry.icon);
            }

            _cachedDatabaseInstanceId = id;
            return _lookup;
        }

        /// <summary>
        /// Resources에서 DB 에셋을 로드합니다.
        /// 이미 로드된 경우 캐시된 인스턴스를 반환합니다.
        /// </summary>
        private static TooltipKeywordDatabase LoadDatabase()
        {
            if (_cachedDatabase != null) return _cachedDatabase;
            _cachedDatabase = Resources.Load<TooltipKeywordDatabase>(DatabaseResourcePath);
            return _cachedDatabase;
        }

        // ═══════════════════════════════════════════════════════
        // 공개 조회 메서드
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 키워드의 색상과 아이콘을 가져옵니다.
        /// DB에 없으면 기본값을 out 파라미터에 설정하고 false를 반환합니다.
        /// </summary>
        public static bool TryGetVisuals(string keyword, out Color color, out Sprite icon)
        {
            // DB에 없어도 색상은 기본값으로 항상 반환합니다
            color = DefaultKeywordColor;
            icon  = null;

            if (string.IsNullOrWhiteSpace(keyword)) return false;

            // O(1) Dictionary 룩업 (기존 O(n) 배열 순회에서 개선)
            if (GetLookup().TryGetValue(keyword, out var entry))
            {
                color = entry.Color;
                icon  = entry.Icon;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 키워드의 설명 텍스트를 가져옵니다.
        /// DB에 없으면 false를 반환합니다.
        /// </summary>
        public static bool TryGetDescription(string keyword, out string description)
        {
            description = null;
            if (string.IsNullOrWhiteSpace(keyword)) return false;

            // O(1) Dictionary 룩업
            if (GetLookup().TryGetValue(keyword, out var entry))
            {
                description = entry.Description;
                return true;
            }
            return false;
        }

        /// <summary>
        /// TMP 링크 ID(형식: "kw:키워드이름")에서 키워드 이름과 설명을 추출합니다.
        /// "kw:" 접두사로 시작하지 않거나 DB에 없으면 false를 반환합니다.
        /// </summary>
        public static bool TryGetDescriptionByLinkId(string linkId, out string keyword, out string description)
        {
            keyword     = null;
            description = null;

            if (string.IsNullOrWhiteSpace(linkId)) return false;

            // 링크 ID는 반드시 "kw:" 접두사로 시작해야 합니다
            const string prefix = "kw:";
            if (!linkId.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return false;

            // "kw:" 뒤의 키워드 이름을 추출합니다
            keyword = linkId.Substring(prefix.Length);
            if (string.IsNullOrWhiteSpace(keyword)) return false;

            return TryGetDescription(keyword, out description);
        }

        // ═══════════════════════════════════════════════════════
        // 텍스트 포매팅
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 원본 툴팁 텍스트를 메인 패널 표시용으로 정규화합니다.
        /// 현재는 끝 공백만 제거하며, 향후 추가 가공 로직이 생기면 여기서 처리합니다.
        /// </summary>
        public static string FormatMainTooltipText(string rawText)
        {
            return string.IsNullOrWhiteSpace(rawText) ? rawText : rawText.TrimEnd();
        }

        /// <summary>
        /// 이전 버전 호환을 위한 메서드입니다. 현재는 FormatMainTooltipText와 동일합니다.
        /// (키워드 설명이 별도 glossary 패널로 분리되면서 본문 가공이 불필요해졌습니다)
        /// </summary>
        public static string AppendKeywordSection(string rawText)
        {
            return FormatMainTooltipText(rawText);
        }

        /// <summary>
        /// 텍스트 안의 DB 키워드를 TMP 링크(&lt;link="kw:키워드"&gt;)로 감쌉니다.
        /// KeywordLinkHover가 링크 호버를 감지해 커서 옆 툴팁으로 정의를 띄운다.
        /// onLightBackground = true면 키워드 색을 밝은 배경 대비로 어둡게 보정.
        /// </summary>
        public static string InsertKeywordLinks(string rawText, bool onLightBackground = false)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return rawText;

            // 매칭되는 키워드 수집 후 긴 것부터 치환 (부분 문자열 이중 래핑 방지)
            var matched = new List<KeyValuePair<string, string>>();   // key → color hex
            foreach (var pair in GetLookup())
            {
                if (rawText.IndexOf(pair.Key, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                Color c = pair.Value.Color;
                if (onLightBackground)
                    c = Color.Lerp(c, new Color(0.08f, 0.09f, 0.12f), 0.45f);
                matched.Add(new KeyValuePair<string, string>(pair.Key, ColorUtility.ToHtmlStringRGB(c)));
            }
            if (matched.Count == 0) return rawText;
            matched.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));

            string result = rawText;
            foreach (var kv in matched)
            {
                // 이미 이 키워드가 링크 처리돼 있으면 건너뜀 (짧은 키가 긴 키의 래핑 결과를 재래핑하는 것 방지)
                if (result.IndexOf($"kw:{kv.Key}", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                result = ReplaceIgnoreCase(result, kv.Key,
                    $"<link=\"kw:{kv.Key}\"><color=#{kv.Value}><u>{kv.Key}</u></color></link>");
            }
            return result;
        }

        private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
        {
            var sb = new System.Text.StringBuilder();
            int prev = 0;
            int idx = source.IndexOf(oldValue, System.StringComparison.OrdinalIgnoreCase);
            while (idx >= 0)
            {
                sb.Append(source, prev, idx - prev);
                sb.Append(newValue);
                prev = idx + oldValue.Length;
                idx = source.IndexOf(oldValue, prev, System.StringComparison.OrdinalIgnoreCase);
            }
            sb.Append(source, prev, source.Length - prev);
            return sb.ToString();
        }

        /// <summary>
        /// 원본 텍스트에서 DB에 등록된 키워드를 모두 추출합니다.
        /// KeywordGlossaryPanelUI에 전달할 목록을 만들 때 사용합니다.
        ///
        /// 참고: 모든 DB 항목을 순회하므로 O(n)이지만,
        ///       툴팁 표시 시점에만 1회 호출되므로 실제 성능 문제는 없습니다.
        /// </summary>
        public static List<KeywordDisplayData> ExtractMatches(string rawText)
        {
            var result = new List<KeywordDisplayData>();
            if (string.IsNullOrWhiteSpace(rawText)) return result;

            // 등록된 모든 키워드를 순회하며 텍스트 내 포함 여부를 확인합니다
            foreach (var pair in GetLookup())
            {
                // 텍스트에 이 키워드가 없으면 건너뜁니다
                if (rawText.IndexOf(pair.Key, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                // 이미 결과에 같은 키워드가 있으면 중복 추가를 건너뜁니다
                bool alreadyAdded = result.Exists(
                    r => string.Equals(r.Key, pair.Key, System.StringComparison.OrdinalIgnoreCase));
                if (alreadyAdded) continue;

                var entry = pair.Value;
                result.Add(new KeywordDisplayData(entry.Key, entry.Description, entry.Color, entry.Icon));
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════
        // 상태이상 표시 데이터 빌드
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 상태이상 효과 하나의 표시 데이터(StatusDisplayData)를 만듭니다.
        /// StatusGlossaryPanelUI에 전달할 때 사용합니다.
        /// </summary>
        /// <param name="rawName">C# 열거형 이름 (예: "Focus") — StatusNameAliases로 한국어 변환됩니다</param>
        /// <param name="value">현재 스택 수 (0이면 스택 텍스트를 표시하지 않습니다)</param>
        /// <param name="duration">남은 지속 턴 수 (-1이면 무한 지속)</param>
        public static StatusDisplayData BuildStatusDisplayData(string rawName, int value, int duration)
        {
            // 열거형 이름을 게임 내 표시 이름으로 변환합니다 (예: "Focus" → "집중")
            string displayName = ResolveStatusDisplayName(rawName);

            // 스택이 있을 때만 스택 수를 표시합니다 (예: "3" — 중첩 수, x 접두 없음)
            string stackText = value > 0 ? value.ToString() : string.Empty;

            // 지속 턴 텍스트: 남은 턴 표시 (예: "2T"). -1(무한/영구)은 표기 생략
            string durationText = duration < 0 ? string.Empty : $"{duration}턴";

            // 설명: 표시 이름으로 먼저 검색, 없으면 원본 열거형 이름으로 재검색합니다
            if (!TryGetDescription(displayName, out string description))
                TryGetDescription(rawName, out description);

            // 색상: 표시 이름 → 원본 이름 순으로 검색, 둘 다 없으면 기본 색상을 사용합니다
            if (!TryGetVisuals(displayName, out Color color, out _) &&
                !TryGetVisuals(rawName, out color, out _))
                color = DefaultStatusColor;

            return new StatusDisplayData(displayName, stackText, durationText, description, color);
        }

        /// <summary>
        /// C# 열거형 이름을 게임 내 표시 이름으로 변환합니다.
        /// StatusNameAliases에 등록되지 않은 이름은 원본 그대로 반환합니다.
        /// </summary>
        private static string ResolveStatusDisplayName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return rawName;

            // 매핑 테이블에서 한국어 이름을 찾습니다
            if (StatusNameAliases.TryGetValue(rawName, out string mapped) &&
                !string.IsNullOrWhiteSpace(mapped))
                return mapped;

            // 매핑이 없으면 원본 이름을 그대로 사용합니다
            return rawName;
        }
    }
}
