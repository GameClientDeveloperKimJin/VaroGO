<#
CI 빌드 요청 — GitHub Actions 의 Build 워크플로를 현재 브랜치로 실행시킨다 (빌드는 빌드 PC 러너가 함).
/build 스킬과 Unity 메뉴 Tools > Build 가 호출한다.

  powershell -ExecutionPolicy Bypass -File Tools/CI/trigger_build.ps1 [-Scenes "A,B"] [-Release] [-Yes]

  -Scenes   씬 이름 또는 Assets/... 경로, 쉼표로 여러 개. 비우면 기본 씬
  -Release  Development 끈 릴리스 빌드
  -Yes      푸시 안 된 커밋·커밋 안 된 변경이 있어도 그냥 진행

빌드되는 건 GitHub에 올라간 커밋이다. 내 PC에만 있는 변경은 빠진다 → 있으면 경고 후 종료 코드 3.
출력 끝에 RUN_URL=<주소> 한 줄.
종료 코드: 0 요청됨 / 1 실패 / 3 확인 필요(경고 확인 후 -Yes 로 다시)
#>
param([string]$Scenes = '', [switch]$Release, [switch]$Yes)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
Set-Location (Join-Path $PSScriptRoot '..\..')

function Fail($msg) { Write-Host "실패: $msg"; exit 1 }

gh auth status *> $null
if ($LASTEXITCODE -ne 0) { Fail 'gh 로그인 안 됨 (터미널에서 gh auth login)' }

$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -eq 'HEAD') { Fail '브랜치가 아님 (detached HEAD)' }
$upstream = git rev-parse --abbrev-ref '@{u}' 2> $null
if ($LASTEXITCODE -ne 0) { Fail "브랜치 '$branch' 가 GitHub에 없음 — 먼저 푸시" }
git fetch -q origin $branch 2> $null

# 내 PC에만 있는 변경 = 빌드에 안 들어감
$warnings = @()
$ahead = [int](git rev-list --count '@{u}..HEAD')
if ($ahead -gt 0) { $warnings += "푸시 안 된 커밋 $ahead 개" }
$behind = [int](git rev-list --count 'HEAD..@{u}')
if ($behind -gt 0) { $warnings += "GitHub에 내 PC보다 새 커밋 $behind 개 (그 상태로 빌드됨)" }
$dirty = @(git status --porcelain)
if ($dirty.Count -gt 0) { $warnings += "커밋 안 된 변경 $($dirty.Count) 개" }

$commit = (git log -1 --format='%h %s' '@{u}').Trim()
Write-Host "빌드할 커밋: $commit ($upstream)"
if ($warnings -and -not $Yes) {
    Write-Host '경고: 아래는 빌드에 안 들어감'
    $warnings | ForEach-Object { Write-Host "  - $_" }
    exit 3
}

$since = (Get-Date).ToUniversalTime().AddSeconds(-5)
$out = gh workflow run build.yml --ref $branch -f "scenes=$Scenes" -f "release=$(if ($Release) { 'true' } else { 'false' })"
if ($LASTEXITCODE -ne 0) { Fail 'gh workflow run 실패' }

# 실행 주소: 새 gh는 바로 출력, 옛 gh는 목록에서 찾기 (생기는 데 몇 초 걸림)
$url = @($out) -match '/actions/runs/\d+' | Select-Object -First 1
for ($i = 0; -not $url -and $i -lt 15; $i++) {
    Start-Sleep -Seconds 2
    $runs = (gh run list --workflow build.yml --branch $branch --event workflow_dispatch --limit 5 --json url,createdAt) | ConvertFrom-Json
    $url = @($runs) | Where-Object { $_.createdAt -and ([datetime]$_.createdAt).ToUniversalTime() -ge $since } |
        Select-Object -First 1 -ExpandProperty url
}
Write-Host "빌드 요청됨: $(if ($Scenes) { $Scenes } else { '기본 씬' }) / $(if ($Release) { 'Release' } else { 'Development' })"
if ($url -match '/actions/runs/(\d+)') {
    Write-Host "RUN_ID=$($Matches[1])"
    Write-Host "RUN_URL=$($url.Trim())"
}
else { Write-Host "RUN_URL=https://github.com/$(gh repo view --json nameWithOwner -q .nameWithOwner)/actions/workflows/build.yml" }
exit 0
