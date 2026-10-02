param(
    [string]$WebProject = '',
    [switch]$SkipTests,
    [switch]$UpdateClients
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$apiProject = Join-Path $repoRoot 'ShoppetAPI\ShoppetAPI.csproj'
if (-not $WebProject) {
    $candidates = @(
        (Join-Path $repoRoot '..\web\Shoppet_VetClinic.csproj'),
        (Join-Path $env:USERPROFILE 'source\repos\ShoppetCare_VetClinic\Shoppet_VetClinic\Shoppet_VetClinic.csproj'),
        (Join-Path $env:USERPROFILE 'source\repos\Shoppet_VetClinic\Shoppet_VetClinic.csproj')
    )
    $WebProject = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $WebProject -or -not (Test-Path $WebProject)) {
    throw 'Web project was not found. Run again with -WebProject "C:\path\to\Shoppet_VetClinic.csproj".'
}
$WebProject = (Resolve-Path $WebProject).Path
$runDirectory = Join-Path $repoRoot ('artifacts\integration\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
Start-Transcript -Path (Join-Path $runDirectory 'journey-results.txt') | Out-Null
$launched = @()
function Wait-Ready($url, $process, $label) {
    $deadline = (Get-Date).AddSeconds(150)
    while ((Get-Date) -lt $deadline) {
        $process.Refresh()
        if ($process.HasExited) { throw "$label stopped during startup. Read $runDirectory\$label-error.log and $label-output.log." }
        try {
            $response = Invoke-RestMethod -Uri $url -TimeoutSec 3
            if ($response.status -eq 'ready') { Write-Host "$label ready" -ForegroundColor Green; return }
        } catch { }
        Start-Sleep -Milliseconds 750
    }
    throw "$label was not ready within 150 seconds. Read its logs in $runDirectory."
}
function Start-ServiceProcess($project, $port, $label) {
    # Refuse to silently reuse an older build or terminate another process.
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        if ($client.ConnectAsync('127.0.0.1', $port).Wait(500) -and $client.Connected) {
            throw "Port $port is already in use. Stop the existing $label (Visual Studio Stop or Ctrl+C in its terminal), then run this launcher again."
        }
    } catch {
        if ($_.Exception.Message -like 'Port *') { throw }
    } finally { $client.Dispose() }
    Write-Host "Building $label..." -ForegroundColor Cyan
    & dotnet build $project --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$label build failed." }
    $projectDirectory = Split-Path $project -Parent
    $dll = Join-Path $projectDirectory ('bin\Debug\net10.0\' + [IO.Path]::GetFileNameWithoutExtension($project) + '.dll')
    $previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    try {
        $process = Start-Process -FilePath 'dotnet' -ArgumentList @(('"' + $dll + '"'), '--urls', "http://0.0.0.0:$port") -WorkingDirectory $projectDirectory -RedirectStandardOutput (Join-Path $runDirectory "$label-output.log") -RedirectStandardError (Join-Path $runDirectory "$label-error.log") -PassThru
    } finally { $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment }
    return $process
}
function Update-ClientRepository($directory, $branch, $url) {
    $currentBranch = (& git -C $directory branch --show-current | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $currentBranch -ne $branch) {
        throw "Expected branch $branch in $directory; found $currentBranch. No branch switch was performed."
    }
    & git -C $directory pull --ff-only $url $branch | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Could not update $directory. Local files were not reset." }
}
try {
    if ($UpdateClients) {
        Update-ClientRepository (Split-Path $WebProject -Parent) 'fix/final-rbac-v4-current' 'https://github.com/JKaye55/Shoppet_VetClinic.git'
        $mobileRoot = Join-Path $env:USERPROFILE 'source\repos\ShoppetApp 1\ShoppetApp'
        if (Test-Path (Join-Path $mobileRoot 'ShoppetApp\ShoppetApp.csproj')) {
            if ((Resolve-Path $mobileRoot).Path -ne (Resolve-Path $repoRoot).Path) {
                Update-ClientRepository $mobileRoot 'fix/mobile-community-polish' 'https://github.com/JKaye55/shoppet.git'
            }
            Write-Host 'Building the updated Android app...' -ForegroundColor Cyan
            & dotnet build (Join-Path $mobileRoot 'ShoppetApp\ShoppetApp.csproj') -f net10.0-android --nologo | Out-Host
            if ($LASTEXITCODE -ne 0) { throw 'Android build failed. See the transcript.' }
        } else {
            Write-Host 'Separate mobile checkout not found; deploy the app from this repository in Visual Studio.' -ForegroundColor Yellow
        }
    }
    $api = Start-ServiceProcess $apiProject 5020 'API'
    $launched += $api
    Wait-Ready 'http://localhost:5020/health/ready' $api 'API'
    $web = Start-ServiceProcess $WebProject 5253 'Web'
    $launched += $web
    Wait-Ready 'http://localhost:5253/health/ready' $web 'Web'
    Write-Host 'Web: http://localhost:5253 | Android API: http://10.0.2.2:5020/api' -ForegroundColor Cyan
    if (-not $SkipTests) {
        & (Join-Path $PSScriptRoot 'Test-SharedIntegration.ps1')
    }
    Write-Host 'Services remain running for your Web/Mobile touchpoint journey.' -ForegroundColor Green
    Write-Host "Report and server logs: $runDirectory"
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    foreach ($label in @('API','Web')) {
        foreach ($stream in @('error','output')) {
            $log = Join-Path $runDirectory "$label-$stream.log"
            if (Test-Path $log) { Get-Content $log -Tail 35 }
        }
    }
    throw
} finally {
    # Record only processes created by this run. No unrelated process is stopped.
    $launched | ForEach-Object { "Process ID: $($_.Id)" } | Set-Content (Join-Path $runDirectory 'processes.txt')
    Write-Host "Logs saved: $runDirectory"
    Stop-Transcript | Out-Null
}
