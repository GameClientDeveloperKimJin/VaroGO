# 전달: 서버 → 클라

서버가 클라 소유 파일(`Assets/03_Scripts/Client/`, `Assets/02_Prefabs/Client/`, 클라 소유 Net 파일 — 계약명세 §7)을 고쳤거나(예: 네트워크 최적화), 클라에 부탁·답변할 일이 있을 때 여기에 적는다.
**클라는 작업 시작 전에 이 문서를 먼저 읽는다.**

> 2026-11-04 초기화 (계약 v0.9 역할 분담 변경). 이전 기록(S-1102-1 ~ S-1103-3)은 git `5ad90df` 이력에서 볼 수 있다.

## 쓰는 법

- 새 항목은 **맨 위**에 추가한다. 번호는 `S-날짜-순번`. `전달_클라_to_서버.md` 항목에 대한 답이면 제목에 그 번호를 적는다 (예: `답: C-1105-1`).
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

## S-1107-3 · 요청: 코스에서 로비로 돌아오면 EventSystem이 2개 (경고 폭주)

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-07 |
| 상태 | 미확인 |
| 파일 | (클라) `Client/UI/PauseMenu.cs`·`PartnerLostOverlay.cs` `EnsureEventSystem()` — 서버는 수정하지 않음 |

- **무엇을**: 재접속 60초 초과로 코스 → `LobbyScene`에 돌아오면 콘솔에 `There are 2 event systems in the scene`이 매 프레임 찍힌다(한 번 테스트에 2만 줄 넘게). 코스 씬에서 `PauseMenu`/`PartnerLostOverlay`가 만든 `EventSystem (…)`이 DontDestroyOnLoad라 남아 있고, `LobbyScene`에도 EventSystem이 있어서다. 방 나가기·끊김으로 로비에 돌아올 때도 같다.
- **왜**: 로비 기획서 흐름상 코스가 끝나면 이제 `LobbyScene`(EventSystem 있음)으로 돌아온다. 예전엔 Boot로만 돌아가서 덜 드러났다.
- **영향**: 경고 폭주로 로그 확인이 어렵고, 어느 EventSystem이 입력을 받을지 불분명해진다.
- **클라가 할 일**: 만든 EventSystem을 DontDestroyOnLoad로 두지 않거나(코스 씬에 귀속), 씬이 바뀔 때 다른 EventSystem이 있으면 스스로 지우게 해 주세요.

## S-1107-2 · 요청: 타이틀 시작 버튼 정리 · 옵션 팝업 · ISessionLobby 의미 변경 · 시작 코스

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-07 |
| 상태 | 확인함 (1 완료 2026-11-08 — StartButton 리스너 제거. 2·3·4 남음) |
| 파일 | (클라) `Assets/03_Scripts/Client/UI/BootMenuUI.cs` — 서버는 수정하지 않음 / `TitleScene.unity` `NetBootstrap.courseScene` |

- **무엇을**
  1. **타이틀 "게임 시작"**: 서버가 `BootMenu/MainPanel/StartButton`에 `TitleStartLink`를 **추가**해 누르면 `LobbyScene`으로 간다. 그런데 `BootMenuUI`가 같은 버튼에 StartPanel(만들기/코드 참가) 여는 리스너를 코드로 붙이고 있어서, 씬이 넘어가기 직전 StartPanel이 잠깐 열린다.
  2. **옵션 팝업**(`BootMenu/OptionPopup`, BGM·SFX·언어·PlayerPrefs 저장)은 네트워크가 필요 없어 클라 몫으로 남긴다(로비 기획서 p5). 지금 OptionButton은 인스펙터 연결(팝업 켜기)과 `BootMenuUI` 코드("옵션은 준비 중입니다")가 둘 다 실행된다.
  3. **`ISessionLobby` 의미 변경 (계약 v0.10 §3.7)**: 팀 = 역할(대기실에서 레드/블루 선택, 방장이 무조건 A가 아님). `SetReady`는 두 사람 모두, `PartnerReady` = 상대 준비, `StartGame`은 방장 + 둘 다 팀·준비일 때만. → `BootMenuUI`의 "게스트만 Ready" 흐름으로는 이제 시작할 수 없다(팀 선택 UI가 없으므로).
  4. **시작 코스**: `TitleScene` `NetBootstrap.courseScene`이 `ObjectTestScene_Stage1_KJ`로 되어 있어 `11_SpiralCourse`로 되돌렸다(사용자 결정).
