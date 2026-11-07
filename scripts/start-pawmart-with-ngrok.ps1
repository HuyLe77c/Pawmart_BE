param(
    [int]$Port = 5165
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $repoRoot 'Pawmart'
$localBaseUrl = "http://127.0.0.1:$Port"
$backendProcess = $null
$ngrokProcess = $null
$sessionId = [guid]::NewGuid().ToString('N')
$logDir = Join-Path $env:TEMP "pawmart-ngrok-$sessionId"

function Test-BackendReady {
    try {
        $response = Invoke-RestMethod -Uri "$localBaseUrl/health/ready" -TimeoutSec 3
        return $response.status -eq 'ready' -and $response.database -eq 'available'
    }
    catch {
        return $false
    }
}

function Get-NgrokPath {
    $command = Get-Command ngrok -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $packagesDir = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    $candidate = Get-ChildItem -Path $packagesDir -Filter ngrok.exe -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    if ($candidate) { return $candidate }

    throw 'Chưa cài ngrok. Chạy: winget install --id Ngrok.Ngrok --exact --source winget'
}

try {
    $ngrokPath = Get-NgrokPath
    $ngrokConfig = Join-Path $env:LOCALAPPDATA 'ngrok\ngrok.yml'
    if (-not (Test-Path -LiteralPath $ngrokConfig)) {
        throw 'Chưa đăng nhập ngrok. Lấy authtoken trong dashboard ngrok, chạy ngrok config add-authtoken <authtoken> trên máy này, rồi chạy lại script. Không gửi authtoken qua chat.'
    }

    New-Item -ItemType Directory -Path $logDir -Force | Out-Null

    if (-not (Test-BackendReady)) {
        Write-Host 'Đang khởi động PawMart BE...'
        $oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        try {
            $backendProcess = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--no-launch-profile', '--urls', "http://0.0.0.0:$Port") -WorkingDirectory $projectDir -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logDir 'backend.out.log') -RedirectStandardError (Join-Path $logDir 'backend.err.log')
        }
        finally {
            $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
        }

        for ($attempt = 0; $attempt -lt 60 -and -not (Test-BackendReady); $attempt++) {
            if ($backendProcess.HasExited) { break }
            Start-Sleep -Seconds 1
        }
        if (-not (Test-BackendReady)) {
            throw "BE chưa sẵn sàng. Xem log tại $logDir"
        }
    }
    else {
        Write-Host "PawMart BE đang chạy tại $localBaseUrl"
    }

    Write-Host 'Đang mở tunnel HTTPS ngrok...'
    $ngrokProcess = Start-Process -FilePath $ngrokPath -ArgumentList @('http', "$Port") -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logDir 'ngrok.out.log') -RedirectStandardError (Join-Path $logDir 'ngrok.err.log')

    $publicUrl = $null
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($ngrokProcess.HasExited) { break }
        try {
            $tunnels = Invoke-RestMethod -Uri 'http://127.0.0.1:4040/api/tunnels' -TimeoutSec 2
            $publicUrl = $tunnels.tunnels |
                Where-Object { $_.public_url -like 'https://*' -and $_.config.addr -match ":$Port$" } |
                Select-Object -First 1 -ExpandProperty public_url
            if ($publicUrl) { break }
        }
        catch { }
        Start-Sleep -Seconds 1
    }
    if (-not $publicUrl) {
        throw "Không lấy được URL HTTPS từ ngrok. Kiểm tra authtoken và log tại $logDir"
    }

    $publicReady = $false
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        try {
            $result = Invoke-RestMethod -Uri "$publicUrl/health/ready" -Headers @{ 'ngrok-skip-browser-warning' = 'true' } -TimeoutSec 5
            if ($result.status -eq 'ready' -and $result.database -eq 'available') {
                $publicReady = $true
                break
            }
        }
        catch { }
        Start-Sleep -Seconds 1
    }
    if (-not $publicReady) {
        throw "Tunnel đã mở nhưng chưa truy cập được BE qua HTTPS. Xem log tại $logDir"
    }

    $webhookConfigured = $false
    try {
        Invoke-WebRequest -Uri "$localBaseUrl/api/payments/sepay-webhook" -Method Post -ContentType 'application/json' -Body '{}' -UseBasicParsing -TimeoutSec 3 | Out-Null
    }
    catch {
        $webhookConfigured = $_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 401
    }

    Write-Host ''
    Write-Host "BE local: $localBaseUrl"
    Write-Host "BE HTTPS: $publicUrl"
    Write-Host "Dán vào URL webhook SePay: $publicUrl/api/payments/sepay-webhook"
    Write-Host 'Sự kiện: Có tiền vào | Kiểu: application/json | Xác thực: API Key'
    if ($webhookConfigured) {
        Write-Host 'Webhook đã bật xác thực API Key trên BE.'
    }
    else {
        Write-Warning 'Webhook chưa xác nhận được cấu hình API Key. Kiểm tra SePay:WebhookApiKey và khởi động lại BE trước khi nhận tiền thật.'
    }
    Write-Host 'Giữ cửa sổ này mở để nhận webhook. Nhấn Ctrl+C để dừng tunnel và BE do script khởi động.'

    while (-not $ngrokProcess.HasExited) {
        Start-Sleep -Seconds 2
    }
    throw "Ngrok đã dừng. Xem log tại $logDir"
}
finally {
    if ($ngrokProcess -and -not $ngrokProcess.HasExited) {
        Stop-Process -Id $ngrokProcess.Id -ErrorAction SilentlyContinue
    }
    if ($backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -ErrorAction SilentlyContinue
    }
}
