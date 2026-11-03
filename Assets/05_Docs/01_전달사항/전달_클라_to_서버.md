# 전달: 클라 → 서버

클라가 서버 소유 파일(`Assets/03_Scripts/Server/`, `00_Boot.unity`, `Assets/02_Prefabs/Net/` 등)을 어쩔 수 없이 고쳤거나, 서버에 부탁할 일이 있을 때 여기에 적는다.
**서버는 작업 시작 전에 이 문서를 먼저 읽는다.** (`/server-start` 1단계)

## 쓰는 법

- 새 항목은 **맨 위**에 추가한다. 번호는 `C-날짜-순번`.
- 항목마다: 무엇을 / 왜 / 서버에 미치는 영향 / 서버가 할 일.
- 상태: `미확인` → 서버가 읽으면 `확인함` → 처리 끝나면 `완료`. 서버는 상태 칸만 바꾸고, 답이 필요하면 `전달_서버_to_클라.md`에 쓴다.
- 계약(§1~§7)에 걸리는 변경은 `계약명세.md` §9 계약 변경 후보에도 올린다.

---

## C-1103-2 · P19 공중 비행 보너스 서버 동기화 — 클라가 서버 코드 작성 (클라가 서버 파일 수정·추가)

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-03 |
| 상태 | 미확인 |
| 파일 | **서버 소유(클라가 수정·추가)**: `Server/NetFlightState.cs`(새로), `Server/Editor/NetSwitchVariantBuilder.cs`(메뉴 3에 `Flight` 폴더 + `NetFlightState` 추가), `02_Prefabs/Net/Level/Flight/FlightBonus_Net.prefab`(새로). 공용: `Shared/Contracts.cs`(추가형). 클라: `Client/Flight/*`, `02_Prefabs/Client/Level/Flight/*`, `01_Scenes/PlayScene_Stage2.unity`(새로) |

- **무엇을**:
  - `Contracts.cs` (추가형만): `enum FlightPhase`(Idle, Summoning, Boarding, Countdown, Flying, Failed, Landing, Arrived), `interface IFlightState`(단계·시작 시각·내구도·시도 횟수·좌우 위치/속도·탑승/누름 여부, `RequestSummon`/`ReportBoarded`/`ReportPressing`, 권한 PC 전용 `SetPhase`/`SetLateral`, `Changed`/`SummonRequested`), `interface IStageLoader { LoadStage(scenePath) }` + `GameServices.StageLoader`.
  - `NetFlightState`(`IFlightState`): Net Variant에서 `LocalFlightState` 옆에 붙음. 상태 권한 = 방장(MasterClientObject)이 이동·충돌·내구도·성공 판정 후 `[Networked]` 값을 씀. 다른 PC는 소환·탑승·A/D 누름을 `RPC_*`(All → StateAuthority)로 보냄. Spawned 전엔 Local 값 반환. 늦게 들어온 PC는 Spawned에서 `Changed` 1회.
  - 시간 기준: `PhaseStartNet` = `Runner.SimulationTime`. 진행 위치는 단계 시작 시각부터 지난 시간으로 각 PC가 계산 → 위치 자체는 좌우(`LateralNet`)만 동기화.
- **왜**: 사용자 지시 (기획서 "Hidden Path 공중 비행 보너스 스테이지" v0.1, 호스트 판정).
- **영향**: Net 오브젝트 1개 추가. 틱마다 쓰는 값은 좌우 위치·속도 2개뿐. 빌더 메뉴 3을 다시 돌리면 `FlightBonus_Net`도 다시 만들어짐.
- **서버가 할 일**: 검토. 2인 온라인 테스트(탑승 동기화, A/D 각자 입력, 내구도·재시작 동기화, 착륙 후 하차) 결과는 클라가 따로 알림.

---

## C-1103-1 · 스테이지 전환(스테이지 1 → 스테이지 2) — 클라가 NetBootstrap 수정

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-03 |
| 상태 | 미확인 |
| 파일 | **서버 소유(클라가 수정)**: `Server/NetBootstrap.cs`. 프로젝트: `ProjectSettings/EditorBuildSettings.asset`(PlayScene_Stage2 추가). 클라: `Client/Goal/StageExit.cs`(새로, `StageLoading.Load` 포함), `ObjectTestScene_Stage1_KJ.unity` 골에 StageExit 부착 |

