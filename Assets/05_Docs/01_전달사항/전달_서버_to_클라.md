# 전달: 서버 → 클라

서버가 클라 소유 파일(`Assets/03_Scripts/Client/`, `Assets/02_Prefabs/Client/`, `Shared`)을 어쩔 수 없이 고쳤거나, 클라에 부탁·답변할 일이 있을 때 여기에 적는다.
**클라는 작업 시작 전에 이 문서를 먼저 읽는다.**

## 쓰는 법

- 새 항목은 **맨 위**에 추가한다. 번호는 `S-날짜-순번`. `전달_클라_to_서버.md` 항목에 대한 답이면 제목에 그 번호를 적는다 (예: `답: C-1028-3`).
- 항목마다: 무엇을 / 왜 / 클라에 미치는 영향 / 클라가 할 일.
- 상태: `미확인` → 클라가 읽으면 `확인함` → 처리 끝나면 `완료`. 클라는 상태 칸만 바꾸고, 답이 필요하면 `전달_클라_to_서버.md`에 쓴다.
- 계약(§1~§7)에 걸리는 변경은 `계약명세.md` §9 계약 변경 후보에도 올린다.

## 항목 틀

```
## S-MMDD-N · 제목

| 항목 | 내용 |
| --- | --- |
| 날짜 | YYYY-MM-DD |
| 상태 | 미확인 |
| 파일 | 경로 (수정/새 파일/삭제) |

- **무엇을**:
- **왜**:
- **영향**:
- **클라가 할 일**:
```

---

## S-1103-3 · 구역 리셋(S2-07) · 파트너 끊김 일시정지(S2-04) · 재접속(S2-05) · 답: C-1031-1·2·3, C-1028-3

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-03 |
| 상태 | 미확인 |
| 파일 | 서버만: `Server/NetZoneReset.cs`(새 파일), `Server/NetSession.cs`·`NetBootstrap.cs`·`NetPlayer.cs`·`NetGameClock.cs`·`NetSwitchState.cs`·`NetGoalState.cs`(수정). 클라 소유 파일·코스 씬 수정 없음 |

- **구역 리셋 (S2-07, 답: C-1031-3)**
  - 온라인에서도 `GameServices.ZoneReset`이 등록된다. 리셋 버튼이 이제 실제로 동작한다.
  - 방장 PC가 같은 `Zone`의 스위치를 처음 상태(`StartOn`)로 되돌리고 자동 꺼짐 타이머도 지운다. 그다음 모든 PC에서 `ZoneReset(zone)`이 1회 발생한다.
  - 순서: `ZoneReset` 이벤트가 스위치 `Changed`(OFF)보다 **먼저** 올 수 있다. 온라인에서 `LocalSwitchState`는 이벤트를 무시하므로 지금 코드엔 문제없다. 이벤트를 받은 순간 스위치 값을 읽는 코드를 새로 만들 거면 알려 달라.
- **파트너 끊김 (S2-04, 답: C-1031-1)**
  - 코스 중 파트너가 나가면 남은 PC가 `Phase = PartnerLost`가 되고, `PartnerLostSeconds`는 끊긴 뒤 경과 초다. 방장이 나가든 게스트가 나가든 같다.
  - 끊긴 동안 공유 시계(`Clock.Now`)가 멈춘다 → 장애물 정지. 재개하면 멈춘 시간만큼 빼고 이어지므로 위치가 튀지 않는다.
  - 스위치 자동 꺼짐 타이머는 멈춤 중에도 흐른다 (2주차 범위 밖, 필요하면 요청).
- **재접속 (S2-05, 답: C-1031-2)**
  1. 역할 중복 고침: 역할을 방장 여부로 정하지 않고, 세션 오브젝트의 역할 칸에서 받는다. 나간 사람 칸은 비워 두고 다시 들어온 사람에게 빈 역할을 준다. 처음 방은 예전처럼 방장 = A.
  2. 체크포인트: S-1103-2 그대로 (재접속 PC는 `ActiveOrder`를 받아 클라가 옮김).
  3. 골: 나간 역할의 골 진입 표시를 지운다.
  - 같은 코드로 다시 참가하면 코스 씬이 자동으로 열리고, 둘 다 스폰되면 `Phase`가 `InGame`으로 돌아온다.
  - **60초 안에 안 돌아오면** 남은 사람은 00_Boot "파트너 대기"로 돌아간다(방 유지). 같은 코드로 들어오면 로비(준비/시작)부터 다시.
  - 재접속한 PC는 코스에 들어온 직후 잠깐(파트너 스폰까지) `PartnerLost` 화면이 보일 수 있다.
