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

## C-1128-3 · 알림: 로비 팝업·대기실·무대 이름표 아트 적용 (이름·경로 그대로)

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-28 |
| 상태 | 미확인 |
| 파일 | 수정: `UI_Lobby.prefab`(CreateRoomPopup·JoinCodePopup·RoomPanel), `LobbyScene.unity`(위 셋의 인스턴스 덮어쓰기 16개 되돌림, `RoomStage/Slot_P*/Occupied/NameTag` 그림·크기 + `ReadyBadge` 추가) / 새 파일: `Client/UI/LobbySkinState.cs`·`RoomStageReadyBadge.cs` (+ `Client/Editor/LobbyRoomListSkin.cs` 메뉴 `VAROGO/Lobby/Apply Popups and Room Skin`) |

- **무엇을**: 아트_UI ② 리소스(KJ 승인) 적용. `LobbyUI`가 찾는 경로·이름은 그대로, 새 오브젝트는 추가만: `JoinCodePopup/Window/GuideText`, `PasteButton/Icon`, `CodeBar/CodeLabel`, `CopyCodeButton/Icon`, `SteamInviteButton/InviteIcon`, `VisibilityTag/LockIcon`, `PrivateNoticeText/LockIcon`, 토글 `RadioBg`. 붙여넣기·복사 버튼은 아이콘만(Label 끔). 팝업 Footer 순서: 확인(초록) → 취소. `CodeInput` 바탕은 투명(6칸 위에 겹쳐 키 입력만).
- **상태 그림**: 클라 `LobbySkinState`(UI_Lobby 루트)가 `NetBootstrap`을 읽어 그림만 바꿈 — 팀 버튼(기본/내가/상대/둘 다), 준비 버튼(준비 초록 ↔ 준비 취소 주황), 공개 태그 색·자물쇠, 코드 6칸(빈칸/입력됨/ErrorText 켜지면 오류). 팀 버튼은 `interactable=false`여도 색이 바래지 않게 disabledColor 흰색. `RoomStageReadyBadge`(슬롯마다)가 `GetSlot(i).Ready`로 이름표 옆 배지 교체. 이름표 테두리 색(`RoomStageView`)은 그대로.
- **영향**: 동작·네트워크 변화 없음. Play로 확인: 팝업 2개, 방 만들기 → 대기실(팀 선택 → 내 팀 금빛 테두리, 준비·시작 비활성 그림), 나가기. 에러 0.
- **⚠ 서버 파일 수정 (KJ 직접 지시 2026-11-28)**: `Server/Lobby/LobbyUI.cs:339` 대기실 공개 태그 글자 `"비밀"` → `"비공개"`(한 줄). 같이 클라 프리팹 문구도 맞춤: 방 만들기 토글 "비밀" → "비공개", 안내문 "비밀방은…" → "비공개 방은…", 초대 버튼 "스팀 초대" → "친구 초대".
- **서버가 할 일**: 확인만. 위 한 줄 변경을 알고 있기.

---

## C-1128-2 · 알림: 로비 방 목록 아트 적용 (UI_Lobby.prefab, 이름·경로 그대로)

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-28 |
| 상태 | 미확인 |
| 파일 | 수정: `02_Prefabs/Client/UI/Lobby/UI_Lobby.prefab`(RoomListPanel), `LobbyScene.unity`(UI_Lobby 인스턴스의 RoomListPanel 하위 덮어쓰기 25개 되돌림 — `RoomListPanel` 켜짐/`Background`는 유지) / 새 파일: `Client/UI/RoomStatusTagTint.cs`, `Client/Editor/LobbyRoomListSkin.cs`, `04_Resources/UI/ONE Mobile POP SDF - Lobby Outline.mat` |