- **왜**: 로비 기획서 v0.1 흐름(타이틀 → LobbyScene 방 리스트 → 대기실 → 게임). 방 만들기·참가는 이제 LobbyScene에서 한다.
- **영향**: 인게임 쪽 `GameServices.Session`(`Phase`·`PartnerLostSeconds`·`Leave`)은 그대로. `PartnerLostOverlay`의 `Leave()`는 이제 TitleScene이 아니라 **LobbyScene 방 리스트**로 돌아간다.
- **클라가 할 일**
  1. `BootMenuUI`에서 StartButton의 StartPanel 리스너와 Start/Session 패널(방 만들기·코드 참가·준비·시작)을 정리. 화면 전환은 서버 `TitleStartLink`가 한다(그 컴포넌트는 지우지 말 것).
  2. 옵션 팝업 구현 시 "옵션은 준비 중입니다" 안내 제거.
  3. 오브젝트 테스트에 다른 코스가 필요하면 `courseScene`은 로컬에서만 바꾸고 커밋하지 않기.
  4. 계약 §9 **P21**(`Contracts.cs` `SetReady`·`StartGame` 주석만 수정) 확인.

## S-1107-1 · 답: C-1104-1 · C-1104-2 — 로비 UI 서버 연결

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-07 |
| 상태 | 미확인 |
| 파일 | 서버 `NetBootstrap.cs`·`NetSession.cs`·`BootUI.cs` (수정) / 새 `Assets/03_Scripts/Server/Lobby/` `LobbyUI`·`LobbyMessageView`·`RoomStageView`·`TitleStartLink`·`LobbyUiUtil` / `LobbyScene.unity`·`TitleScene.unity` 컴포넌트 추가 |

- **무엇을**: 로비 기획서 v0.1 흐름을 서버에 구현하고 클라 UI에 **컴포넌트만 추가**해 연결했다. 오브젝트 이름·배치·클라 인스펙터 연결(팝업 켜기/끄기)은 그대로다.
  - `LobbyScene` `UI_Lobby` 루트 → `LobbyUI` (방 리스트·방 만들기·코드 참가·대기실 버튼과 글자)
  - `LobbyScene` `UI_Message` 루트 → `LobbyMessageView` (알림 팝업·토스트·로딩)
  - `LobbyScene` `RoomStage` → `RoomStageView` (Slot_P1 = 방장, Slot_P2 = 참가자, Occupied/Empty, 이름표 `P1 · 방장 · 블루`, Plate 테두리 = 팀 색, 발밑 준비 상태)
  - `TitleScene` `BootMenu/MainPanel/StartButton` → `TitleStartLink` (LobbyScene으로)
  - `JoinCodePopup/Window/CodeInput`은 `CodeSlots`와 같은 자리에 겹쳐 있어 글자가 두 번 보였다 → `LobbyUI`가 실행 중에 입력칸 글자·커서·선택색만 투명하게 한다(키 입력만 받음, 보이는 글자는 `CodeSlot_1~6`). 프리팹은 그대로. 아트 작업 때 입력칸을 따로 보이게 하려면 알려 달라.
  - UI는 **이름(경로)으로 찾는다.** 이름을 바꾸면 콘솔에 `[Lobby] UI를 찾지 못했습니다: …` 에러가 뜨니, 바꿔야 하면 먼저 알려 달라.
  - C-1104-2: `BootScenePath` → `TitleScene`, 새 `LobbyScenePath`. 계약 §7 `00_Boot` 줄도 `TitleScene`으로 갱신(v0.10).
- **동작 요약**: 게임 시작 → LobbyScene에서 서버 연결(로딩) → 공개방 목록 → 방 만들기(이름 16자, 비우면 "같이 깨요", 공개/비밀) / 코드로 참가 / 목록에서 참가 → 대기실(팀 선택, 둘 다 준비, 방장만 시작) → 11_SpiralCourse. 나가기·해산·끊김은 방 리스트로, 방 리스트 "뒤로"는 타이틀로.
- **기획서와 다른 점**: Photon 세션 목록은 **가득 찬 방(2/2)을 보내지 않는다**. 그래서 p6의 "2/2 게임 중(회색)" 줄은 나오지 않고, 1/2 방만 보인다. 1/2인데 게임 중(파트너 재접속 대기)인 방은 "게임 중" 회색으로 보인다. 닉네임이 없어 이름표는 P1/P2만(사용자 결정).
- **영향**: 클라가 나중에 아트를 입힐 때 이 컴포넌트들은 건드리지 않는다(계약 §0·§7). 개발용 IMGUI 접속창(`BootUI`)은 기본 숨김, F11로 켠다(§7.1).
- **클라가 할 일**: 없음(확인만). 관련 요청은 S-1107-2.
