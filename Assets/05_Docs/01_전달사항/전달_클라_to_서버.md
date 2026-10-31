# 전달: 클라 → 서버

클라가 서버 소유 파일(`Assets/03_Scripts/Server/`, `00_Boot.unity`, `Assets/02_Prefabs/Net/` 등)을 어쩔 수 없이 고쳤거나, 서버에 부탁할 일이 있을 때 여기에 적는다.
**서버는 작업 시작 전에 이 문서를 먼저 읽는다.** (`/server-start` 1단계)

## 쓰는 법

- 새 항목은 **맨 위**에 추가한다. 번호는 `C-날짜-순번`.
- 항목마다: 무엇을 / 왜 / 서버에 미치는 영향 / 서버가 할 일.
- 상태: `미확인` → 서버가 읽으면 `확인함` → 처리 끝나면 `완료`. 서버는 상태 칸만 바꾸고, 답이 필요하면 `전달_서버_to_클라.md`에 쓴다.
- 계약(§1~§7)에 걸리는 변경은 `계약명세.md` §9 계약 변경 후보에도 올린다.

---

## C-1031-2 · 재접속 후 상태 다시 맞추기 클라 쪽 완료 (C2-04) — 서버 확인 3가지

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-10-31 |
| 상태 | 미확인 |
| 파일 | 클라: `Client/Respawn/PlayerRespawn.cs` |

- **무엇을**:
  - 재접속한 PC: 로컬 플레이어가 된 뒤 3초 안에 `ICheckpointState.ActiveOrder >= 0`이면 그 체크포인트 SpawnPoint로 `Teleport` (→ `NetPlayer`가 `NetworkTransform.Teleport`). 새 게임은 -1이라 스폰 위치 그대로. 그 사이 직접 체크포인트를 밟으면 취소.
  - 마법 보임: `NetRoleProvider.SetRole` → `LocalRoleChanged`로 이미 다시 적용됨 (변경 없음).
  - 스위치·문·시간 발판: `NetSwitchState.Spawned()`의 `Sync()`가 `Changed`를 내서 이미 맞춰짐 (변경 없음). 문은 0.6초 동안 열리는 연출로 맞춰진다.
  - 남은 PC: 새 원격 플레이어는 `PlayerController` OnEnable 목록에 자동 추가 (밀기 대상 등). 캐시해 둔 참조 없음 (변경 없음).
- **서버가 확인할 것**:
  1. **역할 중복 위험** — `NetPlayer.Spawned`는 `IsSharedModeMasterClient`면 A, 아니면 B. A(마스터)가 나가면 남은 B가 마스터가 되고, 다시 들어온 쪽도 마스터가 아니라 **B가 되어 둘 다 B**가 된다. 재접속 시에는 "남은 사람이 안 쓰는 역할"을 주도록 바꿔야 한다 (예: 다른 플레이어의 `Role`을 보고 결정).
  2. **체크포인트 동기화(S2-06)** — 지금 온라인 `GameServices.Checkpoints`는 `LocalCheckpointState`라서 재접속하면 -1로 초기화 → 처음 위치에서 시작한다. `NetCheckpointState`가 접속 후 3초 안에 `ActiveOrder`를 받아 오면 클라가 자동으로 옮긴다. 더 늦게 도착하면 알려 주면 시간 늘림.
  3. **골 상태** — 나간 플레이어가 골 안에 있었으면 그 역할의 `ReportInGoal(true)`가 남는다. 나갈 때 그 역할을 false로 지워야 한다.

---

## C-1031-1 · 파트너 끊김 대기 화면 완료 (C2-03) — 서버 S2-04 상태값 필요

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-10-31 |
| 상태 | 미확인 |
| 파일 | 클라: `Client/UI/PartnerLostOverlay.cs` (새 파일), `Client/Player/PlayerInputReader.cs` |

- **무엇을**:
  - `GameServices.Session.Phase == PartnerLost`이면 "파트너 재접속 대기 중 (n초)" + **나가기** 버튼을 화면 전체에 덮는다. n = `PartnerLostSeconds`.
  - 보이는 동안 플레이어 입력 잠금, 커서 풀림. `Phase`가 다른 값이 되면 바로 사라진다.
  - 나가기 → `Session.Leave()` (지금 구현은 `Runner.Shutdown` → 00_Boot 복귀).
  - 씬에 둘 필요 없음. 게임 시작 시 스스로 생기고 씬이 바뀌어도 남는다. 코스 씬에 EventSystem이 없으면 직접 만든다.
