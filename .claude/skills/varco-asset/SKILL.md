---
name: varco-asset
description: VARCO 3D로 VAROGO 맵 소품·기믹 오브젝트 메시를 만들 때 사용. 컨셉 이미지 → 멀티뷰 확인 → 3D 생성 순서, 포인트 비용, 도구 사용 요령, Unity 반입 전 확인. JH_MMO의 varco-gear 스킬에서 VARCO 요령만 가져와 Unity·VAROGO에 맞춘 것.
---

# VARCO 에셋 제작 요령 (VAROGO)

출처: `C:\Users\kjhkjh\KJH\JH_MMO\.claude\skills\varco-gear\SKILL.md` (2026-11-18 가져옴). 언리얼 전용 내용(jm_kit, items.json 등)은 뺐다.
검토 3번(이미지 → 모델 → 애니메이션)과 파일 위치는 `Assets/05_Docs/02_아트/아트_제작_계약.md`를 따른다. **그 문서 §7 제작 기록 같은 기록 표에는 사용자가 시키지 않으면 적지 않는다.**

## 플로우

- 대상 플로우는 **`VaroGo`** (`list_flow_sessions`로 flowId 확인). `JH_MMO` 등 다른 프로젝트 플로우에 노드를 만들지 않는다. 모든 호출에 `flowId`를 넣는다.
- `run_flow`는 반드시 `scope`에 돌릴 노드 ID를 넣는다(비우면 옛 노드까지 전부 돈다).
- 노드가 많아지면 `get_flow_graph`가 너무 커진다 → 상태 확인은 노드별 `get_output_downloads(nodeId)`로 한다.
- 이미지 주소는 `https://3d.varco.ai` + 응답의 `/api/objects/<해시>.png`. 로그인 없이 내려받아진다.
- 로컬 이미지 업로드는 base64를 인자에 통째로 실어야 해서 큰 PNG는 불가 → VARCO 안에서 `EditImage`로 기존 노드 이미지를 편집한다.
- 프롬프트 맨 앞에 구분용 이름(`Z7 01 ThornArch.`)을 넣어도 이미지에 글자로 나오지 않는다.

## 요금 (2026-11-18, CREATOR 등급)

- `GenerateImage`·`EditImage`: **장당 약 50포인트** (`get_account_and_pricing`에는 20으로 나오지만 기억된 모델에 따라 다르다). 실제 예상치는 `run_flow` 응답의 `points`로 확인한다.
- `Generate3D`(텍스처 포함) 200포인트, `Remesh`·`PBR` 0포인트.
- 실행 전에 예상 포인트를 사용자에게 알리고, 알린 예산을 넘기 전에 다시 묻는다.

## 순서

1. **컨셉**: `TextInput` → `GenerateImage`. 물체 하나, 눈높이 3/4 각도, 단색 배경, 기본 모습(동작 중 모습 금지).
2. **검토 1** (사용자 승인).
3. **멀티뷰 확인 (3D 전 필수)**: 컨셉 이미지 → `EditImage`, 지시문 예 —
   `Turnaround reference sheet of this exact same object on a plain white background: front view, left side view, back view, and three-quarter top view in a 2x2 grid. Keep the same design, materials and proportions. No text, no labels.`
   시트를 내려받아 직접 본다:
   - 맵 스타일(스테이지1 = 손그림풍 판타지 숲, TriForge 숲 에셋)과 맞는가. 현대 사물·글자·로고가 보이면 탈락.
   - 뒷면·옆면이 말이 되는가.
   - 3D로 만들기 쉬운 형상인가 — 얇은 끈·사슬·촘촘한 격자, 공중에 뜬 조각, 여러 물체가 흩어진 구성은 무너진다.
   - 쓸 자리에 맞는 비율인가(이어 붙이는 조각은 양 끝이 평평한가).
4. 탈락이면 프롬프트를 고쳐 컨셉부터 다시. 통과한 것만 `Generate3D`.
5. **`Generate3D`에는 물체가 하나만 있는 컨셉 이미지를 넣는다** (멀티뷰 시트를 넣으면 물체 네 개가 한 덩어리가 된다).
6. **검토 2** (Unity에 넣은 모습).

## 형상 요령

- 위에서 내려다본 시점, 촘촘한 디테일, 낮은 폴리 설정은 형상이 무너진다. 눈높이, 단순한 형상, 단색 배경을 쓴다.
- **얇고 긴 물체(벽·울타리·다리 판·간판)는 정면 이미지로 3D를 만들지 않는다.** 두께가 안 보이면 사방이 막힌 상자가 나온다 → 처음부터 3/4 각도(약 40° 돌려 살짝 내려다본)로 만든다.
- 고폴리(3만~5만)로 생성하고 필요하면 `Remesh`(0포인트)로 줄인다.
- VARCO 메시는 긴 변이 1m로 정규화돼 오고(pivot 바닥), 앞면 방향이 제각각이다 → Unity에서 여러 각도로 찍어 앞면과 긴 축을 확인한 뒤 크기·회전을 정한다.
- 색 변형(공용·빨강·파랑)은 같은 모델에 Unity 재질만 바꾼다. 투명함도 Unity 재질로 낸다.

## Z5~Z8에서 얻은 요령 (2026-11-18)

- **무늬는 3D에서 바뀔 수 있다.** 큰 바위 컨셉의 "소용돌이 홈 한 줄"이 3D에서는 사선 홈 여러 줄이 됐다(한 면에만 있는 무늬는 나머지 면을 모델이 지어낸다). 무늬가 중요하면 멀티뷰 시트에서 모든 면의 무늬를 확인하고, 3D 결과도 여러 각도로 찍어 컨셉과 비교한다.
- **움직이는 부품은 따로 만든다.** 레버는 받침만 VARCO로 만들고(`EditImage`로 손잡이 제거) 손잡이는 Unity에서 붙였다. 한 덩어리 모델은 부품을 돌릴 수 없다.
- **여러 번 깔리는 조각은 `Remesh`로 줄인다(0포인트).** 텍스처가 다시 구워져 같이 나온다. 같은 경로의 FBX를 덮어쓰면 씬 참조가 유지된다(메시 이름이 같음). 텍스처는 새로 추출해 기존 png를 덮어쓴다.
- FBX는 텍스처가 들어 있어 약 20MB, 추출한 `base_color.png`는 4096(약 15MB)다 → PIL로 2048로 줄여 저장한다(약 5MB). `Model/` 폴더는 LFS 대상이 아니다.
- 임포트 설정: `bakeAxisConversion = true`(메시가 Y-up, 회전 없음으로 들어와 배치 계산이 쉽다), `materialImportMode = None`, 재질은 URP Lit으로 직접 만든다(`_BaseMap`, `_BumpMap`, Smoothness 0.15).
- Unity 기본 구 메시는 `GameObject.CreatePrimitive`에서 가져온다. `Resources.GetBuiltinResource<Mesh>("Sphere.fbx")`는 지름 2짜리 다른 메시가 잡혔다.

## Unity 반입

- 모델 `Assets/04_Resources/Model/<이름>/`, 재질 `Assets/04_Resources/Material/Client/<이름>/`.
- 기믹 프리팹은 겉모습(`Visual` 아래)만 바꾸고 충돌·동작 컴포넌트는 유지한다. 바꾼 뒤 `현재_프리팹_리스트.md` 갱신(CLAUDE.md 규칙).
- 프리팹에서 MeshRenderer를 지운 뒤 화면에 예전 도형이 남으면 `EditorUtility.RequestScriptReload()` 한 번(렌더 잔상).
