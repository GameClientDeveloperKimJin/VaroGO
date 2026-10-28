# VAROGO

## 레벨 오브젝트 → 프리팹 리스트 자동 반영 (필수)

레벨 오브젝트(장애물·스위치·문·마법 발판·체크포인트·골 등)를 새로 구현하거나 동작/설정값을 바꾸면,
작업 보고 전에 반드시 아래를 한다. 대상 스크립트: `Assets/03_Scripts/Client/{Obstacles,Switches,Magic,Respawn,Goal}/`

1. **프리팹 생성/갱신** — `Assets/02_Prefabs/Client/Level/<Magic|Obstacles|Switches|Progress>/`
   - 씬 오브젝트를 복제해 저장 (Unity MCP). `Assets/01_Scenes/10_DemoRoom.unity`는 저장하지 않는다.
   - 저장 후 참조 깨짐(null/BROKEN/OUTSIDE) 점검.
2. **`Assets/05_Docs/현재_프리팹_리스트.md` 갱신**
   - 기획자도 읽는 문서: 개발 용어 대신 쉬운 말, 핵심만.
   - 프로젝트 기준 전체 경로, 한 줄 설명, Inspector에서 조절할 값(의미 포함).
   - 상단 `최종 갱신:` 날짜 갱신.
3. **사용자에게 알림** — "장애물 구현 → 프리팹 리스트에 반영함" + 추가/변경된 프리팹 목록.

훅(`.claude/settings.json`)이 보조한다:
- PostToolUse: 위 폴더 스크립트 수정 시 이 규칙을 상기.
- Stop: Level 폴더 프리팹 중 리스트 문서에 파일명이 없는 게 있으면 종료를 막음.

## 기타 규칙
- 커밋·푸시는 사용자가 직접 한다. Claude는 커밋하지 않는다.
- 사용자 설명은 한국어.
- 서버↔클라 전달 문서: 작업 시작 전 `Assets/05_Docs/전달_서버_to_클라.md`의 `미확인` 항목을 확인한다. 서버 소유 파일(`03_Scripts/Server/`, `00_Boot.unity`, `02_Prefabs/Net/`)을 고치면 `전달_클라_to_서버.md`에 기록한다.
