---
name: build
description: CI 빌드 요청 — GitHub Actions Build 워크플로를 현재 브랜치로 실행하고 끝날 때까지 지켜본 뒤 결과(공유 폴더 vN 파일명)를 보고. 사용자가 /build, "빌드해줘", "빌드 돌려" 라고 할 때.
---

# CI 빌드

인자 예: `/build`, `/build release`, `/build 11_SpiralCourse,00_Boot`, `/build 10_DemoRoom release`

빌드는 빌드 PC의 러너가 한다. 이 PC에서 Unity를 띄우지 않는다.
빌드되는 건 **GitHub에 푸시된 커밋**이다. 결과는 공유 폴더(팀 Google Drive)에 `VaroGO_v<N>_<씬>_<YYMMDD>.zip`으로 자동 업로드된다.
Unity 메뉴 `Tools > Build`도 같은 스크립트를 쓴다.

## 1. 요청

인자에서 `release`는 `-Release`로 바꾼다. 나머지는 씬으로 넘긴다(`-Scenes "a,b"`). 씬이 없으면 기본 씬 `11_SpiralCourse`.

```
powershell -ExecutionPolicy Bypass -File Tools/CI/trigger_build.ps1 [-Scenes "..."] [-Release]
```

종료 코드에 따라:

- **3**: 푸시 안 된 커밋이나 커밋 안 된 변경이 있어서 경고가 나온 것. 경고 줄을 사용자에게 보여 주고, "GitHub에 올라간 커밋으로 빌드할지" 묻는다.
  - 사용자가 진행하라고 하면 `-Yes`를 붙여 다시 실행한다.
  - 사용자가 이미 "그냥 빌드", "푸시된 걸로"라고 했으면 묻지 않고 바로 `-Yes`.
  - 커밋·푸시를 대신 하지 않는다. 커밋·푸시는 사용자가 한다.
- **1**: 출력의 `실패:` 줄을 보여 준다.
  - gh 로그인이 안 된 경우: `! gh auth login`을 안내한다.
  - 브랜치가 원격에 없는 경우: 먼저 푸시하라고 안내한다.
- **0**: `RUN_URL`을 알려 준다. 그다음 2단계로 간다.

## 2. 지켜보기

`RUN_ID`로 백그라운드에서 실행한다. 2~5분, 첫 빌드면 더 걸린다.

```
gh run watch <RUN_ID> --exit-status
```

끝나면:

```
gh run view <RUN_ID> --log
```

로그에서 다음 줄을 찾는다: `빌드 성공`, `공유 폴더에 올림`, `::error::`, `error CS`, `[VaroGOCIBuild]`.

## 3. 보고 (한국어, 짧게)

- **성공**: 올라간 버전(vN), 파일명, 크기, 빌드 시간, 커밋.
- **실패**: 에러 줄, 원인 추정, 다음 행동.
  - 대기열에서 안 움직임: 러너가 꺼진 것. 작업 스케줄러 `VaroGO Build Runner`를 확인.
  - 씬 못 찾음: 씬 이름을 확인하거나 경로로 지정.
  - 컴파일 에러: 파일·줄 번호를 알려 준다.
  - 공유 폴더 접근 실패: 빌드 PC의 Google Drive 실행·로그인을 확인.
- 빌드 스크립트를 고친 뒤 실패한 실행을 Re-run하면 옛 커밋으로 다시 돈다. 그럴 땐 새로 푸시한 뒤 /build.

## 규칙

- 공유 폴더의 기존 zip·`빌드_기록.txt`는 지우거나 고치지 않는다.
- Build Settings·씬 파일은 건드리지 않는다.
