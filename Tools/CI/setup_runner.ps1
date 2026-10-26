<#
빌드 PC에 GitHub Actions 셀프 호스티드 러너를 설치한다 (한 번만). 저장소 관리자 권한 + gh 로그인 필요.

  powershell -ExecutionPolicy Bypass -File Tools/CI/setup_runner.ps1 [-RunnerDir D:/actions-runner]

  1. 최신 actions/runner 를 RunnerDir 에 받는다
  2. 이 저장소에 라벨 varogo-build 로 등록한다 (작업 폴더: RunnerDir/_work → 여기에 프로젝트 사본이 생긴다)
  3. 로그인할 때 러너가 화면 없이 켜지도록 작업 스케줄러에 등록하고 지금 바로 켠다
     (서비스가 아니라 로그인 사용자로 돌리는 이유: Unity 라이선스·Google Drive(G:)가 사용자 계정에 묶여 있음)

제거: 작업 스케줄러 'VaroGO Build Runner' 삭제 → RunnerDir 에서 config.cmd remove --token <토큰>
#>
param([string]$RunnerDir = 'D:\actions-runner')

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$TaskName = 'VaroGO Build Runner'

$repo = gh repo view --json nameWithOwner -q .nameWithOwner
if (-not $repo) { Write-Host 'gh 로그인 또는 저장소 확인 실패 (gh auth login)' -ForegroundColor Red; exit 1 }

if (Test-Path (Join-Path $RunnerDir '.runner')) {
    Write-Host "이미 등록된 러너가 있음: $RunnerDir" -ForegroundColor Yellow
}
else {
    New-Item -ItemType Directory -Force $RunnerDir | Out-Null
    $tag = gh api repos/actions/runner/releases/latest -q .tag_name
    $v = $tag.TrimStart('v')
    $zip = Join-Path $RunnerDir "actions-runner-win-x64-$v.zip"
    Write-Host "러너 $tag 다운로드"
    Invoke-WebRequest "https://github.com/actions/runner/releases/download/$tag/actions-runner-win-x64-$v.zip" -OutFile $zip
    Expand-Archive $zip -DestinationPath $RunnerDir -Force
    Remove-Item $zip

    $token = gh api -X POST "repos/$repo/actions/runners/registration-token" -q .token
    Push-Location $RunnerDir
    try {
        & .\config.cmd --unattended --url "https://github.com/$repo" --token $token `
            --name "$env:COMPUTERNAME-varogo" --labels 'varogo-build' --work '_work' --replace
        if ($LASTEXITCODE -ne 0) { throw "config.cmd 실패 ($LASTEXITCODE)" }
    }
    finally { Pop-Location }
}

# 로그인 시 자동 실행 (창 없이)
$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -WindowStyle Hidden -Command `"Set-Location '$RunnerDir'; .\run.cmd`""
$trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
# 러너가 꺼지면 1분 뒤 다시 켠다
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName $TaskName

Write-Host "러너 설치 완료: $RunnerDir (라벨 varogo-build). 로그인할 때마다 자동으로 켜짐" -ForegroundColor Green
Write-Host "확인: https://github.com/$repo/settings/actions/runners 에서 Idle 이면 정상"