- **C-1028-3 답**: B안(`ISessionLobby`, S2-03)으로 해결됨. `NetSessionLobbyBridge.cs`는 삭제됨.
- **테스트 (MPPM 2인, 2026-11-03)**: 레버 2개 ON → 리셋 → 두 PC 모두 OFF + `ZoneReset` 1회. 방장(A) 나감 → 게스트가 방장이 되며 일시정지 → 같은 코드로 재참가 → 빈 역할 A 배정, 재개, 시계 58.97에서 이어짐. 게스트 나감 → 방장 쪽 일시정지 → 60초 → 방장이 00_Boot 파트너 대기로 → 같은 코드로 참가 → 로비. 두 PC 에러 0.
- **참고 (클라)**: 00_Boot로 돌아왔을 때 `There are 2 event systems in the scene` 경고가 계속 난다. `PartnerLostOverlay`가 만든 EventSystem(씬 전환에도 남음)과 00_Boot의 EventSystem이 겹치는 것으로 보인다. 오늘 이전에도 메인 콘솔에 있던 경고다.
- **계약 §5 화이트리스트 추가 제안 (문서만 변경)**: 서버가 읽기만 하는 클라 멤버 `LocalSwitchState.Zone`(S2-07), `Checkpoint`·`Checkpoint.Order`(S-1103-2 누락 검사). 괜찮으면 §5에 올리겠다.
- **클라가 할 일**: 끊김 화면·리셋 버튼을 2인으로 한 번 같이 확인. §5 추가 의견.

---

## S-1103-2 · 공용 체크포인트 온라인 구현 (S2-06) · 답: C-1030-1

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-03 |
| 상태 | 미확인 |
| 파일 | **`01_Scenes/11_SpiralCourse.unity`(수정, 클라 소유 — `NetCheckpointState` 1개 배치)**. 서버: `Server/NetCheckpointState.cs`(새 파일), `Server/NetSyncCheck.cs`·`Server/Editor/NetSwitchVariantBuilder.cs`(수정), `02_Prefabs/Net/Level/Progress/NetCheckpointState.prefab`(새 파일) |

- **무엇을**:
  - `NetCheckpointState : ICheckpointState` (계약 §3.6). 코스 씬 1개, Master Client Object(방장이 나가도 값 유지).
  - `ReportReached(order, role)` → 상태 권한(마스터)이 역할별 도착 비트마스크에 기록 → 두 역할 모두 도착한 가장 큰 Order가 `ActiveOrder` (올라가기만). 이미 기록된 (order, role)은 RPC를 보내지 않는다.
  - `Spawned`에서 `GameServices.RegisterCheckpoints(this)` — `OfflineBootstrap`이 먼저 등록한 `LocalCheckpointState`를 덮어쓴다. `Changed`는 값이 바뀔 때마다 모든 PC + 늦게 들어온 PC는 `Spawned`에서 1회.
  - `CanFastTravel(order)` = `order <= ActiveOrder` (S2-08도 같이 됨).
  - **Order는 0~31만 지원** (int 비트마스크). 범위를 넘으면 누락 검사 에러.
  - 누락 검사: 체크포인트가 있는데 `NetCheckpointState`가 없거나 2개 이상이면 에러. 메뉴 `VAROGO/Net/0`이 없으면 자동 배치.
  - 디버그 로그: `[Net] 체크포인트 red=<비트> blue=<비트> active=<n>`.
- **테스트 (MPPM 2인, 2026-11-03)**: 시작 시 0번 둘 다 도착 → active 0. 빨강만 1번 → active 0 유지(양쪽 같음). 파랑도 1번 → 양쪽 active 1. 파랑만 2번 → active 1 유지. 빨강이 KillZone 낙사 → 체크포인트 1에서 부활, 파랑은 그대로. 에러 0. 오프라인(코스 씬 바로 Play)은 `LocalCheckpointState` 그대로.
- **계약 §5 밖 멤버**: 누락 검사(에디터 메뉴·코스 로드 직후)가 `Checkpoint` 타입과 `Checkpoint.Order`(읽기)를 쓴다. 바꿀 거면 알려 달라. §5에 올려도 된다.
- **클라가 할 일**: 체크포인트 Order를 32 이상으로 쓸 계획이면 알려 달라. 그 외 없음.

---

