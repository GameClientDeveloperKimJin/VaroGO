<#
VAROGO CI 빌드 — .github/workflows/build.yml 이 셀프 호스티드 러너(이 PC)에서 실행한다. 직접 실행할 일은 없다.

  1. ProjectVersion.txt 버전의 Unity로 배치 빌드 (Editor/CI/VaroGOCIBuild.cs)
  2. 공유 폴더(환경변수 VAROGO_BUILD_SHARE = 팀 Google Drive) 안 VaroGO_vN_* · 빌드_기록.txt 를 보고 다음 버전 번호 결정
  3. VaroGO_v<N>_<첫 씬 이름>[_외N][_Release]_YYMMDD.zip 으로 올리고 빌드_기록.txt에 한 줄 추가

환경변수(러너가 넘김): VAROGO_SCENES(쉼표로 여러 개, 비면 11_SpiralCourse) · VAROGO_RELEASE(true/false)
                       GITHUB_SHA · GITHUB_REF_NAME · GITHUB_ACTOR · RUNNER_TEMP
Unity 경로를 못 찾으면 환경변수 UNITY_EDITOR_PATH 지정.
#>
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Scene = $env:VAROGO_SCENES
$Release = $env:VAROGO_RELEASE -eq 'true'
$Share = $env:VAROGO_BUILD_SHARE
$Temp = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { $env:TEMP }

function Fail($msg) { Write-Host "::error::$msg"; exit 1 }

# 0. 공유 폴더 먼저 확인 — 빌드 다 하고 못 올리면 시간 낭비
if (-not $Share) { Fail "환경변수 VAROGO_BUILD_SHARE 없음 (러너 PC에 설정 후 러너 재시작)" }
if (-not (Test-Path -LiteralPath $Share)) { Fail "공유 폴더에 접근 못 함 (Google Drive 실행·로그인 확인): $Share" }

# 1. ProjectVersion.txt 버전에 맞는 Unity 찾기
$version = (Select-String -Path (Join-Path $ProjectRoot 'ProjectSettings\ProjectVersion.txt') -Pattern '^m_EditorVersion:\s*(.+)$').Matches[0].Groups[1].Value.Trim()
$UnityPath = $env:UNITY_EDITOR_PATH
if (-not $UnityPath) {
    $roots = @(Join-Path $env:ProgramFiles 'Unity\Hub\Editor')
    $hubCfg = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
    if (Test-Path $hubCfg) {
        $secondary = Get-Content $hubCfg -Raw | ConvertFrom-Json
        if ($secondary) { $roots += $secondary }
    }
    foreach ($root in $roots) {
        $candidate = Join-Path $root "$version\Editor\Unity.exe"
        if (Test-Path $candidate) { $UnityPath = $candidate; break }
    }
}
if (-not $UnityPath -or -not (Test-Path $UnityPath)) { Fail "Unity $version 을 찾지 못함. 러너 PC에 UNITY_EDITOR_PATH 지정" }

