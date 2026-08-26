using System.Collections.Generic;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.UI
{
    /// <summary>정보 패널이 소비하는 유닛 1명의 구조화 데이터. 서식 없음(원시값만).</summary>
    public readonly struct UnitInfoData
    {
        public readonly string Name;
        public readonly int CurrentHp;
        public readonly int MaxHp;
        public readonly int Armor;
        public readonly int Attack;                                          // 기본 공격력 (0이면 표기 생략 — 스탯 없는 몬스터 등)
        public readonly string FlavorText;                                   // 프로필 원문 (서식 없음)
        public readonly string ActivesLabel;                                 // 액티브 섹션 제목 (캐릭터 "액티브" / 몬스터 "다음 행동")
        public readonly IReadOnlyList<SkillInfoData> Actives;
        public readonly IReadOnlyList<PassiveInfoData> Passives;
        public readonly IReadOnlyList<ModifierInfoData> Modifiers;           // 장착 모디파이어 (몬스터는 빈 목록)
        public readonly IReadOnlyList<TooltipKeywordFormatter.StatusDisplayData> Statuses;
        public readonly TileInfoData? CurrentTile;                            // 몬스터는 null

        public UnitInfoData(string name, int currentHp, int maxHp, int armor, string flavorText,
            string activesLabel,
            IReadOnlyList<SkillInfoData> actives, IReadOnlyList<PassiveInfoData> passives,
            IReadOnlyList<ModifierInfoData> modifiers,
            IReadOnlyList<TooltipKeywordFormatter.StatusDisplayData> statuses, TileInfoData? currentTile,
            int attack = 0)
        {
            Name = name; CurrentHp = currentHp; MaxHp = maxHp; Armor = armor; Attack = attack; FlavorText = flavorText;
            ActivesLabel = activesLabel;
            Actives = actives; Passives = passives; Modifiers = modifiers;
            Statuses = statuses; CurrentTile = currentTile;
        }
    }

    /// <summary>장착 모디파이어 1종의 패널 표시 데이터. 같은 종류 중복 장착은 Count로 그룹핑.</summary>
    public readonly struct ModifierInfoData
    {
        public readonly string Name;
        public readonly string Description;
        public readonly int Count;          // 중복 장착 수 (1이면 단일)

        public ModifierInfoData(string name, string description, int count)
        {
            Name = name; Description = description; Count = count;
        }
    }

    /// <summary>액티브 스킬 1개의 패널 표시 데이터. (레벨 개념 없음 — 성장은 모디파이어)</summary>
    public readonly struct SkillInfoData
    {
        public readonly string Name;
        public readonly string DiceCondition;       // 예: "주사위 4 이상" (FormatDiceCondition())
        public readonly string DynamicDescription;  // GetDynamicDescription() 결과 원문
        public readonly string TargetLabel;         // 유효 대상 (모디파이어 반영 — 슬롯 게터 기준). 예: "적 2명"
        public readonly IReadOnlyList<string> ModifierLines;   // 이 스킬에 적용 중인 모디파이어 효과 라인 (×N 그룹핑)

        public SkillInfoData(string name, string diceCondition, string dynamicDescription,
            string targetLabel = "", IReadOnlyList<string> modifierLines = null)
        {
            Name = name; DiceCondition = diceCondition; DynamicDescription = dynamicDescription;
            TargetLabel = targetLabel; ModifierLines = modifierLines;
        }
    }

    /// <summary>패시브 1개의 패널 표시 데이터. 이름/효과/원문을 필드로 분리 (문자열 접합 금지).</summary>
    public readonly struct PassiveInfoData
    {
        public readonly string Name;
        public readonly string DynamicEffect;       // GetDynamicDescription() — 현재 유효 수치
        public readonly string FlavorText;          // Description 원문
        public readonly string LiveEffect;          // 구역 패시브의 '지금 효과' 한 줄 (예: "받는 피해 -20%"). 구역 패시브가 아니면 빈 문자열
        public readonly bool LiveActive;            // 지금 효과가 실제로 걸려 있는가 — false면 흐리게

        public PassiveInfoData(string name, string dynamicEffect, string flavorText, string liveEffect, bool liveActive)
        {
            Name = name; DynamicEffect = dynamicEffect; FlavorText = flavorText;
            LiveEffect = liveEffect ?? ""; LiveActive = liveActive;
        }
    }

    /// <summary>타일 1칸의 패널 표시 데이터.</summary>
    public readonly struct TileInfoData
    {
        public readonly int TileIndex;
        public readonly TileType Type;
        public readonly IReadOnlyList<TileAttributeInfo> Attributes;

        public TileInfoData(int tileIndex, TileType type, IReadOnlyList<TileAttributeInfo> attributes)
        {
            TileIndex = tileIndex; Type = type; Attributes = attributes;
        }
    }

    /// <summary>타일 속성 1개의 데이터. 이름/설명은 속성 인스턴스가 스스로 제공 (서브클래스 오버라이드), 아이콘은 렌더 계층이 DB 조회.</summary>
    public readonly struct TileAttributeInfo
    {
        public readonly TileAttributeType Type;
        public readonly int Value;
        public readonly int Duration;               // -1 = 영구
        public readonly string DisplayName;         // GetDisplayName() — 한국어 이름
        public readonly string Description;         // GetDescription() — 속성 효과 설명

        public TileAttributeInfo(TileAttributeType type, int value, int duration, string displayName, string description)
        {
            Type = type; Value = value; Duration = duration;
            DisplayName = displayName; Description = description;
        }
    }

    /// <summary>정보 패널에 자신을 표시할 수 있는 유닛. Character/Monster가 구현.</summary>
    public interface IBattleInfoProvider
    {
        UnitInfoData GetBattleInfo();
    }
}