## S-1103-1 · 코스 오브젝트 온라인 동기화 (레버문·골 교체, S-09) + 온라인 순간이동 버그 수정 · 답: C-1028-4

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-03 |
| 상태 | 미확인 |
| 파일 | **`01_Scenes/11_SpiralCourse.unity`(수정, 클라 소유 — 코스 잠금 알림 없이 진행함)**. 서버: `Server/NetGoalState.cs`·`Server/NetSyncCheck.cs`(새 파일), `Server/NetPlayer.cs`·`Server/NetSwitchState.cs`·`Server/NetDebugOverlay.cs`·`Server/NetBootstrap.cs`·`Server/Editor/NetSwitchVariantBuilder.cs`(수정), `02_Prefabs/Net/Level/Progress/Goal_Net.prefab`(새 파일), 스위치 Variant 13개(다시 생성, 내용 같음) |

- **무엇을**:
  - **코스 씬 교체** (2개): `Set_LeverDoor_Common` → `Set_LeverDoor_Common_Net`, `Goal` → `Goal_Net`. 이름·위치·값은 그대로 (교체 때 오버라이드 유지). 문·시간 발판·골·ClearUI 7곳의 스위치 연결 그대로.
    - 원인: C2-07에서 `Set_LeverDoor_Common`이 일반 프리팹으로 들어와 레버를 당긴 사람 화면에서만 문이 열렸다. 골도 S-09 전이라 일반 프리팹이었다.
  - **`NetGoalState`** (S-09, 계약 §3.4): 빨강·파랑 둘 다 안에 있으면 모든 PC에서 `ChapterCleared` 1회. 나갔다 들어와도 1회.
  - **누락 검사**: 온라인 코스 로드 때 `LocalSwitchState`/`LocalGoalState`만 있고 Net 짝이 없는 오브젝트가 있으면 콘솔에 `[Net] 온라인 동기화 안 되는 오브젝트 N개: ...` 에러가 난다. **이 에러가 보이면 그 오브젝트는 한쪽 화면에서만 움직인다.**
  - **온라인 순간이동 버그 수정** (`NetPlayer`): Fusion 2.1 `NetworkTransform`이 틱 전에 위치를 되돌려서, 틱 밖(`Update`)에서 한 `PlayerController.Teleport`가 원래 자리로 돌아가는 일이 있었다 → **체크포인트 부활·빠른 이동이 온라인에서 실패할 수 있었음.** 이제 다음 틱에서 다시 적용한다. 클라 코드 수정 없음, `Teleport` 사용법도 그대로.
  - 디버그(에디터·개발 빌드만): 스위치가 바뀔 때 `[Net] 스위치 <이름> = ON/OFF` 로그. 명령창(`) 바로가기 `move lever / leverr / leverb / plate / goal`. 바닥 찾기를 대상 바로 위에서 하도록 바꿈(위층 바닥에 내리던 문제).
- **테스트 (MPPM 2인, 2026-11-03)**: 공용·빨강·파랑 레버, 시간 발판 켜짐/자동 꺼짐이 양쪽 같음. 마스터 쪽·게스트 쪽 요청 둘 다 확인. 권한 없는 역할의 레버 요청은 거절(양쪽 변화 없음). 골은 한 명만 = 클리어 안 됨, 둘 다 = 양쪽 CLEAR 1회. 에러 0.
- **⚠ 계약 §5 밖 멤버 사용 (디버그 전용)**: 명령창이 열려 있는 동안 캐릭터가 키에 반응하지 않도록 서버 `NetDebugOverlay`가 내 캐릭터의 **`PlayerInputReader.enabled`를 껐다 켜고, `PlayerInputReader.MenuOpen`을 잠깐 true로** 둔다(닫을 때 원래 값으로). `UNITY_EDITOR || DEVELOPMENT_BUILD`에서만 컴파일된다. 이 두 멤버를 바꾸거나 이름을 바꿀 거면 알려 달라. 괜찮으면 §5 표에 "디버그 명령창 입력 잠금" 용도로 올려도 된다.
- **참고**: 오프라인에서는 골에 한 명만 들어가도 클리어된다(`LocalGoalState.requireBoth` 꺼짐). 의도한 거면 그대로 두면 된다.
- **클라가 할 일**:
  1. `11_SpiralCourse`를 고치기 전에 이 변경을 먼저 받는다 (씬은 merge 안 됨).
  2. 앞으로 코스에 **레버·발판·Set·골을 넣을 때는 `02_Prefabs/Net/Level/` 쪽 `_Net` 프리팹을 쓴다.** 원본을 넣었으면 메뉴 `VAROGO/Net/0. 코스 씬 온라인 동기화 적용 (전체)` 한 번이면 Variant 생성 + 씬 교체 + 점검까지 된다 (`3. 열린 씬 온라인 동기화 점검`은 확인만).

---

## S-1102-2 · 원격 연출·밀기·핑 온라인 구현 (S2-09·10) · 답: C-1030-4, C-1101-3

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-02 |
| 상태 | 미확인 |
| 파일 | 서버: `Server/NetPlayer.cs`(수정), `Server/NetPlayerInteraction.cs`·`Server/NetPingSystem.cs`(새 파일), `Server/NetBootstrap.cs`(등록). 클라 소유 파일 수정 없음 |

- **무엇을**:
  - **원격 연출** (계약 §3.5, 1주차에서 빠져 있던 것): 로컬 플레이어의 `EventRaised` → RPC → 상대 PC의 그 캐릭터 `PlayRemoteEvent(e)`. 이제 상대 화면에서도 점프 애니메이션·넉백·사망·부활·밀림 연출이 난다.
  - **밀기** (`IPlayerInteraction`): `RequestPush(target, dir)` → 대상 플레이어 PC로 RPC → 그 PC에서만 `Pushed(보낸 역할, dir)` 1회. 밀린 PC에서 0.5초 안에 다시 들어온 밀기는 버린다(S2-10).
  - **핑** (`IPingSystem`): `RequestPing` → RPC → 보낸 PC 포함 모든 PC에서 `PingReceived` 1회. 값 그대로 전달. 코스 들어가기 전(로컬 플레이어 없음)에는 내 화면에만.
  - 등록: 접속 시 `RegisterSystems(null, null, null, interaction)` + `RegisterPing(ping)`. 접속 종료 시 `RegisterPing(null)`.
- **테스트 (MPPM 2인, 2026-11-02)**: 원격 이벤트가 상대 쪽에서만 같은 개수로 수신됨(무한 반복 없음, 로그 확인). 밀기 → 상대 넉백 → "A(빨강)에게 밀려 떨어짐" 확인. 핑 양쪽 같은 위치·종류, "(파트너)" 라벨 상대 쪽만.
- **아직 못 본 것**: 보이스는 PC 한 대로 판단 불가 → 실제 PC 2대 테스트 때 같이 보자 (C-1101-1).
- **클라가 할 일**: 없음.

---

## S-1102-1 · 로비 Ready/Start 온라인 구현 (S2-01~03) · 답: C-1030-3, C-1028-3

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-02 |
| 상태 | 미확인 |
| 파일 | 서버: `Server/NetBootstrap.cs`(수정), `Server/NetSession.cs`(새 파일), `02_Prefabs/Net/NetSession.prefab`(새 파일), `01_Scenes/00_Boot.unity`(NetBootstrap에 `sessionPrefab` 연결), **`Server/NetSessionLobbyBridge.cs` 삭제**. 클라 소유 파일 수정 없음 |

- **무엇을**:
  - `NetBootstrap`이 `ISessionLobby`를 직접 구현하고 `Awake`에서 `RegisterSystems(this, null, null, null)`으로 등록한다. 임시 브리지는 지웠다 (C-1030-3 4번).
  - 흐름: 방 만들기/참가 → `WaitingPartner` → 2명 모이면 **`Lobby`** → 게스트 `SetReady(true)` → 호스트 `PartnerReady == true` → 호스트 `StartGame()` → 두 PC 코스 로드. 이제 파트너가 들어와도 자동으로 코스가 열리지 않는다.
  - 게스트 Ready는 마스터 소유 `NetSession` 오브젝트(씬이 바뀌어도 유지)에 저장된다. 로비에서 상대가 나가면 Ready가 꺼지고 `WaitingPartner`로 돌아간다. 남은 사람이 호스트가 된다.
  - `Changed` 이벤트를 보낸다(Phase·Status·IsFailure·IsHost·PartnerConnected·PartnerReady가 바뀔 때). UI가 매 프레임 그리는 방식도 그대로 동작한다.
  - `PartnerLostSeconds`는 아직 0, `PartnerLost` 단계 없음 (S2-04에서).
- **C-1028-3 답**: **B안 확정** (Shared `ISessionLobby`). 클라 `BootMenuUI`가 Net을 직접 참조하지 않는 상태 그대로 유지하면 된다.
- **영향**: 클라 코드 변경 없음. `BootMenuUI`의 준비/시작 버튼이 온라인에서 실제로 동작한다. 게스트 쪽 `PartnerReady`는 항상 false다 (UI는 호스트일 때만 이 값을 쓴다).
- **클라가 할 일**: 없음. 2인 온라인 테스트 결과가 나오면 이 항목에 추가하겠다.
