"""전투 HUD 버튼(턴 종료)의 보통 상태 그림에서 나머지 세 상태를 만든다 (2026-10-04).
usage: python Tools/make_hud_button_states.py

이 버튼은 알약 밖으로 모래시계가 튀어나온 통짜 일러스트(SkinMode.Simple)다.
상태마다 그림을 따로 생성하면 튀어나온 물체의 모양이 조금씩 달라져 상태가 바뀔 때 덜컥거린다.
그래서 보통 상태(btn_<역할>_normal.png) 한 장만 그림으로 두고 나머지는 여기서 계산한다 — 윤곽이 픽셀 단위로 같다.

  hover    : 살짝 밝게 + 바깥에 흰 테두리 (다른 버튼의 호버 = 흰 테와 같은 말)
  pressed  : 살짝 어둡게 + 아래로 몇 픽셀 내려앉음
  disabled : 채도를 빼고 연보라 회색으로, 외곽선도 흐리게

보통 상태 그림을 바꿨으면 이 도구를 다시 돌린다. 그림 둘레에는 흰 테와 내려앉음이 들어갈 투명 여백(8px 이상)이 있어야 한다.
"""
import numpy as np
from PIL import Image
from scipy import ndimage

SKIN = "Assets/Sprites/UI Skin/"
ROLES = ["btn_end_turn"]   # 주사위 굴리기 버튼은 없앴다 (2026-10-05 — 주사위는 턴 시작 때 자동으로 굴린다)

LUM = np.array([0.299, 0.587, 0.114], dtype=np.float32)
HOVER_LIFT = 0.16          # 흰색 쪽으로 당기는 정도
HOVER_RIM = 6              # 흰 테 두께(px)
PRESSED_SHADE = 0.14
PRESSED_DROP = 6           # 내려앉는 거리(px)
DISABLED_FILL = (0.50, 122.0)                      # 밝기 = lum * a + b (대비를 줄이고 띄운다)
DISABLED_TINT = np.array([0.97, 0.97, 1.05], dtype=np.float32)
DISABLED_OUTLINE = np.array([126.0, 124.0, 150.0], dtype=np.float32)


def fill_weight(rgb):
    """0 = 어두운 외곽선·눈, 1 = 밝은 면. 외곽선은 상태가 바뀌어도 진하게 남긴다."""
    lum = rgb @ LUM
    t = np.clip((lum - 70.0) / 60.0, 0.0, 1.0)
    return (t * t * (3.0 - 2.0 * t))[..., None]


def hover(src):
    rgb, alpha = src[..., :3], src[..., 3]
    w = fill_weight(rgb)
    out = src.copy()
    out[..., :3] = rgb + (255.0 - rgb) * HOVER_LIFT * w

    # 바깥 흰 테: 실루엣에서 HOVER_RIM 안쪽 거리까지, 가장자리는 1px로 풀어 준다
    distance = ndimage.distance_transform_edt(alpha < 128.0)
    rim = np.clip(HOVER_RIM + 0.5 - distance, 0.0, 1.0) * 255.0
    under = alpha / 255.0
    out[..., :3] = out[..., :3] * under[..., None] + 255.0 * (1.0 - under[..., None])   # 테 위에 그림을 얹는다
    out[..., 3] = np.maximum(alpha, rim)
    return out


def pressed(src):
    rgb = src[..., :3]
    w = fill_weight(rgb)
    out = src.copy()
    out[..., :3] = rgb * (1.0 - PRESSED_SHADE * w)

    dropped = np.zeros_like(out)
    dropped[PRESSED_DROP:] = out[:-PRESSED_DROP]
    return dropped


def disabled(src):
    rgb = src[..., :3]
    w = fill_weight(rgb)
    lum = (rgb @ LUM)[..., None]
    fill = (lum * DISABLED_FILL[0] + DISABLED_FILL[1]) * DISABLED_TINT
    out = src.copy()
    out[..., :3] = DISABLED_OUTLINE * (1.0 - w) + fill * w
    return out


def check_margin(name, alpha):
    need = max(HOVER_RIM + 2, PRESSED_DROP + 2)
    ys, xs = np.nonzero(alpha > 8)
    h, w = alpha.shape
    margin = min(xs.min(), ys.min(), w - 1 - xs.max(), h - 1 - ys.max())
    if margin < need:
        raise ValueError(f"{name}: 둘레 투명 여백이 {margin}px — {need}px 이상이어야 흰 테·내려앉음이 잘리지 않는다")


def main():
    for role in ROLES:
        src = np.asarray(Image.open(f"{SKIN}{role}_normal.png").convert("RGBA")).astype(np.float32)
        check_margin(role, src[..., 3])
        for state, make in (("hover", hover), ("pressed", pressed), ("disabled", disabled)):
            out = np.clip(make(src), 0.0, 255.0).astype(np.uint8)
            Image.fromarray(out, "RGBA").save(f"{SKIN}{role}_{state}.png")
            print(f"{role}_{state}.png  {out.shape[1]}x{out.shape[0]}")


if __name__ == "__main__":
    main()