- **무엇을**: 아트_UI 리소스(KJ 승인)로 방 목록을 꾸밈. `LobbyUI`가 찾는 경로(`RoomListPanel/TopBar/BackButton`, `.../Content/RoomItem_Template` 안 `RoomNameText`·`PlayerCountText`·`StateText`·`JoinButton`, `Footer/*`, `EmptyText` 등)는 이름·계층 그대로. 새 오브젝트는 추가만 함: `RoomListPanel/Frame`, `TopBar/BarBg`, 템플릿 안 `CountSlot`·`StatusTag`, `EmptyText/EmptyIsland`. 템플릿에 `Button`(호버 그림용, onClick 없음)과 `RoomStatusTagTint`(StateText 글자로 태그 색) 추가. 뒤로·새로고침은 아이콘만(Label 끔). Footer 순서: 방 만들기 → 코드로 참가.
- **추가 (KJ 결정 2026-11-28)**: 방 목록 바깥으로 숲 배경이 보이게 `RoomListPanel/Background`를 옅은 어둠(알파 0.35)으로 바꿈. 빈 무대가 비치지 않게 `RoomStage`에 클라 `RoomStageListHider`를 붙여 방 밖(`Browsing` 등)에서는 `Slot_P1`·`Slot_P2`를 끄고 `WaitingPartner`·`Lobby`에서 켬. `RoomStageView`는 수정 안 함(Transform.Find라 꺼진 슬롯도 찾음). `BT-02.png`에 9-slice(L60 R60 T24 B24) 추가 — 다른 사용처는 모두 Simple이라 영향 없음.
- **영향**: `LobbyUI.DrawList`는 그대로 동작(Play로 확인). 줄 글자 색은 `DisabledText` 덮어쓰기가 그대로 적용됨. `RoomStageView.cs:50` 주석("불투명 패널이 무대를 가린다")은 이제 "슬롯을 숨긴다"가 맞음 — 원하면 주석만 정리.
- **서버가 할 일**: 없음.

---

## C-1128-1 · 알림: 타이틀 3D 숲 + 로비 배경 미리 로드·로딩 가림막 (서버 파일 수정 없음)

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-28 |
| 상태 | 미확인 |
| 파일 | 수정: `TitleScene.unity`(클라 오브젝트만: BootMenu 배경·로고·버튼, 새 루트 `TitleForest`·`TitleCamera`·`TitleVolume`·`LobbyPreload`, `BootCamera` 끔), `LobbyScene.unity`(새 루트 `LobbyLoadingCover`만), `Client/Loading/AddressablePrefabLoader.cs` / 새 파일: `Client/Loading/AddressablePreload.cs`·`LoadingCover.cs`, `Client/Editor/TitleForestBuilder.cs`, `02_Prefabs/Client/Map/Title/Title_ForestBackground.prefab`(Addressable 그룹 `Title_Background`)·`VP_Title.asset` / Addressables: `Shared_Forest`에 중복 에셋 23개 추가 |

- **무엇을**
  - 타이틀 배경을 3D 숲으로 교체. 숲은 Addressable 프리팹이라 TitleScene이 숲 에셋을 직접 참조하지 않음(Analyze 중복 0).
  - 로비 "맨 무대가 먼저 보임" 버그(팀장 지시): TitleScene `LobbyPreload`가 타이틀 숲 로드 뒤 `Lobby_CampBackground`를 미리 메모리에 올림 → 로비 로더가 생성을 마치면 놓음. LobbyScene `LobbyLoadingCover`(Canvas 정렬 95, UI_Message 100 아래)가 배경 로드가 끝날 때까지 화면을 덮고 0.4초 페이드로 걷힘(최대 20초). 게임 후 로비로 돌아올 때도 같음.
- **왜**: 로비 숲 텍스처 약 1GB가 로비 진입 뒤에야 로드돼서, 그동안 맨 무대가 보였음.
- **영향**: `NetBootstrap`·`TitleStartLink`·`LobbyUI`·`RoomStageView`는 건드리지 않음. 참고: `UI_Lobby/RoomListPanel/Background`(검은 불투명 Image)가 리스트 화면에서 무대를 가리므로 `RoomStageView.cs:50` 주석은 맞음 — 맨 무대는 리스트 패널이 켜지기 전 첫 프레임들에 보였던 것으로 추정.
- **서버가 할 일**: 없음. 빌드에서 로비 진입 시 가림막이 자연스러운지 확인 부탁.

---

## C-1127-1 · Addressables 도입 — 스테이지 씬을 Addressable 씬으로 (KJ 허락, 클라가 서버 파일 수정)

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-27 |
| 상태 | 미확인 |
| 파일 | (서버) `Server/NetBootstrap.cs` 수정 · `LobbyScene.unity` 수정 / (클라) `Packages/manifest.json`(`com.unity.addressables` 5.0.0) · `ProjectSettings/EditorBuildSettings.asset` · 새 `Assets/AddressableAssetsData/` · 새 `Client/Loading/AddressablePrefabLoader.cs` · 새 `02_Prefabs/Client/Map/Lobby/Lobby_CampBackground.prefab` · `Client/Goal/StageExit.cs`·`ClearUI.cs` · `Client/Editor/CI/VaroGOCIBuild.cs` |