# 2. 배치 빌드 (화면 없음)
$outDir = Join-Path $Temp 'VaroGO'
$log = Join-Path $Temp 'unity_build.log'
if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
$unityArgs = @(
    '-batchmode', '-quit', '-nographics',
    '-projectPath', "`"$ProjectRoot`"",
    '-buildTarget', 'Win64',
    '-executeMethod', 'VaroGO.Client.EditorTools.VaroGOCIBuild.Build',
    '-varogoOutput', "`"$outDir`"",
    '-logFile', "`"$log`""
)
if ($Scene) { $unityArgs += @('-varogoScenes', "`"$Scene`"") }
if ($Release) { $unityArgs += '-varogoRelease' }

Write-Host "Unity $version 빌드 시작: $(if ($Scene) { $Scene } else { '11_SpiralCourse' }) / $(if ($Release) { 'Release' } else { 'Development' })"
$started = Get-Date
# 러너와 콘솔을 공유하면 Unity 쪽 Ctrl+C 신호가 러너까지 꺼 버린다 → 숨긴 별도 콘솔에서 실행
$proc = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -Wait -PassThru -WindowStyle Hidden
$seconds = [int]((Get-Date) - $started).TotalSeconds

if ($proc.ExitCode -ne 0 -or -not (Test-Path (Join-Path $outDir 'VaroGO.exe'))) {
    Write-Host "Unity 종료 코드 $($proc.ExitCode) ($seconds s)"
    if (Test-Path $log) {
        Write-Host '--- 에러 줄 ---'
        Select-String -Path $log -Pattern 'error CS\d+|\[VaroGOCIBuild\]|Scripts have compiler errors|Aborting batchmode|another Unity instance|No valid Unity Editor license' |
            Select-Object -First 20 | ForEach-Object { Write-Host "  $($_.Line.Trim())" }
        Write-Host '--- 로그 끝 40줄 ---'
        Get-Content $log -Tail 40 | ForEach-Object { Write-Host "  $_" }
    }
    Fail '빌드 실패'
}
Write-Host "빌드 성공 ($seconds s)"

# 3. 다음 버전 = 공유 폴더의 VaroGO_vN_* 파일과 빌드_기록.txt 중 가장 큰 N + 1
$recordPath = Join-Path $Share '빌드_기록.txt'
$nums = @(Get-ChildItem -LiteralPath $Share | ForEach-Object { if ($_.Name -match '^VaroGO_v(\d+)_') { [int]$Matches[1] } })
if (Test-Path -LiteralPath $recordPath) {
    $nums += @(Get-Content -LiteralPath $recordPath -Encoding UTF8 | ForEach-Object { if ($_ -match '^v(\d+)\s') { [int]$Matches[1] } })
}
$ver = 1 + [int](($nums | Measure-Object -Maximum).Maximum)

$names = @(if ($Scene) { $Scene.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ } } else { '11_SpiralCourse' })
$label = [IO.Path]::GetFileNameWithoutExtension($names[0]) + $(if ($names.Count -gt 1) { "_외$($names.Count - 1)" })
$kind = "$label$(if ($Release) { '_Release' })"
$date = $started.ToString('yyMMdd')
while ($true) { # 다른 사람이 같은 번호를 먼저 올렸으면 다음 번호
    $name = "VaroGO_v${ver}_${kind}_$date"
    $dest = Join-Path $Share "$name.zip"
    if (-not (Test-Path -LiteralPath $dest)) { break }
    $ver++
}

$noBom = New-Object Text.UTF8Encoding $false
[IO.File]::AppendAllText((Join-Path $outDir 'build_info.txt'), "version: v$ver`r`n", $noBom)

# 4. zip(DoNotShip 폴더 제외) → 공유 폴더
$zip = Join-Path $Temp "$name.zip"
$items = Get-ChildItem -LiteralPath $outDir | Where-Object { $_.Name -notlike '*DoNotShip*' }
Compress-Archive -Path $items.FullName -DestinationPath $zip -Force
try { Copy-Item -LiteralPath $zip -Destination $dest }
catch { Fail "공유 폴더 복사 실패: $($_.Exception.Message)" }

if (-not (Test-Path -LiteralPath $recordPath)) {
    [IO.File]::WriteAllText($recordPath, "버전`t빌드 시각`t종류`t커밋`t파일`t올린 사람`r`n", (New-Object Text.UTF8Encoding $true))
}
$commit = "$(if ($env:GITHUB_SHA) { $env:GITHUB_SHA.Substring(0, 7) } else { '?' }) @ $env:GITHUB_REF_NAME"
$who = if ($env:GITHUB_ACTOR) { $env:GITHUB_ACTOR } else { $env:USERNAME }
[IO.File]::AppendAllText($recordPath, "v$ver`t$($started.ToString('yyyy-MM-dd HH:mm:ss'))`t$kind`t$commit`t$name.zip`t$who`r`n", $noBom)

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Remove-Item -Force $zip
Write-Host "공유 폴더에 올림: v$ver → $dest ($mb MB zip)"
if ($env:GITHUB_STEP_SUMMARY) { # Actions 실행 결과 화면에 표시
    "## v$ver 올림`n- 파일: ``$name.zip`` ($mb MB)`n- 씬: $($names -join ', ')`n- 빌드 시간: $seconds s" | Out-File -Append -Encoding utf8 $env:GITHUB_STEP_SUMMARY
}
exit 0
