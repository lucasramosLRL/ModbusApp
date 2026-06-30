<#
.SYNOPSIS
    Debug runner for the Avalonia Android app: build (Debug) + deploy + launch on
    whatever device is connected - Genymotion emulator OR a USB phone - then tail
    a logcat filtered to the app process.

.DESCRIPTION
    Resolves adb (Android SDK first, Genymotion fallback), lists connected devices
    and picks one (auto when there is a single device; interactive menu when there
    are several; or forced via -Device), then runs 'dotnet build -t:Install',
    starts the app and streams 'adb logcat --pid'.

    Start the Genymotion VM and/or plug the USB phone (with USB debugging enabled
    and authorized) BEFORE running this script.

.PARAMETER Configuration
    Build configuration. Default: Debug.

.PARAMETER Device
    Force a specific adb serial (e.g. 192.168.56.102:5555 or a USB id). Skips the
    device picker.

.PARAMETER NoLogcat
    Deploy + launch but do not tail logcat.

.PARAMETER AllLog
    Tail the full logcat (no per-app PID filter).

.EXAMPLE
    pwsh scripts/run-android-avalonia.ps1
.EXAMPLE
    pwsh scripts/run-android-avalonia.ps1 -Device 192.168.56.102:5555
#>
param(
    [string]$Configuration = "Debug",
    [string]$Device = "",
    [switch]$NoLogcat,
    [switch]$AllLog
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projPath = Join-Path $repoRoot "Modbus.Mobile.Avalonia.Android\Modbus.Mobile.Avalonia.Android.csproj"
$appId    = "com.kron.modbusapp.avalonia"
$tfm      = "net8.0-android"

# --- Toolchain locations -----------------------------------------------------
$sdk = $env:ANDROID_HOME
if (-not $sdk) { $sdk = Join-Path $env:LOCALAPPDATA "Android\Sdk" }

$adbCandidates = @(
    (Join-Path $sdk "platform-tools\adb.exe"),
    "C:\Program Files\Genymobile\Genymotion\tools\adb.exe"
)
$adb = $adbCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $adb) { throw "adb.exe not found. Looked in: $($adbCandidates -join '; ')" }

# Build needs the SDK + a JDK 17. Android Studio ships a compatible jbr.
$env:ANDROID_HOME     = $sdk
$env:ANDROID_SDK_ROOT = $sdk
if (-not $env:JAVA_HOME) {
    $jbr = "C:\Program Files\Android\Android Studio\jbr"
    if (Test-Path $jbr) { $env:JAVA_HOME = $jbr }
}

Write-Host "adb : $adb"           -ForegroundColor Cyan
Write-Host "sdk : $sdk"           -ForegroundColor Cyan
Write-Host "jdk : $env:JAVA_HOME" -ForegroundColor Cyan

# --- Helpers -----------------------------------------------------------------
function Get-Prop([string]$serial, [string]$name) {
    (& $adb -s $serial shell getprop $name 2>$null | Out-String).Trim()
}

function Get-DeviceDesc([string]$serial) {
    $model = Get-Prop $serial "ro.product.model"
    $rel   = Get-Prop $serial "ro.build.version.release"
    $sdkv  = Get-Prop $serial "ro.build.version.sdk"
    $isEmu = (
        ($serial -match ':\d+$') -or
        ($serial -like 'emulator-*') -or
        ((Get-Prop $serial 'ro.boot.qemu') -eq '1') -or
        ((Get-Prop $serial 'ro.kernel.qemu') -eq '1') -or
        ((Get-Prop $serial 'ro.product.manufacturer') -like '*Genymotion*')
    )
    $kind = if ($isEmu) { 'emulador' } else { 'USB' }
    return "$model  Android $rel (API $sdkv)  [$kind]"
}

# --- Discover devices --------------------------------------------------------
& $adb start-server | Out-Null
$raw = (& $adb devices) | Select-Object -Skip 1 | Where-Object { $_ -match "\S" }

