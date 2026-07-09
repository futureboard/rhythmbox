param(
    [string]$QemuBinDir = $(if ($env:QEMU_BIN_DIR) { $env:QEMU_BIN_DIR } else { "" }),
    [string]$UefiDir = $(if ($env:UEFI_DIR) { $env:UEFI_DIR } else { "" }),
    [string]$ImagePath = $(if ($env:IMAGE_PATH) { $env:IMAGE_PATH } else { "" }),
    [string]$Qemu = $(if ($env:QEMU) { $env:QEMU } else { "" }),
    [string]$OvmfCode = $(if ($env:OVMF_CODE) { $env:OVMF_CODE } else { "" }),
    [string]$OvmfVars = $(if ($env:OVMF_VARS) { $env:OVMF_VARS } else { "" }),
    [string]$QemuWorkDir = $(if ($env:QEMU_WORK_DIR) { $env:QEMU_WORK_DIR } else { "" }),
    [int]$Memory = $(if ($env:MEMORY) { [int]$env:MEMORY } else { 4096 }),
    [int]$Cpus = $(if ($env:CPUS) { [int]$env:CPUS } else { 4 }),
    [int]$SshPort = $(if ($env:SSH_PORT) { [int]$env:SSH_PORT } else { 2222 }),
    [int]$MonitorPort = $(if ($env:MONITOR_PORT) { [int]$env:MONITOR_PORT } else { 4444 }),
    [int]$SerialPort = $(if ($env:SERIAL_PORT) { [int]$env:SERIAL_PORT } else { 5555 }),
    [switch]$SerialTelnet = $(if ($env:SERIAL_TELNET) { $env:SERIAL_TELNET -eq "1" } else { $true }),
    [switch]$NoSerialTcp = $(if ($env:NO_SERIAL_TCP) { $env:NO_SERIAL_TCP -eq "1" } else { $false }),
    [switch]$AllowReboot = $(if ($env:ALLOW_REBOOT) { $env:ALLOW_REBOOT -eq "1" } else { $false }),
    [string]$QemuTrace = $(if ($env:QEMU_TRACE) { $env:QEMU_TRACE } else { "guest_errors,unimp" }),
    [string]$QemuAccel = $(if ($env:QEMU_ACCEL) { $env:QEMU_ACCEL } else { "tcg" }),
    [string]$QemuDisplay = $(if ($env:QEMU_DISPLAY) { $env:QEMU_DISPLAY } else { "gtk,gl=off" })
)

$ErrorActionPreference = "Stop"

function Resolve-FullPath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Find-FirstFile([string[]]$Candidates) {
    foreach ($candidate in $Candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return (Resolve-FullPath $candidate)
        }
    }

    return $null
}

$ScriptDir = Split-Path -Parent $PSCommandPath
$RepoRoot = Resolve-FullPath (Join-Path $ScriptDir "../..")

if (-not $QemuBinDir) { $QemuBinDir = Join-Path $RepoRoot "bin" }
if (-not $UefiDir) { $UefiDir = Join-Path $RepoRoot "bin/share" }
if (-not $ImagePath) { $ImagePath = Join-Path $RepoRoot "out/rythmbox-arch-efi.img" }
if (-not $QemuWorkDir) { $QemuWorkDir = Join-Path $RepoRoot "out/qemu-debug" }

$QemuBinDir = Resolve-FullPath $QemuBinDir
$UefiDir = Resolve-FullPath $UefiDir
$ImagePath = Resolve-FullPath $ImagePath
$QemuWorkDir = Resolve-FullPath $QemuWorkDir

if (-not $Qemu) {
    $Qemu = Find-FirstFile @(
        (Join-Path $QemuBinDir "qemu-system-x86_64.exe"),
        (Join-Path $QemuBinDir "qemu-system-x86_64w.exe"),
        (Join-Path $QemuBinDir "qemu-system-x86_64")
    )
}

if (-not $Qemu) {
    Write-Error "qemu-system-x86_64 not found in $QemuBinDir. Set -Qemu or -QemuBinDir."
}

$Qemu = Resolve-FullPath $Qemu

if (-not (Test-Path -LiteralPath $ImagePath -PathType Leaf)) {
    Write-Error "Image not found: $ImagePath. Set -ImagePath."
}

if (-not $OvmfCode) {
    $OvmfCode = Find-FirstFile @(
        (Join-Path $UefiDir "OVMF_CODE.fd"),
        (Join-Path $UefiDir "OVMF_CODE_4M.fd"),
        (Join-Path $UefiDir "edk2-x86_64-code.fd")
    )
}

