"""상태이상 아이콘 시트(5x5)를 잘라 스프라이트 + 배선표로 낸다 (2026-10-06).
usage: python Tools/cut_status_icons.py <시트.png>

시트는 5열 5행의 같은 크기 칸이고, 칸 순서(왼쪽 위부터 가로로)가 ORDER와 짝이다. 칸을 고정 격자로 나눈 뒤
각 칸 안의 알파 상자로 잘라 정사각 캔버스 가운데에 놓는다 — 기절의 별처럼 조각이 떨어진 그림도 한 칸으로 묶인다.
status_icons.json: EffectType 이름 → 스프라이트 경로. 한 그림을 여러 상태가 같이 쓸 수 있다(받은 피해 넷).
Unity에서 [DiceOrbit → Assign Status Icons]가 이 표대로 Resources/UI/StatusVisualLibrary.asset을 채운다.
"""
import json
import os
import re
import sys
import uuid

import numpy as np
from PIL import Image

OUT = "Assets/Sprites/UI Skin/"
META_TEMPLATE = "Assets/Sprites/UI Skin/icon_family_dice.png.meta"   # 단일 스프라이트, 밉맵 + 삼선형
COLUMNS, ROWS = 5, 5
PAD = 4
ALPHA_MIN = 8

# (스프라이트 이름, 이 그림을 쓰는 EffectType 이름들)
ORDER = [
    ("poison", ["Poison"]), ("weak", ["Weak"]), ("power", ["Power"]), ("vulnerable", ["Vulnerable"]), ("stunned", ["Stunned"]),
    ("bound", ["Bound"]), ("sugar_spike", ["BloodSugarSpike"]), ("slowed", ["Slowed"]), ("frost", ["FrostStack"]), ("shock", ["Shock"]),
    ("catalyst", ["Catalyst"]), ("inspire", ["Inspire"]), ("weaken", ["Weaken"]), ("attack_up", ["BuffAttack"]), ("crystal", ["CrystalStack"]),
    ("vitality", ["VitalityStack"]), ("fire_guard", ["FireGuard"]), ("bone_mark", ["BoneMark"]), ("extinguished", ["FireExtinguishMark"]),
    ("damage_taken", ["SnowDamageTaken", "CrystalDamageTaken", "BiteDamageTaken", "FireDamageTaken"]),
    ("focus", ["Focus"]), ("honey", ["Honey"]), ("slush", ["SlushSnow"]), ("barrier", ["Shield"]), ("dodge", ["Dodge"]),
]


def write_meta(path):
    """이미 있으면 건드리지 않는다 — GUID가 바뀌면 StatusVisualLibrary의 참조가 끊긴다."""
    if os.path.exists(path + ".meta"):
        return
    meta = open(META_TEMPLATE, encoding="utf-8").read()
    meta = re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, meta, count=1)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(meta)


def cut_cell(sheet, column, row):
    cell_w, cell_h = sheet.width / COLUMNS, sheet.height / ROWS
    box = (int(round(column * cell_w)), int(round(row * cell_h)), int(round((column + 1) * cell_w)), int(round((row + 1) * cell_h)))
    cell = sheet.crop(box)
    alpha = np.asarray(cell)[..., 3]
    ys, xs = np.nonzero(alpha > ALPHA_MIN)
    if len(xs) == 0:
        raise ValueError(f"칸 ({column}, {row})이 비어 있다")
    tile = cell.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    side = max(tile.width, tile.height) + PAD * 2
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.alpha_composite(tile, ((side - tile.width) // 2, (side - tile.height) // 2))
    return canvas


def main(sheet_path):
    sheet = Image.open(sheet_path).convert("RGBA")
    table = []
    for index, (name, types) in enumerate(ORDER):
        image = cut_cell(sheet, index % COLUMNS, index // COLUMNS)
        path = f"{OUT}status_{name}.png"
        image.save(path)
        write_meta(path)
        for effect_type in types:
            table.append({"type": effect_type, "sprite": path})
        print(f"status_{name:14s} {image.width}x{image.height}  ← {', '.join(types)}")
    with open(OUT + "status_icons.json", "w", encoding="utf-8", newline="\n") as f:
        json.dump({"icons": table}, f, ensure_ascii=False, indent=2)
    print(f"배선표 {len(table)}줄 → {OUT}status_icons.json")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
