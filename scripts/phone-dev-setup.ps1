# Подготовка доступа телефона к WildBerriesAnalyzer.Server (USB + adb reverse).
# Запуск: powershell -ExecutionPolicy Bypass -File scripts\phone-dev-setup.ps1

$ErrorActionPreference = "Stop"
$adb = "E:\Android\android-sdk\platform-tools\adb.exe"
if (-not (Test-Path $adb)) {
    $adb = "E:\Android\Sdk\platform-tools\adb.exe"
}
if (-not (Test-Path $adb)) {
    throw "adb.exe не найден. Проверьте AndroidSdkDirectory."
}

Write-Host "Devices:" -ForegroundColor Cyan
& $adb devices

Write-Host "adb reverse tcp:5146 tcp:5146" -ForegroundColor Cyan
& $adb reverse tcp:5146 tcp:5146
& $adb reverse --list

Write-Host ""
Write-Host "Дальше:" -ForegroundColor Yellow
Write-Host "1) Запустите Server (профиль http) — в консоли должно быть: Now listening on: http://0.0.0.0:5146"
Write-Host "2) Mobile: UseAdbReverse=true в ServerSettings.cs, пересоберите и установите на телефон"
Write-Host "3) Проверка с ПК: Invoke-WebRequest http://127.0.0.1:5146/swagger/index.html"
