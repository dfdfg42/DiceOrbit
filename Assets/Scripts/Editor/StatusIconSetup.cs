using System;
using System.Collections.Generic;
using System.IO;
using DiceOrbit.Data;
using DiceOrbit.Visuals;
using UnityEditor;
using UnityEngine;

namespace DiceOrbit.EditorTools
{
    /// <summary>
    /// 상태이상 아이콘 배선 (2026-10-06). 배선표 status_icons.json(Tools/cut_status_icons.py가 만든다)대로
    /// Resources/UI/StatusVisualLibrary.asset의 항목을 채운다 — 없으면 만든다. 표가 권위다: 다시 실행하면 표대로 다시 맞춘다.
    /// 오버레이(유닛 위에 겹치는 장식)는 옛 씬 컴포넌트에 있던 두 장(꿀·속박)을 여기서 함께 꽂는다.
    /// 메뉴 [DiceOrbit → Assign Status Icons].
    /// </summary>
    public static class StatusIconSetup
    {
        private const string TablePath = "Assets/Sprites/UI Skin/status_icons.json";
        private const string AssetPath = "Assets/Resources/UI/StatusVisualLibrary.asset";

        private static readonly Dictionary<EffectType, string> Overlays = new Dictionary<EffectType, string>
        {
            { EffectType.Honey, "Assets/Sprites/상태이상/꿀_상태이상.png" },
            { EffectType.Bound, "Assets/Sprites/상태이상/얼음_상태이상.png" },
        };

        [Serializable]
        private class Table
        {
            public Entry[] icons;
        }

        [Serializable]
        private class Entry
        {
            public string type;
            public string sprite;
        }

        [MenuItem("DiceOrbit/Assign Status Icons")]
        public static void AssignFromMenu() => Assign();

        /// <summary>배선한 상태 수.</summary>
        public static int Assign()
        {
            var table = JsonUtility.FromJson<Table>(File.ReadAllText(TablePath));
            if (table?.icons == null || table.icons.Length == 0)
                throw new InvalidOperationException($"[StatusIconSetup] 배선표가 비어 있다: {TablePath}");

            var library = AssetDatabase.LoadAssetAtPath<StatusVisualLibrary>(AssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<StatusVisualLibrary>();
                AssetDatabase.CreateAsset(library, AssetPath);
            }

            var assigned = new HashSet<EffectType>();
            foreach (var entry in table.icons)
            {
                if (!Enum.TryParse(entry.type, out EffectType type))
                    throw new InvalidOperationException($"[StatusIconSetup] 모르는 상태 이름: {entry.type}");
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>(entry.sprite);
                if (icon == null) throw new InvalidOperationException($"[StatusIconSetup] '{entry.type}': 스프라이트가 없다 — {entry.sprite}");

                Sprite overlay = null;
                if (Overlays.TryGetValue(type, out string overlayPath))
                {
                    overlay = AssetDatabase.LoadAssetAtPath<Sprite>(overlayPath);
                    if (overlay == null) throw new InvalidOperationException($"[StatusIconSetup] '{entry.type}': 오버레이가 없다 — {overlayPath}");
                }

                library.Assign(type, icon, overlay, Color.white);
                assigned.Add(type);
            }

            foreach (var pair in Overlays)
                if (!assigned.Contains(pair.Key))
                    throw new InvalidOperationException($"[StatusIconSetup] 오버레이가 있는 상태 '{pair.Key}'가 배선표에 없다 — 아이콘도 그려서 표에 넣을 것.");

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StatusIconSetup] 상태 {assigned.Count}종에 아이콘을 배선했다 → {AssetPath}");
            return assigned.Count;
        }
    }
}