- **서버가 할 일 (S2-04)**:
  - `ISessionLobby` 구현(`NetSessionLobbyBridge` 또는 정식 로비)에서 InGame 중 파트너가 나가면 `Phase = PartnerLost`, `PartnerLostSeconds`를 끊긴 뒤 경과 초로 올린다. 지금 브리지는 `PartnerLostSeconds => 0`, PartnerLost 단계 없음 → 화면이 안 뜬다.
  - 재접속하면 `Phase = InGame`으로 돌려 놓으면 화면이 사라진다.
  - 제한 시간 초과 시 로비로 보낼 때는 `Phase`만 바꾸면 된다 (화면은 PartnerLost일 때만 보임).
- **참고**: 에디터·개발 빌드에서 **F9**로 끊김 화면을 흉내 낼 수 있다 (서버 없이 UI 확인용). 온라인에서 F9 상태로 나가기를 누르면 실제로 접속을 끊는다.

---

## C-1030-4 · 밀기 클라 쪽 완료 (C2-09·10) — 서버 S2 밀기 RPC 착수 가능

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-10-30 |
| 상태 | 미확인 |
| 파일 | 클라: `Client/Player/PlayerController.cs`, `PlayerInputReader.cs`, `PlayerTuning.cs`, `Client/Offline/OfflinePlayerDriver.cs`<br>`Assets/04_Resources/Input/PlayerInput.inputactions` (Push 액션 추가) |

- **무엇을**:
  - 밀기 키(F / 마우스 오른쪽 / 패드 B) → 앞쪽 범위 안 파트너가 있으면 `GameServices.Interaction.RequestPush(파트너 역할, 방향)`. 대상 없으면 안 보냄, 쿨다운 중이면 안 보냄.
  - `PlayerController`(로컬)는 틱마다 `GameServices.Interaction`을 확인해 `Pushed`를 구독한다. 늦게 등록해도 된다.
  - `Pushed(from, dir)`를 받으면 **다음 틱**에 넉백(속도 `pushForce`=5, 위로 `pushUp`=3, 약 2.8m) + `PlayerEvent.KnockbackHeavy` → `PlayerEvent.Pushed` 발생. `LastPushedBy`/`LastPushedAt` 기록 (C2-07 "밀려 떨어짐"용).
  - 밀린 캐릭터는 몸을 돌리지 않고 뒤로 밀려난다 (이동 입력 전까지 방향 유지). 회전은 밀린 PC가 정하므로 기존 Transform 동기화 그대로면 된다.
- **현재 상태 (2026-10-30 확인)**: 00_Boot → 온라인 접속 상태에서 `GameServices.Interaction == null` → F를 눌러도 아무 일 없음. **서버 구현이 들어와야 온라인 밀기가 동작한다.** 오프라인(11_SpiralCourse 직접 Play)은 동작 확인됨.
- **서버가 할 일**:
  - `IPlayerInteraction` 구현: `RequestPush(target, dir)` → target 역할의 PC에서만 `Pushed(보낸 역할, dir)` 1회. 틱 밖(RPC 콜백)에서 불러도 된다.
  - 등록: `RegisterSystems(null, null, null, netInteraction)`.
  - 상대 화면 연출은 기존처럼 `EventRaised` → `PlayRemoteEvent`로 `KnockbackHeavy`·`Pushed` 전달이면 된다 (새 이벤트 값 없음).
- **참고**: 오프라인은 `OfflinePlayerInteraction`이 같은 PC에서 바로 `Pushed`를 내고, 마지막으로 민 대상(원격 테스트 캐릭터)이 날아간다. 테스트 캐릭터는 이제 입력 없이 중력·넉백만 움직인다 (`OfflinePlayerDriver.simulateRemote`).

---

## C-1030-3 · 로비 UI를 ISessionLobby로 전환 (C2-02) + 서버 폴더에 임시 브리지 추가

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-10-30 |
| 상태 | 미확인 |
| 파일 | **새 파일** `Assets/03_Scripts/Server/NetSessionLobbyBridge.cs` (임시)<br>`Assets/01_Scenes/00_Boot.unity` (SessionPanel에 버튼·글자 추가)<br>클라: `Client/UI/BootMenuUI.cs`, `Client/UI/FakeSessionLobby.cs`, `Client/Editor/BootMenuBuilder.cs` |

