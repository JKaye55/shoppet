param(
    [switch]$SkipCheckout,
    [switch]$SkipAndroidBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "ShoppetAPI\ShoppetAPI.csproj"
$appProject = Join-Path $repoRoot "ShoppetApp\ShoppetApp.csproj"
$smokeScript = Join-Path $PSScriptRoot "api-smoke-test.ps1"
$apiBase = "http://localhost:5020"
$apiProcess = $null
$startedApi = $false

function Wait-ForApi {
    param([int]$Seconds = 30)

    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        try {
            Invoke-RestMethod -Method GET -Uri "$apiBase/api/shop/categories" -TimeoutSec 2 | Out-Null
            return $true
        }
        catch {
            Start-Sleep -Milliseconds 750
        }
    } while ((Get-Date) -lt $deadline)

    return $false
}

try {
    Write-Host ""
    Write-Host "============================================================"
    Write-Host " ShoppetCare shared SQL Server full verification"
    Write-Host "============================================================"
    Write-Host ""

    Write-Host "[1/4] Building API..."
    dotnet build $apiProject
    if ($LASTEXITCODE -ne 0) {
        throw "API build failed."
    }

    if (-not $SkipAndroidBuild) {
        Write-Host ""
        Write-Host "[2/4] Building Android app..."
        dotnet build $appProject -f net10.0-android
        if ($LASTEXITCODE -ne 0) {
            throw "Android app build failed."
        }
    }
    else {
        Write-Host ""
        Write-Host "[2/4] Android build skipped by request."
    }

    Write-Host ""
    Write-Host "[3/4] Checking API..."

    $apiAlreadyRunning = Wait-ForApi -Seconds 2
    if (-not $apiAlreadyRunning) {
        $logsDir = Join-Path $repoRoot "artifacts"
        New-Item -ItemType Directory -Force -Path $logsDir | Out-Null
        $stdout = Join-Path $logsDir "shoppet-api-smoke.stdout.log"
        $stderr = Join-Path $logsDir "shoppet-api-smoke.stderr.log"

        $arguments = @(
            "run",
            "--project", $apiProject,
            "--launch-profile", "http",
            "--no-build"
        )

        $apiProcess = Start-Process dotnet -ArgumentList $arguments -WorkingDirectory $repoRoot -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
        $startedApi = $true

        if (-not (Wait-ForApi -Seconds 30)) {
            $errorText = ""
            if (Test-Path $stderr) {
                $errorText = Get-Content $stderr -Raw
            }
            throw "API did not become ready on $apiBase. $errorText"
        }
    }

    Write-Host "API ready at $apiBase"

    Write-Host ""
    Write-Host "[4/4] Running CRUD smoke test..."

    if ($SkipCheckout) {
        & $smokeScript -BaseUrl "$apiBase/api"
    }
    else {
        & $smokeScript -BaseUrl "$apiBase/api" -IncludeCheckout
    }

    if ($LASTEXITCODE -ne 0) {
        throw "One or more CRUD smoke tests failed."
    }

    Write-Host ""
    Write-Host "============================================================"
    Write-Host " ALL REQUESTED VERIFICATION STEPS PASSED"
    Write-Host "============================================================"
    Write-Host ""
}
finally {
    if ($startedApi -and $apiProcess -and -not $apiProcess.HasExited) {
        Write-Host "Stopping temporary API process..."
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
