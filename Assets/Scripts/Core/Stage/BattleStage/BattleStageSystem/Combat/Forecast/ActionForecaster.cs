using System.Collections.Generic;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Core.Zones;
using DiceOrbit.Data;
using DiceOrbit.Data.Skills;
using DiceOrbit.Data.Tile;
using DiceOrbit.Systems.Effects;

namespace DiceOrbit.Core.Forecast
{
    /// <summary>
    /// 행동 예고 계산 (2026-10-04) — 캐릭터가 이 주사위로 움직이면 벌어질 일을 미리 구한다.
    ///
    /// 예고용 규칙을 따로 두지 않는다. 실행과 같은 코드를 "도착했다고 치고" 부른다:
    ///   경로            OrbitManager.BuildMovePath
    ///   공격 종류·표적   AutoAttackSystem.PlanAttack            (Character.PretendAt 안에서)
    ///   피해 보정        CombatPipeline.SimulateCalculation     (AttackActionScope.Simulate 안에서)
    ///   방어도·체력      UnitStats.ResolveDamage
    ///   타일 효과        TileAttribute.ForecastTraverse / ForecastEndTurn
    ///
    /// 반영하지 않는 것: 회피(확률), 타격이 적중하며 붙는 부가 효과가 같은 행동의 뒤 타격에 주는 영향.
    /// 계산은 상태를 바꾸지 않는다 — 콤보·1회성 상태·알림·RNG 전부 그대로다.
    /// </summary>
    public static class ActionForecaster
    {
        /// <summary>
        /// 예고를 만든다. 전투 준비가 덜 됐거나(궤도·파이프라인·구역 없음) 캐릭터가 움직일 수 없는 상태면 null —
        /// 틀릴 수 있는 숫자는 만들지 않는다.
        /// </summary>
        public static ActionForecast Build(Character character, int diceValue)
        {
            if (character == null || !character.IsAlive || character.Stats == null || character.CurrentTile == null) return null;

            var pipeline = CombatPipeline.Instance;
            var zones = CombatZoneManager.Instance;
            if (pipeline == null || zones == null || !zones.IsGeometryReady) return null;

            var auto = AutoAttackSystem.EnsureInstance();
            var forecast = new ActionForecast { Character = character, DiceValue = diceValue };

            OrbitManager.BuildMovePath(character, diceValue, forecast.Path);
            forecast.Destination = forecast.Path.Count > 0 ? forecast.Path[forecast.Path.Count - 1] : character.CurrentTile;

            var traverse = ForecastTiles(forecast, character);

            forecast.GateCondition = FindGateCondition(character);
            if (auto.HasResolved(character))
            {
                forecast.Kind = ForecastAttackKind.AlreadyAttacked;
            }
            else
            {
                // 지나갈 구역은 이동 '전' 위치 기준, 공격 판정은 '도착' 위치 기준 — 실행 순서와 같다
                var passedZones = AutoAttackSystem.CollectPassedZones(character, diceValue);
                var pendingStatuses = BuildPendingStatuses(character, traverse);

                using (character.PretendAt(forecast.Destination))
                {
                    var plan = auto.PlanAttack(character, diceValue, passedZones);
                    ForecastAttack(forecast, plan, character, diceValue, auto, pipeline, pendingStatuses);
                }
            }

            CollectThreats(forecast, character);
            return forecast;
        }

        // ── 타일 ──────────────────────────────────────────────