- **무엇을**:
  - `BootMenuUI`는 이제 `GameServices.Session`(ISessionLobby)만 본다. `VaroGO.Net` 참조 0개 (C-1028-3 B안 해결).
  - 세션 화면에 파트너 상태 글자, 게스트 **준비** 토글(`SetReady`), 호스트 **시작** 버튼(`PartnerReady`일 때만 눌림 → `StartGame`) 추가.
  - `NetSessionLobbyBridge`: 기존 `NetBootstrap.Instance`를 감싸 ISessionLobby로 보여주는 **임시** 파일. `BeforeSceneLoad`에 `RegisterSystems(bridge, null, null, null)`. `NetBootstrap` 코드는 안 건드림.
    - State 1:1 매핑(Idle/Connecting/WaitingPartner/LoadingCourse/InGame). Lobby·PartnerLost는 안 나옴 → 지금처럼 파트너 접속 = 바로 코스 로드.
    - `SetReady`/`StartGame`은 아무것도 안 함. `Changed` 이벤트 없음 (UI가 매 프레임 그림).
  - 00_Boot: 아트 적용본 유지. SessionPanel에 `ReadyButton`·`StartGameButton`(취소 버튼 복제, 같은 자리에 하나만 보임)·`PartnerText` 추가, `CancelButton`은 오른쪽으로 좁힘. 서버 `BootUI`(IMGUI)는 계속 꺼짐.
- **왜**: C2-02. 서버 S2-03 전에 UI를 먼저 끝내고 오프라인(가짜 로비)으로 준비/시작 흐름 확인.
- **영향**: 온라인 동작 그대로. 방 만들기/참가/취소/코드 복사 모두 브리지 경유.
- **서버가 할 일 (S2-03)**:
  1. `NetBootstrap`이 ISessionLobby 직접 구현 + `RegisterSystems(this, null, null, null)`.
  2. 파트너 접속 시 자동 코스 로드 대신 `Lobby` 단계 → 게스트 Ready 동기화 → 호스트 `StartGame`일 때만 로드.
  3. `Changed` 이벤트 쏘기(선택, UI는 없어도 동작).
  4. **`NetSessionLobbyBridge.cs` 삭제.** (둘 다 등록되면 나중에 등록된 쪽이 이김)
- **테스트 방법(클라 쪽)**: 00_Boot `BootMenu` > BootMenuUI `Fake Lobby` 켜고 Play → 방 만들기: 1.5초씩 대기→로비→파트너 준비→시작 버튼 활성. 코드 참가: 로비→준비→가짜 호스트 시작. **확인 후 꺼둘 것.**

---

## C-1030-2 · 계약 v0.4 나머지 반영 (C2-01) — 서버 S2-03·S2-07·S2-09 착수 가능

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-10-30 |
| 상태 | 미확인 |
| 파일 | `Assets/03_Scripts/Shared/Contracts.cs` (추가만)<br>`Assets/03_Scripts/Client/Offline/OfflineServices.cs`, `OfflineBootstrap.cs` |

- **무엇을** (계약명세 §2, §3.7~3.9, §9 P11~P15):
  - `SessionPhase`, `ISessionLobby`, `IZoneReset`, `IPlayerInteraction` — 2주차_02_서버 §2 제안 그대로.
  - `PlayerEvent.Pushed` 맨 뒤 추가 (R7).
  - `GameServices.Session` / `ZoneReset` / `Interaction` + `RegisterSystems(session, checkpoints, zoneReset, interaction)`. 기존 `Register`·`RegisterCheckpoints` 유지.
  - **`RegisterSystems`의 null 인자는 기존 값 유지.** 그래서 따로따로 등록 가능:
    - `NetBootstrap`(00_Boot): `RegisterSystems(this, null, null, null)` 등.
    - `NetCheckpointState.Spawned`: `RegisterCheckpoints(this)` 또는 `RegisterSystems(null, this, null, null)`.
  - 오프라인 구현: `OfflineSession`(항상 `InGame`, 호출 무시), `OfflineZoneReset`·`OfflinePlayerInteraction`(요청 즉시 이벤트). **오프라인에서만** 등록.
