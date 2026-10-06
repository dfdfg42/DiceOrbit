using System;
using System.Collections.Generic;
using DiceOrbit.Data;
using UnityEngine;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 상태이상 → 비주얼(아이콘/오버레이) 매핑 DB (스펙 2026-07-29 §②).
    /// Resources/UI/StatusVisualLibrary.asset 하나다 — 2026-10-06에 씬 컴포넌트에서 에셋으로 옮겼다
    /// (씬을 저장하지 않고 배선할 수 있고, 어느 씬에서든 같은 표를 쓴다). 아이콘은 [DiceOrbit → Assign Status Icons]가
    /// Tools/cut_status_icons.py의 배선표대로 채운다.
    /// 아이콘 없으면 소비처(StatusIconRow)가 상태색 글자 칩을 띄우고, 오버레이 없으면 오버레이를 생략한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Dice Orbit/UI/Status Visual Library", fileName = "StatusVisualLibrary")]
    public class StatusVisualLibrary : ScriptableObject
    {
        public const string ResourcePath = "UI/StatusVisualLibrary";

        [Serializable]
        public class Entry
        {
            public EffectType Type;
            [Tooltip("체력바 아래 아이콘 (비우면 상태색 글자 칩 폴백)")]
            public Sprite Icon;
            [Tooltip("유닛 스프라이트 위에 겹치는 장식 (비우면 오버레이 생략)")]
            public Sprite Overlay;
            public Color Tint = Color.white;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<EffectType, Entry> _map;
        private static StatusVisualLibrary _current;

        public static StatusVisualLibrary Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Resources.Load<StatusVisualLibrary>(ResourcePath);
                if (_current == null)
                    throw new InvalidOperationException(
                        $"[StatusVisualLibrary] Resources/{ResourcePath}.asset 이 없습니다. 메뉴 [DiceOrbit → Assign Status Icons]로 만드세요.");
                return _current;
            }
        }

        public IReadOnlyList<Entry> Entries => entries;

        public static bool TryGet(EffectType type, out Entry entry)
        {
            var library = Current;
            if (library._map == null) library.BuildMap();
            return library._map.TryGetValue(type, out entry) && entry != null;
        }

        /// <summary>배선용 — 같은 종류가 있으면 덮어쓰고, 없으면 더한다. (에디터 스크립트가 쓴다)</summary>
        public void Assign(EffectType type, Sprite icon, Sprite overlay, Color tint)
        {
            var entry = entries.Find(e => e != null && e.Type == type);
            if (entry == null) { entry = new Entry { Type = type }; entries.Add(entry); }
            entry.Icon = icon;
            entry.Overlay = overlay;
            entry.Tint = tint;
            _map = null;
        }

        private void BuildMap()
        {
            _map = new Dictionary<EffectType, Entry>();
            foreach (var e in entries)
                if (e != null) _map[e.Type] = e;
        }

        private void OnValidate() => _map = null;   // 인스펙터에서 고치면 다음 조회 때 다시 만든다
        private void OnDisable() => _map = null;
    }
}