- **무엇을**
  1. **빌드 씬 목록 = `TitleScene`(0) · `LobbyScene`(1)만.** `PlayScene_Stage1`·`PlayScene_Stage2`·`PlayScene_Flight`·`DemoScene_1123`·`ObjectTestScene_Stage1_KJ`는 Addressable 씬(주소 = 씬 경로, 라벨 `FusionScenes`). 빌드에는 그대로 들어간다. 없는 파일 `11_SpiralCourse.unity` 줄도 뺐다.
  2. `NetBootstrap.cs`: `LoadCourse`·`LoadStage`가 `ResolveSceneRef(path)`를 쓴다 — 빌드 목록에 있으면 지금처럼 `SceneRef.FromIndex`, 없으면 `Runner.SceneManager.GetSceneRef(path)`(Fusion `NetworkSceneManagerDefault`가 `FusionScenes` 라벨로 경로를 찾아 `SceneRef.FromPath`). 로비·타이틀로 돌아가는 코드는 그대로.
  3. `LobbyScene` `RoomStage/Background3D/CampBackground`(숲 배경, 텍스처 약 1GB)를 프리팹으로 빼고, 그 자리에 `CampBackground_Loader`(`AddressablePrefabLoader`)를 두었다. 로비가 열리면 비동기로 생성. `RoomStage`·`RoomStageView`·`Slot_P1/P2`·`Floor_Placeholder`·`BackWall_Placeholder`는 그대로.
  4. Fusion은 패키지가 있으면 asmdef로 `FUSION_ENABLE_ADDRESSABLES`를 자동으로 켠다. `NetworkProjectConfig`·프리팹 테이블은 바꾸지 않음(런타임 Spawn은 `NetSession`·`Player_Net`뿐이고 빌드에 남음. `Net/Level/*`는 씬 오브젝트라 씬과 같이 이동).
- **왜**: 에셋이 계속 늘어난다(KJ). 스테이지별 묶음으로 나눠 패치·로딩·메모리를 관리하고, 로비가 숲 에셋 1GB를 통째로 읽던 문제를 없앤다. 기준 빌드 2.6GB 중 텍스처 2.23GB, 로비 데이터 파일 1.1GB.
- **영향**
  - `SceneUtility.GetBuildIndexByScenePath(스테이지 경로)`는 이제 -1이다. 스테이지를 번호로 찾는 코드를 새로 쓰면 안 된다(경로 → `ResolveSceneRef`).
  - Addressable 씬도 `scene.path`는 원래 경로 그대로라 `IsMenuScene`·스폰 판단은 영향 없음(확인 필요 — 2인 테스트 항목).
  - 빌드 씬 목록에 스테이지를 다시 넣으면 빌드 데이터와 묶음에 중복으로 들어간다.
  - `Server---Network` 브랜치와 겹치는 파일 없음(그 브랜치의 `TitleScene` courseScene → `PlayScene_Stage1` 변경은 그대로 동작).
- **빌드 시 주의사항 (꼭 지킬 것)**
  - **플레이 씬(`PlayScene_*`, `DemoScene_1123`, `ObjectTestScene_*`)은 빌드 씬 목록(Build Profiles / `EditorBuildSettings`)에 넣지 않는다.** 목록은 `TitleScene`(0)·`LobbyScene`(1)만. 넣으면 같은 에셋이 빌드 데이터와 Addressables 묶음에 중복으로 들어가 용량이 커지고, 스테이지를 번호로 불러 Addressables 경로와 어긋난다.
  - 새 스테이지 씬은 빌드 목록 대신 Addressables 그룹 `Scene_<이름>`에 넣고, 주소 = 씬 경로, 라벨 `FusionScenes`. (클라가 한다. 서버 작업 중 필요하면 클라에 요청)
  - 테스트용으로 시작 코스를 바꿀 때도 `NetBootstrap.courseScene`만 바꾸면 된다. 빌드 목록에 씬을 추가할 필요 없음(Addressable 씬도 경로로 로드됨).
  - 빌드는 평소처럼(Build Profiles 또는 CI `/build`). 스테이지 묶음은 플레이어 빌드 때 자동으로 같이 만들어진다.
  - **빌드를 남에게 줄 때는 폴더 전체를 압축한다.** 스테이지 묶음이 `VaroGO_Data/StreamingAssets/aa/`에 있어서, 빠지면 로비까지만 되고 스테이지에 못 들어간다. (도입 후 첫 빌드에서 실제 확인 예정)
