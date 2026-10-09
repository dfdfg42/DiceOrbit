using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using UnityEngine;

namespace DiceOrbit.Data.Modifiers
{
    public interface IModifierManager
    {
        void Initialize(Character character);
        /// <summary>장착만 한다 — 세이브 복원 경로 전용. 같은 종류가 이미 있으면 거부한다.</summary>
        void Add(CharacterModifier mod);
        /// <summary>획득 = 장착 + OnAcquired — 보상·상점·디버그 경로 전용. 같은 종류가 이미 있으면 거부한다.</summary>
        void Acquire(CharacterModifier mod);
        void Remove(CharacterModifier mod);
        void CollectReactors(List<ICombatReactor> list);
        void ApplyTo(CharacterModfierContext context);
        IReadOnlyList<CharacterModifier> Modifiers { get; }
    }

    /// <summary>
    /// 캐릭터에 붙는 모디파이어 컨테이너.
    /// 컨텍스트는 스킬이 필요할 때마다 새로 생성(GenerateContext)되고,
    /// 여기서 장착된 모디파이어들의 OnRefreshSkill을 그 위에 덧칠한다("도화지" 패턴).
    /// 같은 종류는 캐릭터당 1개(D1, 2026-10-09 B) — Add/Acquire 둘 다 중복을 거부한다(옛 세이브의 중첩도 하나만 남는다).
    /// </summary>
    public class ModifierManager : IModifierManager
    {
        private Character _owner;
        private readonly List<CharacterModifier> _modifiers = new();

        public IReadOnlyList<CharacterModifier> Modifiers => _modifiers;
        public int Count => _modifiers.Count;

        public void Initialize(Character character)
        {
            _owner = character;
        }

        public void Add(CharacterModifier mod)
        {
            TryAdd(mod);
        }

        public void Acquire(CharacterModifier mod)
        {
            if (!TryAdd(mod)) return;
            mod.OnAcquired(_owner);
        }

        public void Remove(CharacterModifier mod)
        {
            if (mod == null) return;
            _modifiers.Remove(mod);
        }

        private bool TryAdd(CharacterModifier mod)
        {
            if (mod == null) return false;
            if (ContainsType(mod.GetType()))
            {
                Debug.LogWarning($"[ModifierManager] '{mod.ModifierName}'는 이미 장착돼 있어 추가하지 않는다 (같은 종류 중복 장착 불가).");
                return false;
            }
            mod.owner = _owner;
            _modifiers.Add(mod);
            return true;
        }

        private bool ContainsType(System.Type type)
        {
            for (int i = 0; i < _modifiers.Count; i++)
                if (_modifiers[i] != null && _modifiers[i].GetType() == type) return true;
            return false;
        }

        /// <summary>주어진 컨텍스트(빈 도화지)에 장착된 모디파이어 효과를 일괄 적용한다.</summary>
        public void ApplyTo(CharacterModfierContext context)
        {
            if (context == null) return;
            foreach (var mod in _modifiers)
            {
                if (mod == null) continue;
                mod.OnRefreshSkill(context);
                if (context.IsCancelled) break;
            }
        }

        /// <summary>Character.CollectReactors에서 호출됨 (Generic 모디파이어의 파이프라인 훅).</summary>
        public void CollectReactors(List<ICombatReactor> list)
        {
            foreach (var m in _modifiers)
                if (m != null) list.Add(m);
        }
    }
}
