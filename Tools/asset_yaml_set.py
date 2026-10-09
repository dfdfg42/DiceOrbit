"""
.asset(Unity YAML) 필드 한 줄 교체 도우미.

사용:
  python Tools/asset_yaml_set.py <asset> --rid <rid> <field> <value>      # references.RefIds 항목의 data 안
  python Tools/asset_yaml_set.py <asset> --top <field> <value>            # MonoBehaviour 최상위 필드
  python Tools/asset_yaml_set.py <asset> --entry-key <키> <field> <value> # '- key: <키>' 목록 항목 안
  python Tools/asset_yaml_set.py <asset> --rename-rid <rid> <old> <new> <value>  # 필드 이름 변경 + 값

- 비ASCII 문자열은 "\\uXXXX" 이스케이프된 큰따옴표 문자열로 쓴다 (Unity가 쓰는 형식).
- 기존 값이 여러 줄(큰따옴표 줄바꿈 연속)이어도 연속 줄을 지우고 한 줄로 쓴다.
- 대상 필드를 못 찾으면 비정상 종료한다 (조용히 넘어가지 않는다).
- 값은 항상 숫자(숫자 꼴이면) 또는 큰따옴표 문자열로 쓴다 (bool 등은 지원하지 않음).
- --top 은 들여쓰기 2칸 필드만, --rid/--entry-key 는 항목의 최상위 필드(중첩 하위 필드 제외)만 대상으로 한다.
- BMP 밖 문자(코드포인트 > 0xFFFF)는 거부한다. 인자 개수가 틀리면 사용법을 출력하고 종료 코드 2.
"""
import re
import sys

BS = chr(92)


def esc(value: str) -> str:
    if re.fullmatch(r"-?\d+(\.\d+)?", value):
        return value
    out = []
    for ch in value:
        if ord(ch) > 0xFFFF:
            sys.exit(f"unsupported non-BMP character U+{ord(ch):X} in value")
        if ch == '"':
            out.append(BS + '"')
        elif ch == BS:
            out.append(BS + BS)
        elif ord(ch) < 128:
            out.append(ch)
        else:
            out.append(BS + "u%04X" % ord(ch))
    return '"' + "".join(out) + '"'


def decode(s: str) -> str:
    return re.sub(re.escape(BS) + "u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), s)


def indent_of(line: str) -> int:
    return len(line) - len(line.lstrip(" "))


def find_block(lines, mode, locator):
    """(start, end) 줄 범위 — 그 안에서 필드를 찾는다."""
    if mode == "top":
        for i, l in enumerate(lines):
            if l.startswith("  references:"):
                return 0, i, 2
        return 0, len(lines), 2
    if mode == "rid":
        # StartingActives 등의 '- rid:' 참조 줄이 아니라 references.RefIds 안의 항목만 대상으로 한다
        ref_start = next((k for k, l in enumerate(lines) if l.strip() == "RefIds:"), 0)
        for i in range(ref_start, len(lines)):
            l = lines[i]
            if re.match(r"^\s*- rid: " + re.escape(locator) + r"\s*$", l):
                base = indent_of(l)
                j = i + 1
                while j < len(lines) and not (indent_of(lines[j]) <= base and lines[j].strip().startswith("- ")):
                    j += 1
                d = next((k for k in range(i + 1, j) if lines[k].strip() == "data:"), None)
                if d is None or d + 1 >= j:
                    sys.exit(f"rid {locator} has no data block")
                return d + 1, j, indent_of(lines[d + 1])
        sys.exit(f"rid {locator} not found")
    if mode == "entry":
        for i, l in enumerate(lines):
            m = re.match(r"^(\s*)- key: (.*)$", l)
            if m and decode(m.group(2).strip().strip('"')) == locator:
                base = len(m.group(1))
                j = i + 1
                while j < len(lines) and not (indent_of(lines[j]) <= base and lines[j].strip().startswith("- ")):
                    j += 1
                return i + 1, j, base + 2
        sys.exit(f"entry key {locator} not found")
    sys.exit("bad mode")


def closes(text):
    """여는 따옴표 뒤의 text 안에 닫는 (이스케이프 안 된) 큰따옴표가 있는가."""
    k = 0
    while k < len(text):
        if text[k] == BS:
            k += 2
            continue
        if text[k] == '"':
            return True
        k += 1
    return False


def set_field(lines, start, end, field, new_field, value, indent):
    pat = re.compile(r"^( *)" + re.escape(field) + r":( .*)?$")
    for i in range(start, end):
        m = pat.match(lines[i])
        if not m or len(m.group(1)) != indent:
            continue
        j = i + 1
        old = (m.group(2) or "").strip()
        if old.startswith('"') and not closes(old[1:]):
            # 여러 줄 큰따옴표 값: 닫는 따옴표가 나오는 줄까지 제거
            while True:
                if j >= end:
                    sys.exit(f"unterminated quoted value for {field}")
                done = closes(lines[j].lstrip(" "))
                j += 1
                if done:
                    break
        lines[i:j] = [" " * indent + new_field + ": " + esc(value)]
        return
    sys.exit(f"field {field} not found in block")


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="replace")
    args = sys.argv[1:]
    need = {"--top": 4, "--rid": 5, "--entry-key": 5, "--rename-rid": 6}
    if len(args) < 2 or args[1] not in need or len(args) != need[args[1]]:
        print(__doc__)
        sys.exit(2)
    path, mode = args[0], args[1]
    with open(path, encoding="utf-8", newline="") as f:
        text = f.read()
    nl = "\r\n" if "\r\n" in text else "\n"
    lines = text.split(nl)
    if mode == "--top":
        s, e, ind = find_block(lines, "top", None); field, value = args[2], args[3]; new = field
    elif mode == "--rid":
        s, e, ind = find_block(lines, "rid", args[2]); field, value = args[3], args[4]; new = field
    elif mode == "--entry-key":
        s, e, ind = find_block(lines, "entry", args[2]); field, value = args[3], args[4]; new = field
    elif mode == "--rename-rid":
        s, e, ind = find_block(lines, "rid", args[2]); field, new, value = args[3], args[4], args[5]
    else:
        sys.exit("bad mode")
    esc(value)  # 쓰기 전에 값 검증 (non-BMP 등)
    set_field(lines, s, e, field, new, value, ind)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(nl.join(lines))
    print(f"ok {path} {ascii(new)}={ascii(value)}")


if __name__ == "__main__":
    main()