- **서버가 할 일**: 확인만. 위 빌드 주의사항을 지킨다. 2인 온라인 테스트(스테이지 진입·전환·클리어 후 로비 복귀·늦게 들어온 PC)는 클라가 한다. 결과는 여기에 이어서 적는다.

## C-1122-2 · 알림: TitleScene 옛 시작·세션 패널 삭제 (S-1107-2 2·3번 처리)

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-22 |
| 상태 | 미확인 |
| 파일 | 수정: `TitleScene.unity`(클라 `BootMenu`만) · `Client/UI/BootMenuUI.cs` · `Client/Editor/BootMenuBuilder.cs` / 삭제: `Client/UI/FakeSessionLobby.cs` |

- **무엇을**: `TitleScene`에서 `BootMenu/StartPanel`·`BootMenu/SessionPanel`을 삭제했다(방 만들기·코드 참가·준비·시작). `BootMenuUI`는 종료 버튼만 처리한다. 쓰는 곳이 없어진 테스트용 `FakeSessionLobby`도 삭제했다. `NetBootstrap`·`StartButton`의 `TitleStartLink`·`UI_Message`·`OptionPopup`은 건드리지 않았다.
- **왜**: S-1107-2 요청. 방 만들기·참가는 LobbyScene이 맡고, 옛 패널은 화면에 나올 방법이 없었다.
- **영향**: 서버 코드는 이 오브젝트들을 참조하지 않는다(grep으로 확인). `TitleStartLink.cs` 12·34줄 주석의 "BootMenuUI가 예전 시작 패널을 연다"는 이제 해당하지 않는다(동작에는 영향 없음).
- **서버가 할 일**: 없음. 원하면 위 주석만 정리.

---

## C-1122-1 · 요청: 대기실 UI 두 화면 어긋남 2건 (같은 팀 팝업 · 내 닉네임 라벨)

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-22 |
| 상태 | 미확인 |
| 파일 | (서버) `Server/NetBootstrap.cs` · `Server/NetSession.cs` — 클라는 수정하지 않음 |

- **무엇을**
  1. **"서로 다른 팀을 선택해 주세요" 팝업이 방장 화면에만 뜬다** (`NetBootstrap.StartGame` 390-394 → 로컬 `Popup(SameTeamMessage)`). 게스트는 왜 시작이 안 되는지 모른다. (C-1109-2 연계)
  2. **내 닉네임 라벨만 로컬 값** (`NetBootstrap.GetSlot` 327: 내 칸 = `PlayerNickname.Current`, 상대 칸 = `NetSession.NicknameOf` 90-93의 방 기록값). 방장 검증에서 이름이 거부·변경되면 내 화면은 내 PC 값, 상대 화면은 기록값이라 두 화면 이름이 다르다. (C-1109-1 연계)
- **왜**: 대기실 UI는 두 PC가 같은 결과를 봐야 한다(계약 §4.1 온라인 테스트 기준).
- **영향**: 클라 UI(`UI_Message` 프리팹·`LobbyUI` 표시) 쪽 변경은 없다. 서버 코드만 바뀌면 된다.
- **서버가 할 일**
  1. 같은 팀으로 시작 실패 시 게스트에게도 같은 알림이 뜨게 (예: 모두에게 가는 RPC로 `Popup` 호출).
  2. 내 칸도 `NicknameOf(LocalPlayer)` 기록값을 쓰고, 아직 기록 전(빈 값)일 때만 `PlayerNickname.Current`로 대신 보여 주기.

---

## C-1109-2 · 대기실 같은 팀 선택 허용, 시작 때 다른 팀 검사 (서버 허락, 클라가 서버 파일 수정)

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-09 |
| 상태 | 확인함 |
| 파일 | 수정: `Server/NetSession.cs` · `Server/NetBootstrap.cs` · `Server/Lobby/LobbyUI.cs` / 프리팹(클라): `02_Prefabs/Client/UI/Lobby/UI_Message.prefab`, `02_Prefabs/Client/UI/UI_NicknamePopup.prefab` |