- **무엇을**:
  - `NetBootstrap`이 `IStageLoader` 구현. 접속 후 `GameServices.RegisterStageLoader(this)`로 등록, 종료 시 해제. `LoadStage(scenePath)`: 씬 권한(방장)만 `SceneUtility.GetBuildIndexByScenePath` → `Runner.LoadScene(SceneRef.FromIndex)`. 그 외 PC는 무시(방장이 바꾸면 따라감). Build Settings에 없으면 경고.
  - `OnSceneLoadStart`: 씬 바뀌기 전에 내 플레이어를 Despawn → 새 씬 `OnSceneLoadDone`에서 기존 스폰 로직으로 새 시작 위치에 다시 스폰.
  - 골 CLEAR → `StageExit`가 4초 뒤 `StageLoading.Load` (온라인 = StageLoader, 오프라인 = 바로 씬 열기).
  - Build Settings 순서: 00_Boot, 11_SpiralCourse, ObjectTestScene_Stage1_KJ, PlayScene_Stage2.
- **왜**: 사용자 지시 ("씬 전환 동작은 서버 말고 클라가 해").
- **영향**: 씬 전환 경로가 생김. 플레이어 재스폰 시 NetPlayer 상태(체크포인트 등)는 씬마다 새로 시작.
- **서버가 할 일**: 검토. 씬 전환 중 늦게 접속한 PC 처리, 방장 이탈 중 전환은 아직 확인 안 함 — 판단 부탁.

---

## C-1102-3 · 도토리 도둑 다람쥐 Net Variant 추가 (클라가 Net 폴더에 추가)

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-02 |
| 상태 | 미확인 |
| 파일 | **서버 소유(클라가 추가)**: `02_Prefabs/Net/Level/Magic/AcornThief_Net.prefab`(새로, 빌더 메뉴 3으로 생성). 클라: `Client/Magic/AcornThief.cs`, `02_Prefabs/Client/Level/Magic/AcornThief.prefab` |

- **무엇을**: 모든 스테이지 공통 기믹 "도둑맞은 마법의 도토리". 상태는 기존 `LocalSwitchState`/`NetSwitchState` 그대로 씀 (켜짐 = 다람쥐가 이미 도망감). 연출은 `ISwitchDetails.LastOnTime`부터 지난 시간(`GameClock`)으로 계산 → 두 PC가 같은 위치, 늦게 들어온 PC는 끝난 상태.
- **왜**: 사용자 지시. PDF 구현 메모 "한 번만 재생, 두 화면 같은 연출".
- **영향**: 새 서버 코드 없음. 빌더 메뉴 3이 Variant를 하나 더 만듦. 동시에 두 명이 범위에 들어가면 `RequestSet(true)`가 두 번 와서 시작 시각이 수 ms 늦게 갱신될 수 있음 (연출이 살짝 튈 수 있음, 실사용 영향 작음).
- **서버가 할 일**: 없음. (원하면 NetSwitchState에서 이미 켜진 상태의 RequestSet(true)는 LastOnTime을 안 바꾸게 하면 위 경합이 사라짐 — 다른 스위치의 "다시 누르면 타이머 연장" 동작과 충돌하니 판단 부탁.)

---

## C-1102-2 · P18 상호작용·기믹 서버 동기화 — 클라가 서버 코드 작성 (클라가 서버 파일 수정)

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-02 |
| 상태 | 미확인 |
| 파일 | **서버 소유(클라가 수정·추가)**: `Server/NetPlayer.cs`(수정), `Server/NetPlayerInteraction.cs`·`Server/NetPushableState.cs`·`Server/NetCarryState.cs`(새로), `Server/Editor/NetSwitchVariantBuilder.cs`(메뉴 3 추가), `02_Prefabs/Net/Level/Obstacles/{PushStone_Small,PushRock_Big,PushLog}_Net.prefab`·`02_Prefabs/Net/Level/Magic/{ThrowableStone,RevealablePlatform}_Net.prefab`(새로). 공용: `Shared/Contracts.cs`(추가형, 계약 v0.8) |

