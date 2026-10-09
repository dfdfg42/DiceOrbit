using UnityEngine;
using UnityEngine.InputSystem;
using DiceOrbit.Core;

public class BattleStageDebugManager : MonoBehaviour
{
    private static BattleStageDebugManager _instance;

    public static BattleStageDebugManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<BattleStageDebugManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("BattleStageDebugManager");
                    _instance = go.AddComponent<BattleStageDebugManager>();
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Update()
    {
        // 스페이스바를 누르면 선두 캐릭터의 컨텍스트 확인 (New Input System)
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (PartyManager.Instance != null)
            {
                var party = PartyManager.Instance.Party;
                if (party != null && party.Count > 0)
                {
                    var firstCharacter = party[0];
                    if (firstCharacter != null && firstCharacter.Stats != null)
                    {
                        var modifiers = firstCharacter.Stats.Modifiers;

                        if (modifiers != null)
                        {
                            modifiers.Acquire(new DiceOrbit.Data.Modifiers.Common.JointTacticsModifier());
                            Debug.Log($"[Debug] 선두 캐릭터({firstCharacter.Stats.CharacterName})에게 '합류 전술' 모디파이어를 추가했습니다!");
                        }
                        else
                        {
                            Debug.Log($"[Debug] 선두 캐릭터({firstCharacter.Stats.CharacterName})에 ModifierManager가 없습니다.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[Debug] 파티의 첫 번째 캐릭터 또는 Stats 정보가 유효하지 않습니다.");
                    }
                }
                else
                {
                    Debug.LogWarning("[Debug] 현재 파티에 캐릭터가 존재하지 않습니다.");
                }
            }
            else
            {
                Debug.LogWarning("[Debug] PartyManager.Instance를 찾을 수 없습니다.");
            }
        }
    }
}
