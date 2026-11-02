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
