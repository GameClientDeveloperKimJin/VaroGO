# Stop: Level 폴더의 프리팹이 현재_프리팹_리스트.md에 빠져 있으면 종료를 막고 갱신을 요구한다.
import json
import os
import sys

try:
    data = json.load(sys.stdin)
except Exception:
    data = {}
if data.get("stop_hook_active"):
    sys.exit(0)

root = os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()
level_dir = os.path.join(root, "Assets", "02_Prefabs", "Client", "Level")
doc_path = os.path.join(root, "Assets", "05_Docs", "현재_프리팹_리스트.md")
if not os.path.isdir(level_dir) or not os.path.isfile(doc_path):
    sys.exit(0)

with open(doc_path, encoding="utf-8") as f:
    doc = f.read()

missing = []
for dirpath, _, files in os.walk(level_dir):
    for name in files:
        if name.endswith(".prefab") and name not in doc:
            rel = os.path.relpath(os.path.join(dirpath, name), root).replace("\\", "/")
            missing.append(rel)

if missing:
    reason = (
        "Prefabs missing from Assets/05_Docs/현재_프리팹_리스트.md: " + ", ".join(sorted(missing)) +
        ". Add each (planner-friendly Korean, full path, description, adjustable values), bump '최종 갱신', "
        "then tell the user '장애물 구현 → 프리팹 리스트에 반영함'."
    )
    print(json.dumps({"decision": "block", "reason": reason}))