- **무엇을**:
  - `Contracts.cs` (추가형만, R7): `PlayerEvent` 뒤에 `LedgeGrab, LedgeClimb, Bound, Freed, PickedUp, Thrown`. `IPlayerInteraction`에 `RequestRescueShake(target)` + `RescueShaken(from)`. 새 인터페이스 `IPushableState`(밀기 보고 → 권한 PC), `ICarryState`(줍기·던지기). 자세한 규칙은 계약명세 §3.9·§3.12·§3.13.
  - `NetPlayer`: 스폰된 플레이어 목록 + `FindByRole(role)`. 로컬 `EventRaised` → `RPC_Event`(StateAuthority → Proxies)로 다른 PC에서 `PlayRemoteEvent` 재생. `RPC_Push`·`RPC_RescueShake`(All → StateAuthority): 대상 플레이어 주인 PC에서 판정(R6). 로컬 스폰 때 `GameServices.RegisterSystems(null, null, null, NetPlayerInteraction.Instance)`로 오프라인 구현을 덮어씀.
  - `NetPlayerInteraction`: `IPlayerInteraction` 온라인 구현. 요청을 `NetPlayer.FindByRole(target)`의 RPC로 보내고, 받은 PC에서 `Pushed`/`RescueShaken` 발생.
  - `NetPushableState`(`IPushableState`): 권한(StateAuthority = 방장, MasterClientObject)이면 `PushReported` 바로 발생, 아니면 `RPC_Report`로 권한 PC에 보냄. 큰 바위 2인 판정·이동은 방장 PC의 클라 코드(`Pushable`), 위치는 `NetworkTransform`.
  - `NetCarryState`(`ICarryState`): `[Networked] HolderNet`. `RPC_PickUp`은 먼저 온 요청만(이미 들고 있으면 무시), `RPC_Throw`는 Holder 본인만 → Holder = None 뒤 권한 PC에서 `Thrown`. `IStateAuthorityChanged`로 권한이 바뀌면 물리 담당도 넘어감.
  - 빌더 메뉴 `VAROGO/Net/3. 기믹 Net Variant 만들기 (밀기·돌멩이·숨겨진 발판)`: Client `Level/Obstacles`·`Level/Magic` 중 `LocalPushableState`/`LocalCarryState`/`LocalSwitchState`가 있는 프리팹만 Variant로 만들고, 루트에 NetworkObject(MasterClientObject) + 각 Local 옆에 Net 상태 + 움직이는 것(Pushable·ThrowableStone) 루트에 `NetworkTransform`.
- **왜**: 사용자 지시 "서버쪽 동기화까지 클라가 맡음" (스테이지1 상호작용·기믹 P18). 밀기(1인 돌·2인 바위·통나무), 플레이어 밀기, 돌멩이 줍기·던지기 → 숨겨진 발판, 나뭇가지 함정 묶임 + A/D 구출, 수액, 절벽 매달리기.
- **서버에 미치는 영향**: 기존 RPC·Networked 값은 그대로(추가만). `Player_Net`에 RPC 3개가 늘어남. `RegisterSystems`로 `IPlayerInteraction`만 덮어씀(나머지 인자 null = 그대로). Net 프리팹 5개가 Fusion 프리팹 테이블에 추가됨. 씬 배치·00_Boot는 안 건드림. 수액·나뭇가지 함정·점프 버섯은 각 PC가 자기 화면에서 판정이라 Net 컴포넌트 없음.
- **서버가 할 일**: 코드 검토(특히 `NetCarryState` 동시 줍기, 방장 이탈 시 권한 이전). 2인 온라인 테스트: 큰 바위는 둘이 같은 방향으로 밀 때만 움직이고 한 명이 손 떼면 멈추는지, 돌을 던지면 양쪽 화면에서 같은 발판이 나타나는지, 묶인 파트너를 A/D로 구하면 양쪽 모두 풀리는지. 다른 구조가 좋으면 바꿔도 되지만 계약(§3.9·§3.12·§3.13) 동작은 유지 부탁.

---

## C-1102-1 · 참가자(B) 시작 위치가 원점으로 가던 문제 (참고)

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-02 |
| 상태 | 미확인 |
| 파일 | 서버 파일 변경 없음 (`NetBootstrap.cs`에 임시 진단 로그를 넣었다가 지움) · 클라 `PlayerController.cs`, `PlayerRespawn.cs` |

- **원인**: `Runner.Spawn`은 위치를 맞게 넣지만, 프로젝트가 `Physics.autoSyncTransforms = 0`이라 CharacterController가 원점 위치를 그대로 갖고 있었다. 첫 `Move`에서 원점으로 되돌아갔다. 시뮬레이션 순서 때문에 참가자 쪽에서만 나타났다.
- **클라 수정**: `SetLocal(true)`에서 CharacterController를 껐다 켜서 현재 위치로 맞춘다. `PlayerRespawn`의 시작 위치는 Awake 값(원점) 대신 로컬이 된 첫 프레임 위치를 쓴다.
- **서버가 할 일**: 없음. 서버 쪽에서 스폰 직후 transform을 옮기는 코드를 추가하면 같은 문제가 생기니 `Teleport`를 쓴다.
