using System;
using DiceOrbit.UI.Skin;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 씬에 배치하는 스킨 Image (2026-09-25 리스킨 3단계, 구 UiRoundedImage 개명 — .meta GUID 유지).
    /// 역할 파트만 저장해 두고 Awake/OnValidate에서 UiSkin이 스프라이트·모드(Sliced/Simple)를 꽂는다.
    /// 버튼은 UiSkinButton이 맡는다 (2026-09-26 역할 카탈로그).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UiSkinImage : MonoBehaviour
    {
        [Tooltip("스킨 역할 파트 — 「도구/Dice Orbit/UI 스킨 점검」으로 에셋 상태 확인")]
        [SerializeField] private SkinPart part = SkinPart.InfoFrame;
        [Tooltip("9-slice 경계 배수 덮어쓰기 — 아주 작은 패널(월드 캔버스의 말풍선 옆 칩 등)에서 테두리가 과하게 두껍지 않게 (0 = 스킨 카탈로그 값)")]
        [SerializeField] private float borderScaleOverride;

        public SkinPart Part => part;

        public void SetPart(SkinPart newPart)
        {
            part = newPart;
            Apply();
        }

        private void Awake() => Apply();

        private void OnValidate()
        {
            if (Application.isPlaying) return;
            Apply();   // 인스펙터에서 파트를 바꾸면 씬에 바로 반영 (스프라이트 참조가 씬에 저장됨)
        }

        public void Apply()
        {
            var image = GetComponent<Image>();
            if (image == null) throw new InvalidOperationException($"[UiSkinImage] 「{name}」에 Image가 없습니다.");
            UiSkin.Current.Apply(image, part);
            if (borderScaleOverride > 0f && image.type == Image.Type.Sliced) image.pixelsPerUnitMultiplier = borderScaleOverride;
        }
    }
}
