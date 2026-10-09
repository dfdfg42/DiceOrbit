using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers
{
    /// <summary>
    /// 보상 등에서 제시할 수 있는 모디파이어 목록 레지스트리.
    /// 모디파이어는 [Serializable] 일반 클래스이므로 팩토리 함수로 매번 새 인스턴스를 만든다.
    /// 새 모디파이어를 추가하려면 Factories 배열에 한 줄 추가.
    /// </summary>
    public static class ModifierRegistry
    {
        // 2026-10-09 B — 시트 공용 17종 (위치 5 / 주사위 4 / 콤보 4 / 생존 4). 같은 종류는 캐릭터당 1개(CanApplyTo).
        // 2026-10-09 C — 직업 고유 16종(전사·도적·마법사·연금술사 각 4). CanApplyTo가 직업을 확인한다(JobModifier) → 공용 + 고유 = 33종.
        // 캐릭터 전용 시그니처는 전부 폐기됐다. 삭제된 타입명을 담은 옛 세이브는 Validate에서 실패 → 새 게임 폴백(D9).
        private static readonly System.Func<CharacterModifier>[] Factories =
        {
            // 위치
            () => new Common.JointTacticsModifier(),
            () => new Common.LoneWolfModifier(),
            () => new Common.DuelModifier(),
            () => new Common.MobileArmorModifier(),
            () => new Common.RescueTeamModifier(),
            // 주사위
            () => new Common.LowRollGuardModifier(),
            () => new Common.ReserveChargeModifier(),
            () => new Common.PreparationModifier(),
            () => new Common.PaybackModifier(),
            // 콤보
            () => new Common.GrandFinaleModifier(),
            () => new Common.FractureModifier(),
            () => new Common.PoisonedWeaponModifier(),
            () => new Common.BasicTrainingModifier(),
            // 생존
            () => new Common.OffenseDefenseModifier(),
            () => new Common.SturdyBodyModifier(),
            () => new Common.NaturalHealingModifier(),
            () => new Common.HuntModifier(),

            // ── 직업 고유 (2026-10-09 C) — 스펙 §3.2 표 순서 ──
            // 전사
            // 도적
            // 마법사
            // 연금술사
        };

        // 팩토리를 한 번씩만 돌려 얻은 타입명 캐시 — Exists가 매번 인스턴스를 만들지 않게 한다.
        private static string[] _ids;

        private static string[] Ids
        {
            get
            {
                if (_ids == null)
                {
                    _ids = new string[Factories.Length];
                    for (int i = 0; i < Factories.Length; i++) _ids[i] = Factories[i]().GetType().Name;
                }
                return _ids;
            }
        }

        /// <summary>
        /// 세이브 ID(클래스 타입명)로 모디파이어 새 인스턴스 생성. 없으면 null.
        /// 캐릭터마다 독립 인스턴스가 필요하므로 매번 새로 만든다.
        /// </summary>
        public static CharacterModifier Create(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var ids = Ids;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == id) return Factories[i]();
            return null;
        }

        /// <summary>Validate 단계용 — 인스턴스를 만들지 않는다.</summary>
        public static bool Exists(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var known in Ids)
                if (known == id) return true;
            return false;
        }

        /// <summary>전체 모디파이어를 새 인스턴스로 생성.</summary>
        public static List<CharacterModifier> CreateAll()
        {
            var list = new List<CharacterModifier>(Factories.Length);
            foreach (var f in Factories) list.Add(f());
            return list;
        }

        /// <summary>중복 없이 무작위 count개를 새 인스턴스로 반환.</summary>
        public static List<CharacterModifier> GetRandomChoices(int count)
        {
            return GetRandomChoicesFor(null, count);
        }

        /// <summary>
        /// 해당 캐릭터에게 줄 수 있는(CanApplyTo) 모디파이어만 골라 무작위 count개 반환.
        /// character가 null이면 전체 풀에서 뽑는다.
        /// </summary>
        public static List<CharacterModifier> GetRandomChoicesFor(Core.Character character, int count)
        {
            return GetRandomChoicesWhere(m => character == null || m.CanApplyTo(character), count);
        }

        /// <summary>
        /// 보상 공용 3택 (2026-10-03 보상 리워크) — 생존 파티원 중 한 명이라도 장착 가능한 종류에서 중복 없이 count개.
        /// 파티가 비었거나 아무도 받을 수 없으면 빈 목록 (전체 풀로 대신 뽑지 않는다 — 받을 사람이 없는 카드는 제시하지 않는다).
        /// </summary>
        public static List<CharacterModifier> GetRandomChoicesForParty(IReadOnlyList<Core.Character> party, int count)
        {
            if (party == null || party.Count == 0) return new List<CharacterModifier>();
            return GetRandomChoicesWhere(m =>
            {
                for (int i = 0; i < party.Count; i++)
                {
                    var c = party[i];
                    if (c != null && c.IsAlive && m.CanApplyTo(c)) return true;
                }
                return false;
            }, count);
        }

        /// <summary>조건을 통과한 종류에서 중복 없이 무작위 count개를 새 인스턴스로 반환. 조건 판정용 인스턴스는 버린다.</summary>
        public static List<CharacterModifier> GetRandomChoicesWhere(System.Func<CharacterModifier, bool> eligible, int count)
        {
            if (eligible == null) throw new System.ArgumentNullException(nameof(eligible));

            var pool = new List<System.Func<CharacterModifier>>();
            foreach (var f in Factories)
                if (eligible(f())) pool.Add(f);

            // Fisher-Yates 셔플
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            int n = Mathf.Clamp(count, 0, pool.Count);
            var result = new List<CharacterModifier>(n);
            for (int i = 0; i < n; i++) result.Add(pool[i]());
            return result;
        }

        /// <summary>캐릭터에 장착된 같은 종류(타입) 모디파이어 수 — 중복 장착 불가라 0 또는 1 (보상 카드의 보유 여부 표시용).</summary>
        public static int CountOn(Core.Character character, CharacterModifier modifier)
        {
            if (modifier == null) return 0;
            var mods = character?.Stats?.Modifiers?.Modifiers;
            if (mods == null) return 0;

            int count = 0;
            var type = modifier.GetType();
            for (int i = 0; i < mods.Count; i++)
                if (mods[i] != null && mods[i].GetType() == type) count++;
            return count;
        }
    }
}