- **무엇을**:
  - `NetSession`: 대기실 팀·준비를 사람별로 저장(`[Networked, Capacity(4)] NetworkDictionary<PlayerRef,int> LobbyTeams`, `<PlayerRef,NetworkBool> LobbyReady`). `ReadyRed`/`ReadyBlue` 삭제. 같은 팀 요청도 승인(준비 중이면 거절은 그대로).
    - `TeamOf(player)`: 대기실 = 고른 팀, 코스 중 = 확정 역할(`RoleOf`). `IsReady`·`BothReady`는 사람 기준(2명 모두 팀 + 준비, 같은 팀이어도 true).
    - `TeamsDistinct`(레드 1 + 블루 1), `CommitRoles()`(상태 권한만): 고른 팀으로 `RedPlayer`/`BluePlayer` 확정. `RoleOf`(게임 역할)·재접속 자동 배정(S2-05)은 그대로.
    - `UpdateRoles`에서 나간 사람의 닉네임·대기실 팀·준비를 지움.
  - `NetBootstrap`: `GetSlot`·`LocalTeam` → `TeamOf`. `StartGame`: 같은 팀이면 `Popup("서로 다른 팀을 선택해 주세요")` 후 중단, 다르면 `CommitRoles()` → `LoadCourse()`. `CanStartGame`(버튼 활성)은 그대로 `BothReady`.
  - `LobbyUI`: 팀 버튼 "· 잠김" 제거. 표시 = `(선택)` / `(상대)` / `(둘 다)`.
  - 알림창·닉네임 팝업에 `04_Resources/UI/InGame` 리소스 적용(PN-01 판, BT-02 버튼, X01 닫기, CT-02-1 입력칸, ONE Mobile POP 글꼴). 일시정지 메뉴와 같은 스타일.
- **왜**: 기획 변경 — 대기실에선 같은 팀 허용, 게임 시작 때만 서로 달라야 함.
- **영향**: 게임 역할은 시작 순간에 정해진다. 방장 = 상태 권한(Shared 마스터)이라 `CommitRoles`가 바로 적용된 뒤 코스를 연다. 알림은 시작 버튼을 누른 방장 화면에만 뜬다.
- **확인한 것**: 컴파일·Fusion 위빙 에러 없음. 팝업 화면 렌더 확인.
- **남은 일 (2인 온라인 테스트)**: 같은 팀 선택 → 둘 다 준비 → 시작 → 알림 / 다른 팀 → 시작 → 코스에서 역할이 고른 팀과 같은지 / 코스 중 재접속 시 빈 역할 배정 / 60초 초과 후 로비 복귀 뒤 다시 시작.

---

## C-1109-1 · 닉네임 UI 세팅 완료 + 대기실 팀 발판 — 서버 연결 요청

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-09 |
| 상태 | 확인함 |
| 파일 | 새 파일: `Shared/PlayerNickname.cs`(계약 P22), `Client/UI/NicknamePopup.cs`·`TitleNicknameGate.cs`·`LobbyNicknameButton.cs`·`RoomStageTeamPad.cs`, `02_Prefabs/Client/UI/UI_NicknamePopup.prefab`, `04_Resources/Material/Client/LobbyStage/M_TeamPad_{Red,Blue,None}.mat` / 수정: `TitleScene.unity`(클라 `BootMenu`·팝업만), `LobbyScene.unity`. 서버 파일은 안 고침 |

