# PostToolUse(Write|Edit): 레벨 오브젝트 스크립트를 고치면 프리팹 + 프리팹 리스트 갱신을 상기시킨다.
import json
import re
import sys

LEVEL_SCRIPT = re.compile(r"Assets/03_Scripts/Client/(Obstacles|Switches|Magic|Respawn|Goal)/[^/]+\.cs$")

try:
    data = json.load(sys.stdin)
except Exception:
    sys.exit(0)

path = (data.get("tool_input") or {}).get("file_path") or ""
path = path.replace("\\", "/")
if not LEVEL_SCRIPT.search(path):
    sys.exit(0)

msg = (
    "[prefab-list rule] Level object script changed: " + path + "\n"
    "When this object is done: (1) create/update its prefab under Assets/02_Prefabs/Client/Level/<Magic|Obstacles|Switches|Progress>/ "
    "(clone from scene via MCP, do not save 10_DemoRoom), "
    "(2) add/update it in Assets/05_Docs/현재_프리팹_리스트.md (planner-friendly Korean, full path, description, adjustable values) and bump '최종 갱신' date, "
    "(3) tell the user: '장애물 구현 → 프리팹 리스트에 반영함'. See CLAUDE.md."
)
print(json.dumps({"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": msg}}))
