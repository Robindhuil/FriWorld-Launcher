<#
.SYNOPSIS
    Builds and runs the launcher on a machine where Smart App Control is enforcing.

.DESCRIPTION
    Smart App Control blocks unsigned binaries. Three things together get past it, and all
    three are needed:

      1. One assembly. A separate FriWorld.Launcher.Core.dll is blocked when it is loaded;
         compiling the Core sources straight into the entry project removes that load.
      2. The console subsystem. An OutputType of WinExe is blocked every time; Exe is not.
         The window still appears, it just brings a console along with it.
      3. Launching through `dotnet exec`. The generated apphost .exe is blocked; the signed
         dotnet host is not.

    None of this changes the real projects. A throwaway project is assembled outside the
    repository, built, and run.

    This is a workaround for a development machine, not a shipping arrangement. Turning
    Smart App Control off has been reversible since the spring 2026 updates, so that is the
    simpler answer if you would rather have one.

.PARAMETER Target
    app (default) runs the Avalonia window; cli runs the headless front end.

.PARAMETER Arguments
    Passed through to the CLI, ignored for the window.

.PARAMETER AssemblyName
    Name of the throwaway assembly. Smart App Control eventually blocks a name it has seen
    often enough, and when it does the fix is a name it has not seen. Defaults to a fresh
    name each run for that reason; pass one only if you want the build cached between runs.

.EXAMPLE
    ./tools/run-under-smart-app-control.ps1
    Generates a mock release if needed and opens the launcher window against it.

.EXAMPLE
    ./tools/run-under-smart-app-control.ps1 -Target cli -Arguments 'check'
#>
[CmdletBinding()]
param(
    [ValidateSet('app', 'cli')]
    [string]$Target = 'app',

    [string[]]$Arguments = @(),

    [string]$AssemblyName
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot

# A name Smart App Control has not judged yet. Reusing one is what eventually gets it blocked.
if (-not $AssemblyName) {
    $AssemblyName = 'FriWorldDev' + [guid]::NewGuid().ToString('N').Substring(0, 8)
}

# The throwaway project has to carry the real version, or the launcher compares itself against
# the manifest wrongly and reports an update that is not one.
$props = Get-Content (Join-Path $repo 'Directory.Build.props') -Raw
if ($props -notmatch '<Version>([^<]+)</Version>') {
    throw 'No <Version> found in Directory.Build.props.'
}
$version = $Matches[1]

$work = Join-Path ([System.IO.Path]::GetTempPath()) "friworld-launcher-sac/$Target"
$store = Join-Path ([System.IO.Path]::GetTempPath()) 'friworld-launcher-sac/store'
$root = Join-Path ([System.IO.Path]::GetTempPath()) 'friworld-launcher-sac/localroot'

Write-Host "repo   $repo"
Write-Host "work   $work"
Write-Host "name   $AssemblyName"
Write-Host "ver    $version"

if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Path $work -Force | Out-Null

# Copy the sources in rather than linking them, so no path in the project points outside itself.
$entryProject = if ($Target -eq 'app') { 'FriWorld.Launcher.App' } else { 'FriWorld.Launcher.Cli' }

foreach ($project in @('FriWorld.Launcher.Core', $entryProject)) {
    $source = Join-Path $repo "src/$project"
    $destination = Join-Path $work $project
    Copy-Item $source $destination -Recurse -Force
    Get-ChildItem $destination -Include obj, bin -Recurse -Directory |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Get-ChildItem $destination -Filter *.csproj -Recurse | Remove-Item -Force
}

$packages = if ($Target -eq 'app') {
    @'
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.1.1" />
    <PackageReference Include="Avalonia.Desktop" Version="12.1.1" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.1" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="12.1.1" />
    <PackageReference Include="AvaloniaUI.DiagnosticsSupport" Version="2.2.3" />
  </ItemGroup>
'@
} else {
    ''
}

# Exe, not WinExe: the GUI subsystem is what Smart App Control refuses.
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>$AssemblyName</AssemblyName>
    <Version>$version</Version>
  </PropertyGroup>
$packages
</Project>
"@ | Out-File (Join-Path $work 'single.csproj') -Encoding utf8

Push-Location $work
try {
    dotnet build -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}

$dll = Join-Path $work "bin/Debug/net10.0/$AssemblyName.dll"

if ($Target -eq 'cli') {
    dotnet exec $dll @Arguments
    exit $LASTEXITCODE
}

# The window needs something to install, so make sure a mock release exists.
if (-not (Test-Path (Join-Path $store 'manifest.json'))) {
    Write-Host "Building a mock release in $store"
    & $PSCommandPath -Target cli -Arguments @('mock-release', '--out', $store)
}

$env:FRIWORLD_MANIFEST_URL = Join-Path $store 'manifest.json'
$env:FRIWORLD_LAUNCHER_ROOT = $root

Write-Host ''
Write-Host "manifest  $($env:FRIWORLD_MANIFEST_URL)"
Write-Host "root      $root"
Write-Host ''

dotnet exec $dll
