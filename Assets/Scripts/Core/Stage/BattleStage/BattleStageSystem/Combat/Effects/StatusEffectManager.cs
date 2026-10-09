using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DiceOrbit.Core;
using DiceOrbit.Data;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Systems.Effects
{
    public class StatusEffectManager : MonoBehaviour, ICombatReactor
    {
        private Unit owner;

        // Restore activeEffects
        private Dictionary<EffectType, StatusEffect> activeEffects = new Dictionary<EffectType, StatusEffect>();

        /// <summary>상태 목록/스택 변경 통지 — 상태 아이콘 줄·오버레이 스택 등 UI가 구독.</summary>
        public event System.Action OnChanged;

        public int Priority => 10;

        public void Initialize(Unit unit)
        {
            owner = unit;
        }

        public void AddEffect(StatusEffect newEffect)
        {
            if (activeEffects.ContainsKey(newEffect.Type))
            {
                var existing = activeEffects[newEffect.Type];
                if (existing is RoundStatus)
                    existing.Value = Mathf.Max(existing.Value, newEffect.Value);   // 비중첩: 큰 값 유지 (시트 미중첩 규칙)
                else
                    existing.AddStack(newEffect.Value);
                existing.RefreshDuration(newEffect.Duration);
                Debug.Log($"[Status] refreshed {newEffect.Type} to {name}");
            }
            else
            {
                newEffect.SetOwner(owner); 
                activeEffects.Add(newEffect.Type, newEffect);
                string name = "Unknown";
                if (owner is Character c) name = c.Stats.CharacterName;
                else if (owner is Monster m) name = m.Stats.MonsterName;
                newEffect.EffectApplied();
                Debug.Log($"[Status] Added {newEffect.Type} to {name}");
            }
            OnChanged?.Invoke();
        }

        public void RemoveEffect(EffectType type)
        {
             if (activeEffects.ContainsKey(type))
            {
                activeEffects[type].EffectExpired(); // 효과 만료 시 필요한 로직 실행
                activeEffects.Remove(type);
                OnChanged?.Invoke();
            }
        }

        
        public int GetEffectValue(EffectType type)
        {
            if (activeEffects.TryGetValue(type, out var effect) && effect != null)
            {
                return effect.Value;
            }
            return 0;
        }

        public bool HasEffect(EffectType type)
        {
            return activeEffects.ContainsKey(type);
        }

        /// <summary>라운드형 상태(감전·고양·약화)를 전부 제거 — 라운드 종료·전투 시작에 CombatManager가 호출.</summary>
        public void RemoveRoundStatuses()
        {
            var keys = activeEffects.Where(kv => kv.Value is RoundStatus).Select(kv => kv.Key).ToList();
            foreach (var key in keys) RemoveEffect(key);
        }

        public IReadOnlyCollection<StatusEffect> GetActiveEffects()
        {
            return activeEffects.Values.ToList().AsReadOnly();
        }

        // ICombatReactor Implementation
        public void OnReact(CombatTrigger trigger, CombatContext context)
        {
            // 각 효과의 반응 로직 실행 (StatusEffect가 스스로 Duration 관리)
            foreach (var effect in activeEffects.Values.ToList())
            {
                ((ICombatReactor)effect).OnReact(trigger, context);
            }

            // 턴 시작 시, 반응 처리 후 만료된 효과 정리
            if (context is TurnEventContext { Phase: EventPhase.TurnStart } && context.SourceUnit == owner)
            {
                CleanupExpiredEffects();
            }
        }

        private void CleanupExpiredEffects()
        {
            var keys = activeEffects.Keys.ToList();
            foreach (var key in keys)
            {
                var effect = activeEffects[key];
                // -1은 영구 지속이므로 제거하지 않음
                if (effect.Duration != -1 && effect.Duration <= 0)
                {
                    RemoveEffect(key);
                    Debug.Log($"[Status] {key} expired.");
                }
            }
        }

        public static StatusEffect CreateEffect(EffectType type, int value, int duration)
        {
            switch (type)
            {
                case EffectType.BuffAttack:
                    return new BuffAttackStatus(value, duration);

                case EffectType.Weak:
                    return new WeakStatus(value, duration);

                case EffectType.Power:
                    return new PowerStatus(value, duration);

                case EffectType.Poison:
                    // 중독은 정수 중첩만 갖는다 — duration 인자는 의도적으로 무시 (2026-08-28 재설계)
                    return new PoisonStatus(value);

                // 촉매(1회성) + 감전·고양·약화(라운드형) — duration 무시(-1 고정), 제거는 각 골격 규칙을 따른다
                case EffectType.Shock:
                    return new ShockStatus(value);
                case EffectType.Catalyst:
                    return new CatalystStatus(value);
                case EffectType.Inspire:
                    return new InspireStatus(value);
                case EffectType.Weaken:
                    return new WeakenStatus(value);

                // 추후 BuffDefense, Dot 등 추가

                default:
                    // 기본(아직 구현 안된 타입)은 StatusEffect 사용
                    // (단, 기본 StatusEffect는 특별한 로직 없이 지속시간만 깎임)
                    return new StatusEffect(type, value, duration);
            }
        }
    }
}

