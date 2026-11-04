# 전달: 클라 → 서버

클라가 서버 소유 파일(접속·로비 Net 파일, `00_Boot.unity`, `NetSession.prefab`, `NetworkProjectConfig` 등 — 계약명세 §7)을 고쳤거나, 서버에 부탁할 일(최적화 대상 공유 포함)이 있을 때 여기에 적는다.
**서버는 작업 시작 전에 이 문서를 먼저 읽는다.** (`/server-start` 1단계)

> 2026-11-04 초기화 (계약 v0.9 역할 분담 변경). 이전 기록(C-1028-1 ~ C-1104-1)은 git `5ad90df` 이력에서 볼 수 있다.

## 쓰는 법

- 새 항목은 **맨 위**에 추가한다. 번호는 `C-날짜-순번`.
- 항목마다: 무엇을 / 왜 / 서버에 미치는 영향 / 서버가 할 일.
- 상태: `미확인` → 서버가 읽으면 `확인함` → 처리 끝나면 `완료`. 서버는 상태 칸만 바꾸고, 답이 필요하면 `전달_서버_to_클라.md`에 쓴다.
- 계약(§1~§7)에 걸리는 변경은 `계약명세.md` §9 계약 변경 후보에도 올린다.
- 기능 동기화·온라인 테스트를 끝내면 결과를 한 줄로 남긴다(계약 §4.1 2단계) — 서버 최적화 대상.

## 항목 틀

```
## C-MMDD-N · 제목

| 항목 | 내용 |
| --- | --- |
| 날짜 | YYYY-MM-DD |
| 상태 | 미확인 |
| 파일 | 경로 (수정/새 파일/삭제) |

- **무엇을**:
- **왜**:
- **영향**:
- **서버가 할 일**:
```

---

## C-1104-2 · Boot 씬 = `TitleScene.unity` (00_Boot 이름 변경) ⚠ 서버 확인 필요

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-04 |
| 상태 | 미확인 |
| 파일 | `Assets/01_Scenes/00_Boot.unity` → `Assets/01_Scenes/TitleScene.unity` (이름 변경, 내용 같음) · 빌드 세팅 0번 = `TitleScene` |

- **무엇을**: 시작 씬(예전 `00_Boot`)의 이름을 로비 기획서에 맞춰 `TitleScene`으로 바꿨다. 안의 `NetBootstrap`·`BootMenu`·`EventSystem` 등은 그대로다. 빌드 세팅 0번도 `TitleScene`이다.
- **왜**: 로비 기획서 구조(TitleScene / LobbyScene)에 맞춤.
- **영향**: `NetBootstrap.cs`의 `BootScenePath`가 아직 `"Assets/01_Scenes/00_Boot.unity"`다. 이 값을 쓰는 곳이 맞지 않게 된다.
  - `OnSceneLoadDone`: Boot 씬 판정(재접속 시간 초과로 돌아온 경우 처리, Boot에서 스폰 건너뛰기)
  - `ReturnToBoot`: 빌드 번호를 못 찾아(-1) 타이틀로 돌아가지 않음
  - 문서·스킬에서 `00_Boot`라고 부르는 곳은 이제 `TitleScene`을 뜻한다.
- **서버가 할 일**: `BootScenePath`를 `"Assets/01_Scenes/TitleScene.unity"`로 바꾼다(서버 파일이라 클라는 손대지 않음). 계약명세 §7의 `00_Boot.unity` 줄도 `TitleScene.unity`로 바꿀지 정한다.

## C-1104-1 · 로비 UI 세팅 완료 (기획서 v0.1) — 서버 연결 요청

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-04 |
| 상태 | 미확인 |
| 파일 | 새 프리팹 `Assets/02_Prefabs/Client/UI/Lobby/UI_Lobby.prefab` · `UI_Message.prefab` / 새 씬 `Assets/01_Scenes/LobbyScene.unity` / `TitleScene.unity`: `BootMenu/OptionPopup` 추가, `UI_Message` 배치(꺼짐) / 빌드 세팅 1번에 `LobbyScene` 추가 |

