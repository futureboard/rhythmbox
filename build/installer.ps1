param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [string]$IsccPath = ''
)

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$version = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version').Node.InnerText.Trim()
$publishDir = Join-Path $root "out/publish/$Runtime"
$installerDir = Join-Path $root 'out/installer'
$issFile = Join-Path $root 'installer/Rythmbox.iss'

if (-not (Test-Path $publishDir)) {
    & (Join-Path $PSScriptRoot 'publish.ps1') -Runtime $Runtime -Configuration $Configuration
}

function Find-InnoSetupCompiler {
    $candidates = @(
        $env:INNO_SETUP_PATH,
        (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
    )

    foreach ($version in 7, 6) {
        $candidates += @(
            (Join-Path $env:ProgramFiles "Inno Setup $version\ISCC.exe"),
            (Join-Path ${env:ProgramFiles(x86)} "Inno Setup $version\ISCC.exe")
        )
    }

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

if (-not $IsccPath) {
    $IsccPath = Find-InnoSetupCompiler
}

if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) {
    throw @"
Inno Setup compiler (ISCC.exe) not found.

Install Inno Setup 6 or 7, or pass -IsccPath, e.g.:
  ./build/installer.ps1 -IsccPath 'C:\Program Files\Inno Setup 7\ISCC.exe'

You can also set INNO_SETUP_PATH to the full path of ISCC.exe.
"@
}

New-Item -ItemType Directory -Path $installerDir -Force | Out-Null

Write-Host "Building installer with $IsccPath"
& $IsccPath $issFile `
    "/DAppVersion=$version" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$installerDir" `
    "/DRuntime=$Runtime"

Write-Host "Installer output: $installerDir"
