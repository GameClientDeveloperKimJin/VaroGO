# 전달: 클라 → 서버

클라가 서버 소유 파일(`Assets/03_Scripts/Server/`, `00_Boot.unity`, `Assets/02_Prefabs/Net/` 등)을 어쩔 수 없이 고쳤거나, 서버에 부탁할 일이 있을 때 여기에 적는다.
**서버는 작업 시작 전에 이 문서를 먼저 읽는다.** (`/server-start` 1단계)

## 쓰는 법

- 새 항목은 **맨 위**에 추가한다. 번호는 `C-날짜-순번`.
- 항목마다: 무엇을 / 왜 / 서버에 미치는 영향 / 서버가 할 일.
- 상태: `미확인` → 서버가 읽으면 `확인함` → 처리 끝나면 `완료`. 서버는 상태 칸만 바꾸고, 답이 필요하면 `전달_서버_to_클라.md`에 쓴다.
- 계약(§1~§7)에 걸리는 변경은 `1주차_03_계약명세.md` §9 계약 변경 후보에도 올린다.

---

## C-1028-1 · 접속 실패 시 UI가 "연결 중"에 멈추는 문제 수정

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-10-28 |
| 상태 | 확인함 |
| 파일 | `Assets/03_Scripts/Server/NetBootstrap.cs` (수정) |

- **무엇을**: `StartSessionSafe(mode, create, code)` 추가. `CreateRoom` / `JoinRoom` / `StartSolo`가 `StartSession` 대신 이걸 부른다. 안에서 `StartSession`을 `try/catch`로 감싸고, 예외가 나면 `Debug.LogException` → `CleanupRunner(Runner)` → `Fail("접속 오류: 예외타입 메시지")`.
- **왜**: `_ = StartSession(...)` 식으로 기다리지 않고 호출해서, 안에서 예외가 나면 로그도 없이 사라지고 상태가 `Connecting`에 남아 UI가 멈췄다.
- **영향**: public 멤버·시그니처는 그대로. `StartSession` 본문은 안 건드렸다.
- **서버가 할 일**: 방식이 괜찮은지 확인. 다른 형태로 바꾸고 싶으면 바꿔도 된다. 단, 예외가 나면 상태가 `Idle`로 돌아오고 `Status`에 실패 메시지가 남는 동작은 유지해 줄 것(새 타이틀 UI가 이걸 보고 화면을 바꾼다, C-1028-3).

## C-1028-2 · MPPM 가상 플레이어에서 Fusion 설정 로드 실패 수정

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-10-28 |
| 상태 | 확인함 |
| 파일 | `Assets/03_Scripts/Server/Editor/FusionMppmDependencyResync.cs` (새 파일, 에디터 전용) |

- **무엇을**: 메인 에디터가 도메인 리로드할 때마다 `Temp/FusionMppm/Dependency_*.json`이 빠져 있으면 Fusion 의존성 해시를 다시 보낸다.
- **왜**: Multiplayer Play Mode 가상 플레이어를 띄우면 `Failed to load NetworkProjectConfigAsset`이 났다. Fusion이 이 해시를 에디터 세션당 한 번만 보내서, 파일이 빠지면 가상 플레이어가 설정을 못 읽는다.
- **영향**: 에디터에서만 동작. 빌드에는 안 들어간다. 게임 로직 영향 없음.
- **주의 (확인 필요)**: Fusion 에디터 내부 API(`FusionMppm`, `FusionCustomDependencyEagerAttribute`, `FusionCustomDependency.Refresh`)를 쓴다. **Fusion 버전을 올리면 컴파일이 깨질 수 있다.** 그때는 이 파일을 지우고 증상이 다시 나는지 먼저 확인.
- **서버가 할 일**: 소유권을 서버로 넘긴다. 유지할지 판단.

## C-1028-3 · 00_Boot 타이틀 메뉴를 uGUI로 교체 ⚠ 계약 결정 필요

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-10-28 |
| 상태 | 확인함 |
| 파일 | `Assets/01_Scenes/00_Boot.unity` (수정) · `Assets/03_Scripts/Client/UI/BootMenuUI.cs` (새) · `Assets/03_Scripts/Client/Editor/BootMenuBuilder.cs` (새, 에디터 전용) |

- **무엇을**
  - `00_Boot`에 `BootMenu`(Canvas), `EventSystem`, `BootCamera` 추가. 화면: 메인(게임 시작/옵션/종료) → 시작(방 만들기/코드 참가) → 세션(코드 표시/복사/취소).
  - 기존 IMGUI `BootUI` 컴포넌트는 **지우지 않고 끄기만** 했다(`NetBootstrap` 오브젝트, Enabled 해제). Solo 버튼은 새 메뉴에 없다. 필요하면 `BootUI`를 다시 켜면 된다.
  - `NetBootstrap` 오브젝트의 `playerPrefab` 참조, `NetDebugOverlay`는 그대로 있다.
  - 메뉴 `VAROGO/Build Boot Menu UI`로 메뉴를 다시 만들 수 있다. 실행하면 씬의 `BootMenu`·`EventSystem`·`BootCamera` 이름 오브젝트를 **지우고 새로 만든다.**
- **⚠ 계약 위반**: `BootMenuUI`(클라)가 `VaroGO.Net.NetBootstrap`을 직접 쓴다. 계약명세 §1 "Client → Net 참조 금지"에 걸린다. 지금 쓰는 서버 멤버:
  - `NetBootstrap.Instance`, `State`, `SessionCode`, `Status`, `IsFailure`
  - `CreateRoom()`, `JoinRoom(string)`, `Leave()`
  - `SessionState` enum (`Idle`, `InGame` 사용), `SessionCode.Length`
- **해결 후보 (서버와 정할 것)**
  - A. `BootMenuUI.cs`를 `Assets/03_Scripts/Server/`로 옮겨 서버 소유로 한다. 가장 간단.
  - B. `Shared`에 세션 조작 인터페이스(`ISessionControl` 등)를 추가하고 `NetBootstrap`이 구현한다. 규칙에 맞지만 계약 변경이 필요.
- **서버가 할 일**
  - A/B 중 결정해서 `전달_서버_to_클라.md`에 답한다. 결정 전까지 위 멤버의 이름·동작을 바꾸지 않는다.
  - `00_Boot.unity` 변경이 크다(YAML 약 6,700줄). **00_Boot를 열기 전에 이 변경을 먼저 받아서** 충돌을 피한다.
