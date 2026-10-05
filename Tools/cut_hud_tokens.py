"""캐릭터 머리 위 행동 표시(이동 토큰 · 콤보 보석)의 아이콘 시트를 잘라 스프라이트로 낸다 (2026-10-05).
usage: python Tools/cut_hud_tokens.py <시트.png>

시트의 칸 순서(왼쪽 위부터 가로로)가 ORDER와 짝이다. 칸은 알파의 연결 덩어리로 찾는다.
'다 쓴 이동 토큰'(hud_move_token_spent)은 그림을 따로 생성하지 않고 이동 토큰에서 채도를 빼 만든다 — 윤곽이 같아야 상태가 바뀔 때 튀지 않는다.
나온 스프라이트는 UiSkin의 「전투 머리 위 표시」 칸에 꽂는다 (UI/CharacterActionStrip이 쓴다).
"""
import os
import re
import sys
import uuid

import numpy as np
from PIL import Image
from scipy import ndimage

OUT = "Assets/Sprites/UI Skin/"
META_TEMPLATE = "Assets/Sprites/UI Skin/icon_family_dice.png.meta"   # 단일 스프라이트, 밉맵 + 삼선형

ORDER = [
    "hud_move_token", "hud_combo_gem_empty", "hud_combo_gem_lit",
    "hud_combo_finisher_empty", "hud_combo_finisher_lit", "hud_token_plate",
]
PAD = 4
BORDERS = {"hud_token_plate": (58, 56, 58, 56)}   # 9-slice 경계 (왼쪽, 아래, 오른쪽, 위) — 캡슐 양 끝을 지킨다
LUM = np.array([0.299, 0.587, 0.114], dtype=np.float32)


def find_tiles(alpha, columns):
    labels, _ = ndimage.label(alpha > 40)
    boxes = []
    for ys, xs in ndimage.find_objects(labels):
        if (ys.stop - ys.start) * (xs.stop - xs.start) >= 2500:   # 자잘한 얼룩 제외
            boxes.append((xs.start, ys.start, xs.stop, ys.stop))
    if len(boxes) != len(ORDER):
        raise ValueError(f"칸 {len(boxes)}개를 찾았다 — {len(ORDER)}개여야 한다")
    boxes.sort(key=lambda b: (b[1] + b[3]) / 2)   # 세로 가운데로 줄을 나눈다 (칸마다 높이가 달라도 된다)
    rows = [sorted(boxes[i:i + columns], key=lambda b: b[0]) for i in range(0, len(boxes), columns)]
    return [box for row in rows for box in row]


def spent(image):
    """다 쓴 토큰 — 연보라 회색으로 (전투 HUD 버튼의 비활성과 같은 톤)."""
    data = np.asarray(image).astype(np.float32)
    rgb = data[..., :3]
    lum = (rgb @ LUM)[..., None]
    weight = np.clip((lum - 70.0) / 60.0, 0.0, 1.0)
    fill = (lum * 0.50 + 110.0) * np.array([0.97, 0.97, 1.05], dtype=np.float32)
    outline = np.array([110.0, 108.0, 134.0], dtype=np.float32)
    data[..., :3] = outline * (1.0 - weight) + fill * weight
    return Image.fromarray(np.clip(data, 0, 255).astype(np.uint8), "RGBA")


def write_meta(path, name):
    """이미 있으면 건드리지 않는다 — GUID가 바뀌면 UiSkin 참조가 끊긴다."""
    if os.path.exists(path + ".meta"):
        return
    meta = open(META_TEMPLATE, encoding="utf-8").read()
    meta = re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, meta, count=1)
    if name in BORDERS:
        left, bottom, right, top = BORDERS[name]
        meta = re.sub(r"spriteBorder: \{[^}]*\}", "spriteBorder: {x: %d, y: %d, z: %d, w: %d}" % (left, bottom, right, top), meta)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(meta)


def save(image, name):
    path = f"{OUT}{name}.png"
    image.save(path)
    write_meta(path, name)
    print(f"{name:26s} {image.width}x{image.height}")


def main(sheet_path):
    sheet = Image.open(sheet_path).convert("RGBA")
    boxes = find_tiles(np.asarray(sheet)[..., 3], columns=3)
    for name, (x0, y0, x1, y1) in zip(ORDER, boxes):
        tile = sheet.crop((x0, y0, x1, y1))
        canvas = Image.new("RGBA", (tile.width + PAD * 2, tile.height + PAD * 2), (0, 0, 0, 0))
        canvas.alpha_composite(tile, (PAD, PAD))
        save(canvas, name)
        if name == "hud_move_token":
            save(spent(canvas), "hud_move_token_spent")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