if (-not $OvmfVars) {
    $OvmfVars = Find-FirstFile @(
        (Join-Path $UefiDir "OVMF_VARS.fd"),
        (Join-Path $UefiDir "OVMF_VARS_4M.fd"),
        (Join-Path $UefiDir "edk2-i386-vars.fd"),
        (Join-Path $UefiDir "edk2-x86_64-vars.fd")
    )
}

if (-not $OvmfCode) {
    Write-Error "UEFI code firmware not found in $UefiDir. Expected OVMF_CODE.fd, OVMF_CODE_4M.fd, or edk2-x86_64-code.fd. Set -OvmfCode."
}

$OvmfCode = Resolve-FullPath $OvmfCode
if ($OvmfVars) { $OvmfVars = Resolve-FullPath $OvmfVars }

New-Item -ItemType Directory -Force -Path $QemuWorkDir | Out-Null

$VarsDriveArgs = @()
$OvmfVarsCopy = $null
if ($OvmfVars) {
    $OvmfVarsCopy = Join-Path $QemuWorkDir "OVMF_VARS.fd"
    $copyVars = -not (Test-Path -LiteralPath $OvmfVarsCopy -PathType Leaf)
    if (-not $copyVars) {
        $copyVars = (Get-Item -LiteralPath $OvmfVars).LastWriteTimeUtc -gt (Get-Item -LiteralPath $OvmfVarsCopy).LastWriteTimeUtc
    }

    if ($copyVars) {
        Copy-Item -LiteralPath $OvmfVars -Destination $OvmfVarsCopy -Force
    }

    $VarsDriveArgs = @("-drive", "if=pflash,format=raw,file=$OvmfVarsCopy")
} else {
    Write-Warning "UEFI vars firmware not found; booting without persistent NVRAM vars."
}

$SerialLog = Join-Path $QemuWorkDir "serial.log"
$DebugConLog = Join-Path $QemuWorkDir "debugcon.log"
$QemuLog = Join-Path $QemuWorkDir "qemu.log"
$MonitorAddr = "127.0.0.1:$MonitorPort"
$SerialAddr = "127.0.0.1:$SerialPort"
$SerialChardev = if ($NoSerialTcp) {
    "file,id=serial0,path=$SerialLog"
} else {
    $telnetFlag = if ($SerialTelnet) { ",telnet=on" } else { "" }
    "socket,id=serial0,host=127.0.0.1,port=$SerialPort,server=on,wait=off$telnetFlag,logfile=$SerialLog,logappend=on"
}

Write-Host @"
QEMU debug boot
  QEMU:       $Qemu
  UEFI code:  $OvmfCode
  UEFI vars:  $(if ($OvmfVars) { $OvmfVars } else { "none" })
  Image:      $ImagePath
  Memory:     $Memory MiB
  CPUs:       $Cpus
  SSH:        localhost:$SshPort -> guest:22
  Serial log: $SerialLog
  Debugcon:   $DebugConLog
  QEMU log:   $QemuLog
  Serial TCP: $(if ($NoSerialTcp) { "disabled" } else { "telnet 127.0.0.1 $SerialPort" })
  Monitor:    telnet 127.0.0.1 $MonitorPort

Inside guest after boot:
  ssh rythmbox@localhost -p $SshPort

Serial console/debug:
  Get-Content -Wait "$SerialLog"
  telnet 127.0.0.1 $SerialPort
"@

$QemuArgs = @(
    "-name", "rythmbox-arch-efi-debug",
    "-machine", "q35,accel=$QemuAccel",
    "-m", "$Memory",
    "-smp", "$Cpus",
    "-rtc", "base=utc",
    "-D", "$QemuLog",
    "-d", "$QemuTrace",
    "-drive", "if=pflash,format=raw,readonly=on,file=$OvmfCode"
) + $VarsDriveArgs + @(
    "-drive", "file=$ImagePath,if=virtio,format=raw,cache=writeback",
    "-netdev", "user,id=net0,hostfwd=tcp::$SshPort-:22",
    "-device", "virtio-net-pci,netdev=net0",
    "-device", "virtio-vga",
    "-display", "$QemuDisplay",
    "-device", "intel-hda",
    "-device", "hda-duplex",
    "-chardev", $SerialChardev,
    "-serial", "chardev:serial0",
    "-debugcon", "file:$DebugConLog",
    "-global", "isa-debugcon.iobase=0x402",
    "-monitor", "tcp:$MonitorAddr,server,nowait"
)

if (-not $AllowReboot) {
    $QemuArgs += @("-no-reboot")
}

& $Qemu @QemuArgs
exit $LASTEXITCODE
