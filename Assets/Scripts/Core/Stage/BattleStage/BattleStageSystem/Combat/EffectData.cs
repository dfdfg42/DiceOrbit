using UnityEngine;

namespace DiceOrbit.Data
{
    /// <summary>
    /// 효과 타입
    /// </summary>
    public enum EffectType
    {
        Damage,         // 데미지
        Heal,           // 회복
        BuffAttack,     // 공격력 버프
        BuffDefense,    // 방어력 버프
        DebuffAttack,   // 공격력 디버프
        DebuffDefense,  // 방어력 디버프
        Dot,            // 지속 데미지
        Shield,         // 보호막
        Focus,           // 마법사 집중 스택
        Honey,           // 꿀 타일 효과
        SlushSnow,      // 눈사람 진창눈 이동 디버프 (몬스터 전용 타일/상태 로직)
        Bound,           // 속박: 이동 불가(BindDebuff) (구 Frozen)
        Dodge,           // 회피율 (%)
        Vulnerable,      // 취약: 받는 피해량 증가 (구 Frostbite)
        Weak,            // 쇠약: 가하는 피해 -V% (포션)
        Poison,          // 독: 매턴 최대체력 V% 피해 (포션)
        Power,           // 파워: 가하는 피해 +V% (포션)
        FireExtinguishMark, // 불꽃 소화 마커: 이번 턴 이미 불을 껐음 (Wave5)
        FireDamageTaken,    // 불꽃 요정용: 이번 턴 받은 누적 피해 (Wave5)
        FireGuard,          // 불의 가호: 방어도 보유 시 받는 피해 감소 (Wave5)
        BoneMark,           // 뼈무덤 발동 마커: 이번 라운드 해골병사가 뼈 방어도 획득함 (Wave1)
        Stunned,            // 기절: 다음 턴 행동 불가(이동+스킬) (Wave3 수정)
        CrystalStack,       // 수정 중첩: 수정 핵 스택 카운터(가시화용 상태) (Wave3 수정)
        SnowDamageTaken,    // 받은 피해: 눈사람 진창눈 취소 판정용 누적 피해(가시화) (Wave3 눈사람)
        FrostStack,         // 빙결 중첩: 턴 종료 시 중첩만큼 피해 + 중첩 -1 (Wave3 눈)
        Slowed,             // 둔화: 이동 감소(MoveDebuff) (Wave0 슬라임)
        BloodSugarSpike,    // 혈당 스파이크: 다음 턴 이동 불가(BindDebuff) (Wave2 곰)
        CrystalDamageTaken, // 받은 피해: 수정 파편 이번 라운드 누적 피해(수정 화살 취소 판정, 가시화) (Wave3 수정)
        BiteDamageTaken,    // 받은 피해: 흡혈박쥐 이번 턴 누적 피해(깨물기 취소 판정, 가시화) (Wave6 흡혈박쥐)
        VitalityStack,      // 활력 스택: 식물 몬스터 활력(≤7이면 받는 피해 +20%, 가시화) (Wave7 농장)
        Shock,              // 감전: 이번 라운드 동안 받는 공격 피해 +V% (RoundStatus, 2026-10-09)
        Catalyst,           // 촉매: 다음 자동공격 행동 피해 +V% (시약 타일, 행동 전체 적용 후 제거) (2026-08-28)
        Inspire,            // 고양: 이번 라운드 동안 주는 공격 피해 +V% (RoundStatus, 2026-10-09)
        Weaken,             // 약화: 이번 라운드 동안 주는 공격 피해 -V% (RoundStatus, 2026-10-09)
    }

    /// <summary>
    /// 효과 데이터
    /// </summary>
    [System.Serializable]
    public class EffectData
    {
        public EffectType Type;
        public int Value;
        public int Duration = 0; // 0 = 즉시, 1+ = 턴 지속
        public string Description;
        
        public EffectData(EffectType type, int value, int duration = 0)
        {
            Type = type;
            Value = value;
            Duration = duration;
            Description = GenerateDescription();
        }
        
        private string GenerateDescription()
        {
            string durationText = (Duration == -1) ? "Permanent" : $"{Duration} turns";

            switch (Type)
            {
                case EffectType.Damage:
                    return $"Deal {Value} damage";
                case EffectType.Heal:
                    return $"Heal {Value} HP";
                case EffectType.BuffAttack:
                    return $"+{Value} ATK ({durationText})";
                case EffectType.BuffDefense:
                    return $"+{Value} DEF ({durationText})";
                case EffectType.Dot:
                    return $"{Value} damage ({durationText})";
                default:
                    return Type.ToString();
            }
        }
    }
}

