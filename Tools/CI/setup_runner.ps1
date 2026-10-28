<#
빌드 PC에 GitHub Actions 셀프 호스티드 러너를 설치한다 (한 번만). gh 로그인 필요. 저장소 관리자가 아니면 -Token (아래)

  powershell -ExecutionPolicy Bypass -File Tools/CI/setup_runner.ps1 [-RunnerDir D:/actions-runner] [-Token <등록 토큰>]

  관리자가 아닌 팀원 PC: 관리자가 등록 토큰(1시간 유효)을 만들어 전달 → -Token 으로 넣는다
    gh api -X POST repos/<owner>/<repo>/actions/runners/registration-token -q .token

  1. 최신 actions/runner 를 RunnerDir 에 받는다
  2. 이 저장소에 라벨 varogo-build 로 등록한다 (작업 폴더: RunnerDir/_work → 여기에 프로젝트 사본이 생긴다)
  3. 로그인할 때 러너가 화면 없이 켜지도록 작업 스케줄러에 등록하고 지금 바로 켠다
     (꺼지면 10초 뒤 자동 재시작 + 5분마다 확인. 설정을 바꾼 뒤 다시 실행해도 됨)
     (서비스가 아니라 로그인 사용자로 돌리는 이유: Unity 라이선스·Google Drive(G:)가 사용자 계정에 묶여 있음)

제거: 작업 스케줄러 'VaroGO Build Runner' 삭제 → RunnerDir 에서 config.cmd remove --token <토큰>
#>
param([string]$RunnerDir = 'D:\actions-runner', [string]$Token)

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

    $token = if ($Token) { $Token } else { gh api -X POST "repos/$repo/actions/runners/registration-token" -q .token }
    if (-not $token) { Write-Host '등록 토큰을 못 받음. 저장소 관리자가 아니면 관리자에게 토큰을 받아 -Token 으로 넣기' -ForegroundColor Red; exit 1 }
    Push-Location $RunnerDir
    try {
        & .\config.cmd --unattended --url "https://github.com/$repo" --token $token `
            --name "$env:COMPUTERNAME-varogo" --labels 'varogo-build' --work '_work' --replace
        if ($LASTEXITCODE -ne 0) { throw "config.cmd 실패 ($LASTEXITCODE)" }
    }
    finally { Pop-Location }
}

# 다시 실행할 때: 빌드 중이면 멈춘다 (작업이 끊김). 아니면 돌던 러너를 끄고 새 설정으로 켠다
$runnerName = "$env:COMPUTERNAME-varogo"
# (관리자가 아니면 러너 목록을 못 읽음 → 확인 건너뜀)
$runners = try { (gh api "repos/$repo/actions/runners" 2>$null | ConvertFrom-Json).runners } catch { $null }
if (@($runners) | Where-Object { $_.name -eq $runnerName -and $_.busy }) {
    Write-Host '러너가 빌드 중. 끝난 뒤 다시 실행' -ForegroundColor Red; exit 1
}
$RunnerDir = (Resolve-Path $RunnerDir).Path
if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Stop-ScheduledTask -TaskName $TaskName
    Get-Process Runner.Listener, Runner.Worker -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -like "$RunnerDir\*" } | Stop-Process -Force
}

# 러너가 어떤 이유로 꺼져도(Ctrl+C 종료 코드 0xC000013A 등) 10초 뒤 다시 켜는 루프.
# 작업 스케줄러의 '실패 시 다시 시작'은 실행 자체가 실패할 때만 동작해서, 러너가 종료 코드를 내고 끝나면 안 켜진다
$keepAlive = Join-Path $RunnerDir 'keep_alive.ps1'
@"
# setup_runner.ps1 이 만든 파일. 러너가 꺼지면 10초 뒤 다시 켠다
Set-Location '$RunnerDir'
while (`$true) {
    & .\run.cmd
    Start-Sleep -Seconds 10
}
"@ | Set-Content -Path $keepAlive -Encoding UTF8

# 로그인 시 자동 실행 (창 없이) + 5분마다 확인: 루프째 꺼졌으면 다시 켠다 (돌고 있으면 무시)
$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$keepAlive`""
$triggers = @(
    New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
    New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 5)
)
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -MultipleInstances IgnoreNew -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $triggers -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName $TaskName

Write-Host "러너 설치 완료: $RunnerDir (라벨 varogo-build). 로그인할 때마다 자동으로 켜지고, 꺼지면 다시 켜짐" -ForegroundColor Green
Write-Host "확인: https://github.com/$repo/settings/actions/runners 에서 Idle 이면 정상"
