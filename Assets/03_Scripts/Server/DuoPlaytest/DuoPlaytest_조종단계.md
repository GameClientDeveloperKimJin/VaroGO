# DuoPlaytest 조종 단계 (한 에디터 2인 테스트)

에디터 전용, 오프라인 전용. 씬에는 아무것도 저장하지 않는다. 로그는 `[Duo]`로 시작한다.

## 시작
1. `PlayScene_Stage2`를 열고 Play한다.
2. MCP `execute_code`(codedom)로 다음을 실행한다.

```csharp
VaroGO.Client.DuoPlaytest.Begin(false, @"<CSV 경로 또는 null>");
VaroGO.Client.DuoPlaytest.Run("red",  "명령; 명령; ...");
VaroGO.Client.DuoPlaytest.Run("blue", "명령; 명령; ...");
```

- `Begin`:
  - 씬 플레이어를 복제해 레드·블루 두 몸을 만든다. 둘 다 로컬로 판정받는다(`PlayerController.DebugRoleOverride`).
  - 온라인 규칙에 맞춘다. 2단 박스는 2명이 필요하고, 체크포인트는 둘 다 밟아야 저장된다.
- `DuoPlaytest.Stop()`: 명령 줄을 비운다.
- 결과 확인: `%LOCALAPPDATA%/Unity/Editor/Editor.log`에서 `[Duo]`를 grep한다. Bash로 읽으면 에디터가 멈추지 않는다. 반대로 `execute_code`는 한 번 부를 때마다 에디터를 약 2초 멈춘다.

## 명령
| 명령 | 뜻 |
|---|---|
| `goto x z [허용거리]` | 그 지점까지 걷기 (닫힌 고리, 25초 제한) |
| `jumpto x z [허용거리]` | 달리면서 바로 점프하고, 착지한 뒤 도착까지 걷기 |
| `run dx dz 초 [점프시각]` | 방향으로 계속 입력. 열린 고리라서 카메라 기준이 틀어질 수 있으니 밀기 외에는 `goto`를 쓴다 |
| `jump` / `e` | 점프 / 상호작용(E) 한 번 |
| `prone on` / `prone off` | 엎드리기 키 누름 / 뗌 (토글 방식이라 일어날 때는 `jump`) |
| `face x z` | 그 지점을 바로 보게 돌리기 (박스 잡기 전) |
| `tp x y z` | 준비용 순간이동 (시간 측정에는 넣지 않는다) |
| `wait 초` / `grounded` | 대기 / 땅에 닿을 때까지 |
| `mark 이름` / `waitfor partnerLog 이름` | 봇끼리 신호 맞추기 |
| `waitfor y 높이` | 그 높이 이상에 서 있을 때까지 |
| `waitfor plat 이름 x z r` | 켜진 Z2 발판이 그 지점 근처에 올 때까지 |
| `waitfor platY 이름 y` / `platLow 이름 y` | Z2 발판 높이가 y 이상 / 이하일 때까지 |
| `waitfor z2red n` / `z2blue n` / `rope n` / `cp n` | Z2 단계, 밧줄 단계, 체크포인트 order |
| `waitfor partnerNear x z r` / `partnerIdle` | 상대 위치 / 상대 명령이 끝났을 때 |
| `log 메모` | 로그에 메모 남기기 |

`waitfor`의 제한 시간은 600초다. 다른 명령은 25초다.

## 구역별 명령 (2026-11-28 실측에 쓴 것)

### Z1 유리 다리
배치는 `GlassBridge.DebugLayout()`으로 읽는다. 칸 중심은 x = -7.2 + 3.6×(열-1), z = 23.8 + 3.6×(줄-1)이다. 대각선 이동은 칸 모서리 틈으로 지나가므로 옆 칸에 닿지 않는다.

```
red : goto -7.2 20; goto <줄1 안전칸>; ... ; goto <줄6 안전칸>; goto -7.2 47 0.6
blue: goto -5.7 19; wait 1.2; (같은 경로)
```

### Z2 교대 발판
내가 내 발판을 밟으면 상대의 다음 발판이 켜지고, 상대의 지금 발판은 꺼진다. 그래서 상대가 대기석에 도착했다는 신호(`mark`)를 받은 뒤에 밟는다.