$ready = @()
foreach ($line in $raw) {
    $parts  = $line -split "\s+"
    $serial = $parts[0]
    $state  = $parts[1]
    if ($state -eq "device") {
        $ready += $serial
    }
    elseif ($state -eq "unauthorized") {
        Write-Warning "Device $serial esta 'unauthorized' - aceite o prompt 'Permitir depuracao USB' no aparelho."
    }
    elseif ($state -eq "offline") {
        Write-Warning "Device $serial esta 'offline'."
    }
}

if (-not $ready) {
    Write-Host ""
    Write-Host "Nenhum device pronto. Faca um dos dois:" -ForegroundColor Red
    Write-Host "  - Emulador: inicie a VM no Genymotion (aguarde o boot terminar) e garanta"
    Write-Host "    que o Genymotion usa o adb do SDK (Settings > ADB > Use custom SDK tools)."
    Write-Host "  - USB: conecte o celular com 'Depuracao USB' ativada e aceite a autorizacao."
    throw "Sem device conectado/pronto."
}

# --- Pick target -------------------------------------------------------------
if ($Device) {
    if ($ready -notcontains $Device) { throw "Device '$Device' nao esta conectado/pronto. Disponiveis: $($ready -join ', ')" }
    $target = $Device
}
elseif ($ready.Count -eq 1) {
    $target = $ready[0]
}
else {
    Write-Host ""
    Write-Host "Varios devices conectados:" -ForegroundColor Yellow
    for ($i = 0; $i -lt $ready.Count; $i++) {
        Write-Host ("  [{0}] {1}  -  {2}" -f $i, $ready[$i], (Get-DeviceDesc $ready[$i]))
    }
    $sel = Read-Host "Escolha o indice (Enter = 0)"
    if ([string]::IsNullOrWhiteSpace($sel)) { $sel = 0 }
    $target = $ready[[int]$sel]
}

Write-Host ""
Write-Host "target: $target  -  $(Get-DeviceDesc $target)" -ForegroundColor Green

# Boot guard: a VM still on the boot animation rejects install with
# "cmd: Can't find service: package".
if ((Get-Prop $target "sys.boot_completed") -ne "1") {
    throw "Device $target ainda nao terminou o boot (sys.boot_completed != 1). Aguarde e rode de novo."
}

# --- Build + install ---------------------------------------------------------
Write-Host ""
Write-Host ">> dotnet build -t:Install ($Configuration)" -ForegroundColor Yellow
dotnet build $projPath -c $Configuration -f $tfm -t:Install -p:AdbTarget="-s $target" --nologo
if ($LASTEXITCODE -ne 0) { throw "Build/Install falhou (exit $LASTEXITCODE)." }

# --- Launch ------------------------------------------------------------------
Write-Host ""
Write-Host ">> launching $appId" -ForegroundColor Yellow
& $adb -s $target shell monkey -p $appId -c android.intent.category.LAUNCHER 1 | Out-Null

if ($NoLogcat) { Write-Host "OK (logcat pulado)." -ForegroundColor Green; return }

# --- Logcat ------------------------------------------------------------------
& $adb -s $target logcat -c
if ($AllLog) {
    Write-Host ""
    Write-Host ">> logcat completo (Ctrl+C para parar)" -ForegroundColor Yellow
    & $adb -s $target logcat
    return
}

# Filter to the app process so we see managed exceptions + Debug.WriteLine output.
$appPid = ""
for ($i = 0; $i -lt 10 -and -not $appPid; $i++) {
    $appPid = (& $adb -s $target shell pidof $appId 2>$null | Out-String).Trim()
    if (-not $appPid) { Start-Sleep -Milliseconds 400 }
}

if ($appPid) {
    Write-Host ""
    Write-Host ">> logcat --pid=$appPid  ($appId)  (Ctrl+C para parar)" -ForegroundColor Yellow
    & $adb -s $target logcat --pid=$appPid
} else {
    Write-Warning "Nao achei o PID do app (pode ter fechado). Caindo para filtro por tags."
    & $adb -s $target logcat "AndroidRuntime:E" "MonoDroid:V" "DOTNET:V" "mono-stdout:V" "*:S"
}
