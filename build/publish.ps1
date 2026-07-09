param(
    [ValidateSet('win-x64', 'win-arm64', 'osx-x64', 'osx-arm64', 'linux-x64', 'linux-arm64')]
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [string]$OutputRoot = '',
    [switch]$NoSelfContained
)

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$version = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version').Node.InnerText.Trim()
$publishDir = if ($OutputRoot) { Join-Path $OutputRoot $Runtime } else { Join-Path $root "out/publish/$Runtime" }

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$selfContained = -not $NoSelfContained
$projects = @(
    'src/Rythmbox.App/Rythmbox.App.csproj',
    'src/Rythmbox.Editor/Rythmbox.Editor.csproj',
    'src/Rythmbox.SampleCreator/Rythmbox.SampleCreator.csproj'
)

Write-Host "Publishing Rythmbox $version ($Runtime) to $publishDir"

foreach ($project in $projects) {
    $projectPath = Join-Path $root $project
    Write-Host "-> $project"
    dotnet publish $projectPath `
        -c $Configuration `
        -r $Runtime `
        --self-contained:$selfContained `
        -p:Version=$version `
        -o $publishDir
}

$sharedSource = Join-Path $root 'shared'
$sharedDest = Join-Path $publishDir 'shared'
if (Test-Path -LiteralPath $sharedSource) {
    Write-Host "-> Copying shared content"
    Copy-Item -LiteralPath $sharedSource -Destination $sharedDest -Recurse -Force
}
else {
    Write-Warning "shared folder not found at $sharedSource"
}

Write-Host "Publish complete: $publishDir"