        /// <summary>경로의 통과 효과와 도착 타일의 턴 종료 효과를 타일 속성에 물어 적는다. 통과 예고(얻을 상태 포함)를 돌려준다.</summary>
        private static TileForecast ForecastTiles(ActionForecast forecast, Character character)
        {
            var traverse = new TileForecast();
            var consumedAtDestination = new HashSet<TileAttribute>();

            foreach (var tile in forecast.Path)
            {
                if (tile == null) continue;
                foreach (var attribute in tile.GetAttributes())
                {
                    if (attribute == null) continue;
                    traverse.BeginAttribute();
                    attribute.ForecastTraverse(character, traverse);
                    if (traverse.Consumed && tile == forecast.Destination) consumedAtDestination.Add(attribute);
                }
            }
            forecast.PathNotes.AddRange(traverse.Notes);

            var endTurn = new TileForecast();
            foreach (var attribute in forecast.Destination.GetAttributes())
            {
                if (attribute == null || consumedAtDestination.Contains(attribute)) continue;   // 도착하며 이미 사라진 속성
                endTurn.BeginAttribute();
                attribute.ForecastEndTurn(character, endTurn);
            }
            forecast.EndTurnNotes.AddRange(endTurn.Notes);

            return traverse;
        }

        /// <summary>
        /// 가는 길에 얻을 상태를 가상 리액터로 만든다 — 유닛에 붙이지 않고 시뮬레이션에만 끼운다.
        /// 이미 가진 상태는 건너뛴다 (비중첩 상태는 갱신될 뿐 두 번 적용되지 않는다).
        /// </summary>
        private static List<ICombatReactor> BuildPendingStatuses(Character character, TileForecast traverse)
        {
            var result = new List<ICombatReactor>();
            foreach (var granted in traverse.GrantedStatuses)
            {
                if (character.StatusEffects != null && character.StatusEffects.HasEffect(granted.Key)) continue;

                var effect = StatusEffectManager.CreateEffect(granted.Key, granted.Value, -1);
                effect.Owner = character;
                result.Add(effect);
            }
            return result;
        }

        // ── 공격 ──────────────────────────────────────────────

        private static void ForecastAttack(ActionForecast forecast, AttackPlan plan, Character character, int diceValue,
            AutoAttackSystem auto, CombatPipeline pipeline, List<ICombatReactor> pendingStatuses)
        {
            switch (plan.Kind)
            {
                case AttackPlanKind.ComboNoTarget:
                    forecast.Kind = ForecastAttackKind.NoTarget;
                    forecast.ComboStage = plan.Stage;
                    forecast.AttackName = plan.ComboSkill.GetStageName(plan.Stage);
                    forecast.ComboChange = plan.Stage > 0 ? ForecastComboChange.BrokenByNoTarget : ForecastComboChange.None;
                    return;

                case AttackPlanKind.Basic:
                    forecast.AttackName = auto.BasicAttackName;
                    forecast.ComboChange = plan.HadCombo ? ForecastComboChange.BrokenByDice : ForecastComboChange.None;
                    if (plan.Targets.Count == 0)
                    {
                        forecast.Kind = ForecastAttackKind.NoTarget;
                        return;
                    }
                    forecast.Kind = ForecastAttackKind.Basic;
                    break;

                default:
                    forecast.Kind = ForecastAttackKind.Combo;
                    forecast.ComboStage = plan.Stage;
                    forecast.AttackName = plan.ComboSkill.GetStageName(plan.Stage);
                    forecast.ComboChange = plan.Stage >= ComboActiveSkill.StageCount - 1
                        ? ForecastComboChange.Finish
                        : ForecastComboChange.Advance;
                    break;
            }

            // 실행 루프와 같은 순서 — 타격 회차마다 대상 전원. 앞선 타격으로 쓰러진 대상은 건너뛴다.
            var results = new Dictionary<Unit, ForecastTargetResult>();
            using (AttackActionScope.Simulate(character, plan.BuildActionInfo(diceValue)))
            {
                int hits = plan.Hits;
                for (int h = 0; h < hits; h++)
                {
                    foreach (var target in plan.Targets)
                    {
                        if (target == null || !target.IsAlive || target.Stats == null) continue;

                        if (!results.TryGetValue(target, out var result))
                        {
                            result = NewTargetResult(target);
                            results[target] = result;
                            forecast.Targets.Add(result);
                        }
                        if (result.HpAfter <= 0) continue;

                        int raw = plan.Kind == AttackPlanKind.Combo
                            ? plan.ComboSkill.CalculateStageDamage(character, plan.Stage)
                            : character.Stats.EffectiveAttack;
                        var context = new AttackContext(character, target, forecast.AttackName, raw);
                        int damage = pipeline.SimulateCalculation(context, pendingStatuses);

                        ApplyHit(result, damage);
                    }
                }
            }
        }

