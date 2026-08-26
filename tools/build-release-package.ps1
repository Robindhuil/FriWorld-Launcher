<#
.SYNOPSIS
    Builds both release assets for the launcher: the single-file exe and the full zip.

.DESCRIPTION
    Two assets come out of this, and the split matters.

    `FriWorldLauncher.exe` is one file. This is what the launcher downloads when it replaces
    itself, and a self-update is one rename plus one move — a zip would have to be unpacked
    first, turning one atomic step into several that can each fail half way.

    The zip carries the same exe plus `zaloha\`, a merged build with a console subsystem that
    Smart App Control allows where it refuses the apphost. That cannot live inside a single file,
    which is exactly why there are two assets rather than one.

    Both are stamped with the version from Directory.Build.props. That is the point of doing
    this in a script: assembling it by hand once shipped a zip whose fallback reported the
    previous version, so it would have offered an update to itself forever.

.PARAMETER OutputDirectory
    Where to put the two assets. Defaults to dist/launcher/<version>.
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot

# One source of truth for the version; everything below is stamped from it.
$props = Get-Content (Join-Path $repo 'Directory.Build.props') -Raw
if ($props -notmatch '<Version>([^<]+)</Version>') {
    throw 'No <Version> found in Directory.Build.props.'
}
$version = $Matches[1]

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repo "dist/launcher/$version"
}

Write-Host "version  $version"
Write-Host "output   $OutputDirectory"
Write-Host ''

if (Test-Path $OutputDirectory) { Remove-Item $OutputDirectory -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$staging = Join-Path ([System.IO.Path]::GetTempPath()) "friworld-launcher-package-$version"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
$package = Join-Path $staging 'FriWorld-Launcher'
New-Item -ItemType Directory -Path $package -Force | Out-Null

# --- 1. The single-file executable -------------------------------------------------------

Write-Host 'Building the single-file executable...'
$exeOut = Join-Path $staging 'exe'

dotnet publish (Join-Path $repo 'src/FriWorld.Launcher.App/FriWorld.Launcher.App.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -o $exeOut -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "Publishing the executable failed ($LASTEXITCODE)." }

Get-ChildItem $exeOut -Filter *.pdb | Remove-Item -Force
$exe = Join-Path $OutputDirectory 'FriWorldLauncher.exe'
Move-Item (Join-Path $exeOut 'FriWorld.Launcher.App.exe') $exe -Force

# --- 2. The fallback, for machines where Smart App Control refuses the exe ----------------

Write-Host 'Building the fallback...'
$fallbackSource = Join-Path $staging 'fallback'
New-Item -ItemType Directory -Path (Join-Path $fallbackSource 'src') -Force | Out-Null

foreach ($project in @('FriWorld.Launcher.Core', 'FriWorld.Launcher.App')) {
    $destination = Join-Path $fallbackSource "src/$project"
    Copy-Item (Join-Path $repo "src/$project") $destination -Recurse -Force
    Get-ChildItem $destination -Include obj, bin -Recurse -Directory |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Get-ChildItem $destination -Filter *.csproj -Recurse | Remove-Item -Force
}

# Exe rather than WinExe: the GUI subsystem is what Smart App Control refuses. The window
# still appears; it just brings a console along. The assembly name differs from the real one
# because Smart App Control's verdict attaches to a name, and the launcher's own name has
# already been refused on machines that have seen it.
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>FriWorldLauncherHost</AssemblyName>
    <Version>$version</Version>
    <DebugType>none</DebugType>
  </PropertyGroup>
  <ItemGroup>
    <AvaloniaResource Include="src/FriWorld.Launcher.App/Assets/**" Link="Assets/%(Filename)%(Extension)" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.1.1" />
    <PackageReference Include="Avalonia.Desktop" Version="12.1.1" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.1" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="12.1.1" />
    <PackageReference Include="AvaloniaUI.DiagnosticsSupport" Version="2.2.3" />
  </ItemGroup>
</Project>
"@ | Out-File (Join-Path $fallbackSource 'fallback.csproj') -Encoding utf8

Push-Location $fallbackSource
try {
    dotnet publish -c Release -r win-x64 --self-contained false `
        -o (Join-Path $package 'zaloha') -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "Publishing the fallback failed ($LASTEXITCODE)." }
}
finally {
    Pop-Location
}

Get-ChildItem (Join-Path $package 'zaloha') -Filter *.pdb | Remove-Item -Force

# --- 3. The zip --------------------------------------------------------------------------

Copy-Item $exe $package
foreach ($file in @('Spustit-ak-exe-nejde.cmd', 'CITAJ-MA.txt', 'launcher.json')) {
    Copy-Item (Join-Path $repo "tools/package/$file") $package
}
Copy-Item (Join-Path $package 'launcher.json') (Join-Path $package 'zaloha')

$zip = Join-Path $OutputDirectory "FriWorld-Launcher-$version-win-x64.zip"
Compress-Archive -Path $package -DestinationPath $zip -CompressionLevel Optimal

# --- 4. Say what came out ----------------------------------------------------------------

Write-Host ''
foreach ($item in Get-ChildItem $OutputDirectory) {
    '{0,-46} {1,8:N1} MB' -f $item.Name, ($item.Length / 1MB) | Write-Host
}

# The fallback carrying the wrong version is the mistake this script exists to prevent, so
# it is checked rather than assumed.
$stamped = (Get-Item (Join-Path $package 'zaloha/FriWorldLauncherHost.dll')).VersionInfo.ProductVersion
Write-Host ''
Write-Host "exe      $((Get-Item $exe).VersionInfo.ProductVersion)"
Write-Host "fallback $stamped"

if (-not $stamped.StartsWith($version)) {
    throw "The fallback reports $stamped but the release is $version."
}

Remove-Item $staging -Recurse -Force
