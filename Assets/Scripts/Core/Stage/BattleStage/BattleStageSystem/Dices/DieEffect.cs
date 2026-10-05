using UnityEngine;
using DiceOrbit.Core;   // Character, GoldManager, SubclassPickerAttribute

namespace DiceOrbit.Data
{
    /// <summary>주사위 사용 확정 시 넘어오는 컨텍스트 (최소).</summary>
    public class DieUseContext
    {
        public Character User;      // 이 주사위를 배정해 행동한 캐릭터 (null 가능)
        public int RolledValue;     // 이번에 굴려 나온 면 값
    }

    /// <summary>
    /// 주사위 '사용 시 효과' — 이벤트 결과(EventOutcome)/유물과 같은 [SerializeReference] 다형성.
    /// 새 효과 = 이 클래스를 상속한 클래스 하나 추가.
    /// </summary>
    [System.Serializable]
    public abstract class DieEffect
    {
        public Sprite Icon;                              // 호버 효과 행 아이콘(선택)
        public abstract string Apply(DieUseContext ctx); // 발동 + 사람이 읽을 요약 반환
        public virtual string Preview() => "";           // 호버 라벨용 짧은 설명
    }

    /// <summary>사용 시 골드 +N.</summary>
    [System.Serializable]
    public class GainGoldOnUse : DieEffect
    {
        public int amount = 20;
        public override string Apply(DieUseContext ctx) { GoldManager.EnsureInstance().AddGold(amount); return $"골드 +{amount}"; }
        public override string Preview() => $"사용 시 골드 +{amount}";
    }

    /// <summary>사용 시 사용한 캐릭터 HP +N.</summary>
    [System.Serializable]
    public class HealUserOnUse : DieEffect
    {
        public int amount = 5;
        public override string Apply(DieUseContext ctx)
        {
            var u = ctx?.User;
            if (u != null && u.IsAlive && u.Stats != null)
                u.Stats.CurrentHP = Mathf.Min(u.Stats.MaxHP, u.Stats.CurrentHP + amount);
            return $"HP +{amount}";
        }
        public override string Preview() => $"사용 시 체력 {amount} 회복";
    }

    /// <summary>사용 시 사용자에게 일시 방어도 +N (인챈트 이벤트 — 수호).</summary>
    [System.Serializable]
    public class GainArmorOnUse : DieEffect
    {
        public int amount = 10;
        public override string Apply(DieUseContext ctx)
        {
            var u = ctx?.User;
            if (u == null || !u.IsAlive || u.Stats == null) return "";
            u.Stats.TempArmor += amount;
            DiceOrbit.UI.CombatNotifier.Notify(u, $"방어도 +{amount}", new Color(0.6f, 0.75f, 1f));
            return $"방어도 +{amount}";
        }
        public override string Preview() => $"사용 시 방어도 +{amount}";
    }

    /// <summary>사용 시 사용자에게 고양(다음 공격 행동 피해 +N%, 1회 소모) 부여 (집중 주사위).</summary>
    [System.Serializable]
    public class InspireOnUse : DieEffect
    {
        public int percent = 15;
        public override string Apply(DieUseContext ctx)
        {
            var u = ctx?.User;
            if (u == null || !u.IsAlive || u.StatusEffects == null) return "";
            u.StatusEffects.AddEffect(
                DiceOrbit.Systems.Effects.StatusEffectManager.CreateEffect(EffectType.Inspire, percent, -1));
            var data = DiceOrbit.UI.TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Inspire.ToString(), percent, -1);
            DiceOrbit.UI.CombatNotifier.NotifyStatus(u, data.Name, data.Color);
            return $"고양 +{percent}%";
        }
        public override string Preview() => $"사용 시 다음 자동공격 피해 +{percent}%";
    }

    /// <summary>사용 시 사용자에게 걸린 해제 가능한 디버프 1개 제거 (정제 주사위).</summary>
    [System.Serializable]
    public class CleanseDebuffOnUse : DieEffect
    {
        // 해제 가능한 디버프 목록 — 순서 = 제거 우선순위. 새 디버프가 생기면 여기 추가.
        private static readonly EffectType[] Removable =
        {
            EffectType.Poison, EffectType.Stunned, EffectType.Bound, EffectType.Slowed,
            EffectType.Vulnerable, EffectType.Weak, EffectType.Weaken, EffectType.BloodSugarSpike,
        };

        public override string Apply(DieUseContext ctx)
        {
            var u = ctx?.User;
            if (u == null || !u.IsAlive || u.StatusEffects == null) return "";

            foreach (var type in Removable)
            {
                if (!u.StatusEffects.HasEffect(type)) continue;
                u.StatusEffects.RemoveEffect(type);
                var data = DiceOrbit.UI.TooltipKeywordFormatter.BuildStatusDisplayData(type.ToString(), 0, -1);
                DiceOrbit.UI.CombatNotifier.Notify(u, $"{data.Name} 해제!", new Color(0.65f, 0.95f, 0.75f));
                return $"{data.Name} 제거";
            }
            return "";
        }
        public override string Preview() => "사용 시 디버프 1개 해제";
    }

    /// <summary>사용 시 아직 사용하지 않은 주사위를 전부 재굴림 (혼돈 주사위).</summary>
    [System.Serializable]
    public class RerollUnusedOnUse : DieEffect
    {
        public override string Apply(DieUseContext ctx)
        {
            var dm = DiceManager.Instance;
            if (dm == null) return "";
            dm.RerollAvailableDice();
            return "미사용 주사위 재굴림";
        }
        public override string Preview() => "사용 시 남은 주사위 전부 다시 굴림";
    }

    /// <summary>사용 시 사용자에게 파워(피해 +N%, 1턴) 부여 (인챈트 이벤트 — 공세).</summary>
    [System.Serializable]
    public class EmpowerOnUse : DieEffect
    {
        public int percent = 10;
        public override string Apply(DieUseContext ctx)
        {
            var u = ctx?.User;
            if (u == null || !u.IsAlive || u.StatusEffects == null) return "";
            u.StatusEffects.AddEffect(
                DiceOrbit.Systems.Effects.StatusEffectManager.CreateEffect(EffectType.Power, percent, 1));
            var data = DiceOrbit.UI.TooltipKeywordFormatter.BuildStatusDisplayData(EffectType.Power.ToString(), percent, 1);
            DiceOrbit.UI.CombatNotifier.NotifyStatus(u, data.Name, data.Color);
            return $"피해 +{percent}% (1턴)";
        }
        public override string Preview() => $"사용 시 1턴 동안 주는 피해 +{percent}%";
    }
}
