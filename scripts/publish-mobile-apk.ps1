# =============================================================================
# Публикация подписанного APK (PriceLab Mobile)
# =============================================================================
#
# Алгоритм:
#
# A) Один раз — создать release-keystore (если файла ещё нет)
#    cd WildBerriesAnalyzer.Mobile
#    & "E:\Android\Jdk\bin\keytool.exe" -genkeypair -v `
#      -keystore pricelab.keystore `
#      -alias pricelab `
#      -keyalg RSA -keysize 2048 -validity 10000
#    → задать и СОХРАНИТЬ пароль (keystore + key — обычно один и тот же)
#    → файл: WildBerriesAnalyzer.Mobile\pricelab.keystore (в git не коммитить)
#
# B) Каждая публикация
#    1. При необходимости поднять ApplicationDisplayVersion / ApplicationVersion в csproj
#    2. Из корня репозитория:
#         $env:PRICELAB_KEYSTORE_PASSWORD = "<пароль_от_keystore>"
#         .\scripts\publish-mobile-apk.ps1
#       или:
#         .\scripts\publish-mobile-apk.ps1 -KeystorePassword "<пароль_от_keystore>"
#    3. Скрипт вызывает:
#         dotnet publish ... -c Release -p:AndroidPackageFormat=apk
#           -p:AndroidKeyStore=true
#           -p:AndroidSigningKeyStore=...\pricelab.keystore
#           -p:AndroidSigningKeyAlias=pricelab
#           -p:AndroidSigningKeyPass / StorePass = пароль
#    4. Готовый файл: *-Signed.apk
#       (bin\Release\net10.0-android\publish\ или bin\Release\net10.0-android\)
#    5. Скрипт открывает папку с APK в Explorer
#
# C) Установка на телефон
#    — скопировать *-Signed.apk на устройство
#    — разрешить установку из неизвестных источников / этого файлового менеджера
#
# Примечание: пароль — только от pricelab.keystore, не от аккаунта Google/ПК.
# =============================================================================

[CmdletBinding()]
param(
    [string] $KeystorePath = "",
    [string] $KeyAlias = "pricelab",
    [string] $KeystorePassword = "",
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "WildBerriesAnalyzer.Mobile\WildBerriesAnalyzer.Mobile.csproj"
$mobileDir = Split-Path -Parent $project

if (-not (Test-Path $project)) {
    throw "Не найден проект: $project"
}

if ([string]::IsNullOrWhiteSpace($KeystorePath)) {
    $KeystorePath = Join-Path $mobileDir "pricelab.keystore"
}

if (-not (Test-Path $KeystorePath)) {
    throw @"
Keystore не найден: $KeystorePath

Создайте один раз:
  & `"E:\Android\Jdk\bin\keytool.exe`" -genkeypair -v ``
    -keystore `"$KeystorePath`" -alias $KeyAlias ``
    -keyalg RSA -keysize 2048 -validity 10000
"@
}

if ([string]::IsNullOrWhiteSpace($KeystorePassword)) {
    $KeystorePassword = $env:PRICELAB_KEYSTORE_PASSWORD
}

if ([string]::IsNullOrWhiteSpace($KeystorePassword)) {
    $secure = Read-Host "Пароль keystore" -AsSecureString
    $KeystorePassword = [System.Net.NetworkCredential]::new("", $secure).Password
}

if ([string]::IsNullOrWhiteSpace($KeystorePassword)) {
    throw "Пароль keystore пустой. Задайте -KeystorePassword или env PRICELAB_KEYSTORE_PASSWORD."
}

Write-Host "Publishing signed APK..." -ForegroundColor Cyan
Write-Host "  Project : $project"
Write-Host "  Keystore: $KeystorePath"
Write-Host "  Alias   : $KeyAlias"

dotnet publish $project `
    -f net10.0-android `
    -c $Configuration `
    -p:AndroidPackageFormat=apk `
    -p:AndroidKeyStore=true `
    -p:AndroidSigningKeyStore=$KeystorePath `
    -p:AndroidSigningKeyAlias=$KeyAlias `
    -p:AndroidSigningKeyPass=$KeystorePassword `
    -p:AndroidSigningStorePass=$KeystorePassword

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish завершился с кодом $LASTEXITCODE"
}

$searchRoots = @(
    (Join-Path $mobileDir "bin\$Configuration\net10.0-android\publish"),
    (Join-Path $mobileDir "bin\$Configuration\net10.0-android")
)

$signed = @()
foreach ($root in $searchRoots) {
    if (Test-Path $root) {
        $signed += Get-ChildItem -Path $root -Recurse -Filter "*-Signed.apk" -ErrorAction SilentlyContinue
        $signed += Get-ChildItem -Path $root -Recurse -Filter "*Signed.apk" -ErrorAction SilentlyContinue
    }
}

$signed = $signed | Sort-Object LastWriteTime -Descending | Select-Object -Unique -First 5

if (-not $signed -or $signed.Count -eq 0) {
    Write-Warning "Сборка прошла, но *-Signed.apk не найден. Проверьте bin\$Configuration\net10.0-android\"
    exit 0
}

Write-Host ""
Write-Host "Подписанный APK:" -ForegroundColor Green
$signed | ForEach-Object { Write-Host "  $($_.FullName)" }

$latest = $signed | Select-Object -First 1
Write-Host ""
Write-Host "Открыть папку: $($latest.DirectoryName)" -ForegroundColor Cyan
Start-Process explorer.exe $latest.DirectoryName
