"""SessionStart 훅: 새 세션을 열면 /server-start 안내를 보여준다."""
import json
import sys

notice = "[VAROGO] 서버 작업을 시작하려면 /server-start 를 입력하세요 (문서 읽기 → 계획 보고 → 승인 후 구현)"

out = {
    "systemMessage": notice,
    "hookSpecificOutput": {
        "hookEventName": "SessionStart",
        "additionalContext": "서버 담당자가 아직 /server-start 를 실행하지 않았다면, 첫 응답에서 /server-start 로 시작하라고 한 줄 안내한다.",
    },
}

sys.stdout.reconfigure(encoding="utf-8")
print(json.dumps(out, ensure_ascii=False))
