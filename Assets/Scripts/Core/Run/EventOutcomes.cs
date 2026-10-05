using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DiceOrbit.Data;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 이벤트 선택지의 결과 한 줄. 다형성 — 결과 종류마다 클래스 하나
    /// (유물 RuntimeArtifact / 패시브 PassiveAbility와 같은 [SerializeReference] 방식).
    /// 새 결과 추가 = 이 클래스를 상속한 클래스 하나 추가 (enum/switch 편집 없음).
    /// </summary>
    [System.Serializable]
    public abstract class EventOutcome
    {
        /// <summary>결과를 실제로 적용하고, 사람이 읽을 요약 한 줄을 반환.</summary>
        public abstract string Apply();

        /// <summary>선택지 라벨용 미리보기 (적용/부작용 없이 짧은 요약). 기본은 표시 안 함.</summary>
        public virtual string Preview() => "";

        /// <summary>이 결과를 지금 적용할 수 있는가 — EventUI가 선택지 활성 판정에 AND로 사용
        /// (예: 고대 요정 = 유물 보유, ONE OR ALL = 파티 2인 이상).</summary>
        public virtual bool CanApply() => true;

        /// <summary>살아있는 파티원 (파티 대상 결과 공용 헬퍼).</summary>
        protected static IEnumerable<Character> AliveParty()
        {
            var party = PartyManager.Instance?.Party;
            if (party == null) yield break;
            foreach (var c in party)
                if (c != null && c.IsAlive && c.Stats != null) yield return c;
        }
    }

    /// <summary>대상 선택 종류 — 순서가 선택 UI 순서 (Die 먼저).</summary>
    public enum EventSelectionKind { Die = 0, Character = 1 }

    /// <summary>이벤트 선택지에서 사용자가 고른 대상 묶음 — 같은 선택지의 Outcome들이 공유.</summary>
    public class EventTargetContext
    {
        public int SelectedDieIndex = -1;         // DiceDeckManager.Deck 인덱스
        public Character SelectedCharacter;
    }

    /// <summary>대상 지정이 필요한 결과. EventUI가 Kind별 선택 패널을 먼저 띄운 뒤 Apply(ctx)를 부른다.</summary>
    [System.Serializable]
    public abstract class TargetedEventOutcome : EventOutcome
    {
        public abstract EventSelectionKind Kind { get; }

        /// <summary>대상 없이 호출 금지 — 잘못 배선된 경우 빈 문자열 (로그로 표시).</summary>
        public sealed override string Apply()
        {
            UnityEngine.Debug.LogWarning($"[Event] {GetType().Name}: 대상 없이 Apply 호출됨 — EventTargetContext 경로를 쓰세요.");
            return "";
        }

        public abstract string Apply(EventTargetContext ctx);
    }

    /// <summary>결과 리스트 실행/미리보기 유틸.</summary>
    public static class EventOutcomes
    {
        /// <summary>결과를 전부 적용하고 합쳐진 요약을 돌려준다. 대상형은 ctx 경유.</summary>
        public static string Apply(List<EventOutcome> outcomes, EventTargetContext ctx)
        {
            if (outcomes == null || outcomes.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var outcome in outcomes)
            {
                if (outcome == null) continue;
                string line = outcome is TargetedEventOutcome targeted
                    ? (ctx != null ? targeted.Apply(ctx) : targeted.Apply())
                    : outcome.Apply();
                if (string.IsNullOrEmpty(line)) continue;
                if (sb.Length > 0) sb.Append("  ·  ");
                sb.Append(line);
            }
            return sb.ToString();
        }

        public static string Apply(List<EventOutcome> outcomes) => Apply(outcomes, null);

        /// <summary>선택지 라벨용 미리보기 (적용 없이 — 각 결과의 Preview 조합).</summary>
        public static string Preview(List<EventOutcome> outcomes)
        {
            if (outcomes == null) return "";

            var parts = new List<string>();
            foreach (var o in outcomes)
            {
                if (o == null) continue;
                string p = o.Preview();
                if (!string.IsNullOrEmpty(p)) parts.Add(p);
            }
            return string.Join(" · ", parts);
        }
    }

    // ── 구체 결과 (결과 하나 = 클래스 하나) ───────────────────────────

    /// <summary>골드 +N.</summary>
    [System.Serializable]
    public class GainGold : EventOutcome
    {
        public int amount = 30;
        public override string Apply() { GoldManager.EnsureInstance().AddGold(amount); return $"골드 +{amount}"; }
        public override string Preview() => $"골드 +{amount}";
    }

    /// <summary>골드 -N (0 밑으로 안 내려감 — GoldManager가 클램프).</summary>
    [System.Serializable]
    public class LoseGold : EventOutcome
    {
        public int amount = 20;
        public override string Apply() { GoldManager.EnsureInstance().AddGold(-amount); return $"골드 -{amount}"; }
        public override string Preview() => $"골드 -{amount}";
    }

    /// <summary>파티 전원 최대 HP의 N% 회복.</summary>
    [System.Serializable]
    public class HealParty : EventOutcome
    {
        [Range(0, 100)] public int percent = 30;
        public override string Apply()
        {
            foreach (var c in AliveParty())
            {
                int heal = Mathf.RoundToInt(c.Stats.MaxHP * percent / 100f);
                c.Stats.CurrentHP = Mathf.Min(c.Stats.MaxHP, c.Stats.CurrentHP + heal);
            }
            return $"파티 {percent}% 회복";
        }
        public override string Preview() => $"모든 아군이 최대 체력의 {percent}% 회복";
    }

    /// <summary>파티 전원 N 피해 (HP 1 미만 방지 — 전투 밖 사망 없음).</summary>
    [System.Serializable]
    public class DamageParty : EventOutcome
    {
        public int amount = 5;
        public override string Apply()
        {
            foreach (var c in AliveParty())
                c.Stats.CurrentHP = Mathf.Max(1, c.Stats.CurrentHP - amount);
            return $"파티 전원 {amount} 피해";
        }
        public override string Preview() => $"모든 아군이 피해 {amount}";
    }

    /// <summary>랜덤 포션 1개 (슬롯 가득이면 무효).</summary>
    [System.Serializable]
    public class GainRandomPotion : EventOutcome
    {
        public override string Apply()
        {
            var potion = PotionManager.EnsureInstance().GrantRandomDrop();
            return potion != null ? $"포션 획득 — {potion.PotionName}" : "포션 슬롯이 가득...";
        }
        public override string Preview() => "무작위 포션 1개 획득";
    }

    /// <summary>미보유 유물 랜덤 1개.</summary>
    [System.Serializable]
    public class GainRandomRelic : EventOutcome
    {
        public override string Apply()
        {
            var artifact = ArtifactManager.EnsureInstance().GrantRandom();
            return artifact != null ? $"유물 획득 — {artifact.artifactName}" : "";
        }
        public override string Preview() => "없는 유물 1개 무작위 획득";
    }

    /// <summary>덱의 랜덤 주사위 하나에 효과를 부여한다 (이벤트 결과 → DieInstance.AttachedEffect).</summary>
    [System.Serializable]
    public class AttachDieEffectOutcome : EventOutcome
    {
        [SerializeReference, SubclassPicker] public DieEffect effect;

        public override string Apply()
        {
            if (effect == null) return "";
            var deck = DiceDeckManager.Instance?.Deck;
            if (deck == null || deck.Count == 0) return "부여할 주사위가 없음";
            int idx = Random.Range(0, deck.Count);
            DiceDeckManager.Instance.AttachEffect(idx, effect);
            string name = deck[idx].BaseDie != null ? deck[idx].BaseDie.Name : "주사위";
            return $"{name}에 효과 부여 — {effect.Preview()}";
        }

        public override string Preview()
            => effect != null ? $"무작위 주사위에 효과 부여 — {effect.Preview()}" : "무작위 주사위에 효과 부여";
    }
}