```
red : tp -8 2.2 63; goto -8 65.3 0.3; waitfor platLow R1_Vertical 2.35; goto -8 68 0.5; mark redOnR0; waitfor y 6.1; jumpto -12.4 70.4 0.5; mark redWS1;
      waitfor partnerLog blueWS2; waitfor plat R2_Horizontal -12 73 0.8; jumpto -12 73 0.6; waitfor plat R2_Horizontal -3 73 0.5; jumpto -2.1 75.4 0.5; mark redWS2;
      waitfor partnerLog blueWS3; jumpto -6 78 0.8; jumpto -9.3 80.4 0.5; mark redWS3;
      waitfor partnerLog blueWS4; waitfor platLow R4_Vertical 7.1; jumpto -10 83 0.6; waitfor y 10.8; jumpto -13.4 85.4 0.5; mark redWS4;
      waitfor partnerLog blueWS5; waitfor plat R5_Horizontal -12 88 0.8; jumpto -12 88 0.6; waitfor plat R5_Horizontal -3 88 0.5; jumpto -2.1 90.4 0.5; mark redWS5;
      waitfor partnerLog blueB6; jumpto -6 93 0.8; goto -6 98 0.6; mark redDeck
blue: tp 14 2.2 63; waitfor partnerLog redOnR0; goto 14 65.3 0.3; waitfor platLow Neutral_Entry_Lift 2.25; goto 14 68 0.5; waitfor y 6.2; jumpto 12.4 70.4 0.5; mark blueWS1;
      waitfor partnerLog redWS1; waitfor plat B2_Horizontal 12 73 0.8; jumpto 12 73 0.6; waitfor plat B2_Horizontal 3 73 0.5; jumpto 2.1 75.4 0.5; mark blueWS2;
      waitfor partnerLog redWS2; jumpto 6 78 0.8; jumpto 9.3 80.4 0.5; mark blueWS3;
      waitfor partnerLog redWS3; waitfor platLow B4_Vertical 7.1; jumpto 10 83 0.6; waitfor y 10.8; jumpto 13.4 85.4 0.5; mark blueWS4;
      waitfor partnerLog redWS4; waitfor plat B5_Horizontal 12 88 0.8; jumpto 12 88 0.6; waitfor plat B5_Horizontal 3 88 0.5; jumpto 2.1 90.4 0.5; mark blueWS5;
      waitfor partnerLog redWS5; jumpto 6 93 0.8; mark blueB6; goto 6 98 0.6; mark blueDeck
```

### Z3 창고
```
red : tp -36 6.2 97; goto -55.8 84.5 0.4; goto -55.8 85.6 0.25; face -55.8 90; mark redAt2; waitfor partnerLog blueAt2; e; wait 0.3; run 0 1 9; e;
      goto -50 84 0.5; goto -41 84 0.5; goto -41 86 0.3; goto -41.9 86 0.2; face -50 86; e; wait 0.3; run -1 0 6.9; e;
      goto -52 83.4 0.4; goto -55 83.4 0.3; goto -55 84.2 0.2; face -55 90; e; wait 0.3; run 0 1 9.5; e;
      goto -55.3 96.6 0.3; jumpto -55.3 99.3 0.5; jumpto -55.2 102 0.5; jumpto -55 105 0.5; goto -55 107.5 0.3; face -55 112; mark redReady; waitfor partnerLog blueUp;
      goto -55 104.5 0.4; goto -49 104.5 0.5; goto -40 105 0.5; goto -36 107.6 0.4; mark redWaiting; waitfor rope 3; goto -34.5 118 0.6
blue: tp -36 6.2 99; goto -54.2 84.5 0.4; goto -54.2 85.6 0.25; face -54.2 90; mark blueAt2; waitfor partnerLog redAt2; e; wait 0.3; run 0 1 9; e;
      goto -52 97 0.5; waitfor partnerLog redReady; goto -55.3 96.6 0.3; jumpto -55.3 99.3 0.5; jumpto -55.2 102 0.5; jumpto -55 104.6 0.5; jumpto -55 107.5 0.5; wait 0.3; jumpto -55 109 0.45; wait 0.8;
      prone on; wait 0.5; goto -55 112.8 0.3; prone off; jump; wait 1; mark blueUp;
      goto -64 126.5 0.8; goto -76 126.5 1.2; e; goto -64 126.5 0.8; goto -59 126.5 0.8; goto -40 114 1; goto -36 114.2 0.8; e; waitfor partnerLog redWaiting; e; waitfor rope 3; goto -34.5 118 0.6
```

주의할 점:
- `e`를 연달아 넣지 않는다. 이미 놓은 박스를 다시 잡는다.
- 밧줄은 미로 안에 있다. `Search_Partition_A`(x -62, z 110~124)는 북쪽 끝으로 돌아간다.
- 매달린 동료를 구할 때는 도착하고 한 박자 쉰 뒤 `e`를 누른다. 도착한 순간 누르면 실패한 적이 있다.