        private static ForecastTargetResult NewTargetResult(Unit target)
        {
            var stats = target.Stats;
            int armor = UnityEngine.Mathf.Max(0, stats.TempArmor);
            return new ForecastTargetResult
            {
                Target = target,
                Name = DisplayName(target),
                HpBefore = stats.CurrentHP,
                HpAfter = stats.CurrentHP,
                ArmorBefore = armor,
                ArmorAfter = armor,
                Invulnerable = stats.Invulnerable,
                DodgeChance = stats.DodgeChance,
            };
        }

        /// <summary>타격 1회를 예고 결과에 누적한다 (순수 — ActionForecastSelfTests). 실제 TakeDamage와 같은 함수로 방어도·체력을 깎는다.</summary>
        public static void ApplyHit(ForecastTargetResult result, int damage)
        {
            if (result.Hits == 0) result.DamagePerHit = damage;
            result.Hits++;

            UnitStats.ResolveDamage(damage, result.ArmorAfter, result.HpAfter, result.Invulnerable, out int absorbed, out int hpDamage);
            result.ArmorAfter -= absorbed;
            result.HpAfter = UnityEngine.Mathf.Max(0, result.HpAfter - hpDamage);
        }

        /// <summary>이 캐릭터의 강화 공격 주사위 조건 문구. 강화 공격이 없으면 빈 문자열.</summary>
        private static string FindGateCondition(Character character)
        {
            var abilities = character.Stats.ActiveAbilities;
            if (abilities == null) return string.Empty;

            foreach (var slot in abilities)
            {
                var skill = slot != null ? (slot.RuntimeInstance ?? slot.BaseSkill) : null;
                if (skill is ComboActiveSkill) return skill.FormatDiceCondition();
            }
            return string.Empty;
        }

        // ── 위험 ──────────────────────────────────────────────

        /// <summary>도착 타일(또는 이 캐릭터 자체)을 노리는 몬스터의 다음 행동을 모은다.</summary>
        private static void CollectThreats(ActionForecast forecast, Character character)
        {
            var combat = CombatManager.Instance;
            if (combat == null || combat.ActiveMonsters == null) return;

            foreach (var monster in combat.ActiveMonsters)
            {
                if (monster == null || !monster.IsAlive) continue;
                var intent = monster.CurrentIntent;
                if (intent == null) continue;

                bool onTile = intent.TargetTiles != null && intent.TargetTiles.Contains(forecast.Destination);
                bool onUnit = intent.Targets != null && intent.Targets.Contains(character);
                if (!onTile && !onUnit) continue;

                var skill = monster.NextSkill;
                forecast.Threats.Add(new ForecastThreat
                {
                    Monster = monster,
                    MonsterName = DisplayName(monster),
                    SkillName = skill != null && skill.skillData != null ? skill.skillData.SkillName : string.Empty,
                    IsAttack = intent.Type == IntentType.Attack || intent.Type == IntentType.Multi,
                });
            }
        }

        private static string DisplayName(Unit unit)
        {
            if (unit is Monster monster && monster.Stats != null && !string.IsNullOrWhiteSpace(monster.Stats.MonsterName))
                return monster.Stats.MonsterName;
            if (unit is Character character && character.Stats != null && !string.IsNullOrWhiteSpace(character.Stats.CharacterName))
                return character.Stats.CharacterName;
            return unit != null ? unit.name : string.Empty;
        }
    }
}
