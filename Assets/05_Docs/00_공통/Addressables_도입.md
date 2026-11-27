# Addressables 도입

최종 갱신: 2026-11-27

에셋이 계속 늘어날 것을 대비해, 스테이지를 **묶음(번들)** 단위로 나눠 읽는다. 목적은 패치 크기·로딩 시간·메모리 관리. (화면 프레임(FPS)은 그림자·SSAO 쪽 문제라 이 작업으로는 거의 안 바뀐다 → `성능_측정_계획.md`)

## 1. 지금 구성

| 무엇 | 어디에 들어가나 |
|---|---|
| `TitleScene`, `LobbyScene` | 빌드 씬 목록(0번, 1번). 처음부터 들어 있음 |
| 스테이지 씬 `PlayScene_Stage1` / `PlayScene_Stage2` / `PlayScene_Flight` | Addressable 씬. 그룹 `Scene_Stage1` / `Scene_Stage2` / `Scene_Flight` |
| 개발용 씬 `DemoScene_1123`, `ObjectTestScene_Stage1_KJ` | Addressable 씬. 그룹 `Scene_Dev` (빌드에는 그대로 들어감) |
| 로비 숲 배경 `Assets/02_Prefabs/Client/Map/Lobby/Lobby_CampBackground.prefab` | 그룹 `Lobby_Background`. 로비가 열리면 `CampBackground_Loader`가 불러옴 |
| 여러 씬이 같이 쓰는 숲 에셋(TriForge, 424개) | 그룹 `Shared_Forest` |
| 여러 씬이 같이 쓰는 그 밖의 에셋(310개) | 그룹 `Shared_Common` |
| 플레이어·UI·폰트·`NetSession`·`Player_Net` | 빌드에 그대로 |

- 모든 Addressable 씬: **주소 = 씬 경로**, 라벨 `FusionScenes` (Fusion이 이 라벨로 씬을 찾는다).
- 묶음은 플레이어 빌드할 때 같이 만들어진다(설정: Build Addressables on Player Build = 켬). 따로 버튼 누를 필요 없음.

## 2. 규칙

- **새 스테이지 씬**: 빌드 씬 목록에 넣지 말고, Addressables 창에서 새 그룹 `Scene_<이름>`에 넣고, 주소를 씬 경로로, 라벨 `FusionScenes`를 붙인다.
- **씬 이동은 경로로**: 온라인은 `GameServices.StageLoader.LoadStage(경로)`(= `NetBootstrap.LoadStage`), 오프라인은 `StageLoading.Load(경로)`. 빌드 번호(`buildIndex`)로 스테이지를 찾지 않는다(Addressable 씬은 -1).
- **새 씬·배경 추가 후**: Addressables 창 → Tools → Analyze → "Check Duplicate Bundle Dependencies" 실행 → 중복이 있으면 Fix 후 생긴 그룹의 에셋을 `Shared_Forest`(TriForge) / `Shared_Common`(나머지)으로 옮긴다.
- 무거운 배경을 타이틀·로비에 넣을 때는 씬에 직접 두지 말고 프리팹 + `AddressablePrefabLoader`로.
- 커밋 제외: `/ServerData/`(.gitignore), `Library/com.unity.addressables`(Library 전체 제외).

## 3. 텍스처 크기 (같이 한 작업)

- 숲 배경 텍스처 68장(4K)을 **PC 빌드에서만 최대 2048**로 낮춤. 원본 파일·다른 플랫폼 설정은 그대로. 목록 `Logs/Perf/texture_cap_2048_list.txt`
- 실측(에디터, 2026-11-27, `Logs/Perf/ab_20261127_224027/`): 텍스처 절반 → GPU 시간 변화 없음(약 5ms), **그래픽 메모리 3.65GB → 2.43GB (−33%)**, Z0·Z2·동굴 화면에서 차이 안 보임.

## 4. 전후 비교

| 항목 | 도입 전 (2026-11-27 기준 빌드) | 도입 후 |
|---|---|---|
| 빌드 전체 크기 | 2.6GB (텍스처 2.23GB) | 측정 예정 |
| 로비 데이터 | 1.1GB (숲 에셋이 로비 파일로 몰림) | 측정 예정 |
| 빌드 시간 | 3시간 8분 (대부분 셰이더 변형) | 측정 예정 |
| 로딩 시간·메모리·끊김 | 빌드 실행 측정 필요 (`PerfRuntime`, Server---Network 병합 후) | 측정 예정 |

## 5. 2인 온라인 테스트 목록

| # | 확인 | 결과 |
|---|---|---|
| 1 | 로비 숲 배경이 양쪽 화면에 보임(조금 늦게 떠도 됨) | |
| 2 | 대기실 → 게임 시작 → 두 PC 모두 Stage1 도착, 플레이어 스폰 위치 정상 | |
| 3 | Stage1 레벨 오브젝트(레버·발판·문·돌) 양쪽 상태 같음 | |
| 4 | Stage1 → Flight → Stage2 전환, 양쪽 같은 씬 | |
| 5 | 클리어 후 로비 복귀, 다시 시작 가능 | |
| 6 | 늦게 들어온 PC(재접속)가 지금 스테이지로 바로 들어감 | |
| 7 | 콘솔에 `[Net] 빌드 세팅·Addressables에 없는 씬` / `addressable scene` 오류 없음 | |
| 8 | 빌드 1개 + 에디터 1개로 1~6 반복 (빌드에서 묶음 로딩 확인) | |

확인 필요(Fusion): Addressable 씬 안의 네트워크 씬 오브젝트 등록, 늦게 들어온 PC의 씬 경로 일치 → 위 3·6번으로 확인.