- **왜**: 2주차 C2-01 ("가장 먼저, 서버가 기다림").
- **영향**: 추가형이라 기존 서버 코드 컴파일·동작 영향 없음. 온라인에서 서버 구현 전까지 `Session`·`ZoneReset`·`Interaction`은 null → 클라 소비자는 null이면 기능을 끈다.
- **서버가 할 일**:
  - S2-03: `NetBootstrap`이 `ISessionLobby` 구현 + 등록. 기존 public 멤버는 클라 `BootMenuUI` 전환(C2-02) 끝날 때까지 유지 부탁.
  - S2-07: `IZoneReset` Net 구현. 구역 번호(`LocalSwitchState.Zone`)는 C2-06에서 클라가 추가 예정.
  - S2-09: `IPlayerInteraction` Net 구현. `Pushed`는 밀린 PC에서만 1회.
  - 참고: C-1028-3(BootMenuUI의 NetBootstrap 직접 참조)은 C2-02에서 B안으로 해결한다.

---

## C-1030-1 · 공용 체크포인트 계약(ICheckpointState) 추가 — 서버 NetCheckpointState(S2-06) 요청

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-10-30 |
| 상태 | 미확인 |
| 파일 | `Assets/03_Scripts/Shared/Contracts.cs` (추가만)<br>`Assets/03_Scripts/Client/Respawn/LocalCheckpointState.cs` (새 파일)<br>`Assets/03_Scripts/Client/Respawn/Checkpoint.cs`, `PlayerRespawn.cs`, `Client/Offline/OfflineBootstrap.cs` |

- **무엇을**:
  - 계약 v0.4 P12를 먼저 반영(계약명세 §3.6). `ICheckpointState { ActiveOrder, IsReached, Changed, ReportReached, CanFastTravel }`.
  - `GameServices.Checkpoints` + `GameServices.RegisterCheckpoints(ICheckpointState)` 추가. 기존 `Register`는 그대로. C2-01의 `RegisterSystems`가 들어오면 합친다.
  - 코스 씬 `OfflineBootstrap.Awake`가 **온라인에서도** `LocalCheckpointState`를 먼저 등록(빈 값 방지). 온라인 구현이 Spawned에서 덮어쓰면 된다.
  - 클라: 로컬 플레이어가 체크포인트를 밟으면 `ReportReached(Order, 내 역할)` (같은 지점·역할은 1번). 죽으면 `ActiveOrder` 체크포인트에서 부활, 죽은 사람만 이동.
- **왜**: 사용자 요청 "두 명이 체크포인트를 밟았을 때 위치 저장" (2주차 C2-05).
- **영향**:
  - 지금 온라인에서는 `NetCheckpointState`가 없어 PC마다 따로 동작한다(혼자 밟아도 저장 = 1주차와 같음). 컴파일·기존 동작은 영향 없음.
- **서버가 할 일** (S2-06):
  - `NetCheckpointState : NetworkBehaviour, ICheckpointState` — 코스 씬에 1개, Master Client Object (`DestroyWhenStateAuthorityLeaves`·`AllowStateAuthorityOverride` 끔, `NetSwitchState`와 같음).
  - Networked: 역할별 도착 비트마스크(예: `RedMask`, `BlueMask`, Order 0~31) + `ActiveOrder`(초기 -1, 올라가기만). 변화 시 `Changed`.
  - `ReportReached` → `RPC_ReportReached(order, role)` (All → StateAuthority). 둘 다 도착하면 `ActiveOrder = max`.
  - `Spawned`에서 `GameServices.RegisterCheckpoints(this)` 후 `Changed` 1회. `Despawned`에서 아무것도 안 해도 됨(다음 씬에서 OfflineBootstrap이 다시 등록).
  - 오브젝트 프리팹·씬 배치는 요청 주면 클라가 `11_SpiralCourse`에 넣는다.

---

## C-1028-4 · S-08 스위치 동기화(NetSwitchState + Net Variant) 클라가 대신 구현

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-10-28 |
| 상태 | 미확인 |
| 파일 | `Assets/03_Scripts/Server/NetSwitchState.cs` (새 파일)<br>`Assets/03_Scripts/Server/Editor/NetSwitchVariantBuilder.cs` (새 파일, 에디터 전용)<br>`Assets/02_Prefabs/Net/Level/Switches/*_Net.prefab` (메뉴로 생성)<br>`Assets/01_Scenes/11_SpiralCourse.unity` (스위치 3개를 Net Variant로 교체) |

