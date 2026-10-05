using System.Linq;
using UnityEngine;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Core.Run
{
    // ── 주사위 대상 (교정기/인챈트/회복기) ─────────────────────

    /// <summary>선택 주사위의 무작위 면 count개를 1~6 무작위 값으로 (교정기 1).</summary>
    [System.Serializable]
    public class RandomizeDieFaces : TargetedEventOutcome
    {
        public int count = 2;
        public override EventSelectionKind Kind => EventSelectionKind.Die;

        public override string Apply(EventTargetContext ctx)
        {
            DiceDeckManager.EnsureInstance()?.RandomizeFaces(ctx.SelectedDieIndex, count);
            return $"면 {count}개 무작위 변환";
        }
        public override string Preview() => $"선택한 주사위의 면 {count}개를 무작위로 다시 정함";
    }

    /// <summary>선택 주사위의 무작위 면 count개에 delta (교정기 2 — 기본 -1, 하한 0).</summary>
    [System.Serializable]
    public class AddToDieFaces : TargetedEventOutcome
    {
        public int count = 3;
        public int delta = -1;
        public override EventSelectionKind Kind => EventSelectionKind.Die;

        public override string Apply(EventTargetContext ctx)
        {
            DiceDeckManager.EnsureInstance()?.AddToRandomFaces(ctx.SelectedDieIndex, count, delta);
            return $"면 {count}개 {(delta >= 0 ? "+" : "")}{delta}";
        }
        public override string Preview() => $"선택한 주사위의 무작위 면 {count}개 값 {(delta >= 0 ? "+" : "-")}{Mathf.Abs(delta)}";
    }

    /// <summary>선택 주사위의 무작위 면 1개를 0으로 + 인챈트 부여(선택 — null이면 면만 0).
    /// 인챈트/회복기의 대가 겸용. 재부여 시 기존 인챈트 교체 (AttachEffect 덮어쓰기).</summary>
    [System.Serializable]
    public class ZeroDieFaceAndEnchant : TargetedEventOutcome
    {
        [SerializeReference, SubclassPicker] public DieEffect enchant;
        public override EventSelectionKind Kind => EventSelectionKind.Die;

        public override string Apply(EventTargetContext ctx)
        {
            var dm = DiceDeckManager.EnsureInstance();
            if (dm == null) return "";
            dm.ZeroRandomFace(ctx.SelectedDieIndex);
            if (enchant != null)
            {
                dm.AttachEffect(ctx.SelectedDieIndex, enchant);
                return $"면 1개 → 0, 인챈트 부여 — {enchant.Preview()}";
            }
            return "면 1개 → 0";
        }
        public override string Preview()
            => enchant != null
                ? $"선택한 주사위의 무작위 면 1개를 0으로 + 효과 부여 — {enchant.Preview()}"
                : "선택한 주사위의 무작위 면 1개를 0으로";
    }

    // ── 캐릭터 대상 (회복기/ONE OR ALL) ───────────────────────

    /// <summary>선택 캐릭터 최대체력의 percent% 회복 (전투 밖 — HealParty 관례의 직접 HP).</summary>
    [System.Serializable]
    public class HealSelectedCharacter : TargetedEventOutcome
    {
        [Range(1, 100)] public int percent = 10;
        public override EventSelectionKind Kind => EventSelectionKind.Character;

        public override string Apply(EventTargetContext ctx)
        {
            var c = ctx.SelectedCharacter;
            if (c == null || !c.IsAlive || c.Stats == null) return "";
            int heal = Mathf.Max(1, c.Stats.MaxHP * percent / 100);
            c.Stats.CurrentHP = Mathf.Min(c.Stats.MaxHP, c.Stats.CurrentHP + heal);
            return $"{c.Stats.CharacterName} {heal} 회복";
        }
        public override string Preview() => $"선택한 캐릭터가 최대 체력의 {percent}% 회복";
    }

    /// <summary>ONE FOR ALL — 선택 제외 각자 MaxHP percent% 감소(최소 1, MaxHP 하한 1),
    /// 감소 총합만큼 선택 캐릭터 MaxHP·CurrentHP 증가.</summary>
    [System.Serializable]
    public class OneForAll : TargetedEventOutcome
    {
        [Range(1, 100)] public int percent = 10;
        public override EventSelectionKind Kind => EventSelectionKind.Character;
        public override bool CanApply() => AliveParty().Count() >= 2;

        public override string Apply(EventTargetContext ctx)
        {
            var chosen = ctx.SelectedCharacter;
            if (chosen == null || chosen.Stats == null) return "";

            int total = 0;
            foreach (var c in AliveParty())
            {
                if (c == chosen) continue;
                int loss = Mathf.Max(1, c.Stats.MaxHP * percent / 100);
                loss = Mathf.Min(loss, c.Stats.MaxHP - 1);          // MaxHP 하한 1
                if (loss <= 0) continue;
                c.Stats.MaxHP -= loss;
                c.Stats.CurrentHP = Mathf.Clamp(c.Stats.CurrentHP, 1, c.Stats.MaxHP);
                total += loss;
            }
            chosen.Stats.MaxHP += total;
            chosen.Stats.CurrentHP += total;
            return $"{chosen.Stats.CharacterName} 최대체력 +{total}";
        }
        public override string Preview() => $"다른 아군 최대 체력 -{percent}%, 그만큼 선택한 캐릭터 최대 체력 증가";
    }

    /// <summary>ALL FOR ONE — 선택 캐릭터 MaxHP percent% 감소(MaxHP 하한 1),
    /// 감소분을 나머지에게 균등 분배 (나머지 몫은 앞 순서부터 +1).</summary>
    [System.Serializable]
    public class AllForOne : TargetedEventOutcome
    {
        [Range(1, 100)] public int percent = 30;
        public override EventSelectionKind Kind => EventSelectionKind.Character;
        public override bool CanApply() => AliveParty().Count() >= 2;

        public override string Apply(EventTargetContext ctx)
        {
            var chosen = ctx.SelectedCharacter;
            if (chosen == null || chosen.Stats == null) return "";

            var others = AliveParty().Where(c => c != chosen).ToList();
            if (others.Count == 0) return "";

            int loss = Mathf.Max(1, chosen.Stats.MaxHP * percent / 100);
            loss = Mathf.Min(loss, chosen.Stats.MaxHP - 1);
            if (loss <= 0) return "";
            chosen.Stats.MaxHP -= loss;
            chosen.Stats.CurrentHP = Mathf.Clamp(chosen.Stats.CurrentHP, 1, chosen.Stats.MaxHP);

            int share = loss / others.Count, rem = loss % others.Count;
            for (int i = 0; i < others.Count; i++)
            {
                int gain = share + (i < rem ? 1 : 0);
                others[i].Stats.MaxHP += gain;
                others[i].Stats.CurrentHP += gain;
            }
            return $"{chosen.Stats.CharacterName} 최대체력 -{loss} → 아군 분배";
        }
        public override string Preview() => $"선택한 캐릭터 최대 체력 -{percent}%, 그만큼 다른 아군에게 분배";
    }

    // ── 무대상 (자판기/타일/고대 요정) ─────────────────────────

    /// <summary>무작위 포션 count개 (슬롯 남는 만큼 — 자판기).</summary>
    [System.Serializable]
    public class GainRandomPotions : EventOutcome
    {
        public int count = 2;

        public override string Apply()
        {
            var pm = PotionManager.EnsureInstance();
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < count; i++)
            {
                var p = pm.GrantRandomDrop();
                if (p == null) break;
                names.Add(p.PotionName);
            }
            if (names.Count == 0) return "포션 슬롯이 가득...";
            string got = string.Join(", ", names);
            return names.Count < count ? $"포션 획득 — {got} (슬롯 부족)" : $"포션 획득 — {got}";
        }
        public override string Preview() => $"무작위 포션 최대 {count}개 획득";
    }

    /// <summary>타일 설치 예약 — 다음 전투부터 런 내내 무작위 타일에 배치 (정령/제련소).</summary>
    [System.Serializable]
    public class QueueTileInstall : EventOutcome
    {
        public TileAttributeType installType = TileAttributeType.Sharp;

        public override string Apply()
        {
            EventRunState.EnsureInstance().EnqueueTileInstall(installType);
            return $"{DisplayName()} 타일 설치 예약";
        }
        public override string Preview() => $"다음 전투부터 {DisplayName()} 타일 설치";

        private string DisplayName() => new TileAttribute(installType, 0, -1).GetDisplayName();
    }

    /// <summary>무작위 유물 상실 + 전 파티원 MaxHP percent% 증가 (고대 요정).</summary>
    [System.Serializable]
    public class LoseRandomRelicGainPartyMaxHp : EventOutcome
    {
        [Range(1, 100)] public int percent = 10;

        public override bool CanApply()
            => ArtifactManager.Instance != null && ArtifactManager.Instance.Artifacts.Count > 0;

        public override string Apply()
        {
            var am = ArtifactManager.EnsureInstance();
            if (am.Artifacts.Count == 0) return "";
            var picked = am.Artifacts[Random.Range(0, am.Artifacts.Count)];
            string relicName = picked.data != null ? picked.data.artifactName : picked.GetType().Name;
            am.RemoveArtifact(picked);

            foreach (var c in AliveParty())
            {
                int gain = Mathf.Max(1, c.Stats.MaxHP * percent / 100);
                c.Stats.MaxHP += gain;
                c.Stats.CurrentHP += gain;
            }
            return $"{relicName} 상실 — 파티 최대체력 +{percent}%";
        }
        public override string Preview() => $"무작위 유물 1개 상실, 모든 아군 최대 체력 +{percent}%";
    }
}