- **무엇을**:
  - **닉네임 저장**: `PlayerNickname`(Shared). `Current`(없으면 빈 문자열), `HasNickname`, `TrySave(name)`, `Changed` 이벤트(바뀐 값). 2~12자, 한글·영문·숫자. PlayerPrefs `VaroGO.Nickname`.
  - **닉네임 팝업** `UI_NicknamePopup` (Canvas 정렬 90, UI_Message 100 아래): `Dim/Window/Header/TitleText`·`CloseButton`(X) · `Rows/NicknameRow/NicknameInput`·`CountText` · `Rows/RuleBox/RuleText` · `Footer/CancelButton`·`ConfirmButton`. 규칙 검사·저장까지 클라가 함. 네트워크 동작 없음.
  - **TitleScene**: 팝업 인스턴스 + `BootMenu`에 `TitleNicknameGate`. "게임 시작"을 눌렀는데 닉네임이 없으면 팝업을 연다. 확인 → 저장 후 `StartButton.onClick`을 다시 불러 로비로 / 취소 → 타이틀에 남음.
  - ~~LobbyScene 방 리스트 닉네임 변경 줄~~ → **2026-11-09 제거** (`NicknameRow`·LobbyScene 팝업 인스턴스·`LobbyNicknameButton.cs` 삭제). 닉네임은 타이틀 첫 입력으로만 정한다.
  - **대기실 무대 팀 발판**: `RoomStage/Slot_P1·P2/Occupied/TeamPad`(`RoomStageTeamPad`) 아래 `Pad_None`(회색)·`Pad_Red`·`Pad_Blue`. `NetBootstrap.GetSlot(i).Team`을 읽어 고른 팀 발판만 켠다(`RoomStageCharacters`와 같은 방식). 팀 표시는 이제 발판이 맡는다.
- **왜**: 기획 — 닉네임 입력 팝업, 로비·대기실 무대에 닉네임 표시, 발밑 "팀 미선택" 문구 제거, 팀은 레드/블루 발판으로 표시.
- **영향**: 서버 연결 전에는 ① 닉네임이 없어도 `TitleStartLink`가 바로 로비로 보내서 팝업이 잠깐 뜨고 씬이 넘어감 ② 이름표는 계속 `P1 · 방장` ③ "팀 미선택" 문구가 남음.
- **서버 파일 수정 (2026-11-09, 서버 담당자 허락 받고 클라가 직접 고침)** — 확인 부탁:
  1. `Lobby/TitleStartLink.GoToLobby()` 맨 앞: `if (!PlayerNickname.HasNickname) return;` — 팝업이 확인 후 버튼을 다시 누른다.
  2. **닉네임 동기화** `NetSession`: `[Networked, Capacity(4)] NetworkDictionary<PlayerRef, NetworkString<_16>> Nicknames`. 각 PC가 `Spawned`에서 자기 `PlayerNickname.Current`를 `RPC_SetNickname`(→ 상태 권한)으로 보내고, `PlayerNickname.Changed`가 오면 다시 보낸다. 상태 권한은 규칙(`IsValid`) 검사 후 기록, `UpdateRoles`에서 나간 사람 항목을 지운다. 조회: `NicknameOf(player)`.
     - `NetBootstrap.RoomSlot`에 `Nickname` 추가(생성자 마지막 인자, 기본값 ""라 기존 호출 그대로 동작). `GetSlot`은 내 슬롯이면 내 PC 값, 상대면 `NicknameOf`.
  3. `Lobby/RoomStageView`: 이름표 = `닉네임 · 방장` / `닉네임` (닉네임이 아직 없으면 `P1`/`P2`). 이름표에서 팀 이름은 뺐다(팀은 발판이 보여 줌, 테두리 색은 그대로). 발밑 `ReadyStateText`는 **항상 빈 문자열**(팀 미선택·준비 전·준비 완료 모두 표시 안 함, 2026-11-09 추가 수정).
  4. `Lobby/LobbyUI`: 방 만들기 이름칸 placeholder = `<닉네임>의 방`, 이름을 비우고 만들면 그 이름으로 만든다(닉네임이 없으면 기존 기본 이름). 대기실·방 리스트에는 플레이어 이름이 나오는 곳이 없어 그 외 변경 없음.
- **확인한 것**: 컴파일·Fusion 위빙 에러 없음. 팝업 규칙(싱글) 확인.
- **남은 일**: 2인 온라인 테스트(MPPM) — 양쪽 이름표가 서로의 닉네임인지, 방 리스트에서 닉네임을 바꾸고 방에 들어가면 반영되는지, 방장 이전 후에도 이름이 유지되는지.

---

## C-1104-2 · Boot 씬 = `TitleScene.unity` (00_Boot 이름 변경) ⚠ 서버 확인 필요

| 항목 | 내용 |
| --- | --- |
| 날짜 | 2026-11-04 |
| 상태 | 완료 (2026-11-07, 서버: `BootScenePath` → TitleScene, 계약 v0.10 §7 갱신) |
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
| 상태 | 완료 (2026-11-07, 서버 연결 완료 — 답: 전달_서버_to_클라 S-1107-1) |
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