- **무엇을**:
  - `NetSwitchState` (`VaroGO.Net`): 계약 §3.3대로 구현. `ISwitchState` + `ISwitchDetails`.
    - Networked: `IsOnNet`(`OnChangedRender`), `OffTimer`(TickTimer), `LastOnTimeNet`(Runner.SimulationTime), `Initialized`.
    - 설정값은 같은 GO의 `LocalSwitchState`에서 읽는다(`AllowedRole`·`AutoOffSeconds`·`StartOn`). 자체 필드 없음.
    - `RequestSet` → `RPC_RequestSet` (`RpcSources.All` → `RpcTargets.StateAuthority`). 권한 검사는 상태 권한 쪽에서, 거부면 아무 일도 없음.
    - 켜진 상태에서 다시 켜면 `LastOnTime`·타이머만 갱신. 자동 꺼짐은 상태 권한의 `FixedUpdateNetwork`에서 `OffTimer.Expired`.
    - Spawned 전: `IsOn`은 StartOn, `RequestSet`은 무시. Spawned에서 값이 StartOn과 다르면 `Changed` 1회(늦게 들어온 PC).
  - 메뉴 `VAROGO/Net/1. 스위치 Net Variant 만들기`: `Client/Level/Switches`의 `Lever_*`·`PressurePlate_*`·`Set_*`로 `Net/Level/Switches/<이름>_Net.prefab` Variant 생성.
    - 루트에 NetworkObject(**Is Master Client Object** 켬, `Destroy When State Authority Leaves`·`Allow State Authority Override`는 끔 — Fusion 인스펙터 규칙과 같게), `LocalSwitchState` 옆마다 `NetSwitchState` 추가.
    - 실행 완료(2026-10-28): Variant 13개 생성. 각 NetworkObject의 NetworkBehaviour 1개씩 베이크 확인(Set_* 안쪽 포함).
    - `Set_*`도 NetworkObject는 Set 루트에만(중첩 NetworkObject 없음, 계약 §6). Door·TimedPlatform 단독 Variant는 안 만든다.
  - 메뉴 `VAROGO/Net/2. 11_SpiralCourse 스위치를 Net Variant로 교체`: 코스 씬의 Lever_Red·Lever_Blue·Set_TimedSteps 인스턴스를 `ReplacePrefabAssetOfPrefabInstance`(오버라이드 유지)로 교체 후 Door·TimedPlatform 연결을 로그로 점검하고 저장.
    - 실행 완료: 3개 교체, 오브젝트 이름 유지, Door 2개·TimedPlatform 3개 연결 유지, 씬 SortKey 베이크 확인. 오프라인 Play 에러 0.
- **왜**: 2인 플레이에서 레버를 당겨도 상대 화면 문이 안 열렸다. 스위치가 `LocalSwitchState`만 있어 각 PC에서 따로 돌았다(S-08 미구현). 사용자 요청으로 클라가 대신 진행.
- **영향**:
  - 오프라인은 그대로(`SwitchStates.Pick`이 오프라인에선 `LocalSwitchState`를 고름). 클라 스크립트는 안 건드렸다.
  - 코스 씬 스위치가 씬 NetworkObject가 된다. `NetBootstrap.LoadCourse`가 `Runner.LoadScene`으로 씬을 여니 Fusion이 등록한다고 봄 (확인 필요).
  - 계약 §7 "코스 잠금" 절차는 거치지 않았다. 클라 소유 씬이라 클라가 직접 교체.
- **서버가 할 일**:
  1. 코드 리뷰. 원하는 방식으로 바꿔도 된다. 단 위 계약 §3.3 동작은 유지.
  2. 확인 필요 항목:
     - 자식 GO의 `NetSwitchState`(Set_* 안쪽)가 루트 NetworkObject에 제대로 묶이는지 (NetworkObject 인스펙터의 NetworkBehaviour 목록).
     - 씬 NetworkObject 베이크·등록이 정상인지 (Play 시 `Spawned` 호출 로그 / 두 PC 상태 일치).
     - 마스터가 나갔을 때 상태 권한이 넘어가고 값이 유지되는지.
     - `OnChangedRender`가 상태 권한 PC에서도 호출되는지 (안 되면 `Render`에서 `Sync()` 폴링으로 교체).
  3. S-08 체크 표시, S-09(Goal_Net)는 아직 안 했음.

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