- **무엇을**: 로비 기획서 화면 구조를 uGUI + TMP(Paperlogy) 회색 박스로 만들었다. **네트워크 동작·스크립트 없음.** 버튼에 들어간 연결은 팝업 켜기/끄기(SetActive)뿐이다.
- **화면별 오브젝트 이름** (전부 프리팹 루트 기준 경로)
  - 타이틀: 기존 `TitleScene`의 `BootMenu`가 기획서 타이틀 화면과 같아서 그대로 쓴다. 옵션 팝업만 추가했다.
    - `BootMenu/OptionPopup/Window/` `Header/CloseButton`(→ 끔) · `Rows/BgmRow/BgmSlider`·`BgmValueText` (0~100, 80) · `Rows/SfxRow/SfxSlider`·`SfxValueText` (60) · `Rows/LanguageRow/LanguageDropdown` (한국어/English) · `Rows/ExtraRowSlot` · `Footer/DefaultButton` · `Footer/ApplyButton`
    - `BootMenu/MainPanel/OptionButton` → `OptionPopup` 켬 (인스펙터 OnClick). `BootMenuUI`가 실행 중 붙이는 "옵션은 준비 중입니다" 안내도 아직 같이 뜬다.
  - `UI_Lobby` (Canvas, 정렬 0)
    - `RoomListPanel/` `TopBar/BackButton` · `TopBar/RefreshButton` · `RoomList`(ScrollView) `/Viewport/Content/RoomItem_Template`(꺼짐 — 복제해서 사용: `RoomNameText`·`PlayerCountText`·`StateText`·`JoinButton`) · `RoomList/EmptyText` · `Footer/JoinCodeButton`(→ JoinCodePopup 켬) · `Footer/CreateRoomButton`(→ CreateRoomPopup 켬)
    - `CreateRoomPopup/Window/` `Rows/RoomNameRow/RoomNameInput` (최대 16자) · `Rows/VisibilityRow/VisibilityToggles/PublicToggle`·`PrivateToggle` (ToggleGroup, 기본 공개) · `Rows/PrivateNoticeText`(비밀 켜면 보임) · `Footer/CancelButton`(→ 끔) · `Footer/CreateButton`
    - `JoinCodePopup/Window/` `CodeInput` (TMP_InputField, 6자, 영숫자) · `CodeSlots/CodeSlot_1~6/SlotText`(표시용) · `PasteButton` · `ErrorText`(꺼짐) · `Footer/CancelButton`(→ 끔) · `Footer/JoinButton`(비활성)
    - `RoomPanel/`(꺼짐, 대기실 **화면 위 UI만**) `TopShade` · `RoomInfo/RoomNameText` · `RoomInfo/VisibilityTag/TagText` · `CodeBar/CodeText` · `CodeBar/CopyCodeButton` · `CodeBar/SteamInviteButton`(비활성) · `LeaveButton` · `TeamReadyPanel/RedTeamButton`·`BlueTeamButton`·`ReadyButton`(비활성) · `StartGameButton`(비활성, 방장 전용)
  - 대기실 **3D 무대** (`LobbyScene` 루트 `RoomStage`, 프리팹 아님): 캐릭터·배경은 실제 3D가 들어간다.
    - `StageCamera`(씬 메인 카메라) · `Directional Light` · `Background3D/`(자리표시 `Floor_Placeholder`·`BackWall_Placeholder` → 3D 배경으로 교체)
    - `Slot_P1`·`Slot_P2` (x −1.2 / +1.2, 카메라 쪽을 봄)
      - `Occupied/CharacterAnchor` — **실제 3D 캐릭터를 이 아래에 둔다**(발 = 원점). 지금은 `CharacterPlaceholder` 캡슐
      - `Occupied/NameTag/NameText` (월드 UI, 머리 위. `Plate`의 Outline = 팀 색 테두리) · `Occupied/ReadyState/ReadyStateText` (월드 UI, 발밑)
      - `Empty/Silhouette`(어두운 캡슐) · `Empty/EmptyLabel/EmptyText`
      - 사람이 있으면 `Occupied`, 없으면 `Empty`를 켠다. 처음 상태 = P1 있음, P2 빈 자리
  - `UI_Message` (Canvas, 정렬 100, 두 씬 공통): `MessagePopup/Window/Header/TitleText`·`MessageText`·`Footer/ConfirmButton`(→ 끔) · `Toast/ToastText` · `LoadingOverlay/LoadingText` (전부 꺼짐)
- **씬 구성**: `LobbyScene` = `RoomStage`(3D 무대 + 카메라 + 조명) + EventSystem(InputSystemUIInputModule) + `UI_Lobby` + `UI_Message`. 방 리스트 화면은 불투명 배경이라 무대를 가리고, 대기실(`RoomPanel`)일 때만 무대가 보인다. `TitleScene` = 기존 `BootMenu`(서버 연결 그대로) + `BootMenu/OptionPopup`(꺼짐) + `UI_Message`(꺼짐).
- **영향 / ⚠ 확인 필요**
  - Boot 씬 이름 변경은 C-1104-2 참고.
  - `LobbyScene`이 빌드 1번에 들어가 코스 씬 번호가 하나씩 밀렸다. `NetBootstrap`은 경로로 번호를 찾아서 영향 없을 것으로 본다.
  - 타이틀 → 로비 씬 전환·방 화면 전환(방 리스트 ↔ 대기실)은 네트워크 결과에 따라 바뀌므로 서버 쪽에서 연결한다.
- **서버가 할 일**: 위 오브젝트에 연결 스크립트를 추가해 버튼·글자를 붙인다. 배치·이름은 바꾸지 말고, 바꿔야 하면 클라에 요청한다. `BootScenePath` 갱신 여부를 판단한다.
